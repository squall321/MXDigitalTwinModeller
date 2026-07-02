# encoding: utf-8
# v2 g17 (phone realism wave): multi-lens plateau, front punch-hole, back grille, S01 solid corners.
# All four are param->stage wiring over PROVEN primitives; every verdict is a volume-arithmetic band
# (kernel truth), plus the writer/parser ROUND-TRIP for the new spec keys.
#   T1 lenses: rrect camera + 2x d5 lenses -> S05L 2/2, dV ~= -2*pi*2.5^2*1.5 = -58.9
#   T2 punch:  solid(T=8)+pocket(d1) + d3 punch at (30,0) -> S04b, dV ~= -pi*1.5^2*7 = -49.5
#   T3 grille back: hollow + 2x3 d1 on_back -> S08 "back", dV ~= -6*pi*0.25*0.6 = -2.83
#   T4 S01: solid corner_r=3 T=8 -> S00 vs S01 stop dV = -(4-pi)*9*8 = -61.8
#   T5 round-trip: lenses/front_punch/on_back survive ToJson->Parse (0 err 0 warn); invalid
#      lens-outside-plateau and punch-outside-pocket are REJECTED.
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Generation import (
    SpecParser, GenerationService, PhoneParametersJsonWriter)

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g17_result.txt"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g17_mark.txt"
def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass
log = []
def emit(s):
    try: log.append(str(s))
    except Exception: log.append("(unprintable)")

BASE_H = '{"length_mm":150,"width_mm":72,"thickness_mm":8,"corner_r":3,"min_wall":0.4,"hollow_wall_mm":0.6,"pocket":false'
BASE_S = '{"length_mm":150,"width_mm":72,"thickness_mm":8,"corner_r":3,"min_wall":0.4,"hollow_wall_mm":0'
CAM = '"camera":{"x_mm":0,"y_mm":20,"width_mm":20,"length_mm":16,"height_mm":1.5,"corner_r":3'

def gen(spec, stop=None):
    pr = SpecParser.Parse(spec)
    if not pr.Success:
        return (None, None, ["(parse) " + e for e in pr.Errors])
    Document.Create()
    part = Window.ActiveWindow.Document.MainPart
    g = GenerationService().Generate(part, pr.Params, stop)
    vol = g.Body.Shape.Volume * 1e9 if (g.Success and g.Body is not None) else 0.0
    return (g, vol, list(g.StageLog))

def has(stages, sub):
    return any(sub in s for s in stages or [])

