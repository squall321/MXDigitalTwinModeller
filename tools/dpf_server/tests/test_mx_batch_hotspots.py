# encoding: utf-8
"""
실제 mx_batch.py 의 hotspot 클러스터링 단위 환산 테스트 (가짜 DPF 객체, 라이선스 불필요).

eps_mm=2.0 은 mm 기준인데 DPF 좌표는 해석 단위계(Mechanical 기본 m)를 따른다. 환산이 없으면 m 좌표에서
2 m 반경이 되어 멀리 떨어진 핫스팟들이 한 클러스터로 뭉친다.
"""
import importlib.util
import os
import sys
import types

import numpy as np
import pytest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
from mxdpf.config import DEFAULT_BATCH   # noqa: E402

if not os.path.isfile(DEFAULT_BATCH):
    pytest.skip("mx_batch.py not found at %s (run inside the repo)" % DEFAULT_BATCH, allow_module_level=True)


def load_mx_batch():
    spec = importlib.util.spec_from_file_location("mx_batch_under_test", DEFAULT_BATCH)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


class _Obj:
    def __init__(self, **kw):
        self.__dict__.update(kw)


def fake_dpf(monkeypatch):
    """hotspot_clusters 가 쓰는 표면만: locations.nodal, operators.invariant.von_mises_eqv_fc."""
    core = types.ModuleType("ansys.dpf.core")
    core.locations = _Obj(nodal="Nodal")

    def von_mises_eqv_fc(fields_container):
        return _Obj(eval=lambda: fields_container)
    core.operators = _Obj(invariant=_Obj(von_mises_eqv_fc=von_mises_eqv_fc))
    pkg_ansys = types.ModuleType("ansys")
    pkg_dpf = types.ModuleType("ansys.dpf")
    pkg_ansys.dpf = pkg_dpf
    pkg_dpf.core = core
    monkeypatch.setitem(sys.modules, "ansys", pkg_ansys)
    monkeypatch.setitem(sys.modules, "ansys.dpf", pkg_dpf)
    monkeypatch.setitem(sys.modules, "ansys.dpf.core", core)


def make_model(coords, vm, unit):
    """coords: (n,3) 좌표(해당 unit), vm: (n,) von Mises. 노드 id = 1..n"""
    ids = np.arange(1, len(vm) + 1)
    vm_field = _Obj(data=np.asarray(vm, float), scoping=_Obj(ids=ids), unit="Pa")
    stress = _Obj(on_location=lambda loc: _Obj(eval=lambda: [vm_field]))
    coord_field = _Obj(data=np.asarray(coords, float), unit=unit)
    mesh = _Obj(nodes=_Obj(coordinates_field=coord_field, scoping=_Obj(ids=ids)))
    return _Obj(results=_Obj(stress=stress), metadata=_Obj(meshed_region=mesh))


def two_hotspots(scale):
    """핫스팟 A(원점 부근, 0.5 mm 간격 3노드), B(100 mm 떨어진 곳, 3노드), 나머지 94노드는 저응력.
    scale: mm → 좌표 단위 배율 (m 이면 1e-3)."""
    rng = np.random.default_rng(0)
    cold = rng.uniform(300, 900, size=(94, 3))                    # mm, 핫스팟과 멀리
    a = np.array([[0, 0, 0], [0.5, 0, 0], [1.0, 0, 0]])
    b = np.array([[100, 0, 0], [100.5, 0, 0], [101.0, 0, 0]])
    coords = np.vstack([a, b, cold]) * scale
    vm = np.concatenate([[9e8, 8e8, 7e8], [6e8, 5.5e8, 5e8], rng.uniform(1e6, 1e7, 94)])
    return coords, vm


@pytest.mark.parametrize("unit,scale", [("m", 1e-3), ("mm", 1.0), ("cm", 0.1), ("in", 1 / 25.4)])
@pytest.mark.parametrize("use_scipy", [True, False])
def test_two_hotspots_stay_separate_in_any_unit(monkeypatch, unit, scale, use_scipy):
    fake_dpf(monkeypatch)
    if not use_scipy:
        monkeypatch.setitem(sys.modules, "scipy.spatial", None)   # import 실패 → numpy union-find 경로
    else:
        pytest.importorskip("scipy")
    mb = load_mx_batch()
    coords, vm = two_hotspots(scale)
    out = mb.hotspot_clusters(make_model(coords, vm, unit), quantile=0.94)   # 상위 6노드
    assert out["n_clusters"] == 2, out
    assert out["coord_unit"] == unit and out["eps_unit_assumed"] is False
    assert out["eps_coord_units"] == pytest.approx(2.0 * scale)
    assert [c["size"] for c in out["clusters"]] == [3, 3]
    assert out["clusters"][0]["peak_node"] == 1 and out["clusters"][1]["peak_node"] == 4


def test_unknown_unit_keeps_previous_mm_assumption(monkeypatch):
    fake_dpf(monkeypatch)
    mb = load_mx_batch()
    coords, vm = two_hotspots(1.0)                                  # mm 좌표인데 단위 정보 없음
    out = mb.hotspot_clusters(make_model(coords, vm, ""), quantile=0.94)
    assert out["n_clusters"] == 2 and out["eps_unit_assumed"] is True
    assert out["eps_coord_units"] == 2.0


def test_regression_old_behaviour_would_merge_m_coords(monkeypatch):
    """환산 전 동작(eps 2.0 을 m 좌표에 그대로)이면 100 mm 떨어진 두 핫스팟이 합쳐진다 — 버그 재현."""
    fake_dpf(monkeypatch)
    mb = load_mx_batch()
    coords, vm = two_hotspots(1e-3)
    monkeypatch.setattr(mb, "eps_in_coord_units", lambda eps_mm, unit: (float(eps_mm), False))
    out = mb.hotspot_clusters(make_model(coords, vm, "m"), quantile=0.94)
    assert out["n_clusters"] == 1


def test_eps_helper():
    mb = load_mx_batch()
    assert mb.eps_in_coord_units(2.0, "m") == (pytest.approx(0.002), True)
    assert mb.eps_in_coord_units(2.0, " MM ") == (2.0, True)
    assert mb.eps_in_coord_units(2.0, "µm") == (2000.0, True)
    assert mb.eps_in_coord_units(2.0, None) == (2.0, False)
    assert mb.eps_in_coord_units(2.0, "furlong") == (2.0, False)
