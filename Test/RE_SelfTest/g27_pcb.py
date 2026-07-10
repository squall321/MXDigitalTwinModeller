# encoding: utf-8
# g27 (PCB assembly, g26-level bar): volumes vs INDEPENDENT shoelace analytics on a
# NON-CONVEX board, single-solid integrity, seating coincidence, BGA grid truth:
#   A rect board + holes + cutout + block comp: board V = (A - holes - cutout) * t
#     exact; PieceCount 1; comp bottom z == board top (seating COINCIDES)
#   B non-convex L-board + rotated 45deg block + BGA: shoelace exact for the L,
#     rotated comp bbox = diagonal footprint, BGA ball count/volume/z-span exact,
#     package bottom == board top + standoff
#   C octagon + 3x3 hole grid + bottom stiffener (its own shoelace, z in [-t, 0])
#   D loud errors: self-intersecting bowtie / hole outside / duplicate ref / >400 balls
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
import math
from System.IO import File
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.Geometry import Matrix
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import LlmToolDispatcher

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g27_result.txt"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g27_mark.txt"
DONE = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g27_done.txt"

def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass
log = []
def emit(s):
    try: log.append(str(s))
    except Exception: log.append("(unprintable)")

H = {}

def body_by_name(part, name):
    for b in part.Bodies:
        if b.Name == name: return b
    return None

def vol(part, name):
    b = body_by_name(part, name)
    return b.Shape.Volume * 1e9 if b is not None else float("nan")

def bb_mm(db):
    bb = db.Shape.GetBoundingBox(Matrix.Identity)
    return (bb.MinCorner.X * 1000, bb.MinCorner.Y * 1000, bb.MinCorner.Z * 1000,
            bb.MaxCorner.X * 1000, bb.MaxCorner.Y * 1000, bb.MaxCorner.Z * 1000)

def shoelace(poly):
    a = 0.0
    for i in range(len(poly)):
        p, q = poly[i], poly[(i + 1) % len(poly)]
        a += p[0] * q[1] - q[0] * p[1]
    return abs(a / 2)

def make(json_args):
    Document.Create()
    env = LlmToolDispatcher.Dispatch(None, None, "create_pcb_assembly", json_args)
    return Window.ActiveWindow.Document.MainPart, env

