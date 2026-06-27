# encoding: utf-8
"""
ANSYS Mechanical API 철저 검증 스크립트
사용하는 모든 API가 실제로 존재하는지 확인
"""

import sys

print("=" * 80)
print("ANSYS Mechanical API 철저 검증")
print("=" * 80)

results = {}

# =============================================================================
# 1. ExtAPI 기본 검증
# =============================================================================
print("\n[1] ExtAPI 기본 구조:")
try:
    print("ExtAPI exists: {}".format(ExtAPI is not None))
    print("ExtAPI type: {}".format(type(ExtAPI)))
    results['ExtAPI'] = True
except Exception as e:
    print("ERROR: {}".format(e))
    results['ExtAPI'] = False

# =============================================================================
# 2. Model 접근
# =============================================================================
print("\n[2] Model 접근:")
try:
    model = ExtAPI.DataModel.Project.Model
    print("Model type: {}".format(type(model)))
    print("Model name: {}".format(model.Name if hasattr(model, 'Name') else 'N/A'))
    results['Model'] = True
except Exception as e:
    print("ERROR: {}".format(e))
    results['Model'] = False

# =============================================================================
# 3. Geometry 접근 및 메서드
# =============================================================================
print("\n[3] Geometry 접근:")
try:
    geometry = model.Geometry
    print("Geometry type: {}".format(type(geometry)))

    # GetChildren 메서드 확인
    print("\nGeometry.GetChildren 확인:")
    try:
        # DataModelObjectCategory가 필요
        from Ansys.Mechanical.DataModel.Enums import DataModelObjectCategory

        bodies = geometry.GetChildren(DataModelObjectCategory.Body, True)
        print("  GetChildren(Body, True) works: {} bodies".format(bodies.Count))
        results['Geometry.GetChildren'] = True
    except Exception as e2:
        print("  ERROR: {}".format(e2))
        results['Geometry.GetChildren'] = False

    results['Geometry'] = True
except Exception as e:
    print("ERROR: {}".format(e))
    results['Geometry'] = False

# =============================================================================
# 4. Body 및 GeoBody 접근
# =============================================================================
print("\n[4] Body 및 GeoBody:")
if results.get('Geometry.GetChildren'):
    try:
        bodies = geometry.GetChildren(DataModelObjectCategory.Body, True)
        if bodies.Count > 0:
            test_body = bodies[0]
            print("Test body: {}".format(test_body.Name if hasattr(test_body, 'Name') else 'N/A'))

            # GetGeoBody 메서드
            print("\nBody.GetGeoBody():")
            try:
                geo_body = test_body.GetGeoBody()
                print("  GetGeoBody() works")
                print("  GeoBody type: {}".format(type(geo_body)))
                results['Body.GetGeoBody'] = True

                # Faces 접근
                print("\nGeoBody.Faces:")
                try:
                    faces = geo_body.Faces
                    print("  Faces.Count: {}".format(faces.Count))
                    results['GeoBody.Faces'] = True

                    # 첫 번째 면의 법선
                    if faces.Count > 0:
                        print("\nFace.GetFaceNormal(u, v):")
                        try:
                            face = faces[0]
                            normal = face.GetFaceNormal(0.5, 0.5)
                            print("  Normal: ({}, {}, {})".format(normal.X, normal.Y, normal.Z))
                            results['Face.GetFaceNormal'] = True
                        except Exception as e5:
                            print("  ERROR: {}".format(e5))
                            results['Face.GetFaceNormal'] = False

                except Exception as e4:
                    print("  ERROR: {}".format(e4))
                    results['GeoBody.Faces'] = False

            except Exception as e3:
                print("  ERROR: {}".format(e3))
                results['Body.GetGeoBody'] = False
        else:
            print("No bodies in model - cannot test")
            results['Body.GetGeoBody'] = 'N/A (no bodies)'
    except Exception as e:
        print("ERROR: {}".format(e))

