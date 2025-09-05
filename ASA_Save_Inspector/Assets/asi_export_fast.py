import json
import math
import sys
import time
import uuid
from multiprocessing import Queue, Process
from pathlib import Path
from typing import Optional, OrderedDict

from arkparse import AsaSave, ArkTribe
from arkparse.api.json_api import JsonApi
from arkparse.logging import ArkSaveLogger
from arkparse.object_model import ArkGameObject
from arkparse.object_model.cryopods.cryopod import Cryopod
from arkparse.object_model.dinos import Dino, TamedDino, Baby, TamedBaby
from arkparse.object_model.structures import Structure, StructureWithInventory
from arkparse.parsing import ArkBinaryParser
from arkparse.parsing.struct import ActorTransform, ObjectReference
from arkparse.player.ark_player import ArkPlayer
from arkparse.saves.save_connection import SaveConnection
from arkparse.utils.json_utils import DefaultJsonEncoder

def human_readable_time(time_in_sec : float):
    hours = math.floor(time_in_sec / 3600)
    remaining = time_in_sec % 3600
    minutes = math.floor(remaining / 60)
    remaining = remaining % 60
    seconds = round(remaining)
    if seconds == 0:
        seconds = 1
    if hours > 0:
        return str(hours) + 'h' + str(minutes) + 'm' + str(seconds) + 's'
    elif minutes > 0:
        return str(minutes) + 'm' + str(seconds) + 's'
    else:
        return str(seconds) + 's'

class PlayersAndTribesParsing:
    HEADER_OFFSET_ADJUSTMENT = 4
    TRIBE_HEADER_BASE_OFFSET = 4

    # "/Script/ShooterGame.PrimalTribeData"
    TRIBE_DATA_NAME = bytes([
        0x2f, 0x53, 0x63, 0x72, 0x69, 0x70, 0x74, 0x2f, 0x53, 0x68, 0x6f, 0x6f, 0x74, 0x65, 0x72, 0x47,
        0x61, 0x6d, 0x65, 0x2e, 0x50, 0x72, 0x69, 0x6d, 0x61, 0x6c, 0x54, 0x72, 0x69, 0x62, 0x65,
        0x44, 0x61, 0x74, 0x61, 0x00
    ])

    # "Game/PrimalEarth/CoreBlueprints/PrimalPlayerDataBP.PrimalPlayerDataBP_C"
    PLAYER_DATA_NAME = bytes([
        0x47, 0x61, 0x6d, 0x65, 0x2f, 0x50, 0x72, 0x69, 0x6d, 0x61, 0x6c, 0x45, 0x61, 0x72, 0x74, 0x68,
        0x2f, 0x43, 0x6f, 0x72, 0x65, 0x42, 0x6c, 0x75, 0x65, 0x70, 0x72, 0x69, 0x6e, 0x74, 0x73, 0x2f,
        0x50, 0x72, 0x69, 0x6d, 0x61, 0x6c, 0x50, 0x6c, 0x61, 0x79, 0x65, 0x72, 0x44, 0x61, 0x74, 0x61,
        0x42, 0x50, 0x2e, 0x50, 0x72, 0x69, 0x6d, 0x61, 0x6c, 0x50, 0x6c, 0x61, 0x79, 0x65, 0x72, 0x44,
        0x61, 0x74, 0x61, 0x42, 0x50, 0x5f, 0x43, 0x00
    ])

    save: AsaSave = None
    from_store: bool = True
    profile_paths: list[Path] = []
    tribe_paths: list[Path] = []
    data: ArkBinaryParser = None
    tribe_data_pointers: OrderedDict[uuid.UUID, list] = {}
    player_data_pointers: OrderedDict[uuid.UUID, list] = {}
    tribes_data: dict[uuid.UUID, bytes] = {}
    players_data: dict[uuid.UUID, bytes] = {}
    players: dict[int, ArkPlayer] = {}
    tribes: dict[int, ArkTribe] = {}
    tribe_to_player_map: dict[int, list[ArkPlayer]] = {}

def find_last_none_before(data: ArkBinaryParser, end_pos: int, pattern: bytes, adjust_offset: int = -1) -> Optional[int]:
    # adjust offset is a temporary fix for off-by-one errors which i still have to figure out
    original_position = data.get_position()
    pos = data.byte_buffer.rfind(pattern, 0, end_pos)
    data.set_position(original_position)
    return None if pos == -1 else pos + adjust_offset

def get_tribe_offsets_process(arg_obj): # parsing_data: PlayersAndTribesParsing) -> None:
    # tribe_data_name: bytes = arg_obj["data_name"]
    # data: ArkBinaryParser = arg_obj["data"]
    # tribe_offsets_queue: Queue = arg_obj["queue"]

    tribe_pointers: OrderedDict[uuid.UUID, list] = OrderedDict()

    positions = arg_obj["data"].find_byte_sequence(arg_obj["data_name"], print_findings=False)

    for pos in positions:
        arg_obj["data"].set_position(pos - 20)
        uuid_bytes = arg_obj["data"].read_bytes(16)
        uuid_pos = arg_obj["data"].find_byte_sequence(uuid_bytes)
        tribe_uuid = SaveConnection.byte_array_to_uuid(uuid_bytes)

        offset = pos - 36
        size = uuid_pos[1] - offset

        #ArkSaveLogger.api_log(f"Tribe UUID: {tribe_uuid}, Position: {uuid_pos[0]}, Second UUID position: {uuid_pos[1]}")
        tribe_pointers[tribe_uuid] = [uuid_bytes, offset + 1, size]

    arg_obj["queue"].put(tribe_pointers)

