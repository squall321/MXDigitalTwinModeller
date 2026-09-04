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


# ── Energy gate: energy.json contract + basis-aware share suppression ──────────
# 핵심 계약: strain_energy 의 집계 기준(basis)이 'Total' 이 아니면 그 값들의 합은
# 총 에너지가 아니므로 점유율(%)을 그리면 안 된다. 그걸 코드가 실제로 지키는지 본다.
import json, tempfile, shutil

def _mk(dirpath, basis):
    ej = {
        'schema_version': 'energy-1.0',
        'analysis': 'M1', 'analysis_type': 'Modal',
        'energy_basis': basis, 'energy_kind': 'strain',
        'units': {'energy': 'mJ'},
        'filter': {'top_n': 3, 'cum_cut': 0.9, 'min_share': 0.02, 'localized_thresh': 0.5},
        'sets': [
            {'set': 1, 'kind': 'mode', 'frequency_hz': 120.5, 'total_energy': 10.0,
             'dominant_body': 'A', 'dominant_share': 0.7, 'localized': True, 'n_bodies': 3,
             'kept': [{'body': 'A', 'energy': 7.0, 'share': 0.7, 'cum_share': 0.7},
                      {'body': 'B', 'energy': 2.0, 'share': 0.2, 'cum_share': 0.9}]},
            {'set': 2, 'kind': 'mode', 'frequency_hz': 340.0, 'total_energy': 8.0,
             'dominant_body': 'B', 'dominant_share': 0.4, 'localized': False, 'n_bodies': 3,
             'kept': [{'body': 'B', 'energy': 3.2, 'share': 0.4, 'cum_share': 0.4},
                      {'body': 'C', 'energy': 3.0, 'share': 0.375, 'cum_share': 0.775}]},
        ],
        'bodies': [
            {'body': 'A', 'appears': 1, 'max_share': 0.7, 'worst_set': 1, 'sum_energy': 7.0},
            {'body': 'B', 'appears': 2, 'max_share': 0.4, 'worst_set': 2, 'sum_energy': 5.2},
            {'body': 'C', 'appears': 1, 'max_share': 0.375, 'worst_set': 2, 'sum_energy': 3.0},
        ],
        'worst_body': 'A', 'mosaics': {'StrainEnergy': True}, 'figure_added': True,
    }
    with open(os.path.join(dirpath, 'energy.json'), 'w', encoding='utf-8') as f:
        json.dump(ej, f)

_tmp_ok = tempfile.mkdtemp()
_tmp_bad = tempfile.mkdtemp()
try:
    _mk(_tmp_ok, 'Total')
    _mk(_tmp_bad, 'MaximumOfMaximumOverTime')

    # (1) basis=Total -> 점유율 유효, 경고 없음, set 콤보 = 종합 + set 2개
    w = V.EnergyTab(meta, _tmp_ok)
    assert w.ej is not None, "energy.json 을 못 읽었다"
    assert w._shares_valid() is True, "basis=Total 인데 share 가 무효 처리됐다"
    assert w._warn_active is False, "basis=Total 인데 경고가 떴다"
    assert w.set_cb.count() == 3, "set 콤보 항목수 %d != 3" % w.set_cb.count()
    w.set_cb.setCurrentIndex(1)          # mode 1 로 전환 -> 그리기 경로 실행
    w.set_cb.setCurrentIndex(2)

    # (2) basis=폴백 -> 점유율 무효, 경고 표시
    w2 = V.EnergyTab(meta, _tmp_bad)
    assert w2._shares_valid() is False, "폴백 basis 인데 share 를 유효로 봤다"
    assert w2._warn_active is True, "폴백 basis 인데 경고가 안 떴다"

    # (3) energy.json 없음 + metadata 에 basis=Total -> 레거시 경로, 유효
    meta21 = dict(meta)
    meta21['schema_version'] = '2.1'
    meta21['bodies'] = [dict(b) for b in meta['bodies']]
    meta21['bodies'][0]['strain_energy_basis'] = 'Total'
    w3 = V.EnergyTab(meta21, HERE)
    assert w3.ej is None, "energy.json 이 없어야 하는데 읽혔다"
    assert w3._shares_valid() is True, "metadata basis=Total 인데 무효 처리됐다"

    # (4) basis 미기록(구버전) -> 경고 표시, 점유율 억제
    w4 = V.EnergyTab(meta, HERE)
    assert w4._shares_valid() is False, "basis 미기록인데 share 를 유효로 봤다"
    assert w4._warn_active is True, "basis 미기록인데 경고가 안 떴다"
finally:
    shutil.rmtree(_tmp_ok, ignore_errors=True)
    shutil.rmtree(_tmp_bad, ignore_errors=True)

print("ENERGY_OK  (energy.json 2 basis variants + metadata 2.1 + legacy fallback)")