def _do():
    _mk("do-start")

    # ---- A: rect board + 2 holes + cutout + block comp ------------------------
    part, env = make(
        '{"outline_mm": [[0,0],[50,0],[50,40],[0,40]], "thickness_mm": 1.6, '
        '"holes": [{"x": 5, "y": 5, "dia_mm": 3}, {"x": 45, "y": 35, "dia_mm": 3}], '
        '"cutouts_mm": [[[20,15],[28,15],[28,21],[20,21]]], '
        '"components": [{"ref": "U1", "x": 35, "y": 20, "w_mm": 10, "l_mm": 10, "h_mm": 2}]}')
    okA = '"success": true' in env
    if okA:
        area = 50 * 40 - 2 * math.pi * 1.5 ** 2 - 8 * 6
        vb = vol(part, "Pcb_Board")
        board = body_by_name(part, "Pcb_Board")
        comp = body_by_name(part, "Comp_U1")
        okA = (abs(vb - area * 1.6) < area * 1.6 * 0.001
               and board.Shape.PieceCount == 1 and comp is not None
               and abs(bb_mm(comp)[2] - 1.6) < 1e-6      # seating COINCIDES with board top
               and abs(vol(part, "Comp_U1") - 200) < 0.2
               and len(list(part.Bodies)) == 2)
        _mk("A rect: board=%.3f(exp %.3f) compZ=%.4f pieces=%d -> %s" % (
            vb, area * 1.6, bb_mm(comp)[2], board.Shape.PieceCount, okA))
    else:
        _mk("A env=" + env[:200])
    H["A"] = okA

    # ---- B: non-convex L-board + rotated block + BGA --------------------------
    Lpoly = [[0, 0], [60, 0], [60, 25], [30, 25], [30, 45], [0, 45]]
    part, env = make(
        '{"outline_mm": [[0,0],[60,0],[60,25],[30,25],[30,45],[0,45]], "thickness_mm": 1.0, '
        '"components": ['
        '{"ref": "SH1", "x": 15, "y": 35, "w_mm": 8, "l_mm": 8, "h_mm": 1.5, "rot_deg": 45}, '
        '{"ref": "AP1", "type": "bga", "x": 40, "y": 12, "w_mm": 12, "l_mm": 12, "h_mm": 1.0, '
        '"standoff_mm": 0.3, "ball_pitch_mm": 1.0}]}')
    okB = '"success": true' in env
    if okB:
        areaL = shoelace(Lpoly)
        vb = vol(part, "Pcb_Board")
        okB = abs(vb - areaL * 1.0) < areaL * 0.001
        # rotated block: bbox = 8/sqrt2 * 2 = 11.3137 across
        sh = body_by_name(part, "Comp_SH1")
        wx = bb_mm(sh)[3] - bb_mm(sh)[0]
        okB = okB and abs(wx - 8 * math.sqrt(2)) < 0.01
        # BGA: nx = ny = floor((12-1)/1)+1 = 12 -> 144 balls
        nballs = sum(1 for b in part.Bodies if (b.Name or "").startswith("Comp_AP1_Ball_"))
        vball = vol(part, "Comp_AP1_Ball_0001")
        exp_ball = math.pi * (0.55 / 2) ** 2 * 0.3
        ball1 = body_by_name(part, "Comp_AP1_Ball_0001")
        pkg = body_by_name(part, "Comp_AP1")
        okB = (okB and nballs == 144 and abs(vball - exp_ball) < exp_ball * 0.01
               and abs(bb_mm(ball1)[2] - 1.0) < 1e-6              # ball base ON board top
               and abs(bb_mm(ball1)[5] - 1.3) < 1e-6              # ball top at standoff
               and abs(bb_mm(pkg)[2] - 1.3) < 1e-6)               # package bottom on balls
        _mk("B L-board: V=%.2f(exp %.2f) rotW=%.4f(exp %.4f) balls=%d ballV=%.5f(exp %.5f) -> %s" % (
            vb, areaL, wx, 8 * math.sqrt(2), nballs, vball, exp_ball, okB))
    else:
        _mk("B env=" + env[:200])
    H["B"] = okB

    # ---- C: octagon + hole grid + bottom stiffener ------------------------------
    oct_pts = []
    for i in range(8):
        a = math.pi / 8 + i * math.pi / 4
        oct_pts.append([20 * math.cos(a), 20 * math.sin(a)])
    oct_json = ",".join("[%.6f,%.6f]" % (p[0], p[1]) for p in oct_pts)
    holes = ",".join('{"x": %d, "y": %d, "dia_mm": 2}' % (x, y)
                     for x in (-6, 0, 6) for y in (-6, 0, 6))
    part, env = make(
        '{"outline_mm": [%s], "thickness_mm": 0.8, "holes": [%s], '
        '"stiffener": {"outline_mm": [[-8,-8],[8,-8],[8,8],[-8,8]], "thickness_mm": 0.2}}'
        % (oct_json, holes))
    okC = '"success": true' in env
    if okC:
        areaO = shoelace(oct_pts) - 9 * math.pi * 1.0
        vb = vol(part, "Pcb_Board")
        st = body_by_name(part, "Pcb_Stiffener")
        okC = (abs(vb - areaO * 0.8) < areaO * 0.8 * 0.001
               and abs(vol(part, "Pcb_Stiffener") - 16 * 16 * 0.2) < 0.05
               and abs(bb_mm(st)[5]) < 1e-6 and abs(bb_mm(st)[2] + 0.2) < 1e-6)
        _mk("C octagon: V=%.3f(exp %.3f) stiff z[%.3f..%.3f] -> %s" % (
            vb, areaO * 0.8, bb_mm(st)[2], bb_mm(st)[5], okC))
    else:
        _mk("C env=" + env[:200])
    H["C"] = okC

    # ---- D: loud errors ----------------------------------------------------------
    # symmetric bowtie has EXACTLY zero shoelace -> the degenerate check fires first;
    # an ASYMMETRIC bowtie (net area 10) exercises the self-intersection path proper
    _, e0 = make('{"outline_mm": [[0,0],[10,10],[10,0],[0,10]]}')   # symmetric -> degenerate
    _, e1 = make('{"outline_mm": [[0,0],[10,8],[10,0],[0,10]]}')    # asymmetric -> crossing
    _, e2 = make('{"outline_mm": [[0,0],[50,0],[50,40],[0,40]], '
                 '"holes": [{"x": 100, "y": 5, "dia_mm": 3}]}')
    _, e3 = make('{"outline_mm": [[0,0],[50,0],[50,40],[0,40]], '
                 '"components": [{"ref": "U1", "x": 10, "y": 10, "w_mm": 5, "l_mm": 5, "h_mm": 1}, '
                 '{"ref": "U1", "x": 20, "y": 10, "w_mm": 5, "l_mm": 5, "h_mm": 1}]}')
    _, e4 = make('{"outline_mm": [[0,0],[50,0],[50,40],[0,40]], '
                 '"components": [{"ref": "B1", "type": "bga", "x": 25, "y": 20, '
                 '"w_mm": 30, "l_mm": 30, "h_mm": 1, "standoff_mm": 0.3, "ball_pitch_mm": 1.0}]}')
    H["D"] = ('degenerate' in e0 and 'self-intersecting' in e1
              and 'outside the board outline' in e2
              and 'duplicate component ref' in e3 and 'per-component limit' in e4
              and all('"success": false' in e for e in (e0, e1, e2, e3, e4)))
    _mk("D errors -> %s | %s | %s | %s | %s | %s" % (
        H["D"], e0[:60], e1[:60], e2[:60], e3[:60], e4[:70]))

try:
    WriteBlock.ExecuteTask("g27", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name + ": " + e.Message)
    emit("WB THREW: %s: %s" % (e.GetType().Name, e.Message))

KEYS = ["A", "B", "C", "D"]
for k in KEYS:
    emit("%s %s" % (k, H.get(k)))
allp = all(bool(H.get(k)) for k in KEYS)
emit("G27_PASS ALL=%s (%d/4)" % (allp, sum(1 for k in KEYS if H.get(k))))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
File.WriteAllText(DONE, "done\n", UTF8Encoding(False))
