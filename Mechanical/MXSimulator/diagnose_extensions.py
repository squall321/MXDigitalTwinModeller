# encoding: utf-8
"""
Extension 로딩 진단 스크립트
Mechanical Scripting Console에서 실행
"""

import sys
import os

print("=" * 70)
print("ANSYS Mechanical Extension 진단")
print("=" * 70)

# 1. ANSYS 버전
print("\n[1] ANSYS 버전:")
try:
    print("ExtAPI Version: {}".format(ExtAPI.ExtensionManager.ProductVersion))
except Exception as e:
    print("ERROR: {}".format(e))

# 2. Extension 경로 확인
print("\n[2] Extension 검색 경로:")
try:
    import System
    app_data = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData)
    local_app = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData)

    print("APPDATA: {}".format(app_data))
    print("LOCALAPPDATA: {}".format(local_app))

    paths = [
        os.path.join(app_data, "ANSYS", "v252", "ACT", "extensions"),
        os.path.join(local_app, "ANSYS", "v252", "ACT", "extensions"),
    ]

    for path in paths:
        if os.path.exists(path):
            print("\n경로 존재: {}".format(path))
            items = os.listdir(path)
            for item in items:
                if item.startswith("MX"):
                    full = os.path.join(path, item)
                    if os.path.isdir(full):
                        print("  [DIR]  {}".format(item))
                    else:
                        print("  [FILE] {}".format(item))
except Exception as e:
    print("ERROR: {}".format(e))

# 3. 로드된 Extensions
print("\n[3] 현재 로드된 Extensions:")
try:
    mgr = ExtAPI.ExtensionManager
    print("총 Extension 수: {}".format(mgr.Extensions.Count))

    found_mx = False
    for ext in mgr.Extensions:
        name = ext.Name
        if "MX" in name or "Rocky" in name or "LSDYNA" in name:
            print("  - {} (v{})".format(name, ext.Version))
            if "MX" in name:
                found_mx = True

    if not found_mx:
        print("\n  ⚠ MX Extension이 로드되지 않았습니다!")
        print("  모든 Extension 목록:")
        for ext in mgr.Extensions:
            print("    - {}".format(ext.Name))

except Exception as e:
    print("ERROR: {}".format(e))

# 4. Python 환경
print("\n[4] Python 환경:")
print("Python 버전: {}".format(sys.version))
print("실행 경로: {}".format(sys.executable))

# 5. ACT API
print("\n[5] ACT API 확인:")
try:
    print("ExtAPI: {}".format(ExtAPI is not None))
    print("DataModel: {}".format(ExtAPI.DataModel is not None))
    print("Application: {}".format(ExtAPI.Application is not None))
except Exception as e:
    print("ERROR: {}".format(e))

# 6. 직접 파일 확인
print("\n[6] MXSimulator 파일 직접 확인:")
try:
    import System
    app_data = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData)

    xml_path = os.path.join(app_data, "ANSYS", "v252", "ACT", "extensions", "MXSimulator.xml")
    folder_path = os.path.join(app_data, "ANSYS", "v252", "ACT", "extensions", "MXSimulator")

    print("MXSimulator.xml: {}".format("존재" if os.path.exists(xml_path) else "없음"))
    print("MXSimulator 폴더: {}".format("존재" if os.path.exists(folder_path) else "없음"))

    if os.path.exists(folder_path):
        print("\n폴더 내용:")
        for item in os.listdir(folder_path):
            print("  - {}".format(item))

    if os.path.exists(xml_path):
        print("\nXML 파일 크기: {} bytes".format(os.path.getsize(xml_path)))

except Exception as e:
    print("ERROR: {}".format(e))

print("\n" + "=" * 70)
print("진단 완료")
print("=" * 70)
