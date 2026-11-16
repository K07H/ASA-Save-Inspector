import base64
import faulthandler
import json
import math
import os
import sqlite3
import sys
import threading
import time
import uuid
from collections import Counter
from concurrent.futures import ThreadPoolExecutor
from functools import lru_cache
from multiprocessing import Pipe, Process
from multiprocessing.connection import Connection, wait as wait_for_connections
from pathlib import Path
from typing import Optional

from arkparse import AsaSave
from arkparse.api import PlayerApi
from arkparse.api.json_api import JsonApi
from arkparse.logging import ArkSaveLogger
from arkparse.object_model.ark_game_object import ArkGameObject
from arkparse.object_model.cryopods.cryopod import Cryopod
from arkparse.object_model.dinos import Dino, TamedDino, Baby, TamedBaby
from arkparse.object_model.structures import Structure, StructureWithInventory
from arkparse.parsing import ArkBinaryParser
from arkparse.parsing.struct import ActorTransform, ObjectReference
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

UNSUPPORTED_EMBEDDED_DATA_VERSION: str = "Unsupported embedded data version (only Unreal 5.5 is supported)"

class ErrorReport:
    def __init__(self):
        self.verbose: bool = False
        self._lock = threading.Lock()
        self._counts: Counter = Counter()
        self._first_details: dict = {}

    def add(self, category: str, detail: str) -> None:
        with self._lock:
            self._counts[category] += 1
            if category not in self._first_details:
                self._first_details[category] = detail
        if self.verbose:
            print(detail, flush=True)

    def snapshot(self) -> tuple:
        with self._lock:
            return (dict(self._counts), dict(self._first_details))

    def merge(self, snapshot: tuple) -> None:
        counts, first_details = snapshot
        with self._lock:
            self._counts.update(counts)
            for category, detail in first_details.items():
                self._first_details.setdefault(category, detail)

    def print_summary(self) -> None:
        with self._lock:
            if len(self._counts) <= 0:
                return
            print(f"Parsing errors summary ({sum(self._counts.values())} error(s){'' if self.verbose else ', enable debug logging for details'}):", flush=True)
            for category, count in sorted(self._counts.items(), key=lambda kv: (-kv[1], kv[0])):
                print(f"  - {count} x {category}. First one: {self._first_details.get(category, '')}", flush=True)

# Errors of the current process (each worker process has its own instance, merged into the main one at the end).
ERRORS: ErrorReport = ErrorReport()

def is_in_str(search_for: list[str], search_in: str) -> bool:
    if search_in is None or len(search_in) <= 0 or search_for is None or len(search_for) <= 0:
        return False
    for s in search_for:
        if s is not None and len(s) > 0 and s in search_in:
            return True
    return False

@lru_cache(maxsize=None)
def is_skipped_class(blueprint: str) -> bool:
    return is_in_str(BLUEPRINTS_TO_SKIP, blueprint)

@lru_cache(maxsize=None)
def is_no_export_class(blueprint: str) -> bool:
    return is_in_str(BLUEPRINTS_NO_EXPORT, blueprint)

@lru_cache(maxsize=None)
def is_day_cycle_class(blueprint: str) -> bool:
    return "daycycle" in blueprint.lower()

@lru_cache(maxsize=None)
def matches_additional_blueprints(blueprint: str, additional_blueprints: tuple) -> bool:
    for additional_blueprint in additional_blueprints:
        if additional_blueprint and additional_blueprint in blueprint:
            return True
    return False

