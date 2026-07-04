# encoding: utf-8
"""
Gate for mx_batch.py — runs the DPF sidecar on the bundled ANSYS DPF example result files
(no network, local server) and asserts the JSON carries the Phase-2 quantities.

Run:  .venv-pyansys\\Scripts\\python.exe selftest_mx_batch.py
Expect:  GATE_OK
"""
import os
import sys
import json
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import mx_batch as MB
from ansys.dpf.core import examples


def run_on(rst, label):
    out = os.path.join(tempfile.gettempdir(), "mx_batch_%s.json" % label)
    MB.main([rst, out])
    with open(out, "r", encoding="utf-8") as f:
        d = json.load(f)
    print("--- %s (%s) ---" % (label, d.get("analysis_type")))
    print("  errors:", d.get("errors"))
    if d.get("fatal"):
        print("  FATAL:", d["fatal"])
    return d


def main():
    # 1) MODAL example -> frequencies, participation, MAC, hotspots
    modal_rst = examples.download_modal_frame()
    dm = run_on(modal_rst, "modal")
    assert not dm.get("fatal"), "modal run fatal"
    assert dm.get("analysis_type", "").lower().find("modal") >= 0, "not detected modal"
    md = dm.get("modal") or {}
    assert md.get("n_modes", 0) >= 1, "no modes"
    assert len(md.get("freqs_hz", [])) == md["n_modes"], "freq count mismatch"
    assert all(f > 0 for f in md["freqs_hz"]), "non-positive freq"
    part = dm.get("participation") or {}
    assert part.get("method") in ("lumped_mass", "unit_mass"), "participation method unset"
    mac = dm.get("mac") or {}
    assert mac.get("present"), "no MAC"
    if mac.get("mode") == "self":
        assert mac.get("diag_min", 0) > 0.99, "self-MAC diagonal not ~1 (got %s)" % mac.get("diag_min")
    hs = dm.get("hotspots") or {}
    assert "clusters" in hs, "no hotspots block"
    print("  modes=%d f1=%.2f%s  participation=%s  MAC=%s  hotspots=%d" % (
        md["n_modes"], md["freqs_hz"][0], md.get("unit", ""),
        part.get("method"), mac.get("mode"), hs.get("n_clusters", 0)))

    # 2) STATIC example -> real elemental strain energy (Joules)
    static_rst = examples.find_static_rst()
    ds = run_on(static_rst, "static")
    assert not ds.get("fatal"), "static run fatal"
    se = ds.get("strain_energy") or {}
    hs2 = ds.get("hotspots") or {}
    print("  strain_energy branch=%s total=%s  hotspots=%d" % (
        se.get("branch"), se.get("total"), hs2.get("n_clusters", 0)))
    # static SE may be license/op-dependent; require the block to exist and be non-fatal
    assert se.get("branch") == "static", "static SE branch missing"

    print("GATE_OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
