import base64
import json
import math
import os
import sqlite3
import sys
import time
import uuid
from multiprocessing import Queue, Process
from queue import Empty as QueueEmpty
from pathlib import Path
from typing import Optional, OrderedDict

from arkparse import AsaSave
from arkparse.api import PlayerApi
from arkparse.api.json_api import JsonApi
from arkparse.ark_tribe import ArkTribe
from arkparse.logging import ArkSaveLogger
from arkparse.object_model.ark_game_object import ArkGameObject
from arkparse.object_model.cryopods.cryopod import Cryopod
from arkparse.object_model.dinos import Dino, TamedDino, Baby, TamedBaby
from arkparse.object_model.structures import Structure, StructureWithInventory
from arkparse.parsing import ArkBinaryParser
from arkparse.parsing.struct import ActorTransform, ObjectReference
from arkparse.player.ark_player import ArkPlayer
from arkparse.player.hexagon_state import HexagonState
from arkparse.saves.save_connection import SaveConnection
from arkparse.utils.json_utils import DefaultJsonEncoder

BLUEPRINTS_TO_SKIP: list[str] = [ "/ByteArrayObject",
                                  "/Doorsystem/ButtonSystem",
                                  "Script/Foliage",
                                  "/NPCZoneManagerBlueprint_",
                                  "/PlayerControllerBlueprint",
                                  "/Script/Engine.Texture2D",
                                  "/Script/ShooterGame.ShooterMatineeActor",
                                  "/Script/ShooterGame.StructurePaintingComponent",
                                  "/Script/ShooterGame.VesselDynamicsComponent",
                                  "/Sound/",
                                  "/Traps/DoorAndButtons",
                                  "/Trap/Multi_Switch_Door",
                                  "/Weapons/Weap",
                                  "/Weapons/GeneScanner/WeapGeneScanner.WeapGeneScanner_C",
                                  "AnimSequence",
                                  "BossArenaManager",
                                  "Engine.BlockingVolume",
                                  "Engine.MaterialInstanceConstant",
                                  "InstancedFoliageActor",
                                  "NPCZoneManager",
                                  "NPCZoneVolume",
                                  "MissionManager_LC_Overworld_Base",
                                  "PrimalPersistentWorldData",
                                  "PrimalInventory",
                                  "GargoyleHarvestComponent_",
                                  "CreatureTamedInventoryComponent_GrandTortugar_",
                                  "/DinoDeathHarvestingComponent_",
                                  "/DinoDropInventoryComponent_",
                                  "/DinoInventoryComponent_",
                                  "/DinoTamedInventoryComponent_",
                                  "/DinoWildInventoryComponent_",
                                  "/InventoryComponent_",
                                  "/AstraeosCreatures/AstraeosCreatures_Singleton.AstraeosCreatures_Singleton_C" ]

BLUEPRINTS_NO_EXPORT: list[str] = [ "/AI/",
                                    "_AI_Blueprint.",
                                    "_AIController",
                                    "_AI_Controller",
                                    "RAG_IceWyvern_AI_C",
                                    "Paracer_AI_Blueprint_Corrupt",
                                    "Spino_AI_Blueprint_Corrupt",
                                    "/DinoCharacterStatus_BP",
                                    "DinoCharacterStatusComponent_",
                                    "DinoCharacterStatusComponent_BP",
                                    "/PlayerCharacterStatusComponent_BP",
                                    "/Buffs/",
                                    "Buff_DinoCompanion_YoungIceFox_C",
                                    "Buff_SanguineElixirUsed_C",
                                    "Buff_TrickMeterBonuses_Concavenator_C",
                                    "Buff_TimeRewind_Cooldown_C",
                                    "Buff_WoundedFury_Gigant_C",
                                    "PrimalBuffPersistentData",
                                    "DayCycle" ]

def is_in_str(search_for: list[str], search_in: str) -> bool:
    if search_in is None or len(search_in) <= 0 or search_for is None or len(search_for) <= 0:
        return False
    for s in search_for:
        if s is not None and len(s) > 0 and s in search_in:
            return True
    return False