@lru_cache(maxsize=None)
def is_dino_class(blueprint: str) -> bool:
    if is_no_export_class(blueprint):
        return False
    if ("_Character_" in blueprint or "_Char_" in blueprint) and ("Dino" in blueprint or "Creature" in blueprint):
        return True
    return "/Raft_BP.Raft_BP" in blueprint \
            or "/Raft/MotorRaft_BP.MotorRaft_BP" in blueprint \
            or "/Raft/Tireme_BP_" in blueprint \
            or "Car_Vehicle_BP.Car_Vehicle_BP_C" in blueprint \
            or "InsectSwarmChar_BP.InsectSwarmChar_BP_C" in blueprint \
            or "MicrobeSwarmChar_BP.MicrobeSwarmChar_BP_C" in blueprint \
            or "BrigShipBP.BrigShipBP_C" in blueprint \
            or "SloopShipBP.SloopShipBP_C" in blueprint \
            or "GalleonShipBP.GalleonShipBP_C" in blueprint \
            or "TriremeShipBP.TriremeShipBP_C" in blueprint \
            or "TriremeShipPlayerFollowingBP.TriremeShipPlayerFollowingBP_C" in blueprint \
            or "BrigShipPlayerFollowingBP.BrigShipPlayerFollowingBP_C" in blueprint \
            or "SloopShipPlayerFollowingBP.SloopShipPlayerFollowingBP_C" in blueprint \
            or "TriremeShipPlayerFollowingBP_FleetCaptain_SmallFleet.TriremeShipPlayerFollowingBP_FleetCaptain_SmallFleet_C" in blueprint \
            or "TriremeShipPlayerFollowingBP_FleetEscort.TriremeShipPlayerFollowingBP_FleetEscort_C" in blueprint \
            or "Gargantar_Character_BP.Gargantar_Character_BP_C" in blueprint \
            or "Lumina_Character_BP.Lumina_Character_BP_C" in blueprint \
            or "Umbra_Character_BP.Umbra_Character_BP_C" in blueprint \
            or "Polar_Bear.Polar_Bear_C" in blueprint

@lru_cache(maxsize=None)
def is_structure_class(blueprint: str) -> bool:
    return "/Structures" in blueprint \
            or "/GigantoraptorNest" in blueprint \
            or "Rug_Shag" in blueprint \
            or "TributeTerminal" in blueprint \
            or "StructureStandingTurret_Flightseeker" in blueprint \
            or "ArtifactCrate_" in blueprint \
            or "/WyvernNest" in blueprint \
            or "/Structure_PlantSpecies" in blueprint \
            or "/HordeCrates/ElementWall_Horde" in blueprint \
            or "/Flag_SM_" in blueprint \
            or "/SupplyCrateBaseBP_" in blueprint \
            or "/StructureBP_WoodBench" in blueprint \
            or "MB_Button.MB_Button_C" in blueprint \
            or "/PortableRope_Ladder_" in blueprint \
            or "BP_HeroPoisonPlant.BP_HeroPoisonPlant_C" in blueprint \
            or "BP_HelperBot_DockingBay" in blueprint

@lru_cache(maxsize=None)
def is_item_class(blueprint: str) -> bool:
    if is_no_export_class(blueprint):
        return False
    return "/DroppedItemGeneric" in blueprint or \
            "Egg_Wyvern_Fertilized" in blueprint or \
            "/PrimalItem_" in blueprint or \
            "/PrimalItemAmmo_" in blueprint or \
            "/PrimalItemArmor_" in blueprint or \
            "/PrimalItemArtifact" in blueprint or \
            "/PrimalItemC4Ammo" in blueprint or \
            "/PrimalItemConsumable_" in blueprint or \
            "/PrimalItemConsumableBuff_Parachute" in blueprint or \
            "/PrimalItemConsumableEatable_" in blueprint or \
            "/PrimalItemConsumableMiracleGro" in blueprint or \
            "/PrimalItemConsumableRespecSoup" in blueprint or \
            "/PrimalItemConsumableSoap" in blueprint or \
            "/PrimalItemCostume_" in blueprint or \
            "/PrimalItemCustomDrinkRecipe_" in blueprint or \
            "/PrimalItemCustomFoodRecipe_" in blueprint or \
            "/PrimalItemDinoCostume_" in blueprint or \
            "/PrimalItemDye_" in blueprint or \
            "/PrimalItemMotorboat" in blueprint or \
            "/PrimalItemRadio" in blueprint or \
            "/PrimalItemRaft" in blueprint or \
            "/PrimalItemResource_" in blueprint or \
            "/PrimalItemSkin_" in blueprint or \
            "/PrimalItemStructure_" in blueprint or \
            "/PrimalItemStructureSkin_" in blueprint or \
            "/PrimalItemTrophy" in blueprint or \
            "/PrimalItemWeaponAttachment_" in blueprint