def get_player_offsets_process(arg_obj): # parsing_data: PlayersAndTribesParsing) -> None:
    # player_data_name: bytes = arg_obj["data_name"]
    # data: ArkBinaryParser = arg_obj["data"]
    # player_offsets_queue: Queue = arg_obj["queue"]

    player_pointers: OrderedDict[uuid.UUID, list] = OrderedDict()
    pattern = bytes([0x4E, 0x6F, 0x6E, 0x65])

    positions = arg_obj["data"].find_byte_sequence(arg_obj["data_name"], print_findings=False)

    for cnt, pos in enumerate(positions):
        # Get ID
        arg_obj["data"].set_position(pos - 20)
        uuid_bytes = arg_obj["data"].read_bytes(16)
        player_uuid = SaveConnection.byte_array_to_uuid(uuid_bytes)

        offset = pos - 36
        next_player_data = positions[cnt + 1] if cnt + 1 < len(positions) else None
        last_none = find_last_none_before(arg_obj["data"], next_player_data, pattern)
        if last_none is None:
            continue
        end_pos = last_none + 4
        size = end_pos - offset

        #ArkSaveLogger.api_log(f"Player UUID: {player_uuid}, Offset: {offset}, Size: {size}, End: {offset + size}, Next Player Data: {next_player_data}")
        player_pointers[player_uuid] = [uuid_bytes, offset, size + 1]

    arg_obj["queue"].put(player_pointers)

def get_offsets_from_db(save: AsaSave, parsing_data: PlayersAndTribesParsing):
    if save is None:
        raise ValueError("Save not provided")

    parsing_data.data = save.get_custom_value("GameModeCustomBytes")
    if parsing_data.data is None:
        raise ValueError("No GameModeCustomBytes found in the save data")

    # Read initial flag (unused)
    _ = parsing_data.data.read_boolean()

    tribe_offsets_queue: Queue = Queue()
    player_offsets_queue: Queue = Queue()
    tribe_offsets_process: Process = Process(target=get_tribe_offsets_process, args=({"data_name": parsing_data.TRIBE_DATA_NAME,
                                                                                      "data": players_and_tribes_data.data,
                                                                                      "queue": tribe_offsets_queue},))
    player_offsets_process: Process = Process(target=get_player_offsets_process, args=({"data_name": parsing_data.PLAYER_DATA_NAME,
                                                                                        "data": players_and_tribes_data.data,
                                                                                        "queue": player_offsets_queue},))

    tribe_offsets_process.start()
    player_offsets_process.start()

    parsing_data.tribe_data_pointers = tribe_offsets_queue.get(timeout=300)
    tribe_offsets_queue.close()
    parsing_data.player_data_pointers = player_offsets_queue.get(timeout=300)
    player_offsets_queue.close()

def get_ark_tribe_raw_data(index: uuid.UUID, parsing_data: PlayersAndTribesParsing) -> Optional[bytes]:
    pointer = parsing_data.tribe_data_pointers[index]
    if not pointer:
        return None
    parsing_data.data.set_position(pointer[1])
    return parsing_data.data.read_bytes(pointer[2])

def get_ark_profile_raw_data(index: uuid.UUID, parsing_data: PlayersAndTribesParsing) -> Optional[bytes]:
    pointer = parsing_data.player_data_pointers[index]
    if not pointer:
        return None
    parsing_data.data.set_position(pointer[1])
    return parsing_data.data.read_bytes(pointer[2]) + bytes([0x00, 0x01, 0x00, 0x00, 0x00]) + pointer[0]

def get_files_from_directory(directory: Path, parsing_data: PlayersAndTribesParsing):
    for path in directory.glob("*.arkprofile"):
        parsing_data.profile_paths.append(path)
    for path in directory.glob("*.arktribe"):
        parsing_data.tribe_paths.append(path)

def update_files(parsing_data: PlayersAndTribesParsing):
    if not parsing_data.from_store:
        index: int = 0
        for path in parsing_data.profile_paths:
            parsing_data.players_data[uuid.UUID(int=index)] = path.read_bytes()
            index += 1
        index = 0
        for path in parsing_data.tribe_paths:
            parsing_data.tribes_data[uuid.UUID(int=index)] = path.read_bytes()
            index += 1

    for player_uuid, player_data in parsing_data.players_data.items():
        try:
            parsed_player: ArkPlayer = ArkPlayer(player_data, parsing_data.from_store)
            parsing_data.players[parsed_player.id_] = parsed_player
        except Exception:
            # if "Unsupported archive version" in str(e):
            #     ArkSaveLogger.api_log(f"Skipping player data {player_uuid} due to unsupported archive version: {e}")
            pass

    for tribe_uuid, tribe_data in parsing_data.tribes_data.items():
        try:
            parsed_tribe: ArkTribe = ArkTribe(tribe_data, parsing_data.from_store)
        except Exception:
            # if "Unsupported archive version" in str(e):
            #     ArkSaveLogger.api_log(f"Skipping player data {tribe_uuid} due to unsupported archive version: {e}")
            continue

        tribe_players = []
        for member_id in parsed_tribe.member_ids:
            if parsing_data.players.__contains__(member_id):
                tribe_players.append(parsing_data.players[member_id])
            # else:
            #     ArkSaveLogger.api_log(f"Player with ID {member_id} not found in player list")

        parsing_data.tribes[parsed_tribe.tribe_id] = parsed_tribe
        parsing_data.tribe_to_player_map[parsed_tribe.tribe_id] = tribe_players

def parse_single_structure(obj: ArkGameObject, save: AsaSave, bypass_inventory: bool = True) -> Optional[Structure | StructureWithInventory]:
    try:
        if obj.get_property_value("MaxItemCount") is not None or (obj.get_property_value("MyInventoryComponent") is not None and obj.get_property_value("CurrentItemCount") is not None):
            structure = StructureWithInventory(obj.uuid, save, bypass_inventory=bypass_inventory)
        else:
            structure = Structure(obj.uuid, save)
        if obj.uuid in save.save_context.actor_transforms:
            structure.set_actor_transform(save.save_context.actor_transforms[obj.uuid])
        return structure
    except Exception as e:
        print(f"Exception caught when parsing structure: {e}", flush=True)
        return None