def is_dino_blueprint(game_obj: ArkGameObject, additional_blueprints: list[str]) -> bool:
    if game_obj.get_property_value("DinoID1") is None \
            or game_obj.get_property_value("DinoID2") is None:
        return False
    if is_in_str(BLUEPRINTS_NO_EXPORT, game_obj.blueprint):
        return False
    if ("_Character_" in game_obj.blueprint or "_Char_" in game_obj.blueprint) and ("Dino" in game_obj.blueprint or "Creature" in game_obj.blueprint):
        return True
    if "/Raft_BP.Raft_BP" in game_obj.blueprint \
            or "/Raft/MotorRaft_BP.MotorRaft_BP" in game_obj.blueprint \
            or "/Raft/Tireme_BP_" in game_obj.blueprint \
            or "Car_Vehicle_BP.Car_Vehicle_BP_C" in game_obj.blueprint \
            or "InsectSwarmChar_BP.InsectSwarmChar_BP_C" in game_obj.blueprint \
            or "MicrobeSwarmChar_BP.MicrobeSwarmChar_BP_C" in game_obj.blueprint \
            or "BrigShipBP.BrigShipBP_C" in game_obj.blueprint \
            or "SloopShipBP.SloopShipBP_C" in game_obj.blueprint \
            or "GalleonShipBP.GalleonShipBP_C" in game_obj.blueprint \
            or "TriremeShipBP.TriremeShipBP_C" in game_obj.blueprint \
            or "TriremeShipPlayerFollowingBP.TriremeShipPlayerFollowingBP_C" in game_obj.blueprint \
            or "BrigShipPlayerFollowingBP.BrigShipPlayerFollowingBP_C" in game_obj.blueprint \
            or "SloopShipPlayerFollowingBP.SloopShipPlayerFollowingBP_C" in game_obj.blueprint \
            or "TriremeShipPlayerFollowingBP_FleetCaptain_SmallFleet.TriremeShipPlayerFollowingBP_FleetCaptain_SmallFleet_C" in game_obj.blueprint \
            or "TriremeShipPlayerFollowingBP_FleetEscort.TriremeShipPlayerFollowingBP_FleetEscort_C" in game_obj.blueprint \
            or "Gargantar_Character_BP.Gargantar_Character_BP_C" in game_obj.blueprint \
            or "Lumina_Character_BP.Lumina_Character_BP_C" in game_obj.blueprint \
            or "Umbra_Character_BP.Umbra_Character_BP_C" in game_obj.blueprint \
            or "Polar_Bear.Polar_Bear_C" in game_obj.blueprint:
        return True
    if additional_blueprints is not None and len(additional_blueprints) > 0:
        for additional_blueprint in additional_blueprints:
            if additional_blueprint:
                if additional_blueprint in game_obj.blueprint:
                    return True
    return False

def is_structure_blueprint(game_obj: ArkGameObject, additional_blueprints: list[str]) -> bool:
    if "TekBunker/Structures/BP_Bunker_Base.BP_Bunker_Base_C" in game_obj.blueprint:
        return True
    if game_obj.get_property_value("StructureID") is None:
        return False
    if is_in_str(BLUEPRINTS_NO_EXPORT, game_obj.blueprint):
        return False
    if game_obj.get_property_value("DinoID1") is None \
            and game_obj.get_property_value("ItemID") is None:
        return True
    if "/Structures" in game_obj.blueprint \
            or "/GigantoraptorNest" in game_obj.blueprint \
            or "Rug_Shag" in game_obj.blueprint \
            or "TributeTerminal" in game_obj.blueprint \
            or "StructureStandingTurret_Flightseeker" in game_obj.blueprint \
            or "ArtifactCrate_" in game_obj.blueprint \
            or "/WyvernNest" in game_obj.blueprint \
            or "/Structure_PlantSpecies" in game_obj.blueprint \
            or "/HordeCrates/ElementWall_Horde" in game_obj.blueprint \
            or "/Flag_SM_" in game_obj.blueprint \
            or "/SupplyCrateBaseBP_" in game_obj.blueprint \
            or "/StructureBP_WoodBench" in game_obj.blueprint \
            or "MB_Button.MB_Button_C" in game_obj.blueprint \
            or "/PortableRope_Ladder_" in game_obj.blueprint \
            or "BP_HeroPoisonPlant.BP_HeroPoisonPlant_C" in game_obj.blueprint \
            or "BP_HelperBot_DockingBay" in game_obj.blueprint:
        return True
    if additional_blueprints is not None and len(additional_blueprints) > 0:
        for additional_blueprint in additional_blueprints:
            if additional_blueprint:
                if additional_blueprint in game_obj.blueprint:
                    return True
    return False

def is_item_blueprint(game_obj: ArkGameObject, additional_blueprints: list[str]) -> bool:
    if is_in_str(BLUEPRINTS_NO_EXPORT, game_obj.blueprint):
        return False
    if "/DroppedItemGeneric" in game_obj.blueprint or \
            "Egg_Wyvern_Fertilized" in game_obj.blueprint or \
            "/PrimalItem_" in game_obj.blueprint or \
            "/PrimalItemAmmo_" in game_obj.blueprint or \
            "/PrimalItemArmor_" in game_obj.blueprint or \
            "/PrimalItemArtifact" in game_obj.blueprint or \
            "/PrimalItemC4Ammo" in game_obj.blueprint or \
            "/PrimalItemConsumable_" in game_obj.blueprint or \
            "/PrimalItemConsumableBuff_Parachute" in game_obj.blueprint or \
            "/PrimalItemConsumableEatable_" in game_obj.blueprint or \
            "/PrimalItemConsumableMiracleGro" in game_obj.blueprint or \
            "/PrimalItemConsumableRespecSoup" in game_obj.blueprint or \
            "/PrimalItemConsumableSoap" in game_obj.blueprint or \
            "/PrimalItemCostume_" in game_obj.blueprint or \
            "/PrimalItemCustomDrinkRecipe_" in game_obj.blueprint or \
            "/PrimalItemCustomFoodRecipe_" in game_obj.blueprint or \
            "/PrimalItemDinoCostume_" in game_obj.blueprint or \
            "/PrimalItemDye_" in game_obj.blueprint or \
            "/PrimalItemMotorboat" in game_obj.blueprint or \
            "/PrimalItemRadio" in game_obj.blueprint or \
            "/PrimalItemRaft" in game_obj.blueprint or \
            "/PrimalItemResource_" in game_obj.blueprint or \
            "/PrimalItemSkin_" in game_obj.blueprint or \
            "/PrimalItemStructure_" in game_obj.blueprint or \
            "/PrimalItemStructureSkin_" in game_obj.blueprint or \
            "/PrimalItemTrophy" in game_obj.blueprint or \
            "/PrimalItemWeaponAttachment_" in game_obj.blueprint:
        return True
    if additional_blueprints is not None and len(additional_blueprints) > 0:
        for additional_blueprint in additional_blueprints:
            if additional_blueprint:
                if additional_blueprint in game_obj.blueprint:
                    return True
    return False