def is_dino_blueprint(game_obj: ArkGameObject, additional_blueprints: tuple) -> bool:
    if matches_additional_blueprints(game_obj.blueprint, additional_blueprints):
        return True
    if game_obj.get_property_value("DinoID1") is None \
            or game_obj.get_property_value("DinoID2") is None:
        return False
    return is_dino_class(game_obj.blueprint)

def is_structure_blueprint(game_obj: ArkGameObject, additional_blueprints: tuple) -> bool:
    if matches_additional_blueprints(game_obj.blueprint, additional_blueprints):
        return True
    if "TekBunker/Structures/BP_Bunker_Base.BP_Bunker_Base_C" in game_obj.blueprint:
        return True
    if game_obj.get_property_value("StructureID") is None:
        return False
    if is_no_export_class(game_obj.blueprint):
        return False
    if game_obj.get_property_value("DinoID1") is None \
            and game_obj.get_property_value("ItemID") is None:
        return True
    return is_structure_class(game_obj.blueprint)

def is_item_blueprint(game_obj: ArkGameObject, additional_blueprints: tuple) -> bool:
    if matches_additional_blueprints(game_obj.blueprint, additional_blueprints):
        return True
    return is_item_class(game_obj.blueprint)

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

def read_game_time(day_cycle_objects: list) -> Optional[tuple]:
    """Returns (current_day, current_time) read from the DayCycle object of the save (same logic as ArkParse),
    or None if none of the given objects contains the current day."""
    for obj in day_cycle_objects:
        day_id = obj.get_property_value("theDayNumberToMakeSerilizationWork", None)
        if day_id is not None:
            return (day_id, obj.get_property_value("CurrentTime", 0))
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
        ERRORS.add("structure failed to parse", f"Exception caught when parsing structure {obj.uuid} of class {obj.blueprint}: {e}")
        return None

def parse_single_cryopod(obj: ArkGameObject, save: AsaSave) -> Optional[Cryopod]:
    try:
        return Cryopod(obj.uuid, save)
    except Exception as e:
        if UNSUPPORTED_EMBEDDED_DATA_VERSION in str(e):
            ERRORS.add("cryopod with unsupported embedded data version (its dino is not exported)", f"Cryopod {obj.uuid}: {e}")
        else:
            ERRORS.add("cryopod failed to parse", f"Exception caught when parsing cryopod {obj.uuid}: {e}")
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
        ERRORS.add("dino failed to parse", f"Exception caught when parsing dino {obj.uuid} of class {obj.blueprint}: {e}")
        return None

def pawn_to_json(pawn_obj: ArkGameObject) -> dict:
    pawn_data: dict = {"UUID": pawn_obj.uuid.__str__(),
                       "ClassName": "player",
                       "ItemArchetype": pawn_obj.blueprint}

    # Grab pawn location if it exists
    if pawn_obj.has_property("SavedBaseWorldLocation"):
        # ActorTransform(vector=None) does not fail, it keeps x = y = z = 0: the pawn would be exported at (0, 0, 0).
        location_vector = pawn_obj.get_property_value("SavedBaseWorldLocation")
        if location_vector is not None:
            pawn_location = ActorTransform(vector=location_vector)
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
WORKER_CHUNK_SIZE: int = 5000

# Messages sent by worker processes on their pipe (processes mode only): (message type, worker index, payload).
MSG_CHUNK: str = "chunk"  # payload: list of parsed objects
MSG_DONE: str = "done"    # payload: (number of objects sent by this worker, snapshot of its ErrorReport)
MSG_ERROR: str = "error"  # payload: error message