def pawn_to_json(pawn_obj: ArkGameObject) -> dict:
    pawn_data: dict = {"UUID": pawn_obj.uuid.__str__(),
                       "ClassName": "player",
                       "ItemArchetype": pawn_obj.blueprint}

    # Grab pawn location if it exists
    if pawn_obj.has_property("SavedBaseWorldLocation"):
        pawn_location = ActorTransform(vector=pawn_obj.get_property_value("SavedBaseWorldLocation"))
        if pawn_location is not None:
            pawn_data["ActorTransformX"] = pawn_location.x
            pawn_data["ActorTransformY"] = pawn_location.y
            pawn_data["ActorTransformZ"] = pawn_location.z

    # Grab pawn inventory UUID if it exists
    if pawn_obj.has_property("MyInventoryComponent"):
        inv_comp = pawn_obj.get_property_value("MyInventoryComponent")
        if inv_comp is not None and inv_comp.value is not None:
            pawn_data["InventoryUUID"] = inv_comp.value

    # Grab pawn owner inventory UUID if it exists
    if pawn_obj.has_property("OwnerInventory"):
        owner_inv: ObjectReference = pawn_obj.get_property_value("OwnerInventory")
        if owner_inv is not None and owner_inv.value is not None:
            pawn_data["OwnerInventoryUUID"] = owner_inv.value

    # Grab remaining properties if any
    if pawn_obj.properties is not None and len(pawn_obj.properties) > 0:
        for prop in pawn_obj.properties:
            if prop is not None and \
                    prop.name is not None and \
                    len(prop.name) > 0 and \
                    "SavedBaseWorldLocation" not in prop.name and \
                    "MyInventoryComponent" not in prop.name and \
                    "OwnerInventory" not in prop.name:
                pawn_data[prop.name] = pawn_obj.get_property_value(prop.name)

    return pawn_data

def asi_export_process(arg_obj):
    # the_dataset: dict = arg_obj["dataset"]
    # save_context: SaveContext = arg_obj["save_context"]
    # the_queue: Queue = arg_obj["queue"]

    skip_list = [
        "/Game/PrimalEarth/CoreBlueprints/Items/Notes/PrimalItem_StartingNote.PrimalItem_StartingNote_C",
        "/Script/ShooterGame.StructurePaintingComponent",
        "/Game/Packs/Frontier/Structures/TreasureCache/TreasureMap/PrimalItem_TreasureMap_WildSupplyDrop.PrimalItem_TreasureMap_WildSupplyDrop_C",
        "/Game/PrimalEarth/Structures/Wooden/CropPlotLarge_SM.CropPlotLarge_SM_C",
        "/Game/PrimalEarth/Structures/Pipes/WaterPipe_Stone_Intake.WaterPipe_Stone_Intake_C",
        "/Game/PrimalEarth/Structures/BuildingBases/WaterTank_Metal.WaterTank_Metal_C",
        "/Game/PrimalEarth/Structures/WaterTap_Metal.WaterTap_Metal_C"
    ]

    all_objs: list = []
    for key, value in arg_obj["dataset"].items():
        byte_buffer = ArkBinaryParser(value, arg_obj["save_context"])
        obj_uuid = uuid.UUID(bytes=key)
        try:
            class_name = byte_buffer.read_name()
        except Exception as ex:
            class_name = None
            print(f"Exception caught when parsing object with UUID {obj_uuid}: {ex}", flush=True)

        if class_name is not None and not class_name in skip_list:
            try:
                game_obj = ArkGameObject(obj_uuid, class_name, byte_buffer)
            except Exception as e:
                game_obj = None
                print(f"Exception caught when parsing object of class {class_name}: {e}", flush=True)
            if game_obj is not None:
                if game_obj.has_property("bIsEngram") and game_obj.get_property_value("bIsEngram", False):
                    continue
                all_objs.append({ "UUID": obj_uuid, "class": class_name, "object": game_obj, "bytes": value })

    arg_obj["queue"].put(all_objs)

