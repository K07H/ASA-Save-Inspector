import math
import os
import sys
import json

from pathlib import Path
from typing import Optional

from arkparse.object_model.cluster_data.ark_cluster_data import ClusterData
from arkparse.utils.json_utils import DefaultJsonEncoder

def to_json_compatible(obj, encoder: json.JSONEncoder, depth: int = 0):
    """Converts obj into plain JSON types, replacing NaN / +-Infinity (invalid JSON) by the strings "NaN" / "Infinity" / "-Infinity"."""
    if depth > 200:
        return None  # Protection against reference cycles.
    if obj is None or isinstance(obj, (str, bool, int)):
        return obj
    if isinstance(obj, float):
        if math.isnan(obj):
            return "NaN"
        if math.isinf(obj):
            return "Infinity" if obj > 0 else "-Infinity"
        return obj
    if isinstance(obj, dict):
        return { k: to_json_compatible(v, encoder, depth + 1) for k, v in obj.items() }
    if isinstance(obj, (list, tuple, set)):
        return [ to_json_compatible(v, encoder, depth + 1) for v in obj ]
    converted = encoder.default(obj)
    if converted is obj:
        return None
    return to_json_compatible(converted, encoder, depth + 1)

def get_files(path: Path) -> list[Path]:
    return [f for f in path.iterdir() if
            (f.is_file() and not f.suffix and not f.name.startswith(".") and not f.name.endswith("."))]

def get_cluster_data_from_directory(directory: Optional[Path] = None) -> Optional[dict[str, ClusterData]]:
    if directory is None or not directory.is_dir():
        return None
    all_data: dict[str, ClusterData] = {}
    all_files = get_files(directory)
    for current_file in all_files:
        if current_file.name is not None and len(current_file.name) > 0:
            print(f"Found cluster data file: {current_file.name}", flush=True)
            full_path = directory / current_file.name
            if full_path.stat().st_size > 0:
                current_data: Optional[ClusterData] = None
                try:
                    current_data = ClusterData(directory, current_file.name)
                except Exception as e:
                    print(f"Exception caught while parsing cluster item data: {e}", flush=True)
                if current_data is not None:
                    all_data[current_file.name] = current_data
            else:
                print(f"File is empty, skipping: {current_file.name}", flush=True)
    return all_data

if __name__ == '__main__':
    # argv[0]: This script name
    # argv[1]: Cluster data folder path
    # argv[2]: JSON export folder path

    if len(sys.argv) < 3:
        print('Wrong number of arguments provided.', flush=True)
        print('USAGE: asi_export_cluster_data.py [Cluster_Data_Folder_Path] [JSON_Export_Folder_Path]', flush=True)
        sys.exit(2) # Exit with command line syntax error code

    cluster_data_path: Path = Path(sys.argv[1])
    export_path: Path = Path(sys.argv[2])

    if not cluster_data_path.is_dir():
        raise NotADirectoryError(f"Wrong cluster data folder path: {cluster_data_path}")
    
    if not export_path.is_dir():
        raise NotADirectoryError(f"Wrong JSON export folder path: {export_path}")

    try:
        all_cluster_data: Optional[dict[str, ClusterData]] = get_cluster_data_from_directory(cluster_data_path)
        if all_cluster_data is not None:
            cluster_datas: list[ClusterData] = list(all_cluster_data.values())
            data_json = [data.to_json_obj() for data in cluster_datas]
            # Atomic write: a crash can no longer leave a truncated cluster_data.json.
            output_path: Path = export_path / "cluster_data.json"
            tmp_path: Path = output_path.with_name(output_path.name + ".tmp")
            with open(tmp_path, "w", encoding="utf-8") as text_file:
                json.dump(to_json_compatible(data_json, DefaultJsonEncoder()), text_file, ensure_ascii=False, indent=4, cls=DefaultJsonEncoder)
            os.replace(tmp_path, output_path)
    except Exception as e:
        print(f"Exception caught while parsing cluster data: {e}", flush=True)
        sys.exit(1) # Let ASI know that the export failed (it used to exit with code 0 in this case).
