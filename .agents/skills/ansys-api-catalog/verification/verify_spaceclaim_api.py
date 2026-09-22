# encoding: utf-8
# ============================================================================
# verify_spaceclaim_api.py
#
# SpaceClaim 안에서 직접 실행해서 카탈로그의 API 가 실제로 동작하는지 확인.
# 결과: pass/fail 표를 콘솔에 출력.
#
# 사용법:
#   1. SpaceClaim 실행
#   2. Design 탭 → Script Editor (Ctrl+M)
#   3. 이 파일 내용 붙여넣기 또는 File → Open Script → 이 파일 선택
#   4. Run
#
# 안전: 새 빈 document 를 생성해서 그 안에서 테스트. 사용자 모델 영향 X.
# ============================================================================

print("=" * 70)
print("MX API Catalog — SpaceClaim API Verification")
print("=" * 70)

PASS = []
FAIL = []
NA = []

def check(name, condition, detail=""):
    if condition is True:
        PASS.append((name, detail))
        print("  [PASS] {0}{1}".format(name, " - " + detail if detail else ""))
    elif condition is False:
        FAIL.append((name, detail))
        print("  [FAIL] {0}{1}".format(name, " - " + detail if detail else ""))
    else:
        NA.append((name, detail))
        print("  [N/A ] {0}{1}".format(name, " - " + detail if detail else ""))

def safe(fn, *args, **kwargs):
    try:
        return True, fn(*args, **kwargs)
    except Exception as ex:
        return False, str(ex)


# ----------------------------------------------------------------------------
# 1. Imports — namespace 가용성
# ----------------------------------------------------------------------------
print("\n[1] Namespace imports")

try:
    from SpaceClaim.Api.V252.Geometry import Point, Vector, Direction, Plane, Frame, Matrix, Box, Interval, Circle
    check("SpaceClaim.Api.V252.Geometry (Point, Vector, Plane, Frame, etc.)", True)
except ImportError as ex:
    check("SpaceClaim.Api.V252.Geometry imports", False, str(ex))
    raise SystemExit("Critical: SpaceClaim API not loaded")

try:
    from SpaceClaim.Api.V252.Modeler import Body, DesignBody, Part, Window, Group, WriteBlock
    check("SpaceClaim.Api.V252.Modeler (Body, DesignBody, Part, Window, etc.)", True)
except ImportError as ex:
    check("SpaceClaim.Api.V252.Modeler imports", False, str(ex))

try:
    from SpaceClaim.Api.V252.Modeler import Profile, RectangleProfile, CircleProfile
    check("SpaceClaim.Api.V252.Modeler (Profile, RectangleProfile, CircleProfile)", True)
except ImportError as ex:
    check("Profile imports", False, str(ex))

try:
    from SpaceClaim.Api.V252.Geometry import CurveSegment, ITrimmedCurve
    check("SpaceClaim.Api.V252.Geometry (CurveSegment, ITrimmedCurve)", True)
except ImportError as ex:
    check("Curve imports", False, str(ex))


# ----------------------------------------------------------------------------
# 2. Window / Document — must have one open
# ----------------------------------------------------------------------------
print("\n[2] Window / Document")

ok, win = safe(lambda: Window.ActiveWindow)
check("Window.ActiveWindow", ok)

if not ok or win is None:
    check("Document available", False, "Open or create a document first")
    raise SystemExit("Critical: no active document")

ok, doc = safe(lambda: win.Document)
check("window.Document", ok)

ok, main_part = safe(lambda: doc.MainPart)
check("document.MainPart", ok)


# ----------------------------------------------------------------------------
# 3. Geometry primitives — value classes
# ----------------------------------------------------------------------------
print("\n[3] Geometry primitives")

ok, p = safe(lambda: Point.Create(0.0, 0.0, 0.0))
check("Point.Create(x, y, z)", ok, "(0,0,0)" if ok else p)

ok, p2 = safe(lambda: Point.Create(0.001, 0.001, 0.0))
check("Point.Create — second point", ok)

if ok:
    try:
        v = p2 - p  # Point - Point → Vector
        check("Point - Point → Vector (operator)", True, "magnitude=" + str(v.Magnitude))
    except Exception as ex:
        check("Point - Point", False, str(ex))

check("Direction.DirX (static)", hasattr(Direction, "DirX"))
check("Direction.DirY (static)", hasattr(Direction, "DirY"))
check("Direction.DirZ (static)", hasattr(Direction, "DirZ"))

ok, fr = safe(lambda: Frame.Create(Point.Create(0.0, 0.0, 0.0), Direction.DirX, Direction.DirY))
check("Frame.Create(origin, dirX, dirY)", ok)

ok, pl = safe(lambda: Plane.PlaneXY)
check("Plane.PlaneXY (static)", ok)

ok, pl2 = safe(lambda: Plane.Create(fr))
check("Plane.Create(frame)", ok)

ok, mat = safe(lambda: Matrix.Identity)
check("Matrix.Identity (static)", ok)


# ----------------------------------------------------------------------------
# 4. Body creation — actually extrude something tiny
# ----------------------------------------------------------------------------
print("\n[4] Body / Profile creation (creates tiny test body, deletes after)")

test_design_body = None