def iter_parsed_chunks(save_path: Path, offset: int, limit: int, save_context):
    chunk: list = []
    conn = open_save_read_only(save_path)
    try:
        cursor = conn.execute("SELECT key, value FROM game ORDER BY key LIMIT ? OFFSET ?", (limit, offset))
        for key, value in cursor:
            try:
                byte_buffer = ArkBinaryParser(value, save_context)
                obj_uuid = uuid.UUID(bytes=key)
            except Exception as ex:
                ERRORS.add("object failed to read", f"Exception caught when reading object with key {key!r}: {ex}")
                continue
            try:
                class_name = byte_buffer.read_name()
            except Exception as ex:
                class_name = None
                ERRORS.add("object class name failed to read", f"Exception caught when reading the class name of object {obj_uuid}: {ex}")

            if class_name is not None:
                # Skipped objects are not parsed (they are never exported), but their raw bytes are still sent back
                # so that ArkParse can still find them if another object references them (inventories...).
                if is_skipped_class(class_name):
                    chunk.append({ "UUID": obj_uuid, "class": class_name, "game_obj": None, "bytes": value })
                else:
                    try:
                        game_obj = ArkGameObject(obj_uuid, class_name, byte_buffer)
                    except Exception as e:
                        game_obj = None
                        ERRORS.add("object failed to parse", f"Exception caught when parsing object {obj_uuid} of class {class_name}: {e}")
                    if game_obj is not None:
                        chunk.append({ "UUID": obj_uuid, "class": class_name, "game_obj": game_obj, "bytes": value })

                if len(chunk) >= WORKER_CHUNK_SIZE:
                    yield chunk
                    chunk = []
        cursor.close()
    finally:
        conn.close()
    if len(chunk) > 0:
        yield chunk

def asi_export_process(arg_obj):
    # arg_obj["save_path"]: Path of the .ark save file
    # arg_obj["offset"] / arg_obj["limit"]: Rows range handled by this worker
    # arg_obj["save_context"]: SaveContext
    # arg_obj["conn"]: Sending end of the pipe of THIS worker (one pipe per worker: never two writers on the same channel)
    # arg_obj["worker_index"]: Index of this worker
    # arg_obj["debug"]: Debug logging enabled?
    result_conn: Connection = arg_obj["conn"]
    worker_index: int = arg_obj["worker_index"]
    ERRORS.verbose = arg_obj["debug"]
    try:
        sent: int = 0
        # The main process receives the chunks while this worker keeps parsing (send() waits until the main process reads).
        for chunk in iter_parsed_chunks(arg_obj["save_path"], arg_obj["offset"], arg_obj["limit"], arg_obj["save_context"]):
            result_conn.send((MSG_CHUNK, worker_index, chunk))
            sent += len(chunk)
        result_conn.send((MSG_DONE, worker_index, (sent, ERRORS.snapshot())))
    except Exception as ex:
        # Always send something to the main process, otherwise it would wait for a result that will never come.
        print(f"Worker process {worker_index} failed: {ex}", flush=True)
        try:
            result_conn.send((MSG_ERROR, worker_index, str(ex)))
        except Exception:
            pass # Pipe already broken: the main process will see EOF.
    finally:
        result_conn.close()

def asi_export_thread(save_path: Path, offset: int, limit: int, save_context, worker_index: int) -> list:
    results: list = []
    try:
        for chunk in iter_parsed_chunks(save_path, offset, limit, save_context):
            results.extend(chunk)
    except Exception as ex:
        raise RuntimeError(f"Worker thread {worker_index} failed: {ex}") from ex
    return results

def gil_enabled() -> bool:
    return getattr(sys, "_is_gil_enabled", lambda: True)()

def get_workers_results(readers: list, processes: list, max_wait_without_news_sec: int = 3600) -> list:
    nb_workers: int = len(processes)
    results: list = [[] for _ in range(nb_workers)]
    pending: dict = { readers[i]: i for i in range(nb_workers) }  # Pipes of the workers that have not finished yet.
    waited: int = 0
    while len(pending) > 0:
        ready = wait_for_connections(list(pending.keys()), timeout=5)
        if not ready:
            waited += 5
            if waited >= max_wait_without_news_sec:
                raise RuntimeError(f"Worker processes did not return any result for {max_wait_without_news_sec} seconds.")
            continue
        waited = 0
        for reader in ready:
            i: int = pending[reader]
            try:
                msg_type, worker_index, payload = reader.recv()
            except EOFError:
                # The pipe was closed before "done": the worker died (crash, killed...).
                processes[i].join(timeout=5)
                raise RuntimeError(f"Worker process {i} exited (exit code {processes[i].exitcode}) without returning all its results.")
            if worker_index != i:
                raise RuntimeError(f"Unexpected message from worker {worker_index} on the pipe of worker {i}.")
            if msg_type == MSG_CHUNK:
                results[i].extend(payload)
            elif msg_type == MSG_DONE:
                ERRORS.merge(payload[1])
                del pending[reader]
                reader.close()
            elif msg_type == MSG_ERROR:
                raise RuntimeError(f"Worker process {i} failed: {payload}")
    return results