def asi_parse_process(arg_obj):
    # objects: list = arg_obj["objects"]
    # save: AsaSave = arg_obj["save"]
    # the_queue: Queue = arg_obj["queue"]

    skip_list = [
        "/Game/PrimalEarth/CoreBlueprints/Items/Notes/PrimalItem_StartingNote.PrimalItem_StartingNote_C",
        "/Script/ShooterGame.StructurePaintingComponent",
        "/Game/Packs/Frontier/Structures/TreasureCache/TreasureMap/PrimalItem_TreasureMap_WildSupplyDrop.PrimalItem_TreasureMap_WildSupplyDrop_C",
        "/Game/PrimalEarth/Structures/Wooden/CropPlotLarge_SM.CropPlotLarge_SM_C",
        "/Game/PrimalEarth/Structures/Pipes/WaterPipe_Stone_Intake.WaterPipe_Stone_Intake_C",
        "/Game/PrimalEarth/Structures/BuildingBases/WaterTank_Metal.WaterTank_Metal_C",
        "/Game/PrimalEarth/Structures/WaterTap_Metal.WaterTap_Metal_C",
    ]

    all_dinos = []
    all_pawns_objects = []
    all_items = []
    all_structures = []
    for obj in arg_obj["objects"]:
        if obj is not None:
            try:
                if obj.blueprint in skip_list or (obj.has_property("bIsEngram") and obj.get_property_value("bIsEngram", False)):
                    continue

                if "Dinos/" in obj.blueprint and "_Character_" in obj.blueprint:
                    is_tamed = obj.get_property_value("TamedTimeStamp") is not None
                    is_baby = obj.get_property_value("bIsBaby", False)
                    if is_tamed:
                        if is_baby:
                            dino = TamedBaby(obj.uuid, arg_obj["save"])
                        else:
                            dino = TamedDino(obj.uuid, arg_obj["save"])
                    else:
                        if is_baby:
                            dino = Baby(obj.uuid, arg_obj["save"])
                        else:
                            dino = Dino(obj.uuid, arg_obj["save"])
                    if dino is not None:
                        all_dinos.append(dino)
                elif "/Raft_BP.Raft_BP" in obj.blueprint or "/Raft/MotorRaft_BP.MotorRaft_BP" in obj.blueprint:
                    is_tamed = obj.get_property_value("TamedTimeStamp") is not None
                    if is_tamed:
                        dino = TamedDino(obj.uuid, arg_obj["save"])
                    else:
                        dino = Dino(obj.uuid, arg_obj["save"])
                    if dino is not None:
                        all_dinos.append(dino)
                elif "PrimalItem_WeaponEmptyCryopod_C" in obj.blueprint:
                    try:
                        cryopod = Cryopod(obj.uuid, arg_obj["save"])
                    except Exception as ce:
                        cryopod = None
                        if not "Unsupported embedded data version (only Unreal 5.5 is supported)" in str(ce):
                            print(f"Exception caught during cryopod parsing: {ce}", flush=True)
                    if not cryopod is None:
                        if not cryopod.dino is None:
                            cryopod.dino.is_cryopodded = True
                            all_dinos.append(cryopod.dino)
                    all_items.append(JsonApi.primal_item_to_json_obj(obj))
                elif "/PlayerPawnTest_Female.PlayerPawnTest_Female_C" in obj.blueprint or "/PlayerPawnTest_Male.PlayerPawnTest_Male_C" in obj.blueprint:
                    all_pawns_objects.append(obj)
                elif ("/Structures" in obj.blueprint or "/GigantoraptorNest" in obj.blueprint or "Rug_Shag" in obj.blueprint) \
                        and not "PrimalItemStructure_" in obj.blueprint \
                        and not "/Skins/" in obj.blueprint \
                        and not "PrimalInventory" in obj.blueprint \
                        and not "PrimalItemStructureSkin" in obj.blueprint \
                        and not "PrimalItemResource" in obj.blueprint \
                        and not "/TrainCarts/" in obj.blueprint:
                    if obj.get_property_value("StructureID") is None:
                        continue
                    structure = parse_single_structure(obj, arg_obj["save"], True)
                    if structure is not None:
                        all_structures.append(structure)
                elif "/PrimalItemArmor_" in obj.blueprint or \
                        "/PrimalItem_" in obj.blueprint or \
                        "/PrimalItemAmmo_" in obj.blueprint or \
                        "/PrimalItemC4Ammo" in obj.blueprint or \
                        "/PrimalItemResource_" in obj.blueprint or \
                        "/PrimalItemConsumable_" in obj.blueprint or \
                        "/PrimalItemConsumableSoap" in obj.blueprint or \
                        "/PrimalItemConsumableMiracleGro" in obj.blueprint or \
                        "/PrimalItemConsumableRespecSoup" in obj.blueprint or \
                        "/PrimalItemConsumableBuff_Parachute" in obj.blueprint or \
                        "/PrimalItemConsumableEatable_" in obj.blueprint or \
                        "/PrimalItemArtifact" in obj.blueprint or \
                        "/PrimalItemTrophy" in obj.blueprint or \
                        "/PrimalItemWeaponAttachment_" in obj.blueprint or \
                        "/PrimalItemCustomDrinkRecipe_" in obj.blueprint or \
                        "/PrimalItemCustomFoodRecipe_" in obj.blueprint or \
                        "/PrimalItemDye_" in obj.blueprint or \
                        "/PrimalItemRadio" in obj.blueprint or \
                        "/PrimalItemRaft" in obj.blueprint or \
                        "/PrimalItemStructure_" in obj.blueprint or \
                        "/DroppedItemGeneric_" in obj.blueprint or \
                        "Egg_Wyvern_Fertilized" in obj.blueprint:
                    all_items.append(JsonApi.primal_item_to_json_obj(obj))
                elif not "/PrimalInventory_" in obj.blueprint \
                        and not "/PrimalInventoryBP_" in obj.blueprint \
                        and not "/Skins/" in obj.blueprint \
                        and not "PrimalInventory" in obj.blueprint \
                        and not "/PrimalItemStructureSkin_" in obj.blueprint \
                        and not "/PrimalItemSkin_" in obj.blueprint \
                        and not "/PrimalItemCostume_" in obj.blueprint \
                        and not "/PrimalItemDinoCostume_" in obj.blueprint \
                        and not "/TrainCarts/" in obj.blueprint \
                        and not "/WeapFists" in obj.blueprint \
                        and not "/PlayerControllerBlueprint" in obj.blueprint \
                        and not "/PlayerCharacterStatusComponent_BP" in obj.blueprint \
                        and not "DinoCharacterStatusComponent_BP" in obj.blueprint \
                        and not "/DinoCharacterStatus_BP" in obj.blueprint \
                        and not "/DinoInventoryComponent_" in obj.blueprint \
                        and not "/DinoTamedInventoryComponent_" in obj.blueprint \
                        and not "/DinoDropInventoryComponent_" in obj.blueprint \
                        and not "/NPCZoneManagerBlueprint_" in obj.blueprint \
                        and not "/Foliage" in obj.blueprint \
                        and not "InstancedFoliageActor" in obj.blueprint \
                        and not "BossArenaManager" in obj.blueprint \
                        and not "PrimalPersistentWorldData" in obj.blueprint \
                        and not "_AIController" in obj.blueprint \
                        and not "/Buffs/" in obj.blueprint \
                        and not "/AI/" in obj.blueprint \
                        and not "/Sound/" in obj.blueprint \
                        and not "/ByteArrayObject" in obj.blueprint \
                        and not "DayCycle" in obj.blueprint \
                        and not "AnimSequence" in obj.blueprint \
                        and not "Engine.BlockingVolume" in obj.blueprint \
                        and not "Engine.MaterialInstanceConstant" in obj.blueprint \
                        and not "NPCZoneManager" in obj.blueprint \
                        and not "NPCZoneVolume" in obj.blueprint:
                    print(f"Unknown object of class {obj.blueprint}", flush=True)
            except Exception as e:
                if not "Unsupported embedded data version (only Unreal 5.5 is supported)" in str(e):
                    print(f"Exception caught during parsing: {e}", flush=True)

    arg_obj["queue"].put({ "dinos": all_dinos, "pawns": all_pawns_objects, "items": all_items, "structures": all_structures })