def human_readable_time(time_in_sec : float):
    # Round the total first (rounding only the seconds could give "1m60s"), and show at least 1s.
    # The previous version also turned 0 seconds into 1 second in all cases (ex: exactly 1 hour was shown as "1h0m1s").
    total_seconds: int = max(1, round(time_in_sec))
    hours, remaining = divmod(total_seconds, 3600)
    minutes, seconds = divmod(remaining, 60)
    if hours > 0:
        return str(hours) + 'h' + str(minutes) + 'm' + str(seconds) + 's'
    elif minutes > 0:
        return str(minutes) + 'm' + str(seconds) + 's'
    else:
        return str(seconds) + 's'

def get_obj_for_uuid(obj_uuid: uuid.UUID, save: AsaSave):
    if obj_uuid in save.parsed_objects:
        return save.parsed_objects[obj_uuid]
    elif obj_uuid in save.game_obj_binaries:
        reader = ArkBinaryParser(save.game_obj_binaries[obj_uuid], save.save_context)
        try:
            return ArkGameObject(obj_uuid, reader.read_name(), reader)
        except Exception:
            pass
    return None

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
        print(f"Exception caught when parsing structure {obj.uuid}: {e}", flush=True)
        return None

def parse_single_cryopod(obj: ArkGameObject, save: AsaSave) -> Optional[Cryopod]:
    try:
        return Cryopod(obj.uuid, save)
    except Exception as e:
        if not "Unsupported embedded data version (only Unreal 5.5 is supported)" in str(e):
            print(f"Exception caught when parsing cryopod {obj.uuid}: {e}", flush=True)
        return None

def parse_single_dino(obj: ArkGameObject, save: AsaSave):
    try:
        is_tamed = obj.get_property_value("TamedTimeStamp") is not None
        is_baby = obj.get_property_value("bIsBaby", False)

        if is_tamed:
            if is_baby:
                return TamedBaby(obj.uuid, save)
            else:
                return TamedDino(obj.uuid, save)
        else:
            if is_baby:
                return Baby(obj.uuid, save)
            else:
                return Dino(obj.uuid, save)
    except Exception as e:
        print(f"Exception caught when parsing dino {obj.uuid}: {e}", flush=True)
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

def open_save_read_only(save_file_path: Path) -> sqlite3.Connection:
    """Opens the .ark save (SQLite database) in read-only mode."""
    try:
        return sqlite3.connect(Path(save_file_path).resolve().as_uri() + "?mode=ro", uri=True)
    except sqlite3.OperationalError:
        # Some paths (ex: network shares) cannot be opened as an URI: fall back to a regular connection (only SELECT queries are run).
        return sqlite3.connect(str(save_file_path))

# Number of objects sent at once by a worker process to the main process.
# Before, each worker sent ALL its objects in a single message: the worker had to keep its whole list AND its whole pickled copy
# in memory, and the main process had to receive the whole pickled copy before unpickling it (memory peaks on big saves).
WORKER_CHUNK_SIZE: int = 5000

# Messages sent by worker processes on the shared result queue: (message type, worker index, payload).
MSG_CHUNK: str = "chunk"  # payload: list of parsed objects
MSG_DONE: str = "done"    # payload: number of objects sent by this worker
MSG_ERROR: str = "error"  # payload: error message