class AsiJsonEncoder(DefaultJsonEncoder):
    _warned_types: set = set()

    def default(self, o):
        if hasattr(o, 'to_json_obj'):
            return o.to_json_obj()
        try:
            return super().default(o)
        except TypeError:
            type_name = type(o).__name__
            if type_name not in AsiJsonEncoder._warned_types:
                AsiJsonEncoder._warned_types.add(type_name)
                print(f"Warning: values of type {type_name} cannot be serialized to JSON, they are written as null.", flush=True)
            return None

def non_finite_float_to_str(value: float):
    if math.isnan(value):
        return "NaN"
    if math.isinf(value):
        return "Infinity" if value > 0 else "-Infinity"
    return value

def to_json_compatible(obj, encoder: json.JSONEncoder, depth: int = 0):
    if depth > 200:
        return None # Protection against reference cycles.
    if obj is None or isinstance(obj, (str, bool, int)):
        return obj
    if isinstance(obj, float):
        return non_finite_float_to_str(obj)
    if isinstance(obj, dict):
        return { (k if k is None or isinstance(k, (str, int, float, bool)) else str(k)): to_json_compatible(v, encoder, depth + 1) for k, v in obj.items() }
    if isinstance(obj, (list, tuple, set)):
        return [ to_json_compatible(v, encoder, depth + 1) for v in obj ]
    converted = encoder.default(obj)
    if converted is obj:
        return None
    return to_json_compatible(converted, encoder, depth + 1)

def write_json_file(file_path: Path, data, use_to_json_obj: bool = True) -> None:
    encoder = AsiJsonEncoder() if use_to_json_obj else DefaultJsonEncoder()
    # Note: indent=4 is kept on purpose, ASI's fallback JSON loader relies on it.
    tmp_path = file_path.with_name(file_path.name + ".tmp")
    with open(tmp_path, "w", encoding="utf-8", errors="replace") as text_file:
        if isinstance(data, list) and len(data) > 0:
            text_file.write("[\n")
            for i, item in enumerate(data):
                if i > 0:
                    text_file.write(",\n")
                item_json: str = json.dumps(to_json_compatible(item, encoder), indent=4, ensure_ascii=False)
                text_file.write("    " + item_json.replace("\n", "\n    "))
            text_file.write("\n]")
        else:
            text_file.write(json.dumps(to_json_compatible(data, encoder), indent=4, ensure_ascii=False))
    os.replace(tmp_path, file_path)