def parse_players_and_tribes_for_process(players_data: dict[uuid.UUID, bytes], tribes_data: dict[uuid.UUID, bytes]):
    parsed_players: dict[int, ArkPlayer] = {}
    parsed_tribes: dict[int, ArkTribe] = {}
    tribe_to_players_map: dict[int, list[ArkPlayer]] = {}

    for player_uuid, player_data in players_data.items():
        try:
            parsed_player: ArkPlayer = ArkPlayer(player_data, True)
            parsed_players[parsed_player.id_] = parsed_player
        except Exception:
            # if "Unsupported archive version" in str(e):
            #     ArkSaveLogger.api_log(f"Skipping player data {player_uuid} due to unsupported archive version: {e}")
            pass

    for tribe_uuid, tribe_data in tribes_data.items():
        try:
            parsed_tribe: ArkTribe = ArkTribe(tribe_data, True)
        except Exception:
            # if "Unsupported archive version" in str(e):
            #     ArkSaveLogger.api_log(f"Skipping player data {tribe_uuid} due to unsupported archive version: {e}")
            continue

        tribe_players = []
        for member_id in parsed_tribe.member_ids:
            if parsed_players.__contains__(member_id):
                tribe_players.append(parsed_players[member_id])
            # else:
            #     ArkSaveLogger.api_log(f"Player with ID {member_id} not found in player list")

        parsed_tribes[parsed_tribe.tribe_id] = parsed_tribe
        tribe_to_players_map[parsed_tribe.tribe_id] = tribe_players

    return [parsed_players, parsed_tribes, tribe_to_players_map]

def asi_parse_players_process(arg_obj):
    # players_indexes: tuple[int, int] = arg_obj["players_indexes"]
    # players_pointers: OrderedDict[uuid.UUID, list] = arg_obj["players_pointers"]
    # data: ArkBinaryParser = arg_obj["data"]
    # players_queue: Queue = arg_obj["queue"]

    parsed_players: dict[int, ArkPlayer] = {}

    start_index = arg_obj["players_indexes"][0]
    stop_index = arg_obj["players_indexes"][1]
    player_index: int = -1
    for key, value in arg_obj["players_pointers"].items():
        player_index += 1
        if player_index >= start_index and player_index < stop_index:
            pointer = arg_obj["players_pointers"][key]
            if not pointer:
                continue
            arg_obj["data"].set_position(pointer[1])
            player_data = arg_obj["data"].read_bytes(pointer[2]) + bytes([0x00, 0x01, 0x00, 0x00, 0x00]) + pointer[0]
            try:
                parsed_player: ArkPlayer = ArkPlayer(player_data, True)
                if not parsed_player is None:
                    parsed_players[parsed_player.id_] = parsed_player
            except Exception:
                # if "Unsupported archive version" in str(e):
                #     ArkSaveLogger.api_log(f"Skipping player data {player_uuid} due to unsupported archive version: {e}")
                pass

    arg_obj["queue"].put(parsed_players)

def asi_parse_tribes_process(arg_obj):
    # tribes_indexes: tuple[int, int] = arg_obj["tribes_indexes"]
    # tribes_pointers: OrderedDict[uuid.UUID, list] = arg_obj["tribes_pointers"]
    # parsed_players: dict[int, ArkPlayer] = arg_obj["parsed_players"]
    # data: ArkBinaryParser = arg_obj["data"]
    # tribes_queue: Queue = arg_obj["queue"]

    parsed_tribes: dict[int, ArkTribe] = {}
    tribe_to_players_map: dict[int, list[ArkPlayer]] = {}

    start_index = arg_obj["tribes_indexes"][0]
    stop_index = arg_obj["tribes_indexes"][1]
    tribe_index: int = start_index - 1
    for key, value in arg_obj["tribes_pointers"].items():
        tribe_index += 1
        if tribe_index >= start_index and tribe_index < stop_index:
            pointer = arg_obj["tribes_pointers"][key]
            if not pointer:
                continue
            arg_obj["data"].set_position(pointer[1])
            tribe_data = arg_obj["data"].read_bytes(pointer[2])

            try:
                parsed_tribe: ArkTribe = ArkTribe(tribe_data, True)
            except Exception:
                # if "Unsupported archive version" in str(e):
                #     ArkSaveLogger.api_log(f"Skipping player data {tribe_uuid} due to unsupported archive version: {e}")
                continue

            tribe_players = []
            for member_id in parsed_tribe.member_ids:
                if member_id in arg_obj["parsed_players"]: # arg_obj["parsed_players"].__contains__(member_id):
                    tribe_players.append(arg_obj["parsed_players"][member_id])
                # else:
                #     ArkSaveLogger.api_log(f"Player with ID {member_id} not found in player list")

            parsed_tribes[parsed_tribe.tribe_id] = parsed_tribe
            tribe_to_players_map[parsed_tribe.tribe_id] = tribe_players

    arg_obj["queue"].put([parsed_tribes, tribe_to_players_map])

