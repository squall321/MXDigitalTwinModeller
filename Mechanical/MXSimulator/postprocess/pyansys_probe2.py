# encoding: utf-8
# PyAnsys/DPF probe v2 — the DECISIVE Phase-2 test. The v1 probe proved pip install works on
# Python 3.13 and DPF imports cleanly; launch_mechanical failed only on a version/path mixup
# (grabbed v251, "no path found"). Phase 2's real need is DPF reading result FIELDS from an .rst,
# NOT a live Mechanical session. This probe answers the actual gate:
#   A) does DPF start its LOCAL server (Ans.Dpf.Grpc.exe exists under v252\aisol\bin\winx64)?
#   B) can DPF LOAD a result model + read a displacement/stress FIELD (its own bundled example)?
#   C) (secondary) can launch_mechanical find v252 when pointed at the exe explicitly?
# Each block is isolated and prints a machine-parsable RESULT: line.
import os
import sys
import traceback

AWP = os.environ.get("AWP_ROOT252", r"D:\Program Files\ANSYS Inc\ANSYS Student\v252")
DPF_EXE = os.path.join(AWP, "aisol", "bin", "winx64", "Ans.Dpf.Grpc.exe")
# Help ansys-tools-path find v252 for launch_mechanical (C).
os.environ.setdefault("AWP_ROOT252", AWP)

def line(tag, ok, detail=""):
    print("RESULT %-14s : %-4s | %s" % (tag, "OK" if ok else "FAIL", detail))

# --- A) local DPF server -------------------------------------------------
srv = None
try:
    from ansys.dpf import core as dpf
    print("Ans.Dpf.Grpc.exe present:", os.path.exists(DPF_EXE))
    # Point DPF at the v252 install so it uses the local server, not an ambient one.
    try:
        dpf.set_default_server_context(dpf.AvailableServerContexts.premium)
    except Exception:
        pass
    if os.path.exists(DPF_EXE):
        try:
            dpf.start_local_server(ansys_path=AWP)
        except Exception as e:
            print("start_local_server(ansys_path) note:", type(e).__name__, str(e)[:200])
    srv = dpf.server.get_or_create_server(None)
    line("dpf_server", srv is not None, "server=%s ver=%s" % (
        srv, getattr(srv, "version", "?")))
except Exception as e:
    line("dpf_server", False, "%s: %s" % (type(e).__name__, str(e)[:250]))
    traceback.print_exc()

# --- B) DPF loads a result model + reads a FIELD -------------------------
try:
    from ansys.dpf import core as dpf
    from ansys.dpf.core import examples
    # a bundled static-analysis result shipped with ansys-dpf-core
    rst = examples.find_static_rst()
    print("example rst:", rst)
    model = dpf.Model(rst)
    disp = model.results.displacement().eval()
    f0 = disp[0]
    nvals = len(f0.data)
    dmax = max((sum(v*v for v in row) ** 0.5) for row in f0.data) if nvals else 0.0
    line("dpf_read_field", nvals > 0, "disp nodes=%d |u|max=%.4g m" % (nvals, dmax))
    # also try stress (Phase-2 hot-spot needs it)
    try:
        stress = model.results.stress().eval()
        sn = len(stress[0].data)
        line("dpf_read_stress", sn > 0, "stress entities=%d" % sn)
    except Exception as e:
        line("dpf_read_stress", False, "%s: %s" % (type(e).__name__, str(e)[:150]))
except Exception as e:
    line("dpf_read_field", False, "%s: %s" % (type(e).__name__, str(e)[:250]))
    traceback.print_exc()

# --- C) launch_mechanical(v252) pointed at the real exe (secondary) ------
try:
    from ansys.mechanical.core import launch_mechanical
    exe = os.path.join(AWP, "aisol", "bin", "winx64", "AnsysWBU.exe")
    print("Mechanical exe present:", os.path.exists(exe))
    m = launch_mechanical(exec_file=exe, version="252", batch=True)
    line("launch_mechanical", True, "session=%s" % m)
    try: m.exit()
    except Exception: pass
except Exception as e:
    line("launch_mechanical", False, "%s: %s" % (type(e).__name__, str(e)[:250]))

print("PROBE2_DONE")
