# encoding: utf-8
"""Gate for sweep_analyzer — synthetic sweep with a KNOWN analytic relationship so sensitivity /
Pareto / response-surface are checkable. Run: python selftest_sweep.py  → expect SWEEP_OK."""
import os
import sys
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sweep_analyzer as SA


def main():
    # Ground truth: max_vm = 200 - 30*rib + 2*rib^2  (decreasing then flattening in rib),
    #               max_def = 0.8 - 0.1*rib          (linear).  wall is a dummy (no effect).
    rng = np.linspace(1.0, 5.0, 9)
    cases = []
    for i, rib in enumerate(rng):
        vm = 200 - 30 * rib + 2 * rib * rib
        dof = 0.8 - 0.1 * rib
        cases.append({"name": "c%d" % i, "params": {"rib_mm": float(rib), "wall_mm": 0.6},
                      "metrics": {"max_vm": float(vm), "max_def": float(dof)}})

    # --- sensitivity: ∂max_def/∂rib should be ~ -0.1 (exact linear) ---
    sens = SA.sensitivity(cases)
    s_def_rib = sens["max_def"]["rib_mm"]["slope"]
    assert abs(s_def_rib - (-0.1)) < 1e-6, "def/rib slope %.4f != -0.1" % s_def_rib
    # wall has zero effect -> slope ~ 0
    assert abs(sens["max_def"]["wall_mm"]["slope"]) < 1e-6, "wall should have no effect"
    print("  sensitivity: d(max_def)/d(rib)=%.4f (exp -0.1), wall~0 OK" % s_def_rib)

    # --- response surface: quadratic recovers max_vm exactly (r2 ~ 1) ---
    rs = SA.response_surface(cases, "max_vm", degree=2)
    assert rs["r2"] > 0.999, "quadratic RSM r2=%.4f should be ~1" % rs["r2"]
    print("  RSM(max_vm) degree=%d r2=%.5f OK" % (rs["degree"], rs["r2"]))

    # --- Pareto: minimise both max_vm and max_def ---
    # max_vm is convex (min near rib~5 tail), max_def strictly decreasing -> the front is the
    # set of non-dominated (low-vm, low-def) cases. Just assert it is non-empty and a subset.
    front, dom = SA.pareto_front(cases, {"max_vm": "min", "max_def": "min"})
    assert len(front) >= 1 and len(front) + len(dom) == len(cases), "pareto partition broken"
    # the case with the globally-min max_vm must be on the front
    vms = [c["metrics"]["max_vm"] for c in cases]
    imin = int(np.argmin(vms))
    assert imin in front, "global-min-vm case must be Pareto-optimal"
    print("  Pareto: front=%d dominated=%d, argmin(vm) on front OK" % (len(front), len(dom)))

    # --- one-call bundle is JSON-serialisable ---
    import json
    bundle = SA.analyze_sweep(cases, objectives={"max_vm": "min", "max_def": "min"})
    json.dumps(bundle)  # must not raise
    assert bundle["n_cases"] == len(cases)
    assert "pareto" in bundle and "sensitivity" in bundle and "response_surface" in bundle
    print("  analyze_sweep bundle JSON-serialisable, keys OK")

    print("SWEEP_OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