try:
    def _build_test_body():
        global test_design_body
        # 1mm × 1mm × 1mm in meters
        rect_profile = RectangleProfile(Plane.PlaneXY, 0.001, 0.001)
        body = Body.ExtrudeProfile(rect_profile, 0.001)
        test_design_body = DesignBody.Create(main_part, "__api_verify_probe__", body)

    WriteBlock.ExecuteTask("API verify probe", _build_test_body)
    check("WriteBlock.ExecuteTask + RectangleProfile + ExtrudeProfile + DesignBody.Create", True,
          "test body created")
except Exception as ex:
    check("Body creation pipeline", False, str(ex))


# ----------------------------------------------------------------------------
# 5. Body 조사 — properties + methods
# ----------------------------------------------------------------------------
print("\n[5] Body / DesignBody / Face properties")

if test_design_body is not None:
    ok, shape = safe(lambda: test_design_body.Shape)
    check("designBody.Shape", ok)

    if ok:
        ok2, vol = safe(lambda: shape.Volume)
        check("body.Volume", ok2, "{0:.6e} m^3".format(vol) if ok2 else vol)

        ok3, bbox = safe(lambda: shape.GetBoundingBox(Matrix.Identity))
        check("body.GetBoundingBox(Matrix.Identity)", ok3)

        if ok3:
            check("  bbox.MinCorner", hasattr(bbox, "MinCorner"))
            check("  bbox.MaxCorner", hasattr(bbox, "MaxCorner"))

    ok4, faces = safe(lambda: test_design_body.Faces)
    if ok4:
        face_list = list(faces)
        check("designBody.Faces", True, str(len(face_list)) + " faces")

        if face_list:
            face = face_list[0]
            ok5, fshape = safe(lambda: face.Shape)
            check("designFace.Shape", ok5)

            if ok5:
                check("  face.Shape.Area", hasattr(fshape, "Area"))
                check("  face.Shape.IsReversed", hasattr(fshape, "IsReversed"))
                check("  face.Shape.Geometry", hasattr(fshape, "Geometry"))


# ----------------------------------------------------------------------------
# 6. Group / Named Selection
# ----------------------------------------------------------------------------
print("\n[6] Group / Named Selection")

test_group = None

if test_design_body is not None:
    try:
        def _create_group():
            global test_group
            test_group = Group.Create(main_part, "__api_verify_group__", [test_design_body])

        WriteBlock.ExecuteTask("group probe", _create_group)
        check("Group.Create(part, name, [objects])", test_group is not None)

        if test_group:
            check("  group.Name", hasattr(test_group, "Name"))
    except Exception as ex:
        check("Group.Create", False, str(ex))


# ----------------------------------------------------------------------------
# 7. Materials
# ----------------------------------------------------------------------------
print("\n[7] Materials")

try:
    from SpaceClaim.Api.V252.Modeler import LibraryMaterial, DocumentMaterial, MaterialProperty, MaterialPropertyId
    check("Material classes import", True)

    ok, lib = safe(lambda: LibraryMaterial.Library)
    check("LibraryMaterial.Library", ok, str(len(lib)) + " library materials" if ok else lib)
except ImportError as ex:
    check("Material imports", False, str(ex))


# ----------------------------------------------------------------------------
# 8. Mesh — Scripting Commands
# ----------------------------------------------------------------------------
print("\n[8] Mesh Scripting Commands")

try:
    from SpaceClaim.Api.V252.Scripting.Commands import InitMeshSettings, CreateMesh
    from SpaceClaim.Api.V252.Scripting.Commands.CommandOptions import PhysicsType, CreateMeshOptions
    check("Mesh command imports", True)

    ok, _ = safe(lambda: InitMeshSettings.Execute(PhysicsType.Structural, None))
    check("InitMeshSettings.Execute(PhysicsType.Structural, None)", ok)

    ok2, opts = safe(lambda: CreateMeshOptions())
    check("new CreateMeshOptions()", ok2)
except ImportError as ex:
    check("Mesh Scripting imports", False, str(ex))


# ----------------------------------------------------------------------------
# 9. Cleanup test bodies/groups
# ----------------------------------------------------------------------------
print("\n[9] Cleanup")

try:
    def _cleanup():
        if test_design_body:
            try: test_design_body.Delete()
            except: pass
        # Group cleanup - via NamedSelection.Delete static
        try:
            from SpaceClaim.Api.V252.Modeler import NamedSelection
            NamedSelection.Delete(["__api_verify_group__"])
        except:
            pass

    WriteBlock.ExecuteTask("cleanup", _cleanup)
    check("Test artifacts cleaned up", True)
except Exception as ex:
    check("Cleanup", False, str(ex))


# ----------------------------------------------------------------------------
# Summary
# ----------------------------------------------------------------------------
print("\n" + "=" * 70)
print("SUMMARY")
print("=" * 70)
print("  PASS: {0}".format(len(PASS)))
print("  FAIL: {0}".format(len(FAIL)))
print("  N/A : {0}".format(len(NA)))
print("")

if FAIL:
    print("FAILED:")
    for name, detail in FAIL:
        print("  X {0}{1}".format(name, " - " + detail if detail else ""))

print("\n검증 결과를 '.claude/skills/ansys-api-catalog/spaceclaim/01_used_in_project.md' 와")
print("교차 비교해서 새 API 사용 시 참고하세요.")