def asi_export_process(arg_obj):
    # arg_obj["save_path"]: Path of the .ark save file
    # arg_obj["offset"] / arg_obj["limit"]: Rows range handled by this worker
    # arg_obj["save_context"]: SaveContext
    # arg_obj["queue"]: Queue (shared by all workers) used to return the results
    # arg_obj["worker_index"]: Index of this worker
    result_queue = arg_obj["queue"]
    worker_index: int = arg_obj["worker_index"]
    try:
        chunk: list = []
        sent: int = 0
        # Each worker reads its own rows: the main process does not have to load the whole save and send it to every worker.
        conn = open_save_read_only(arg_obj["save_path"])
        try:
            cursor = conn.execute("SELECT key, value FROM game ORDER BY key LIMIT ? OFFSET ?", (arg_obj["limit"], arg_obj["offset"]))
            for key, value in cursor:
                try:
                    byte_buffer = ArkBinaryParser(value, arg_obj["save_context"])
                    obj_uuid = uuid.UUID(bytes=key)
                except Exception as ex:
                    print(f"Exception caught when reading object with key {key!r}: {ex}", flush=True)
                    continue
                try:
                    class_name = byte_buffer.read_name()
                except Exception as ex:
                    class_name = None
                    print(f"Exception caught when parsing object with UUID {obj_uuid}: {ex}", flush=True)

                if class_name is not None:
                    # Skipped objects are not parsed (they are never exported), but their raw bytes are still sent back
                    # so that ArkParse can still find them if another object references them (inventories...).
                    if is_in_str(BLUEPRINTS_TO_SKIP, class_name):
                        chunk.append({ "UUID": obj_uuid, "class": class_name, "game_obj": None, "bytes": value })
                    else:
                        try:
                            game_obj = ArkGameObject(obj_uuid, class_name, byte_buffer)
                        except Exception as e:
                            game_obj = None
                            print(f"Exception caught when parsing object of class {class_name}: {e}", flush=True)
                        if game_obj is not None:
                            chunk.append({ "UUID": obj_uuid, "class": class_name, "game_obj": game_obj, "bytes": value })

                    # Send the objects by chunks, as soon as they are parsed (the main process receives them while the workers keep parsing).
                    if len(chunk) >= WORKER_CHUNK_SIZE:
                        result_queue.put((MSG_CHUNK, worker_index, chunk))
                        sent += len(chunk)
                        chunk = []
            cursor.close()
        finally:
            conn.close()

        if len(chunk) > 0:
            result_queue.put((MSG_CHUNK, worker_index, chunk))
            sent += len(chunk)
            chunk = []
        result_queue.put((MSG_DONE, worker_index, sent))
    except Exception as ex:
        # Always send something to the main process, otherwise it would wait for a result that will never come.
        print(f"Worker process {worker_index} failed: {ex}", flush=True)
        result_queue.put((MSG_ERROR, worker_index, str(ex)))

def get_workers_results(result_queue: Queue, processes: list, max_wait_without_news_sec: int = 3600) -> list:
    """Receives the chunks sent by all worker processes (in any order), and returns the objects of each worker, in worker order
    (so that the objects order, and thus the JSON files content, stays the same as before).
    Fails explicitly if a worker reports an error, dies without finishing, or if no worker sends anything for too long."""
    nb_workers: int = len(processes)
    results: list = [[] for _ in range(nb_workers)]
    done: set = set()
    waited: int = 0
    while len(done) < nb_workers:
        try:
            msg_type, worker_index, payload = result_queue.get(timeout=5)
        except QueueEmpty:
            waited += 5
            dead_workers = [i for i in range(nb_workers) if i not in done and not processes[i].is_alive()]
            if len(dead_workers) > 0:
                # A worker has exited without saying it was done: give it a last chance to deliver its last messages.
                try:
                    msg_type, worker_index, payload = result_queue.get(timeout=2)
                except QueueEmpty:
                    i = dead_workers[0]
                    raise RuntimeError(f"Worker process {i} exited (exit code {processes[i].exitcode}) without returning all its results.")
            elif waited >= max_wait_without_news_sec:
                raise RuntimeError(f"Worker processes did not return any result for {max_wait_without_news_sec} seconds.")
            else:
                continue
        waited = 0
        if msg_type == MSG_CHUNK:
            results[worker_index].extend(payload)
        elif msg_type == MSG_DONE:
            done.add(worker_index)
        elif msg_type == MSG_ERROR:
            raise RuntimeError(f"Worker process {worker_index} failed: {payload}")
    return results

class AsiJsonEncoder(DefaultJsonEncoder):
    """Uses to_json_obj() when available, and falls back to ArkParse's DefaultJsonEncoder for everything else (UUID, dates...).
    Previously, passing default=... together with cls=DefaultJsonEncoder replaced DefaultJsonEncoder.default, so these values were written as null."""
    _warned_types: set = set()

    def default(self, o):
        if hasattr(o, 'to_json_obj'):
            return o.to_json_obj()
        try:
            return super().default(o)
        except TypeError:
            # Same tolerance as before for types that nobody knows how to serialize, but it is now reported (once per type).
            type_name = type(o).__name__
            if type_name not in AsiJsonEncoder._warned_types:
                AsiJsonEncoder._warned_types.add(type_name)
                print(f"Warning: values of type {type_name} cannot be serialized to JSON, they are written as null.", flush=True)
            return None

