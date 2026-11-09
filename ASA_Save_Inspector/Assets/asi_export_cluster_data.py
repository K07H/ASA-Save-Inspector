import sys
import json

from pathlib import Path
from typing import Optional

from arkparse.object_model.cluster_data.ark_cluster_data import ClusterData
from arkparse.utils.json_utils import DefaultJsonEncoder

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
            with open(export_path / "cluster_data.json", "w", encoding="utf-8") as text_file:
                json.dump(data_json, text_file, ensure_ascii=False, indent=4, cls=DefaultJsonEncoder)
    except Exception as e:
        print(f"Exception caught while parsing cluster data: {e}")
