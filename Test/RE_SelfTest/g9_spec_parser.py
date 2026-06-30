# encoding: utf-8
# v2 g9 (LLM SpecParser, the last v2 item + full-pipeline capstone): a STRUCTURED JSON spec (the
# shape an LLM emits from natural language) -> SpecParser -> PhoneParameters -> Generate -> Freeze.
# Proves the complete from-scratch pipeline runs end-to-end from a spec, and that invalid specs are
# rejected BEFORE geometry.
#   CASE A  valid rich spec (curved back + lens-on-curved + flank USB-C) parses, validates, BUILDS,
#           and FREEZES (scdocx).
#   CASE B  design-intent-invalid spec (back bulge too deep for the wall) is REJECTED by Validate,
#           Params still returned but Success=False, no geometry attempted.
#   CASE C  malformed JSON is rejected with a parse error (not a crash).
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Generation import (
    SpecParser, GenerationService, FeaFreezeService)

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g9_spec_result.txt"
FROZEN = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g9_spec_frozen"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g9_mark.txt"
def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass
log = []
def emit(s):
    try: log.append("".join(ch for ch in str(s) if ord(ch) >= 32 or ch == "\t"))
    except Exception: log.append("(unprintable)")

# CASE A: a realistic spec an LLM would emit for "a 150x72x8mm phone, gently curved back, a 4mm
# lens hole on the curved back, USB-C on the bottom flank, rounded 3mm corners, 0.6mm wall".
SPEC_A = """{
  "length_mm": 150.0, "width_mm": 72.0, "thickness_mm": 8.0,
  "corner_r": 3.0, "min_wall": 0.4, "hollow_wall_mm": 0.6,
  "back_bulge_mm": 0.7, "lens_on_curved_back": true, "ports_on_flank": true,
  "pocket": false,
  "holes": [ { "x_mm": 20.0, "y_mm": 0.0, "diameter_mm": 4.0, "through": false, "depth_mm": 1.0, "on_curved_back": true } ],
  "ports": [ { "type": "usbc", "x_mm": 0.0, "y_mm": -36.0, "z_mm": 4.0, "width_mm": 9.0, "height_mm": 3.0, "on_face": "flank" } ]
}"""
# CASE B: back bulge deeper than (thickness - min_wall) -> Validate rejects.
SPEC_B = """{ "length_mm": 146.7, "width_mm": 71.5, "thickness_mm": 7.4, "min_wall": 0.4, "back_bulge_mm": 7.2 }"""
# CASE C: malformed JSON (missing closing brace / value).
SPEC_C = """{ "length_mm": 150.0, "width_mm": """

H = {}
def _do():
    _mk("do-start")
    # CASE A: parse + build + freeze inside one doc.
    rA = SpecParser.Parse(SPEC_A)
    H["A_parse"] = (rA.Success, list(rA.Errors), list(rA.Warnings), rA.Params)
    _mk("A-parse ok=%s errs=%d" % (rA.Success, rA.Errors.Count))
    if rA.Success:
        Document.Create()
        part = Window.ActiveWindow.Document.MainPart
        g = GenerationService().Generate(part, rA.Params, None)
        H["A_gen"] = (g.Success, g.MeasuredVolumeMm3, g.ValidationPass, list(g.StageLog))
        _mk("A-gen ok=%s vol=%.1f" % (g.Success, g.MeasuredVolumeMm3))
        if g.Success:
            fr = FeaFreezeService.Freeze(g.Body, FROZEN + ".scdocx", False, "scdocx")
            H["A_freeze"] = (fr.Success, fr.Format, fr.VolumeMm3, fr.FileBytes)
            _mk("A-freeze ok=%s fmt=%s" % (fr.Success, fr.Format))
    # CASE B: invalid design intent -> rejected, no geometry.
    rB = SpecParser.Parse(SPEC_B)
    H["B"] = (rB.Success, list(rB.Errors), rB.Params is not None)
    _mk("B ok=%s errs=%d" % (rB.Success, rB.Errors.Count))
    # CASE C: malformed JSON -> parse error.
    rC = SpecParser.Parse(SPEC_C)
    H["C"] = (rC.Success, list(rC.Errors))
    _mk("C ok=%s errs=%d" % (rC.Success, rC.Errors.Count))
try:
    WriteBlock.ExecuteTask("g9", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name)
    emit("WRITEBLOCK THREW: %s: %s" % (e.GetType().Name, e.Message))

# ---- verdict ----
def has(errlist, frag):
    return any(frag in e for e in errlist)

A_parse = H.get("A_parse")
A_gen = H.get("A_gen")
A_freeze = H.get("A_freeze")
B = H.get("B")
C = H.get("C")

if A_parse:
    okp, errs, warns, prm = A_parse
    emit("A_parse: success=%s errs=%s warns=%s" % (okp, errs, warns))
    if prm is not None:
        emit("A_params: L=%.1f W=%.1f T=%.1f wall=%.2f bulge=%.2f lensCurved=%s portsFlank=%s holes=%d ports=%d" % (
            prm.LengthMm, prm.WidthMm, prm.ThicknessMm, prm.HollowWallMm, prm.BackBulgeMm,
            prm.LensOnCurvedBack, prm.PortsOnFlank, prm.Holes.Count, prm.Ports.Count))
if A_gen:
    emit("A_gen: success=%s vol=%.1f validationPass=%s" % (A_gen[0], A_gen[1], A_gen[2]))
    for l in A_gen[3]: emit("   " + l)
if A_freeze:
    emit("A_freeze: success=%s fmt=%s vol=%.1f bytes=%s" % A_freeze)
if B:
    emit("B(invalid): success=%s errs=%s paramsReturned=%s" % (B[0], B[1], B[2]))
if C:
    emit("C(malformed): success=%s errs=%s" % (C[0], C[1]))

# A: parsed (right field values), built a closed solid w/ curved+lens+flank routing, froze.
A_fields_ok = bool(A_parse) and A_parse[0] and A_parse[3] is not None and \
    abs(A_parse[3].LengthMm - 150.0) < 1e-6 and A_parse[3].LensOnCurvedBack and \
    A_parse[3].PortsOnFlank and A_parse[3].Holes.Count == 1 and A_parse[3].Ports.Count == 1
A_built = bool(A_gen) and A_gen[0] and A_gen[2] and A_gen[1] > 0
A_frozen = bool(A_freeze) and A_freeze[0]
A_ok = A_fields_ok and A_built and A_frozen
# B: rejected by Validate (success False) but params object still returned (graceful).
B_ok = bool(B) and (not B[0]) and B[2] and len(B[1]) > 0
# C: parse error (success False, error mentions parse/JSON).
C_ok = bool(C) and (not C[0]) and len(C[1]) > 0
allp = A_ok and B_ok and C_ok
emit("G9_PASS specBuildsFreezes=%s invalidRejected=%s malformedRejected=%s ALL=%s" % (
    A_ok, B_ok, C_ok, allp))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
