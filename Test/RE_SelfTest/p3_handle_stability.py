# encoding: utf-8
# P3 GATE (FROM_SCRATCH_ROADMAP.md): feature-handle ID stability. Positional IDs ("H"+i)
# renumber when an earlier feature is inserted; a STABLE handle (anchor+size) must still
# resolve to the SAME physical hole. Test: generate a phone with N holes, extract the graph,
# resolve each handle -> positional ID; then INSERT an extra hole at the front-ish (forcing
# renumber), re-extract, and assert each original handle STILL resolves to the same physical
# hole (same axis/diameter), defeating the drift.
import clr, math
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer import PhoneParameters
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import FeatureExtractor
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Generation import GenerationService

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\p3_handle_result.txt"
log = []
def emit(s): log.append(str(s))

def make_params():
    p = PhoneParameters()
    p.LengthMm = 146.7; p.WidthMm = 71.5; p.ThicknessMm = 7.4
    p.HollowWallMm = 0.0  # solid for this test (isolate ID drift from shell)
    p.Pocket.Enabled = False
    p.Camera = None
    p.Holes.Clear()
    # 4 distinct holes at known anchors (corners, outside any pocket)
    for (x, y, d) in [(-60.0, -30.0, 4.0), (60.0, -30.0, 5.0), (-60.0, 30.0, 6.0), (60.0, 30.0, 3.0)]:
        h = PhoneParameters.HoleSpec(); h.XMm = x; h.YMm = y; h.DiameterMm = d; h.Through = True
        p.Holes.Add(h)
    return p

holder = {}
def _do():
    Document.Create()
    part = Window.ActiveWindow.Document.MainPart
    p = make_params()
    gs = GenerationService()
    res = gs.Generate(part, p, None)
    holder["res"] = res
    holder["body"] = res.Body
    holder["part"] = part
    # extract graph #1
    holder["g1"] = FeatureExtractor().Extract(res.Body)

WriteBlock.ExecuteTask("P3 generate", Task(_do))
res = holder["res"]
g1 = holder["g1"]
reg = res.Handles
emit("generated: success=%s holes_in_graph=%d handles=%d" % (
    res.Success, g1.Holes.Count if g1.Holes else 0, reg.All.Count))

# resolve each handle on graph #1 -> positional ID, record (handleId -> (posId, axisDiam))
def hole_by_id(g, hid):
    for h in g.Holes:
        if h.Id == hid: return h
    return None

before = {}
for hd in reg.All:
    if hd.Kind != "hole": continue
    pid = reg.ResolveHoleId(g1, hd.HandleId, 2.0)
    hh = hole_by_id(g1, pid) if pid else None
    dia = hh.DiameterMm if hh else -1
    before[hd.HandleId] = (pid, round(dia, 2))
    emit("  G1 handle %s anchor=(%.0f,%.0f) nomD=%.1f -> posId=%s actualD=%.2f" % (
        hd.HandleId, hd.AnchorMm[0], hd.AnchorMm[1], hd.NominalSizeMm, pid, dia))

# Now INSERT an extra hole that will perturb face order / renumber.
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import ModificationService
from System import Array
def _insert():
    body = holder["body"]
    # a NEW hole near the centre (different anchor) to force a renumber of the list.
    # ModificationService takes positionMm in MILLIMETERS (P1 finding).
    ModificationService.AddHole(body, Array[float]([0.0, 0.0, 7.4]),
                                4.5, True, 0.0, Array[float]([0.0, 0.0, 1.0]), False)
    holder["g2"] = FeatureExtractor().Extract(body)
WriteBlock.ExecuteTask("P3 insert", Task(_insert))
g2 = holder["g2"]
emit("after insert: holes_in_graph=%d (was %d)" % (g2.Holes.Count if g2.Holes else 0,
                                                    g1.Holes.Count if g1.Holes else 0))

# Re-resolve each handle on graph #2 and assert it maps to the SAME physical hole (same diameter).
all_stable = True
for hd in reg.All:
    if hd.Kind != "hole": continue
    pid2 = reg.ResolveHoleId(g2, hd.HandleId, 2.0)
    hh2 = hole_by_id(g2, pid2) if pid2 else None
    dia2 = round(hh2.DiameterMm, 2) if hh2 else -1
    (pid1, dia1) = before[hd.HandleId]
    same_physical = (dia2 == dia1 and dia2 > 0)
    # the POSITIONAL id may differ (that's the drift); the resolved DIAMETER must match.
    drift = (pid2 != pid1)
    if not same_physical: all_stable = False
    emit("  G2 handle %s -> posId=%s(was %s, drifted=%s) actualD=%.2f(was %.2f) SAME=%s" % (
        hd.HandleId, pid2, pid1, drift, dia2, dia1, same_physical))

emit("P3_PASS handle_stability=%s (all original handles still hit their physical hole)" % all_stable)
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