def asi_parse_classic(save: AsaSave, dino_bps: list[str], item_bps: list[str], structure_bps: list[str], debug: bool):
    dino_bps_tuple: tuple = tuple(dino_bps or [])
    item_bps_tuple: tuple = tuple(item_bps or [])
    structure_bps_tuple: tuple = tuple(structure_bps or [])
    all_dinos = []
    all_pawns_objects = []
    all_items = []
    all_structures = []
    skipped_bps: list[str] = []
    unknown_bps: list[str] = []
    failed_parsing: list[str] = []
    for obj in list(save.parsed_objects.values()):
        if obj is not None:
            try:
                if is_skipped_class(obj.blueprint):
                    if debug and obj.blueprint not in skipped_bps:
                        skipped_bps.append(obj.blueprint)
                    continue

                if is_dino_blueprint(obj, dino_bps_tuple):
                    dino = parse_single_dino(obj, save)
                    if dino is not None:
                        all_dinos.append(dino)
                    elif debug:
                        failed_parsing.append(f"Dino {obj.uuid} with class {obj.blueprint}.")
                elif "PrimalItem_WeaponEmptyCryopod" in obj.blueprint or "PrimalItem_SCSCryopod" in obj.blueprint:
                    all_items.append(JsonApi.primal_item_to_json_obj(obj))
                    cryopod = parse_single_cryopod(obj, save)
                    if cryopod is not None:
                        if cryopod.dino is not None:
                            cryopod.dino.is_cryopodded = True
                            all_dinos.append(cryopod.dino)
                        if cryopod.saddle is not None:
                            try:
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
                            except Exception as e:
                                ERRORS.add("cryopod saddle failed to export", f"Exception caught when exporting the saddle of cryopod {obj.uuid}: {e}")
                    elif debug:
                        failed_parsing.append(f"Cryopod {obj.uuid} content.")
                elif "/PlayerPawnTest_Female.PlayerPawnTest_Female_C" in obj.blueprint or "/PlayerPawnTest_Male.PlayerPawnTest_Male_C" in obj.blueprint:
                    all_pawns_objects.append(obj)
                elif is_item_blueprint(obj, item_bps_tuple):
                    all_items.append(JsonApi.primal_item_to_json_obj(obj))
                elif is_structure_blueprint(obj, structure_bps_tuple):
                    structure = parse_single_structure(obj, save, True)
                    if structure is not None:
                        all_structures.append(structure)
                    elif debug:
                        failed_parsing.append(f"Structure {obj.uuid} with class {obj.blueprint}.")
                # Fallback cases
                elif obj.get_property_value("ItemID") is not None:
                    all_items.append(JsonApi.primal_item_to_json_obj(obj))
                # Absolutely unknown
                elif not is_no_export_class(obj.blueprint):
                    if debug and obj.blueprint not in unknown_bps:
                        unknown_bps.append(obj.blueprint)
                        print(f"Unknown object {obj.uuid} with class {obj.blueprint}.", flush=True)
            except Exception as e:
                if UNSUPPORTED_EMBEDDED_DATA_VERSION in str(e):
                    ERRORS.add("object with unsupported embedded data version (not exported)", f"Object {obj.uuid} of class {obj.blueprint}: {e}")
                else:
                    ERRORS.add("object failed to export", f"Exception caught when exporting object {obj.uuid} of class {obj.blueprint}: {e}")

    return { "dinos": all_dinos, "pawns": all_pawns_objects, "items": all_items, "structures": all_structures, "skipped_blueprints": skipped_bps, "unknown_blueprints": unknown_bps, "failed_parsing": failed_parsing }