def legacy_parse_players_and_tribes(players_and_tribes: PlayersAndTribesParsing):
    for key, value in players_and_tribes.player_data_pointers.items():
        players_and_tribes.players_data[key] = get_ark_profile_raw_data(key, players_and_tribes)

    for key, value in players_and_tribes.tribe_data_pointers.items():
        players_and_tribes.tribes_data[key] = get_ark_tribe_raw_data(key, players_and_tribes)

    if players_and_tribes.from_store:
        ArkSaveLogger.api_log(
            f"Found {len(players_and_tribes.players_data)} profile files and {len(players_and_tribes.tribes_data)} tribe files in save file")
    else:
        ArkSaveLogger.api_log(
            f"Found {len(players_and_tribes.profile_paths)} profile files and {len(players_and_tribes.tribe_paths)} tribe files in save directory")

    update_files(players_and_tribes)

def players_and_tribes_to_json(players_and_tribes: PlayersAndTribesParsing, pawn_objects: list[ArkGameObject]):
    players = []
    tribes = []

    # Format players into JSON.
    for player in players_and_tribes.players.values():  # player_api.players:
        player_json_obj = player.to_json_obj()
        found: bool = False
        for p in pawn_objects:
            platform_profile_id = p.get_property_value("PlatformProfileID")
            if not platform_profile_id is None:
                uniqueid = platform_profile_id.value
                if uniqueid is not None and player.unique_id == uniqueid:
                    found = True
                    break
        player_json_obj["FoundOnMap"] = found
        players.append(player_json_obj)

    # Format tribes into JSON.
    for tribe in players_and_tribes.tribes.values():  # player_api.tribes:
        # Grab the tribe json object
        tribe_json_obj = tribe.to_json_obj()
        # Grab tribe members as json objects if they exists
        tribe_members = []
        for p in players_and_tribes.tribe_to_player_map[
            tribe.tribe_id]:  # player_api.tribe_to_player_map[tribe.tribe_id]:
            tribe_members.append({"PlayerCharacterName": p.char_name, "PlayerDataID": p.id_, "IsActive": True})
        for idx, p_id in enumerate(tribe.member_ids):
            is_active = False
            for pl in players_and_tribes.tribe_to_player_map[
                tribe.tribe_id]:  # player_api.tribe_to_player_map[tribe.tribe_id]:
                if pl.id_ == p_id:
                    is_active = True
                    break
            if not is_active:
                tribe_members.append(
                    {"PlayerCharacterName": tribe.members[idx], "PlayerDataID": p_id, "IsActive": False})
        tribe_json_obj["TribeMembers"] = tribe_members
        # Add to the tribes array
        tribes.append(tribe_json_obj)

    return players, tribes

def parse_players_and_tribes_as_json(players_and_tribes: PlayersAndTribesParsing, pawn_objects: list, processes_amount: int) -> tuple[list, list]:
    if not arkparse_save.profile_data_in_saves():
        ArkSaveLogger.api_log("Profile data not found in save, checking database")
        players_and_tribes.from_store = False

    legacy_parse: bool = False
    legacy_parse_is_valid: bool = False
    if not players_and_tribes.from_store:
        legacy_parse = True
        if arkparse_save.save_dir is not None:
            legacy_parse_is_valid = True
            get_files_from_directory(arkparse_save.save_dir, players_and_tribes)
    else:
        get_offsets_from_db(arkparse_save, players_and_tribes)

        nb_tribes = len(players_and_tribes.tribe_data_pointers)
        nb_players = len(players_and_tribes.player_data_pointers)

        if processes_amount < 2 or nb_tribes < processes_amount or nb_players < processes_amount:
            legacy_parse = True
            legacy_parse_is_valid = True
        else:
            tribes_per_thread: int = math.floor(nb_tribes / processes_amount)
            remaining_tribes: int = nb_tribes % processes_amount
            players_per_thread: int = math.floor(nb_players / processes_amount)
            remaining_players: int = nb_players % processes_amount

            tribes_indexes_per_thread: list[tuple[int, int]] = []
            players_indexes_per_thread: list[tuple[int, int]] = []

            for j in range(processes_amount):
                tribes_offset = j * tribes_per_thread
                players_offset = j * players_per_thread
                if j == processes_amount - 1:
                    tribes_indexes_per_thread.append((tribes_offset, tribes_per_thread + tribes_offset + remaining_tribes))
                    players_indexes_per_thread.append((players_offset, players_per_thread + players_offset + remaining_players))
                else:
                    tribes_indexes_per_thread.append((tribes_offset, tribes_per_thread + tribes_offset))
                    players_indexes_per_thread.append((players_offset, players_per_thread + players_offset))

            players_queues: list[Queue] = []
            players_processes: list[Process] = []
            for j in range(processes_amount):
                players_queue = Queue()
                players_queues.append(players_queue)

                players_processes.append(Process(target=asi_parse_players_process, args=({"players_indexes": players_indexes_per_thread[j],
                                                                                          "players_pointers": players_and_tribes_data.player_data_pointers,
                                                                                          "data": players_and_tribes_data.data,
                                                                                          "queue": players_queue},)))

            for j in range(processes_amount):
                players_processes[j].start()

            players_processes_results: list = []
            for j in range(processes_amount):
                players_processes_results.append(players_queues[j].get(timeout=300))
                players_queues[j].close()

            for j in range(processes_amount):
                for key, val in players_processes_results[j].items():
                    for pawn in pawn_objects:
                        if val.id_ == pawn.get_property_value("LinkedPlayerDataID"):
                            val.location = ActorTransform(vector=pawn.get_property_value("SavedBaseWorldLocation"))
                            break
                    players_and_tribes.players[key] = val

            tribes_queues: list[Queue] = []
            tribes_processes: list[Process] = []
            for j in range(processes_amount):
                tribes_queue = Queue()
                tribes_queues.append(tribes_queue)
                tribes_processes.append(Process(target=asi_parse_tribes_process, args=({"tribes_indexes": tribes_indexes_per_thread[j],
                                                                                        "tribes_pointers": players_and_tribes_data.tribe_data_pointers,
                                                                                        "parsed_players": players_and_tribes.players,
                                                                                        "data": players_and_tribes_data.data,
                                                                                        "queue": tribes_queue},)))

            for j in range(processes_amount):
                tribes_processes[j].start()

            tribes_processes_results: list = []
            for j in range(processes_amount):
                tribes_processes_results.append(tribes_queues[j].get(timeout=300))
                tribes_queues[j].close()

            for j in range(processes_amount):
                for key, val in tribes_processes_results[j][0].items():
                    players_and_tribes.tribes[key] = val
                for key, val in tribes_processes_results[j][1].items():
                    players_and_tribes.tribe_to_player_map[key] = val

    if legacy_parse and legacy_parse_is_valid:
        legacy_parse_players_and_tribes(players_and_tribes)

    return players_and_tribes_to_json(players_and_tribes, pawn_objects)