def non_finite_float_to_str(value: float):
    """NaN and +/-Infinity are not valid JSON: Python writes them as bare NaN / Infinity tokens, which System.Text.Json rejects
    (the whole file then went through ASI's slow fallback loader, and the objects containing them were lost).
    They are written as the strings "NaN", "Infinity" and "-Infinity" instead, which ASI reads back as float values."""
    if math.isnan(value):
        return "NaN"
    if math.isinf(value):
        return "Infinity" if value > 0 else "-Infinity"
    return value

def to_json_compatible(obj, encoder: json.JSONEncoder, depth: int = 0):
    """Converts obj into plain JSON types (dict, list, str, int, float, bool, None), replacing non-finite floats by strings.
    Objects that are not plain JSON types are converted with encoder.default() (to_json_obj(), UUIDs, dates...)."""
    if depth > 200:
        return None  # Protection against reference cycles.
    if obj is None or isinstance(obj, (str, bool, int)):
        return obj
    if isinstance(obj, float):
        return non_finite_float_to_str(obj)
    if isinstance(obj, dict):
        return { k: to_json_compatible(v, encoder, depth + 1) for k, v in obj.items() }
    if isinstance(obj, (list, tuple, set)):
        return [ to_json_compatible(v, encoder, depth + 1) for v in obj ]
    converted = encoder.default(obj)
    if converted is obj:
        return None
    return to_json_compatible(converted, encoder, depth + 1)

def write_json_file(file_path: Path, data, use_to_json_obj: bool = True) -> None:
    """Writes a JSON file atomically: a crash can no longer leave a truncated JSON file that ASI would try to load."""
    encoder = AsiJsonEncoder() if use_to_json_obj else DefaultJsonEncoder()
    # Note: indent=4 is kept on purpose, ASI's fallback JSON loader relies on it.
    json_str = json.dumps(to_json_compatible(data, encoder), indent=4, cls=(AsiJsonEncoder if use_to_json_obj else DefaultJsonEncoder))
    tmp_path = file_path.with_name(file_path.name + ".tmp")
    with open(tmp_path, "w", encoding="utf-8") as text_file:
        text_file.write(json_str)
    os.replace(tmp_path, file_path)

def asi_parse_classic(save: AsaSave, dino_bps: list[str], item_bps: list[str], structure_bps: list[str]):
    all_dinos = []
    all_pawns_objects = []
    all_items = []
    all_structures = []
    skipped_bps: list[str] = []
    unknown_bps: list[str] = []
    failed_parsing: list[str] = []
    # Iterate over a copy: ArkParse may lazily add objects to parsed_objects while we parse (ex: skipped objects referenced by others).
    for obj in list(save.parsed_objects.values()):
        if obj is not None:
            try:
                if is_in_str(BLUEPRINTS_TO_SKIP, obj.blueprint):
                    if debug_logging and obj.blueprint not in skipped_bps:
                        skipped_bps.append(obj.blueprint)
                    continue

                if is_dino_blueprint(obj, dino_bps):
                    dino = parse_single_dino(obj, save)
                    if dino is not None:
                        all_dinos.append(dino)
                    elif debug_logging:
                        failed_parsing.append(f"Dino {obj.uuid} with class {obj.blueprint}.")
                elif "PrimalItem_WeaponEmptyCryopod" in obj.blueprint or "PrimalItem_SCSCryopod" in obj.blueprint:
                    cryopod = parse_single_cryopod(obj, save)
                    if cryopod is not None:
                        if cryopod.dino is not None:
                            cryopod.dino.is_cryopodded = True
                            all_dinos.append(cryopod.dino)
                        if cryopod.saddle is not None:
                            saddle_from_cryopod = JsonApi.primal_item_to_json_obj(cryopod.saddle.object)
                            # Get blueprint.
                            saddle_blueprint: str = cryopod.saddle.blueprint.__str__()
                            delimiter_pos: int = saddle_blueprint.find(" ")
                            if delimiter_pos > 0 and len(saddle_blueprint) > delimiter_pos:
                                saddle_blueprint = saddle_blueprint[delimiter_pos + 1:]
                            saddle_from_cryopod["ItemArchetype"] = saddle_blueprint
                            # Get owner cryopod UUID.
                            saddle_owner_cryopod_uuid: str = "00000000000000000000000000000000"
                            if cryopod.uuid is not None:
                                saddle_owner_cryopod_uuid = cryopod.uuid.__str__()
                            saddle_from_cryopod["OwnerCryopodUUID"] = saddle_owner_cryopod_uuid
                            # Adjust quantity.
                            saddle_from_cryopod["ItemQuantity"] = 1
                            # Append saddle to items.
                            all_items.append(saddle_from_cryopod)
                    elif debug_logging:
                        failed_parsing.append(f"Cryopod {obj.uuid} content.")
                    all_items.append(JsonApi.primal_item_to_json_obj(obj))
                elif "/PlayerPawnTest_Female.PlayerPawnTest_Female_C" in obj.blueprint or "/PlayerPawnTest_Male.PlayerPawnTest_Male_C" in obj.blueprint:
                    all_pawns_objects.append(obj)
                elif is_item_blueprint(obj, item_bps):
                    all_items.append(JsonApi.primal_item_to_json_obj(obj))
                elif is_structure_blueprint(obj, structure_bps):
                    structure = parse_single_structure(obj, save, True)
                    if structure is not None:
                        all_structures.append(structure)
                    elif debug_logging:
                        failed_parsing.append(f"Structure {obj.uuid} with class {obj.blueprint}.")
                # Fallback cases
                elif obj.get_property_value("ItemID") is not None:
                    all_items.append(JsonApi.primal_item_to_json_obj(obj))
                # Absolutely unknown
                elif not is_in_str(BLUEPRINTS_NO_EXPORT, obj.blueprint):
                    if debug_logging and obj.blueprint not in unknown_bps:
                        unknown_bps.append(obj.blueprint)
                        print(f"Unknown object {obj.uuid} with class {obj.blueprint}.", flush=True)
            except Exception as e:
                if not "Unsupported embedded data version (only Unreal 5.5 is supported)" in str(e):
                    print(f"Exception caught during parsing: {e}", flush=True)

    return { "dinos": all_dinos, "pawns": all_pawns_objects, "items": all_items, "structures": all_structures, "skipped_blueprints": skipped_bps, "unknown_blueprints": unknown_bps, "failed_parsing": failed_parsing }