H = {}
def _do():
    _mk("do-start")
    # ---- T1 multi-lens plateau -------------------------------------
    specA = BASE_H + ', ' + CAM + '}}'
    specB = BASE_H + ', ' + CAM + ', "lenses":[{"x_mm":-4,"y_mm":0,"diameter_mm":5},{"x_mm":4,"y_mm":0,"diameter_mm":5}]}}'
    gA, vA, sA = gen(specA)
    gB, vB, sB = gen(specB)
    dv1 = (vA - vB) if (vA and vB) else None
    H["t1"] = (gA is not None and gA.Success and gB is not None and gB.Success
               and has(sB, "S05L lenses 2/2") and dv1 is not None and 45.0 < dv1 < 75.0)
    _mk("T1 vA=%s vB=%s dV=%s s05L=%s" % (vA, vB, dv1, [s for s in sB if "S05L" in s]))

    # ---- T2 front punch (solid + pocket) ----------------------------
    specC = BASE_S + ', "pocket":{"enabled":true,"width_mm":130,"length_mm":60,"depth_mm":1}}'
    specD = BASE_S + ', "pocket":{"enabled":true,"width_mm":130,"length_mm":60,"depth_mm":1}, ' + \
            '"front_punch":{"x_mm":30,"y_mm":0,"diameter_mm":3}}'
    gC, vC, sC = gen(specC)
    gD, vD, sD = gen(specD)
    dv2 = (vC - vD) if (vC and vD) else None
    H["t2"] = (gC is not None and gC.Success and gD is not None and gD.Success
               and has(sD, "S04b punch success=True") and dv2 is not None and 38.0 < dv2 < 53.0)
    _mk("T2 vC=%s vD=%s dV=%s s04b=%s" % (vC, vD, dv2, [s for s in sD if "S04b" in s]))

    # ---- T3 back grille (hollow) ------------------------------------
    specE = BASE_H + '}'
    specF = BASE_H + ', "grille":{"origin_x_mm":0,"origin_y_mm":-20,"pitch_mm":2,"rows":2,"cols":3,"hole_diameter_mm":1,"on_back":true}}'
    gE, vE, sE = gen(specE)
    gF, vF, sF = gen(specF)
    dv3 = (vE - vF) if (vE and vF) else None
    H["t3"] = (gE is not None and gE.Success and gF is not None and gF.Success
               and has(sF, "back") and has(sF, "S08 grille success=True")
               and dv3 is not None and 2.0 < dv3 < 3.7)
    _mk("T3 vE=%s vF=%s dV=%s s08=%s" % (vE, vF, dv3, [s for s in sF if "S08" in s]))

    # ---- T4 S01 solid corner rounding --------------------------------
    specG = BASE_S + '}'
    g0, v0, s0 = gen(specG, "S00")
    g1, v1, s1 = gen(specG, "S01")
    dv4 = (v0 - v1) if (v0 and v1) else None
    H["t4"] = (g0 is not None and g0.Success and g1 is not None and g1.Success
               and has(s1, "S01 corners success=True") and dv4 is not None and 58.0 < dv4 < 66.0)
    _mk("T4 v0=%s v1=%s dV=%s s01=%s" % (v0, v1, dv4, [s for s in s1 if "S01" in s]))

    # ---- T5 round-trip + rejections (pure C#, no geometry) -----------
    prG = SpecParser.Parse(specD)  # solid+punch spec
    prL = SpecParser.Parse(specB)                                            # lens spec
    rt_ok = False
    if prL.Success and prG.Success:
        j = PhoneParametersJsonWriter.ToJson(prL.Params)
        pr2 = SpecParser.Parse(j)
        j2 = PhoneParametersJsonWriter.ToJson(prG.Params)
        pr3 = SpecParser.Parse(j2)
        rt_ok = (pr2.Success and pr2.Errors.Count == 0 and pr2.Warnings.Count == 0
                 and pr2.Params.Camera is not None and pr2.Params.Camera.Lenses.Count == 2
                 and pr3.Success and pr3.Errors.Count == 0 and pr3.Warnings.Count == 0
                 and pr3.Params.FrontPunch is not None
                 and abs(pr3.Params.FrontPunch.DiameterMm - 3.0) < 1e-9)
        # grille on_back round-trip
        prF = SpecParser.Parse(specF)
        jF = PhoneParametersJsonWriter.ToJson(prF.Params)
        prF2 = SpecParser.Parse(jF)
        rt_ok = rt_ok and prF2.Success and prF2.Warnings.Count == 0 and prF2.Params.Grille.OnBack
    bad1 = SpecParser.Parse(BASE_H + ', ' + CAM + ', "lenses":[{"x_mm":12,"y_mm":0,"diameter_mm":5}]}}')
    bad2 = SpecParser.Parse(BASE_S + ', "pocket":{"enabled":true,"width_mm":130,"length_mm":60,"depth_mm":1}, ' +
                            '"front_punch":{"x_mm":70,"y_mm":0,"diameter_mm":3}}')
    H["t5"] = (rt_ok and (not bad1.Success) and (not bad2.Success))
    _mk("T5 rt=%s badLens=%s badPunch=%s" % (rt_ok, not bad1.Success, not bad2.Success))

try:
    WriteBlock.ExecuteTask("g17", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name + ": " + e.Message)
    emit("WB THREW: %s: %s" % (e.GetType().Name, e.Message))

for k in ["t1", "t2", "t3", "t4", "t5"]:
    emit("%s %s" % (k.upper(), H.get(k)))
allp = all(bool(H.get(k)) for k in ["t1", "t2", "t3", "t4", "t5"])
emit("G17_PASS ALL=%s (%d/5)" % (allp, sum(1 for k in ["t1", "t2", "t3", "t4", "t5"] if H.get(k))))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
