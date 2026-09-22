# encoding: utf-8
# ============================================================================
# verify_mechanical_api.py
#
# ANSYS Mechanical 안에서 직접 실행해서 카탈로그의 API 가 실제로 동작하는지 확인.
# 결과: pass/fail 표를 콘솔에 출력 + 각 카테고리별 검증 결과.
#
# 사용법:
#   1. Mechanical 실행 → File → Scripting → Open Script
#   2. 이 파일 선택 → Run
#   3. 콘솔에서 결과 확인
#
# 안전: 모델을 mutation 하지 않음. 검증용 NS/Analysis 는 즉시 제거.
# ============================================================================

print("=" * 70)
print("MX API Catalog — Mechanical ACT API Verification")
print("=" * 70)

PASS = []
FAIL = []
NA = []

def check(name, condition, detail=""):
    """결과 기록 + 콘솔 출력."""
    if condition is True:
        PASS.append((name, detail))
        print("  [PASS] {0}{1}".format(name, " — " + detail if detail else ""))
    elif condition is False:
        FAIL.append((name, detail))
        print("  [FAIL] {0}{1}".format(name, " — " + detail if detail else ""))
    else:  # None
        NA.append((name, detail))
        print("  [N/A ] {0}{1}".format(name, " — " + detail if detail else ""))

def safe(fn, *args, **kwargs):
    """함수 호출 후 (ok, result_or_error) 반환."""
    try:
        return True, fn(*args, **kwargs)
    except Exception as ex:
        return False, str(ex)


# ----------------------------------------------------------------------------
# 1. ExtAPI 글로벌
# ----------------------------------------------------------------------------
print("\n[1] ExtAPI Root")

try:
    _ = ExtAPI
    check("ExtAPI is injected", True, "type=" + str(type(ExtAPI).__name__))
except NameError:
    check("ExtAPI is injected", False, "NameError — not in Mechanical scripting context")
    raise SystemExit(1)

ok, model = safe(lambda: ExtAPI.DataModel.Project.Model)
check("ExtAPI.DataModel.Project.Model", ok, "type=" + str(type(model).__name__) if ok else model)

ok, mgr = safe(lambda: ExtAPI.SelectionManager)
check("ExtAPI.SelectionManager", ok)

ok, em = safe(lambda: ExtAPI.ExtensionManager)
check("ExtAPI.ExtensionManager", ok)

ok, prod = safe(lambda: ExtAPI.ExtensionManager.ProductVersion)
check("ExtAPI.ExtensionManager.ProductVersion", ok, str(prod) if ok else prod)


# ----------------------------------------------------------------------------
# 2. Selection
# ----------------------------------------------------------------------------
print("\n[2] Selection")

try:
    from Ansys.ACT.Interfaces.Common import SelectionTypeEnum
    check("from Ansys.ACT.Interfaces.Common import SelectionTypeEnum", True)

    ok, sel = safe(lambda: ExtAPI.SelectionManager.CreateSelectionInfo(SelectionTypeEnum.GeometryEntities))
    check("SelectionManager.CreateSelectionInfo(GeometryEntities)", ok)
except ImportError as ex:
    check("SelectionTypeEnum import", False, str(ex))


# ----------------------------------------------------------------------------
# 3. Enums
# ----------------------------------------------------------------------------
print("\n[3] Enums")

try:
    from Ansys.Mechanical.DataModel.Enums import DataModelObjectCategory
    check("DataModelObjectCategory import", True)
    check("  .Body", hasattr(DataModelObjectCategory, "Body"))
    check("  .NamedSelection", hasattr(DataModelObjectCategory, "NamedSelection"))
    check("  .Analysis", hasattr(DataModelObjectCategory, "Analysis"))
    check("  .Solution", hasattr(DataModelObjectCategory, "Solution"))
    check("  .ContactRegion", hasattr(DataModelObjectCategory, "ContactRegion"))
except ImportError as ex:
    check("DataModelObjectCategory", False, str(ex))

try:
    from Ansys.Mechanical.DataModel.Enums import GeometryDefineByType
    check("GeometryDefineByType import", True)
    check("  .GeometryEntities", hasattr(GeometryDefineByType, "GeometryEntities"))
    check("  .Worksheet", hasattr(GeometryDefineByType, "Worksheet"))