if __name__ == '__main__':
    # argv[0]: This script name
    # argv[1]: ASA save file path
    # argv[2]: JSON export folder path
    # argv[3]: Export dinos?
    # argv[4]: Export player pawns?
    # argv[5]: Export items?
    # argv[6]: Export structures?
    # argv[7]: Export players?
    # argv[8]: Export tribes?

    if len(sys.argv) < 9:
        print('Wrong number of arguments provided.', flush=True)
        print('USAGE: asasaveinspector_api.py [ASA_Save_File_Path] [JSON_Export_Folder_Path] [Export_Dinos] [Export_Pawns] [Export_Items] [Export_Structures] [Export_Players] [Export_Tribes]', flush=True)
        sys.exit(2) # Exit with command line syntax error code

    save_path: Path = Path(sys.argv[1])
    export_path: Path = Path(sys.argv[2])
    export_dinos: bool = sys.argv[3] == '1'
    export_pawns: bool = sys.argv[4] == '1'
    export_items: bool = sys.argv[5] == '1'
    export_structures: bool = sys.argv[6] == '1'
    export_players: bool = sys.argv[7] == '1'
    export_tribes: bool = sys.argv[8] == '1'

    '''
    save_path: Path = Path("C:\\Users\\Shadow\\Documents\\ArkBkps\\Ragnarok\\Ragnarok_WP.ark")
    export_path: Path = Path.cwd() / "json_exports"
    export_dinos: bool = True
    export_pawns: bool = True
    export_items: bool = True
    export_structures: bool = True
    export_players: bool = True
    export_tribes: bool = True
    '''

    if not export_dinos and not export_pawns and not export_items and not export_structures and not export_players and not export_tribes:
        print('Nothing selected for extraction.', flush=True)
        sys.exit(0) # Exit with default code

    # Configure logging (only show errors)
    ArkSaveLogger.set_log_level(ArkSaveLogger.LogTypes.API, False)
    ArkSaveLogger.set_log_level(ArkSaveLogger.LogTypes.PARSER, False)
    ArkSaveLogger.set_log_level(ArkSaveLogger.LogTypes.SAVE, False)
    ArkSaveLogger.set_log_level(ArkSaveLogger.LogTypes.OBJECTS, False)
    ArkSaveLogger.set_log_level(ArkSaveLogger.LogTypes.DEBUG, False)
    ArkSaveLogger.set_log_level(ArkSaveLogger.LogTypes.INFO, False)
    ArkSaveLogger.set_log_level(ArkSaveLogger.LogTypes.WARNING, False)
    ArkSaveLogger.set_log_level(ArkSaveLogger.LogTypes.ERROR, True)

    # Configure number of process to use
    nb_processes: int = 8

    print('Loading ASA save...', flush=True)
    start = time.time()

    arkparse_save = AsaSave(path=save_path, contents=None, read_only=True, use_connection=False)
    save_connection: SaveConnection = SaveConnection(save_context=arkparse_save.save_context, path=save_path, contents=None, read_only=True)
    arkparse_save.custom_value_GameModeCustomBytes = save_connection.get_custom_value("GameModeCustomBytes")
    arkparse_save.custom_value_SaveHeader = save_connection.get_custom_value("SaveHeader")
    arkparse_save.custom_value_ActorTransforms = save_connection.get_custom_value("ActorTransforms")
    arkparse_save.initialize()

    end = time.time()
    print('ASA save loaded (time spent: ' + human_readable_time(end - start) + ').', flush=True)

    print('Parsing game objects...', flush=True)
    start = time.time()

    count_query = "SELECT COUNT(*) FROM game"
    with (save_connection.connection as conn):

        count_cursor = conn.cursor()
        count_cursor.execute(count_query)
        result = count_cursor.fetchone()
        row_count = result[0]
        count_cursor.close()

        rows_per_thread: int = math.floor(row_count / nb_processes)
        remaining_rows: int = row_count % nb_processes

        queries: list[str] = []
        if nb_processes > 1:
            for i in range(nb_processes - 1):
                queries.append(f"SELECT key, value FROM game LIMIT {rows_per_thread * i}, {rows_per_thread}")
        queries.append(f"SELECT key, value FROM game LIMIT {rows_per_thread * (nb_processes - 1)}, {rows_per_thread + remaining_rows}")

        queues: list[Queue] = []
        processes: list[Process] = []
        for i in range(nb_processes):
            dataset = {}
            cursor = conn.execute(queries[i])
            for row in cursor:
                dataset[row[0]] = row[1]
            cursor.close()
            queue = Queue()
            queues.append(queue)
            processes.append(Process(target=asi_export_process, args=({ "dataset": dataset, "save_context": arkparse_save.save_context, "queue": queue },)))

    for i in range(nb_processes):
        processes[i].start()

    processes_results: list = []
    for i in range(nb_processes):
        processes_results.append(queues[i].get(timeout=300))
        queues[i].close()

    merged_results: list = []
    for i in range(nb_processes):
        merged_results += processes_results[i]

    if arkparse_save.game_obj_binaries is None:
        arkparse_save.game_obj_binaries = {}
    if arkparse_save.all_classes is None:
        arkparse_save.all_classes = []
    for parsed in merged_results:
        arkparse_save.parsed_objects[parsed["UUID"]] = parsed["object"]
        arkparse_save.game_obj_binaries[parsed["UUID"]] = parsed["bytes"]
        arkparse_save.all_classes.append(parsed["class"])

    arkparse_save._get_game_time_params()
    save_info = { "MapName": arkparse_save.save_context.map_name,
                  "GameTime": arkparse_save.save_context.game_time,
                  "CurrentDay": arkparse_save.save_context.current_day,
                  "CurrentTime": arkparse_save.save_context.current_time }

    queues_b: list[Queue] = []
    processes_b: list[Process] = []
    for i in range(nb_processes):
        queue_b = Queue()
        queues_b.append(queue_b)
        processes_b.append(Process(target=asi_parse_process, args=({ "objects": [item["object"] for item in processes_results[i]], "save": arkparse_save, "queue": queue_b },)))

    for i in range(nb_processes):
        processes_b[i].start()

    processes_results_b: list[dict] = []
    for i in range(nb_processes):
        processes_results_b.append(queues_b[i].get(timeout=300))
        queues_b[i].close()

    pawn_objects: list = []
    dinos: list = []
    pawns: list = []
    items: list = []
    structures: list = []

    for i in range(nb_processes):
        dinos += processes_results_b[i]["dinos"]
        items += processes_results_b[i]["items"]
        structures += processes_results_b[i]["structures"]
        pawn_objects += processes_results_b[i]["pawns"]
        for pawn_object in processes_results_b[i]["pawns"]:
            if not pawn_object is None:
                pawns.append(pawn_to_json(pawn_object))

    end = time.time()
    print('Parsed game objects (time spent: ' + human_readable_time(end - start) + ').', flush=True)

    all_players = []
    all_tribes = []
    if export_players or export_tribes:
        print('Parsing players and tribes...', flush=True)
        players_and_tribes_start = time.time()

        # player_api = PlayerApi(arkparse_save, True, True, True)
        players_and_tribes_data = PlayersAndTribesParsing()
        players_and_tribes_data.save = arkparse_save
        all_players, all_tribes = parse_players_and_tribes_as_json(players_and_tribes_data, pawn_objects, nb_processes) # math.floor(nb_processes / 2)

        players_and_tribes_end = time.time()
        print('Parsed players and tribes (time spent: ' + human_readable_time(players_and_tribes_end - players_and_tribes_start) + ').', flush=True)

    print('Creating JSON files...', flush=True)
    start = time.time()

    # Create json exports folder if it does not exist.
    if not (export_path.exists() and export_path.is_dir()):
        export_path.mkdir(parents=True, exist_ok=True)

    # Write JSONs.
    with open(export_path / "save_info.json", "w") as text_file:
        text_file.write(
            json.dumps(save_info, default=lambda o: o.to_json_obj() if hasattr(o, 'to_json_obj') else None,
                       indent=4, cls=DefaultJsonEncoder))
    if export_dinos:
        with open(export_path / "dinos.json", "w") as text_file:
            text_file.write(
                json.dumps(dinos, default=lambda o: o.to_json_obj() if hasattr(o, 'to_json_obj') else None,
                           indent=4, cls=DefaultJsonEncoder))
    if export_items:
        with open(export_path / "items.json", "w") as text_file:
            text_file.write(
                json.dumps(items, default=lambda o: o.to_json_obj() if hasattr(o, 'to_json_obj') else None,
                           indent=4, cls=DefaultJsonEncoder))
    if export_pawns:
        with open(export_path / "player_pawns.json", "w") as text_file:
            text_file.write(
                json.dumps(pawns, default=lambda o: o.to_json_obj() if hasattr(o, 'to_json_obj') else None,
                           indent=4, cls=DefaultJsonEncoder))
    if export_structures:
        with open(export_path / "structures.json", "w") as text_file:
            text_file.write(
                json.dumps(structures, default=lambda o: o.to_json_obj() if hasattr(o, 'to_json_obj') else None,
                           indent=4, cls=DefaultJsonEncoder))
    if export_players:
        with open(export_path / "players.json", "w") as text_file:
            text_file.write(
                json.dumps(all_players, default=lambda o: o.to_json_obj() if hasattr(o, 'to_json_obj') else None,
                           indent=4, cls=DefaultJsonEncoder))
    if export_tribes:
        with open(export_path / "tribes.json", "w") as text_file:
            text_file.write(
                json.dumps(all_tribes, default=lambda o: o.to_json_obj() if hasattr(o, 'to_json_obj') else None,
                           indent=4, cls=DefaultJsonEncoder))

    end = time.time()
    print('JSON files created (time spent: ' + human_readable_time(end - start) + ').', flush=True)

    for i in range(nb_processes):
        processes[i].join(timeout=5)
    for i in range(nb_processes):
        processes_b[i].join(timeout=5)
