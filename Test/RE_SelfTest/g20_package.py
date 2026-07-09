# encoding: utf-8
# g20 (package stack): the ball-map -> CAD pipeline must (T1) parse the REAL conformal
# example clean (8 layers / 2131 balls / 2 dies, Mesh* keys filtered), (T2) build a small
# stack with CYLINDER joints + resin matrix at kernel-true volumes, (T3) build BARREL
# (convex reflowed) joints inside the analytic volume window, (T4) flow through the real
# MCP dispatcher (parse + generate), and (T5) laminate a generated layer BY NAME
# (laminate_body body_name) - the productivity ask.
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
import math
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.Geometry import Matrix
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import LlmToolDispatcher
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Package import (
    PackageFileParser, PackageGenerationService)
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Package import PackageGenOptions

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g20_result.txt"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g20_mark.txt"
DONE = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g20_done.txt"
REAL = r"D:\MXDigitalTwinModeller\Examples\packages\aptest_conformal_complete.txt"
SMALL = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g20_small_package.txt"

def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass
log = []
def emit(s):
    try: log.append(str(s))
    except Exception: log.append("(unprintable)")

SMALL_TXT = """*Layer,PCB
Location,0,0,0
Length,10,10
Thickness,0.5
MeshGenerationType,Solid,ConformalHexa
MeshSizeInPlane,0.5
*Layer,BGA
Location,0,0
Length,10,10
Thickness,0.3
MeshSizeInPlane,0.5
Cylinder,-2,-2,0.5
Cylinder,2,-2,0.5
Cylinder,-2,2,0.5
Cylinder,2,2,0.5
Box,0,0,2,1.5
*Layer,LID
Location,0,0
Length,10,10
Thickness,0.4
"""

H = {}

def vol_mm3(db):
    return db.Shape.Volume * 1e9

def body_by_name(part, name):
    for b in part.Bodies:
        if b.Name == name: return b
    return None

