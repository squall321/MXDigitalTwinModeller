# encoding: utf-8
"""
Cap Vibration - Direct Script Execution
ANSYS Mechanical Scripting Console에서 직접 실행
"""

import sys
import os

# 현재 스크립트 디렉토리 가져오기
script_dir = os.path.dirname(__file__)
if not script_dir:
    script_dir = os.path.dirname(os.path.abspath(__file__))

# main.py 경로
main_py_path = os.path.join(script_dir, "main.py")

print("=" * 60)
print("Cap Vibration Time Force Setup - Phase 1")
print("=" * 60)
print("Script directory: " + script_dir)
print("main.py path: " + main_py_path)
print("main.py exists: " + str(os.path.exists(main_py_path)))
print("=" * 60)

try:
    # main.py를 직접 실행 (execfile)
    if os.path.exists(main_py_path):
        # execfile로 main.py의 모든 함수와 클래스를 현재 네임스페이스에 로드
        execfile(main_py_path)

        # show_cap_vibration_dialog 함수 호출
        show_cap_vibration_dialog(None)
    else:
        print("\nERROR: main.py not found!")
        print("Expected location: " + main_py_path)
        print("\nPlease check if the file exists at this location.")

except Exception as ex:
    import traceback
    print("\n" + "=" * 60)
    print("ERROR:")
    print("=" * 60)
    print(str(ex))
    print("-" * 60)
    print("Traceback:")
    print(traceback.format_exc())
    print("=" * 60)