except ImportError as ex:
    check("GeometryDefineByType", False, str(ex))

try:
    from Ansys.Mechanical.DataModel.Enums import LoadDefineBy
    check("LoadDefineBy import", True)
    check("  .Components", hasattr(LoadDefineBy, "Components"))
    check("  .Vector", hasattr(LoadDefineBy, "Vector"))
except ImportError as ex:
    check("LoadDefineBy", False, str(ex))


# ----------------------------------------------------------------------------
# 4. Quantity / Units
# ----------------------------------------------------------------------------
print("\n[4] Quantity")

try:
    from Ansys.Core.Units import Quantity
    check("from Ansys.Core.Units import Quantity", True)

    ok, q1 = safe(Quantity, 2.0, "mm")
    check("Quantity(2.0, 'mm') two-arg form", ok, "value=" + str(q1.Value) if ok else q1)

    ok, q2 = safe(Quantity, "1 [Hz]")
    check("Quantity('1 [Hz]') string form", ok, "value=" + str(q2.Value) if ok else q2)

    ok, q3 = safe(Quantity, 0.3, "")
    check("Quantity(0.3, '') dimensionless", ok, "value=" + str(q3.Value) if ok else q3)
except ImportError as ex:
    check("Quantity import", False, str(ex))


# ----------------------------------------------------------------------------
# 5. Geometry / Body
# ----------------------------------------------------------------------------
print("\n[5] Geometry / Body")

ok, geo = safe(lambda: model.Geometry)
check("model.Geometry", ok)

bodies = []
if ok:
    ok2, bodies = safe(lambda: list(model.Geometry.GetChildren(DataModelObjectCategory.Body, True)))
    check("Geometry.GetChildren(Body, True)", ok2, str(len(bodies)) + " bodies" if ok2 else bodies)

if bodies:
    body = bodies[0]
    check("body.Name", hasattr(body, "Name"), str(body.Name) if hasattr(body, "Name") else "")

    ok, geo_body = safe(lambda: body.GetGeoBody())
    check("body.GetGeoBody()", ok)

    if ok and geo_body is not None:
        ok2, faces = safe(lambda: geo_body.Faces)
        check("geo_body.Faces", ok2, "count=" + str(faces.Count) if ok2 else faces)

        if ok2 and faces.Count > 0:
            face = faces[0]
            ok3, n = safe(lambda: face.NormalAtParam(0.5, 0.5))
            if ok3:
                check("face.NormalAtParam(0.5, 0.5) — CORRECT API", True,
                      "({0:.3f}, {1:.3f}, {2:.3f})".format(n.X, n.Y, n.Z))
            else:
                check("face.NormalAtParam(0.5, 0.5)", False, n)

            # Wrong API verification
            ok4, _ = safe(lambda: face.GetFaceNormal(0.5, 0.5))
            if ok4:
                check("face.GetFaceNormal — UNEXPECTED PASS (should fail!)", False, "API exists on this face object")
            else:
                check("face.GetFaceNormal — correctly fails (gotcha verified)", True, "method does not exist")

            ok5, centroid = safe(lambda: face.Centroid)
            check("face.Centroid", ok5,
                  "({0:.3f}, {1:.3f}, {2:.3f})".format(centroid.X, centroid.Y, centroid.Z) if ok5 else centroid)
else:
    check("body iteration (no model bodies loaded)", None)


# ----------------------------------------------------------------------------
# 6. NamedSelection — create + delete
# ----------------------------------------------------------------------------
print("\n[6] NamedSelection")

ok, ns = safe(lambda: model.AddNamedSelection())
check("model.AddNamedSelection()", ok)

if ok:
    try:
        ns.Name = "__api_verify_probe__"
        check("ns.Name = '...'", True)
    except Exception as ex:
        check("ns.Name", False, str(ex))

    try:
        ns.ScopingMethod = GeometryDefineByType.GeometryEntities
        check("ns.ScopingMethod = GeometryEntities", True)
    except Exception as ex:
        check("ns.ScopingMethod", False, str(ex))

    # Cleanup
    try:
        ns.Delete()
        check("ns.Delete()", True)
    except Exception as ex:
        check("ns.Delete()", False, str(ex))


# ----------------------------------------------------------------------------
# 7. Analysis — Add + Delete
# ----------------------------------------------------------------------------
print("\n[7] Analysis")