def players_and_tribes_to_json(player_api: PlayerApi, pawn_objects: list):
    players = []
    tribes = []

    pawn_by_id = {p.get_property_value("LinkedPlayerDataID"): p for p in player_api.pawns.values()}

    # Unique IDs of the players that have a pawn on the map, computed once (it used to loop over every pawn for every player).
    unique_ids_on_map: set = set()
    for p in pawn_objects:
        platform_profile_id = p.get_property_value("PlatformProfileID")
        if not platform_profile_id is None and platform_profile_id.value is not None:
            unique_ids_on_map.add(platform_profile_id.value)

    # Format players into JSON.
    for player in player_api.players:
        player_json_obj = player.to_json_obj()

        player_json_obj["FoundOnMap"] = player.unique_id in unique_ids_on_map
        
        hexagons = HexagonState.from_player(player, pawn_by_id.get(player.id_))
        if hexagons.has_hexagons:
            player_json_obj["Hexagons"] = hexagons.count

        players.append(player_json_obj)

    # Format tribes into JSON.
    for tribe in player_api.tribes:
        # Grab the tribe json object
        tribe_json_obj = tribe.to_json_obj()
        # Grab tribe members as json objects if they exists
        tribe_members = []
        tribe_players = player_api.tribe_to_player_map.get(tribe.tribe_id, [])  # A tribe can have no known player.
        active_ids: set = set()
        for p in tribe_players:
            tribe_members.append({"PlayerCharacterName": p.char_name, "PlayerDataID": p.id_, "IsActive": True})
            active_ids.add(p.id_)
        for idx, p_id in enumerate(tribe.member_ids):
            if p_id not in active_ids:
                member_name = tribe.members[idx] if idx < len(tribe.members) else ""
                tribe_members.append({"PlayerCharacterName": member_name, "PlayerDataID": p_id, "IsActive": False})
        tribe_json_obj["TribeMembers"] = tribe_members
        # Add to the tribes array
        tribes.append(tribe_json_obj)

    return players, tribes

def parse_custom_blueprints_arg(custom_blueprints: str):
    custom_blueprints_dinos: list[str] = []
    custom_blueprints_items: list[str] = []
    custom_blueprints_structures: list[str] = []
    try:
        if custom_blueprints is not None and len(custom_blueprints) > 0:
            split_blueprints: list[str] = custom_blueprints.split("|")
            if split_blueprints is not None and len(split_blueprints) > 0:
                for split_blueprint in split_blueprints:
                    if split_blueprint is not None and len(split_blueprint) > 0:
                        if split_blueprint.startswith("Dinos=") and len(split_blueprint) > 6:
                            dinos_blueprints: list[str] = split_blueprint[6:].split(";")
                            if dinos_blueprints is not None and len(dinos_blueprints) > 0:
                                for dino_blueprint in dinos_blueprints:
                                    if dino_blueprint is not None and len(dino_blueprint) > 0:
                                        custom_blueprints_dinos.append(dino_blueprint)
                        elif split_blueprint.startswith("Items=") and len(split_blueprint) > 6:
                            items_blueprints: list[str] = split_blueprint[6:].split(";")
                            if items_blueprints is not None and len(items_blueprints) > 0:
                                for items_blueprint in items_blueprints:
                                    if items_blueprint is not None and len(items_blueprint) > 0:
                                        custom_blueprints_items.append(items_blueprint)
                        elif split_blueprint.startswith("Structures=") and len(split_blueprint) > 11:
                            structures_blueprints: list[str] = split_blueprint[11:].split(";")
                            if structures_blueprints is not None and len(structures_blueprints) > 0:
                                for structures_blueprint in structures_blueprints:
                                    if structures_blueprint is not None and len(structures_blueprint) > 0:
                                        custom_blueprints_structures.append(structures_blueprint)
    except Exception:
        pass
    return [ custom_blueprints_dinos, custom_blueprints_items, custom_blueprints_structures ]

