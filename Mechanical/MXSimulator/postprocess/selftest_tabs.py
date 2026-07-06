# encoding: utf-8
"""
Gate for the viewer tabs (Task B): headless-instantiate every tab (QT offscreen) with a
schema-2.0 metadata that has the new Energy/Reactions/directional fields, AND with a legacy
1.x-style body missing them — proving graceful degradation. Expect: TABS_OK.

Run with the VIEWER venv (has PyQt5):  postprocess/venv/Scripts/python.exe selftest_tabs.py
"""
import os
os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from PyQt5.QtWidgets import QApplication
app = QApplication([])

import visualizer as V

meta = {
    'schema_version': '2.0',
    'generated_at': '2026-07-05T00:00:00',
    'source': 'selftest',
    'units': {'deformation': 'mm', 'stress': 'MPa', 'frequency': 'Hz', 'energy': 'mJ', 'force': 'N'},
    'analysis': 'TestAnalysis',
    'operating_freq_hz': 100.0,
    'thresh_red_mm': 0.30,
    'thresh_yellow_mm': 0.10,
    'force_csv': '',
    'reactions': [
        {'scope': 'FixedSupport', 'x': 12.0, 'y': -5.0, 'z': 3.0, 'mag': 13.5},
        {'scope': 'Clamp2', 'x': 1.0, 'y': 2.0, 'z': 0.5, 'mag': 2.3},
    ],
    'bodies': [
        {'rank': 1, 'name': 'BodyA', 'max_def': 0.42, 'max_vm': 130.0, 'csv': '',
         'strain_energy': 5.2, 'directional_def': {'x': 0.10, 'y': 0.20, 'z': 0.40}},
        {'rank': 2, 'name': 'BodyB', 'max_def': 0.05, 'max_vm': 30.0, 'csv': ''},  # legacy: no new fields
    ],
}

legacy = {  # a pre-2.0 metadata with none of the new fields
    'analysis': 'Legacy', 'operating_freq_hz': 60.0, 'thresh_red_mm': 0.3, 'thresh_yellow_mm': 0.1,
    'force_csv': '', 'bodies': [{'rank': 1, 'name': 'Old', 'max_def': 0.2, 'max_vm': 80.0, 'csv': ''}],
}

TABS = [V.SummaryTab, V.TimeHistTab, V.FFTTab, V.FRFTab, V.FatigueTab, V.EnergyTab, V.ReactionsTab,
        V.SweepTab]

for T in TABS:
    w = T(meta, HERE)
    assert w is not None, T.__name__
for T in TABS:
    w = T(legacy, HERE)   # must not crash on absent new fields
    assert w is not None, T.__name__ + " (legacy)"

# full window (registration wiring)
mw = V.MainWindow(meta, HERE)
assert mw.centralWidget() is not None

print("TABS_OK  ({} tab classes x 2 metadata variants + MainWindow)".format(len(TABS)))
