#!/usr/bin/env python3
# encoding: utf-8
"""
MX Post-Process Viewer — entry point
Usage: python runner.py <path/to/metadata.json>
"""
import sys
import os
import json


def main():
    if len(sys.argv) < 2:
        print("Usage: runner.py <metadata.json>")
        sys.exit(1)

    meta_path = os.path.abspath(sys.argv[1])
    if not os.path.exists(meta_path):
        print("ERROR: metadata file not found: {}".format(meta_path))
        sys.exit(1)

    with open(meta_path, 'r', encoding='utf-8') as f:
        meta = json.load(f)

    # schema_version (POSTPROCESS_IDEAS.md §10.4). Absent = legacy 1.x (pre-2.0 exports).
    # This viewer understands the 2.x envelope; a newer MAJOR is loaded best-effort with a warning.
    sv = str(meta.get('schema_version', '1.0'))
    try:
        major = int(sv.split('.')[0])
    except (ValueError, IndexError):
        major = 1
    if major > 2:
        print("WARNING: metadata schema_version {0} is newer than this viewer (2.x); "
              "loading best-effort — some fields may be ignored.".format(sv))

    base_dir = os.path.dirname(meta_path)

    # Add this folder to sys.path so visualizer/analyzer can be imported
    here = os.path.dirname(os.path.abspath(__file__))
    if here not in sys.path:
        sys.path.insert(0, here)

    from PyQt5.QtWidgets import QApplication
    from visualizer import MainWindow

    app = QApplication(sys.argv)
    app.setStyle('Fusion')

    win = MainWindow(meta, base_dir)
    win.show()
    sys.exit(app.exec_())


if __name__ == '__main__':
    main()
