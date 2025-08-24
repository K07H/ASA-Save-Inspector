import sys
import time
import math
from pathlib import Path

from arkparse import AsaSave
from arkparse.api.json_api import JsonApi, DinoApi, PlayerApi, StructureApi
from arkparse.logging import ArkSaveLogger

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

# argv[0]: This script name
# argv[1]: ASA save file path
# argv[2]: JSON export folder path
# argv[3]: Export dinos?
# argv[4]: Export player pawns?
# argv[5]: Export items?
# argv[6]: Export structures?
# argv[7]: Export players?
# argv[8]: Export tribes?

if len(sys.argv) > 8:
    save_path = Path(sys.argv[1])
    export_path = Path(sys.argv[2])
    export_dinos = sys.argv[3] == '1'
    export_playerpawns = sys.argv[4] == '1'
    export_items = sys.argv[5] == '1'
    export_structures = sys.argv[6] == '1'
    export_players = sys.argv[7] == '1'
    export_tribes = sys.argv[8] == '1'

    # Configure logging
    ArkSaveLogger.set_log_level(ArkSaveLogger.LogTypes.API, True)
    ArkSaveLogger.set_log_level(ArkSaveLogger.LogTypes.ERROR, True)

    print('Loading ASA save...', flush=True)
    start = time.time()
    save = AsaSave(save_path)
    end = time.time()
    print('ASA save loaded (time spent: ' + human_readable_time(end - start) + ').', flush=True)

    print('Initializing JSON API...', flush=True)
    start = time.time()
    json_api = JsonApi(save, ignore_error=True)
    end = time.time()
    print('JSON API initialized (time spent: ' + human_readable_time(end - start) + ').', flush=True)

    if export_dinos:
        print('Exporting dinos...', flush=True)
        start = time.time()
        json_api.export_dinos(export_folder_path=export_path)
        end = time.time()
        print('Dinos successfully exported (time spent: ' + human_readable_time(end - start) + ').', flush=True)

    if export_playerpawns:
        print('Exporting player pawns...', flush=True)
        start = time.time()
        json_api.export_player_pawns(export_folder_path=export_path)
        end = time.time()
        print('Player pawns successfully exported (time spent: ' + human_readable_time(end - start) + ').', flush=True)

    if export_items:
        print('Exporting items...', flush=True)
        start = time.time()
        json_api.export_items(export_folder_path=export_path)
        end = time.time()
        print('Items successfully exported (time spent: ' + human_readable_time(end - start) + ').', flush=True)

    if export_structures:
        print('Exporting structures...', flush=True)
        start = time.time()
        json_api.export_structures(export_folder_path=export_path)
        end = time.time()
        print('Structures successfully exported (time spent: ' + human_readable_time(end - start) + ').', flush=True)
        
    if export_players:
        print('Exporting players...', flush=True)
        start = time.time()
        json_api.export_players(export_folder_path=export_path)
        end = time.time()
        print('Players successfully exported (time spent: ' + human_readable_time(end - start) + ').', flush=True)

    if export_tribes:
        print('Exporting tribes...', flush=True)
        start = time.time()
        json_api.export_tribes(export_folder_path=export_path)
        end = time.time()
        print('Tribes successfully exported (time spent: ' + human_readable_time(end - start) + ').', flush=True)