def players_and_tribes_to_json(player_api: PlayerApi, pawn_objects: list):
    players = []
    tribes = []

    pawn_by_id = {p.get_property_value("LinkedPlayerDataID"): p for p in player_api.pawns.values()}

    unique_ids_on_map: set = set()
    for p in pawn_objects:
        platform_profile_id = p.get_property_value("PlatformProfileID")
        if not platform_profile_id is None and platform_profile_id.value is not None:
            unique_ids_on_map.add(platform_profile_id.value)

    for player in player_api.players:
        try:
            player_json_obj = player.to_json_obj()

            player_json_obj["FoundOnMap"] = player.unique_id in unique_ids_on_map

            hexagons = HexagonState.from_player(player, pawn_by_id.get(player.id_))
            if hexagons.has_hexagons:
                player_json_obj["Hexagons"] = hexagons.count

            players.append(player_json_obj)
        except Exception as e:
            ERRORS.add("player failed to export", f"Exception caught when exporting player {getattr(player, 'id_', '?')}: {e}")

    for tribe in player_api.tribes:
        try:
            tribe_json_obj = tribe.to_json_obj()
            tribe_members = []
            tribe_players = player_api.tribe_to_player_map.get(tribe.tribe_id, [])
            active_ids: set = set()
            for p in tribe_players:
                tribe_members.append({"PlayerCharacterName": p.char_name, "PlayerDataID": p.id_, "IsActive": True})
                active_ids.add(p.id_)
            for idx, p_id in enumerate(tribe.member_ids):
                if p_id not in active_ids:
                    member_name = tribe.members[idx] if idx < len(tribe.members) else ""
                    tribe_members.append({"PlayerCharacterName": member_name, "PlayerDataID": p_id, "IsActive": False})
            tribe_json_obj["TribeMembers"] = tribe_members
            tribes.append(tribe_json_obj)
        except Exception as e:
            ERRORS.add("tribe failed to export", f"Exception caught when exporting tribe {getattr(tribe, 'tribe_id', '?')}: {e}")

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
    # On a native crash (ex: access violation 0xC0000005 in the interpreter or in a C extension), print the Python stack
    # of every thread on stderr (ASI logs it), instead of dying silently.
    faulthandler.enable(all_threads=True)

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
    ERRORS.verbose = debug_logging

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

    # Configure number of workers (processes or threads) to use (no more than the number of CPUs usable by this process, 8 at most).
    nb_workers: int = max(1, min(getattr(os, "process_cpu_count", os.cpu_count)() or 1, 8))

    # DEBUG: Allows to enforce the number of workers (ex: set ASI_NB_WORKERS=1).
    forced_nb_workers: str = os.environ.get("ASI_NB_WORKERS", "")
    if forced_nb_workers.isdigit():
        nb_workers = max(1, int(forced_nb_workers))
        print(f"Number of workers forced to {nb_workers}.", flush=True)

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

    rows_per_worker: int = math.floor(row_count / nb_workers)
    remaining_rows: int = row_count % nb_workers

    offsets: list[int] = [rows_per_worker * i for i in range(nb_workers)]
    limits: list[int] = [rows_per_worker + (remaining_rows if i == nb_workers - 1 else 0) for i in range(nb_workers)]

    # Execution mode:
    #  - GIL active (standard Python): threads cannot parse in parallel, so worker PROCESSES are used (the objects are pickled between processes).
    #  - GIL disabled (free-threaded Python, ex: 3.14t): worker THREADS are used (truly parallel, shared memory, nothing to pickle).
    use_threads: bool = not gil_enabled()
    processes: list[Process] = []
    if use_threads:
        print(f"Free-threaded Python (GIL disabled): parsing with {nb_workers} threads.", flush=True)
        try:
            with ThreadPoolExecutor(max_workers=nb_workers) as executor:
                futures = [executor.submit(asi_export_thread, save_path, offsets[i], limits[i], arkparse_save.save_context, i) for i in range(nb_workers)]
                processes_results: list = [future.result() for future in futures]  # In worker order.
        except Exception as ex:
            print(f"Failed to parse game objects: {ex}", flush=True)
            sys.exit(1)
    else:
        print(f"Standard Python (GIL enabled): parsing with {nb_workers} processes.", flush=True)
        readers: list = []
        writers: list = []
        for i in range(nb_workers):
            reader, writer = Pipe(duplex=False)
            readers.append(reader)
            writers.append(writer)
            processes.append(Process(target=asi_export_process, args=({ "save_path": save_path, "offset": offsets[i], "limit": limits[i], "save_context": arkparse_save.save_context, "conn": writer, "worker_index": i, "debug": debug_logging },), daemon=True))

        for process in processes:
            process.start()

        for writer in writers:
            writer.close()

        try:
            processes_results: list = get_workers_results(readers, processes)
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

    day_cycle_objects: list = []  # Found during the merge: the game time is read from them (no second pass on every object).
    for i in range(nb_workers):
        for parsed in processes_results[i]:
            if parsed["game_obj"] is not None:
                arkparse_save.parsed_objects[parsed["UUID"]] = parsed["game_obj"]
                if is_day_cycle_class(parsed["class"]):
                    day_cycle_objects.append(parsed["game_obj"])
            arkparse_save.game_obj_binaries[parsed["UUID"]] = parsed["bytes"]
            arkparse_save.all_classes.append(parsed["class"])
        processes_results[i] = []
    del processes_results

    game_time = read_game_time(day_cycle_objects)
    if game_time is not None:
        arkparse_save.save_context.current_day, arkparse_save.save_context.current_time = game_time
    else:
        # No DayCycle object found by its class name: same full search as before (ArkParse checks every object).
        arkparse_save._get_game_time_params()
    save_info = { "MapName": arkparse_save.save_context.map_name,
                  "GameTime": arkparse_save.save_context.game_time,
                  "CurrentDay": arkparse_save.save_context.current_day,
                  "CurrentTime": arkparse_save.save_context.current_time }

    parsed_result = asi_parse_classic(arkparse_save, custom_bps_dinos, custom_bps_items, custom_bps_structures, debug_logging)

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
            try:
                pawns.append(pawn_to_json(pawn_object))
            except Exception as e:
                ERRORS.add("pawn failed to export", f"Exception caught when exporting pawn {getattr(pawn_object, 'uuid', '?')}: {e}")

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

    # Printed after the players & tribes step, so that its errors are included.
    ERRORS.print_summary()

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

    # Worker processes (processes mode only) have already finished: they are joined (and killed if they are stuck).
    for process in processes:
        process.join(timeout=5)
        if process.is_alive():
            process.terminate()