def load_custom_blueprints_file(file_path: Path):
    """Loads the custom blueprints JSON file written by ASI: {"Dinos": [...], "Items": [...], "Structures": [...]}."""
    with open(file_path, "r", encoding="utf-8-sig") as f:
        data = json.load(f)
    def get_list(key: str) -> list[str]:
        values = data.get(key) if isinstance(data, dict) else None
        if not isinstance(values, list):
            return []
        return [v for v in values if isinstance(v, str) and len(v.strip()) > 0]
    return [ get_list("Dinos"), get_list("Items"), get_list("Structures") ]

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
    # argv[9]: Enable debug logging?
    # argv[10]: (Optional) Path of the custom blueprints JSON file (the previous base64 format is still accepted).

    if len(sys.argv) < 10:
        print('Wrong number of arguments provided.', flush=True)
        print('USAGE: asi_export_fast.py [ASA_Save_File_Path] [JSON_Export_Folder_Path] [Export_Dinos] [Export_Pawns] [Export_Items] [Export_Structures] [Export_Players] [Export_Tribes] [Debug_Logging] [Additional_Blueprints]', flush=True)
        sys.exit(2) # Exit with command line syntax error code

    save_path: Path = Path(sys.argv[1])
    export_path: Path = Path(sys.argv[2])
    export_dinos: bool = sys.argv[3] == '1'
    export_pawns: bool = sys.argv[4] == '1'
    export_items: bool = sys.argv[5] == '1'
    export_structures: bool = sys.argv[6] == '1'
    export_players: bool = sys.argv[7] == '1'
    export_tribes: bool = sys.argv[8] == '1'
    debug_logging: bool = sys.argv[9] == '1'

    custom_bps_dinos: list[str] = []
    custom_bps_items: list[str] = []
    custom_bps_structures: list[str] = []
    if len(sys.argv) > 10 and len(sys.argv[10]) > 0:
        try:
            if sys.argv[10].lower().endswith(".json"):
                # Custom blueprints are given in a JSON file (no command line length limit, no separator issue).
                parsed_custom_bps = load_custom_blueprints_file(Path(sys.argv[10]))
            else:
                # Previous format (base64 command line argument), kept for compatibility (ex: existing automation scripts).
                encoded_bps: str = sys.argv[10].replace('_', '/').replace('-', '+').replace(',','=')
                custom_bps: str = base64.b64decode(encoded_bps).decode('utf-8')
                parsed_custom_bps = parse_custom_blueprints_arg(custom_bps)
            custom_bps_dinos = parsed_custom_bps[0]
            custom_bps_items = parsed_custom_bps[1]
            custom_bps_structures = parsed_custom_bps[2]
        except Exception as ex:
            print(f"Failed to load custom blueprints from '{sys.argv[10]}': {ex}", flush=True)

    '''
    save_path: Path = Path("D:/BACKUPS/TheIsland/TheIsland_WP.ark")
    export_path: Path = Path.cwd() / "json_exports" / "Test"
    export_dinos: bool = True
    export_pawns: bool = True
    export_items: bool = True
    export_structures: bool = True
    export_players: bool = True
    export_tribes: bool = True
    debug_logging: bool = True
    custom_bps_dinos: list[str] = []
    custom_bps_items: list[str] = []
    custom_bps_structures: list[str] = []
    '''

    if not export_dinos and not export_pawns and not export_items and not export_structures and not export_players and not export_tribes:
        print('Nothing selected for extraction.', flush=True)
        sys.exit(0) # Exit with default code

    # Configure logging (only show warnings and errors)
    ArkSaveLogger.set_log_level(ArkSaveLogger.LogTypes.API, False)
    ArkSaveLogger.set_log_level(ArkSaveLogger.LogTypes.PARSER, False)
    ArkSaveLogger.set_log_level(ArkSaveLogger.LogTypes.SAVE, False)
    ArkSaveLogger.set_log_level(ArkSaveLogger.LogTypes.OBJECTS, False)
    ArkSaveLogger.set_log_level(ArkSaveLogger.LogTypes.DEBUG, False)
    ArkSaveLogger.set_log_level(ArkSaveLogger.LogTypes.INFO, False)
    ArkSaveLogger.set_log_level(ArkSaveLogger.LogTypes.WARNING, True)
    ArkSaveLogger.set_log_level(ArkSaveLogger.LogTypes.ERROR, True)

    # Configure number of process to use (no more than the number of CPUs, 8 at most).
    nb_processes: int = max(1, min(os.cpu_count() or 1, 8))

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

    count_conn = open_save_read_only(save_path)
    try:
        row_count: int = count_conn.execute("SELECT COUNT(*) FROM game").fetchone()[0]
    finally:
        count_conn.close()

    rows_per_process: int = math.floor(row_count / nb_processes)
    remaining_rows: int = row_count % nb_processes

    # A single queue shared by all workers: the main process receives the chunks of every worker as soon as they are sent.
    result_queue = Queue()  # Must not be named "queue" (it would shadow the "queue" module).
    processes: list[Process] = []
    for i in range(nb_processes):
        offset: int = rows_per_process * i
        limit: int = rows_per_process + (remaining_rows if i == nb_processes - 1 else 0)
        processes.append(Process(target=asi_export_process, args=({ "save_path": save_path, "offset": offset, "limit": limit, "save_context": arkparse_save.save_context, "queue": result_queue, "worker_index": i },), daemon=True))

    for i in range(nb_processes):
        processes[i].start()

    try:
        processes_results: list = get_workers_results(result_queue, processes)
    except Exception as ex:
        print(f"Failed to parse game objects: {ex}", flush=True)
        for process in processes:
            if process.is_alive():
                process.terminate()
        sys.exit(1)

    if arkparse_save.game_obj_binaries is None:
        arkparse_save.game_obj_binaries = {}
    if arkparse_save.all_classes is None:
        arkparse_save.all_classes = []
    # Merged in worker order (same order as before). Each worker list is released once merged (no merged copy of everything anymore).
    for i in range(nb_processes):
        for parsed in processes_results[i]:
            if parsed["game_obj"] is not None:
                arkparse_save.parsed_objects[parsed["UUID"]] = parsed["game_obj"]
            arkparse_save.game_obj_binaries[parsed["UUID"]] = parsed["bytes"]
            arkparse_save.all_classes.append(parsed["class"])
        processes_results[i] = []
    del processes_results

    arkparse_save._get_game_time_params()
    save_info = { "MapName": arkparse_save.save_context.map_name,
                  "GameTime": arkparse_save.save_context.game_time,
                  "CurrentDay": arkparse_save.save_context.current_day,
                  "CurrentTime": arkparse_save.save_context.current_time }

    parsed_result = asi_parse_classic(arkparse_save, custom_bps_dinos, custom_bps_items, custom_bps_structures)

    pawn_objects: list = parsed_result["pawns"]
    dinos: list = parsed_result["dinos"]
    items: list = parsed_result["items"]
    structures: list = parsed_result["structures"]
    skipped_blueprints: list[str] = []
    unknown_blueprints: list[str] = []
    failed_to_parse: list[str] = []

    if debug_logging:
        skipped_blueprints = parsed_result["skipped_blueprints"]
        unknown_blueprints = parsed_result["unknown_blueprints"]
        failed_to_parse = parsed_result["failed_parsing"]

    pawns: list = []
    for pawn_object in pawn_objects:
        if not pawn_object is None:
            pawns.append(pawn_to_json(pawn_object))

    if debug_logging:
        skipped_blueprints = list(dict.fromkeys(skipped_blueprints))
        skipped_blueprints.sort()
        unknown_blueprints = list(dict.fromkeys(unknown_blueprints))
        unknown_blueprints.sort()
        failed_to_parse.sort()

    end = time.time()
    print('Parsed game objects (time spent: ' + human_readable_time(end - start) + ').', flush=True)

    all_players = []
    all_tribes = []
    if export_players or export_tribes:
        print('Parsing players and tribes...', flush=True)
        players_and_tribes_start = time.time()

        player_api = PlayerApi(arkparse_save, True, True, True, pawn_objects)
        all_players, all_tribes = players_and_tribes_to_json(player_api, pawn_objects)

        players_and_tribes_end = time.time()
        print('Parsed players and tribes (time spent: ' + human_readable_time(players_and_tribes_end - players_and_tribes_start) + ').', flush=True)

    print('Creating JSON files...', flush=True)
    start = time.time()

    # Create json exports folder if it does not exist.
    if not (export_path.exists() and export_path.is_dir()):
        export_path.mkdir(parents=True, exist_ok=True)

    # Write JSONs (each file is written atomically).
    if debug_logging:
        write_json_file(export_path / "skipped_blueprints.json", skipped_blueprints, False)
        write_json_file(export_path / "unknown_blueprints.json", unknown_blueprints, False)
        write_json_file(export_path / "failed_to_parse.json", failed_to_parse, False)

    write_json_file(export_path / "save_info.json", save_info)
    if export_dinos:
        write_json_file(export_path / "dinos.json", dinos)
    if export_items:
        write_json_file(export_path / "items.json", items)
    if export_pawns:
        write_json_file(export_path / "player_pawns.json", pawns)
    if export_structures:
        write_json_file(export_path / "structures.json", structures)
    if export_players:
        write_json_file(export_path / "players.json", all_players)
    if export_tribes:
        write_json_file(export_path / "tribes.json", all_tribes)

    end = time.time()
    print('JSON files created (time spent: ' + human_readable_time(end - start) + ').', flush=True)

    for i in range(nb_processes):
        processes[i].join(timeout=5)
        if processes[i].is_alive():
            processes[i].terminate()