# =============================================================================
# 5. Named Selection
# =============================================================================
print("\n[5] Named Selection:")
try:
    # AddNamedSelection 메서드
    print("Model.AddNamedSelection():")
    try:
        ns = model.AddNamedSelection()
        print("  AddNamedSelection() works")
        print("  NS type: {}".format(type(ns)))

        # 속성 확인
        print("  NS.Name settable: {}".format(hasattr(ns, 'Name')))
        print("  NS.ScopingMethod: {}".format(hasattr(ns, 'ScopingMethod')))
        print("  NS.Location: {}".format(hasattr(ns, 'Location')))

        # 삭제
        ns.Delete()
        print("  Test NS deleted")

        results['Model.AddNamedSelection'] = True
    except Exception as e2:
        print("  ERROR: {}".format(e2))
        results['Model.AddNamedSelection'] = False

except Exception as e:
    print("ERROR: {}".format(e))

# =============================================================================
# 6. SelectionManager
# =============================================================================
print("\n[6] SelectionManager:")
try:
    sel_mgr = ExtAPI.SelectionManager
    print("SelectionManager type: {}".format(type(sel_mgr)))

    print("\nCreateSelectionInfo:")
    try:
        from Ansys.ACT.Interfaces.Common import SelectionTypeEnum
        selection = sel_mgr.CreateSelectionInfo(SelectionTypeEnum.GeometryEntities)
        print("  CreateSelectionInfo works")
        print("  Selection type: {}".format(type(selection)))
        results['SelectionManager.CreateSelectionInfo'] = True
    except Exception as e2:
        print("  ERROR: {}".format(e2))
        results['SelectionManager.CreateSelectionInfo'] = False

except Exception as e:
    print("ERROR: {}".format(e))

# =============================================================================
# 7. Mesh
# =============================================================================
print("\n[7] Mesh:")
try:
    mesh = model.Mesh
    print("Mesh type: {}".format(type(mesh)))

    # 속성 확인
    print("\nMesh 속성:")
    print("  ElementSize: {}".format(hasattr(mesh, 'ElementSize')))
    print("  GenerateMesh: {}".format(hasattr(mesh, 'GenerateMesh')))
    print("  AddSizing: {}".format(hasattr(mesh, 'AddSizing')))
    print("  Nodes: {}".format(hasattr(mesh, 'Nodes')))
    print("  Elements: {}".format(hasattr(mesh, 'Elements')))

    results['Mesh'] = True
except Exception as e:
    print("ERROR: {}".format(e))
    results['Mesh'] = False

# =============================================================================
# 8. Analysis
# =============================================================================
print("\n[8] Analysis:")
try:
    analyses = model.Analyses
    print("Analyses type: {}".format(type(analyses)))
    print("Analyses count: {}".format(analyses.Count))

    # 메서드 확인
    print("\nAnalysis 생성 메서드:")
    print("  AddModalAnalysis: {}".format(hasattr(analyses, 'AddModalAnalysis')))
    print("  AddTransientStructuralAnalysis: {}".format(hasattr(analyses, 'AddTransientStructuralAnalysis')))

    results['Analyses'] = True

    # 실제 생성 테스트 (삭제할 것이므로 주의)
    if analyses.Count == 0:
        print("\n테스트 Analysis 생성:")
        try:
            test_modal = analyses.AddModalAnalysis()
            print("  Modal Analysis created")
            print("  Type: {}".format(type(test_modal)))
            print("  Has AnalysisSettings: {}".format(hasattr(test_modal, 'AnalysisSettings')))

            # 삭제
            test_modal.Delete()
            print("  Test analysis deleted")

            results['AddModalAnalysis'] = True
        except Exception as e2:
            print("  ERROR: {}".format(e2))
            results['AddModalAnalysis'] = False

except Exception as e:
    print("ERROR: {}".format(e))
    results['Analyses'] = False

# =============================================================================
# 9. Quantity (Units)
# =============================================================================
print("\n[9] Quantity (Units):")
try:
    from Ansys.Core.Units import Quantity

    test_qty = Quantity(2.0, "mm")
    print("Quantity created: {} {}".format(test_qty.Value, test_qty.Unit))
    results['Quantity'] = True
except Exception as e:
    print("ERROR: {}".format(e))
    results['Quantity'] = False

# =============================================================================
# 요약
# =============================================================================
print("\n" + "=" * 80)
print("검증 결과 요약:")
print("=" * 80)

for api, status in sorted(results.items()):
    status_str = "✓ OK" if status == True else ("✗ FAIL" if status == False else str(status))
    print("{:40s} : {}".format(api, status_str))

print("\n" + "=" * 80)
print("검증 완료")
print("=" * 80)
