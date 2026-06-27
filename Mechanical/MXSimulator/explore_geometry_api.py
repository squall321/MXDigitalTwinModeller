# encoding: utf-8
"""
ANSYS Mechanical Geometry Import API 탐색
Scripting Console에서 실행하여 실제 사용 가능한 API 확인
"""

print("=" * 70)
print("ANSYS Mechanical Geometry Import API 탐색")
print("=" * 70)

# 1. Model 객체 확인
print("\n[1] Model 객체:")
model = ExtAPI.DataModel.Project.Model
print("Model type: {}".format(type(model)))

# 2. Model의 속성/메서드 탐색
print("\n[2] Model 속성 중 'Geometry' 또는 'Import' 관련:")
for attr in dir(model):
    if 'Geometry' in attr or 'Import' in attr or 'import' in attr.lower():
        print("  - {}".format(attr))
        try:
            obj = getattr(model, attr)
            print("    Type: {}".format(type(obj)))
        except:
            pass

# 3. Geometry 객체 확인
print("\n[3] Geometry 객체:")
try:
    geometry = model.Geometry
    print("Geometry type: {}".format(type(geometry)))

    print("\n[4] Geometry의 메서드/속성:")
    for attr in dir(geometry):
        if not attr.startswith('_'):
            print("  - {}".format(attr))

except Exception as e:
    print("ERROR: {}".format(e))

# 5. GeometryImportGroup 확인
print("\n[5] GeometryImportGroup 확인:")
try:
    import_group = model.GeometryImportGroup
    print("GeometryImportGroup type: {}".format(type(import_group)))

    print("\n[6] GeometryImportGroup의 메서드:")
    for attr in dir(import_group):
        if 'Add' in attr or 'Import' in attr or 'Create' in attr:
            print("  - {}".format(attr))

except AttributeError as e:
    print("GeometryImportGroup 없음: {}".format(e))

# 6. 직접 Import 관련 클래스 탐색
print("\n[7] Ansys.ACT.Mechanical.Utilities 모듈 탐색:")
try:
    import Ansys.ACT.Mechanical.Utilities as Utils

    print("사용 가능한 클래스/함수:")
    for attr in dir(Utils):
        if 'Import' in attr or 'Geometry' in attr:
            print("  - {}".format(attr))
            try:
                obj = getattr(Utils, attr)
                print("    Type: {}".format(type(obj)))

                # 클래스면 메서드 확인
                if hasattr(obj, '__dict__'):
                    for method in dir(obj):
                        if 'Import' in method or 'import' in method.lower():
                            print("      Method: {}".format(method))
            except:
                pass

except Exception as e:
    print("ERROR: {}".format(e))

# 7. 대안: Workbench Scripting 확인
print("\n[8] ExtAPI 메서드 중 Import 관련:")
for attr in dir(ExtAPI):
    if 'Import' in attr or 'import' in attr.lower():
        print("  - {}".format(attr))

# 8. 실제 작동하는 예제 찾기
print("\n[9] 실제 STEP 임포트 방법 시도:")
print("\n방법 1: File → Import 메뉴 자동화 불가능 (GUI only)")
print("방법 2: AGDocExternalData 사용 가능성")

try:
    # AGDocExternalData를 통한 임포트 시도
    print("\nExtAPI.Application.ScriptByName 확인:")
    print("  Available: {}".format(hasattr(ExtAPI.Application, 'ScriptByName')))

except Exception as e:
    print("ERROR: {}".format(e))

print("\n" + "=" * 70)
print("탐색 완료")
print("=" * 70)
print("\n위 결과를 보고 실제 사용 가능한 API를 확인하세요.")