def _do():
    _mk("do-start")
    File.WriteAllText(SMALL, SMALL_TXT, UTF8Encoding(False))

    # ---- T1: parse the REAL example ------------------------------------
    spec = PackageFileParser.ParseFile(REAL)
    nlay = spec.Layers.Count
    balls = sum(l.Balls.Count for l in spec.Layers)
    boxes = sum(l.Boxes.Count for l in spec.Layers)
    meshed = sum(1 for l in spec.Layers if l.MeshOptions.Count >= 3)
    names = [l.Name for l in spec.Layers]
    tt = spec.GetTotalThicknessMm()
    H["t1"] = (nlay == 8 and balls == 2131 and boxes == 2 and meshed == 8
               and names[0] == "PCB" and names[1] == "SolderJoint" and tt > 1.3)
    _mk("t1 layers=%d balls=%d boxes=%d meshed=%d tt=%.3f warn=%d -> %s" % (
        nlay, balls, boxes, meshed, tt, spec.Warnings.Count, H["t1"]))

    # ---- T2: small stack, CYLINDER joints + matrix ----------------------
    Document.Create()
    part = Window.ActiveWindow.Document.MainPart
    spec2 = PackageFileParser.ParseFile(SMALL)
    r2 = PackageGenerationService().BuildStack(part, spec2, PackageGenOptions())
    res2, bound2 = r2[0], r2[1]
    bnames = sorted([b.Name for b in part.Bodies])
    vball = vol_mm3(body_by_name(part, "BGA_Ball_0001"))
    vmat = vol_mm3(body_by_name(part, "BGA_Matrix"))
    vdie = vol_mm3(body_by_name(part, "BGA_Die_1"))
    exp_ball = math.pi * 0.5 * 0.5 * 0.3            # 0.23562
    exp_die = 2.0 * 1.5 * 0.3                       # 0.9
    exp_mat = 10 * 10 * 0.3 - 4 * exp_ball - exp_die  # 28.1575
    lid = body_by_name(part, "LID")
    zmin = lid.Shape.GetBoundingBox(Matrix.Identity).MinCorner.Z * 1000.0
    H["t2"] = (res2.Success and res2.TotalBodies == 8
               and abs(vball - exp_ball) < exp_ball * 0.02
               and abs(vdie - exp_die) < exp_die * 0.02
               and abs(vmat - exp_mat) < exp_mat * 0.01
               and bound2 is not None and bound2.Name == "PCB"
               and abs(zmin - 0.8) < 0.01)
    _mk("t2 ok=%s bodies=%d ball=%.4f(exp %.4f) mat=%.3f(exp %.3f) die=%.3f lidZ=%.3f bound=%s" % (
        res2.Success, res2.TotalBodies, vball, exp_ball, vmat, exp_mat, vdie, zmin,
        bound2.Name if bound2 else "-"))
    _mk("t2 names=" + ",".join(bnames))

    # ---- T3: BARREL joints ----------------------------------------------
    Document.Create()
    part3 = Window.ActiveWindow.Document.MainPart
    opt3 = PackageGenOptions()
    opt3.BallShape = "barrel"
    opt3.BarrelBulgeRatio = 1.3
    opt3.BarrelSlices = 8
    r3 = PackageGenerationService().BuildStack(part3, PackageFileParser.ParseFile(SMALL), opt3)
    res3 = r3[0]
    vb = vol_mm3(body_by_name(part3, "BGA_Ball_0001"))
    vm3 = vol_mm3(body_by_name(part3, "BGA_Matrix"))
    # stacked-disc analytic for r0=0.5, bulge=1.3, t=0.3, 8 slices -> 0.3624 mm^3
    H["t3"] = (res3.Success and res3.TotalBodies == 8
               and vb > exp_ball * 1.2 and 0.30 < vb < 0.395
               and vm3 < vmat)  # bulged balls remove MORE resin
    _mk("t3 ok=%s bodies=%d barrelBall=%.4f (cyl %.4f) mat=%.3f -> %s" % (
        res3.Success, res3.TotalBodies, vb, vball, vm3, H["t3"]))

    # ---- T4: MCP dispatcher flow ----------------------------------------
    env_p = LlmToolDispatcher.Dispatch(None, None, "parse_package_file",
        '{"path": "%s"}' % REAL.replace("\\", "\\\\"))
    ok_p = ('"success": true' in env_p) and ('"total_balls": 2131' in env_p) \
        and ('"total_boxes": 2' in env_p) and ('MeshGenerationType' in env_p)
    Document.Create()
    env_g = LlmToolDispatcher.Dispatch(None, None, "generate_package_from_file",
        '{"path": "%s", "ball_shape": "barrel", "barrel_bulge_ratio": 1.3}'
        % SMALL.replace("\\", "\\\\"))
    ok_g = ('"success": true' in env_g) and ('"total_bodies": 8' in env_g) \
        and ('"ball_shape": "barrel"' in env_g) and ('"bound_body": "PCB"' in env_g)
    H["t4"] = ok_p and ok_g
    _mk("t4 parse=%s gen=%s -> %s" % (ok_p, ok_g, H["t4"]))
    _mk("t4 parse " + env_p[:220])
    _mk("t4 gen   " + env_g[:300])

    # ---- T5: laminate a generated layer BY NAME -------------------------
    part5 = Window.ActiveWindow.Document.MainPart
    pcb = body_by_name(part5, "PCB")
    env_l = LlmToolDispatcher.Dispatch(pcb, None, "laminate_body",
        '{"body_name": "LID", "layers": [{"name": "LID_a", "thickness_mm": 0.2}, '
        '{"name": "LID_b", "thickness_mm": 0.2}], "delete_original": true}')
    lid_a = body_by_name(part5, "LID_a")
    lid_b = body_by_name(part5, "LID_b")
    H["t5"] = (('"success": true' in env_l) and lid_a is not None and lid_b is not None
               and abs(vol_mm3(lid_a) - 10 * 10 * 0.2) < 0.2)
    _mk("t5 ok=%s env=%s" % (H["t5"], env_l[:220]))

try:
    WriteBlock.ExecuteTask("g20", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name + ": " + e.Message)
    emit("WB THREW: %s: %s" % (e.GetType().Name, e.Message))

KEYS = ["t1", "t2", "t3", "t4", "t5"]
for k in KEYS:
    emit("%s %s" % (k.upper(), H.get(k)))
allp = all(bool(H.get(k)) for k in KEYS)
emit("G20_PASS ALL=%s (%d/5)" % (allp, sum(1 for k in KEYS if H.get(k))))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
File.WriteAllText(DONE, "done\n", UTF8Encoding(False))