# Test on existing analyses first
ok, analyses = safe(lambda: list(model.Analyses))
check("model.Analyses (iter)", ok, str(len(analyses)) + " existing" if ok else analyses)

# AddModalAnalysis — only if no analyses exist (don't pollute existing model)
if ok and len(analyses) == 0:
    ok2, modal = safe(lambda: model.AddModalAnalysis())
    check("model.AddModalAnalysis()", ok2)

    if ok2:
        ok3, settings = safe(lambda: modal.AnalysisSettings)
        check("modal.AnalysisSettings", ok3)

        if ok3:
            try:
                settings.MaximumModesToFind = 10
                check("settings.MaximumModesToFind = 10", True)
            except Exception as ex:
                check("settings.MaximumModesToFind", False, str(ex))

            try:
                settings.ModalRangeMaximum = Quantity(1000.0, "Hz")
                check("settings.ModalRangeMaximum = Quantity(1000, 'Hz')", True)
            except Exception as ex:
                check("settings.ModalRangeMaximum", False, str(ex))

            # Wrong API
            try:
                settings.RangeMaximum = Quantity(1000.0, "Hz")
                check("settings.RangeMaximum — UNEXPECTED (should fail!)", False)
            except Exception as ex:
                check("settings.RangeMaximum — correctly fails (gotcha verified)", True, "use ModalRangeMaximum")

        # Cleanup
        try:
            modal.Delete()
            check("modal.Delete()", True)
        except Exception as ex:
            check("modal.Delete()", False, str(ex))
else:
    check("AddModalAnalysis test", None, "skipped — existing analyses present (avoid pollution)")


# ----------------------------------------------------------------------------
# 8. Mesh
# ----------------------------------------------------------------------------
print("\n[8] Mesh")

ok, mesh = safe(lambda: model.Mesh)
check("model.Mesh", ok)

if ok:
    check("  .ElementSize attribute", hasattr(mesh, "ElementSize"))
    check("  .GenerateMesh method", hasattr(mesh, "GenerateMesh"))
    check("  .AddSizing method", hasattr(mesh, "AddSizing"))
    check("  .AddAutomaticMethod method", hasattr(mesh, "AddAutomaticMethod"))
    check("  .AddInflation method", hasattr(mesh, "AddInflation"))


# ----------------------------------------------------------------------------
# 9. Materials
# ----------------------------------------------------------------------------
print("\n[9] Materials / Engineering Data")

ok, mats = safe(lambda: model.Materials)
check("model.Materials", ok)

if ok:
    try:
        import Ansys
        # Try the C# generic form (project's verified pattern)
        ok2, _ = safe(lambda: mats.GetChildren[Ansys.ACT.Automation.Mechanical.Material](True))
        check("Materials.GetChildren[Material](True) — generic-typed", ok2)
    except Exception as ex:
        check("Materials generic GetChildren", False, str(ex))


# ----------------------------------------------------------------------------
# 10. Connections / Contact
# ----------------------------------------------------------------------------
print("\n[10] Connections")

ok, conn = safe(lambda: model.Connections)
check("model.Connections", ok)

if ok:
    ok2, contacts = safe(lambda: list(conn.GetChildren(DataModelObjectCategory.ContactRegion, True)))
    check("Connections.GetChildren(ContactRegion, True)", ok2,
          str(len(contacts)) + " existing" if ok2 else contacts)


# ----------------------------------------------------------------------------
# 11. Parameters (Workbench)
# ----------------------------------------------------------------------------
print("\n[11] Parameters")

ok, params = safe(lambda: model.Parameters)
check("model.Parameters", ok)

if ok and params is not None:
    try:
        count = sum(1 for _ in params)
        check("Parameters iterable", True, str(count) + " params")
    except Exception as ex:
        check("Parameters iterable", False, str(ex))


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
        print("  ✗ {0}{1}".format(name, " — " + detail if detail else ""))

if NA:
    print("\nN/A (skipped):")
    for name, detail in NA:
        print("  - {0}{1}".format(name, " — " + detail if detail else ""))

print("\n검증 결과를 '.claude/skills/ansys-api-catalog/mechanical/01_used_in_project.md' 와")
print("교차 비교해서 새 API 사용 시 참고하세요.")
