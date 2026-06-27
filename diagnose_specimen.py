#!/usr/bin/env python
# encoding: utf-8
"""
Specimen Detection 자동 진단 스크립트
SpaceClaim YAML 생성 여부 및 Mechanical 인식 확인
"""

import os
import sys
import glob

def check_yaml_files():
    """생성된 specimen.yaml 파일 찾기"""
    print("=" * 60)
    print("1. Searching for specimen.yaml files...")
    print("=" * 60)

    # 일반적인 경로들 확인
    search_paths = [
        "D:/",
        "C:/Users/Sonic/Documents",
        "C:/Users/Sonic/AppData/Local/Temp",
        "D:/TestProjects",
    ]

    found_files = []
    for base_path in search_paths:
        if os.path.exists(base_path):
            pattern = os.path.join(base_path, "**", "specimen.yaml")
            files = glob.glob(pattern, recursive=True)
            found_files.extend(files)

    if found_files:
        print(f"\n[OK] Found {len(found_files)} specimen.yaml file(s):")
        for f in found_files:
            print(f"  - {f}")
            # 파일 내용 미리보기
            try:
                with open(f, 'r', encoding='utf-8') as file:
                    lines = file.readlines()[:5]
                    print("    Preview:")
                    for line in lines:
                        print(f"      {line.rstrip()}")
                print()
            except Exception as ex:
                print(f"    Error reading file: {ex}\n")
    else:
        print("\n[WARNING] No specimen.yaml files found.")
        print("Possible reasons:")
        print("  1. SpaceClaim document was not saved before creating specimen")
        print("  2. Specimen was not created yet")
        print("  3. Files are in different location")

    return found_files


def check_yaml_parser():
    """YAML parser 동작 확인"""
    print("\n" + "=" * 60)
    print("2. Testing YAML parser...")
    print("=" * 60)

    # calibration 경로 추가
    here = os.path.dirname(os.path.abspath(__file__))
    calib_path = os.path.join(here, 'Mechanical', 'MXSimulator', 'calibration')

    if calib_path not in sys.path:
        sys.path.insert(0, calib_path)

    try:
        from utils.yaml_parser import parse_yaml, get_specimen_info_from_yaml
        print("[OK] YAML parser modules imported successfully")

        # 테스트 YAML 파일이 있으면 파싱 테스트
        test_yaml = os.path.join(here, 'test_specimen.yaml')
        if os.path.exists(test_yaml):
            print(f"\nTesting with: {test_yaml}")

            yaml_data = parse_yaml(test_yaml)
            print("\nParsed YAML data:")
            for key, value in yaml_data.items():
                print(f"  {key}: {value}")

            specimen_info = get_specimen_info_from_yaml(yaml_data)
            if specimen_info:
                print("\nExtracted specimen info:")
                for key, value in specimen_info.items():
                    print(f"  {key}: {value}")
                print("\n[PASS] YAML parser works correctly")
            else:
                print("\n[FAIL] Failed to extract specimen info")
        else:
            print(f"\n[INFO] Test YAML not found: {test_yaml}")
            print("      (This is OK - just a test file)")

        return True

    except ImportError as ex:
        print(f"[FAIL] Failed to import YAML parser: {ex}")
        return False
    except Exception as ex:
        print(f"[FAIL] YAML parser error: {ex}")
        import traceback
        traceback.print_exc()
        return False


def check_spaceclaim_code():
    """SpaceClaim 코드에서 YAML 생성 구현 확인"""
    print("\n" + "=" * 60)
    print("3. Checking SpaceClaim YAML generation code...")
    print("=" * 60)

    service_file = os.path.join(
        os.path.dirname(os.path.abspath(__file__)),
        'Services', 'TensileTest', 'SpecimenMetadataService.cs'
    )

    if os.path.exists(service_file):
        print(f"[OK] SpecimenMetadataService.cs found")

        # SaveMetadata 메서드 존재 확인
        with open(service_file, 'r', encoding='utf-8') as f:
            content = f.read()

        if 'public static void SaveMetadata' in content:
            print("[OK] SaveMetadata method exists")
        else:
            print("[FAIL] SaveMetadata method not found")
            return False

        if 'specimen.yaml' in content or 'MetadataFileName' in content:
            print("[OK] YAML file generation code exists")
        else:
            print("[WARNING] YAML file name reference not found")

        # Document 저장 체크 코드 확인
        if 'document.Path' in content and 'IsNullOrEmpty' in content:
            print("[OK] Document save check implemented")
            print("     → YAML is only created when Document is saved!")
        else:
            print("[WARNING] Document save check not found")

        return True
    else:
        print(f"[FAIL] SpecimenMetadataService.cs not found: {service_file}")
        return False


def check_mechanical_detection():
    """Mechanical detection 코드 확인"""
    print("\n" + "=" * 60)
    print("4. Checking Mechanical detection code...")
    print("=" * 60)

    main_file = os.path.join(
        os.path.dirname(os.path.abspath(__file__)),
        'Mechanical', 'MXSimulator', 'main.py'
    )

    if os.path.exists(main_file):
        print(f"[OK] main.py found")

        with open(main_file, 'r', encoding='utf-8') as f:
            content = f.read()

        # Detection 메서드들 확인
        checks = [
            ('detect_specimen_from_yaml', 'SpaceClaim YAML detection'),
            ('detect_specimen_from_workbench', 'Workbench Parameters detection'),
            ('detect_specimen_from_json', 'JSON file detection'),
        ]

        all_ok = True
        for method_name, description in checks:
            if f'def {method_name}' in content:
                print(f"[OK] {description} implemented")
            else:
                print(f"[FAIL] {description} NOT implemented")
                all_ok = False

        # yaml_parser import 확인
        if 'from utils.yaml_parser import' in content:
            print("[OK] yaml_parser import found")
        else:
            print("[WARNING] yaml_parser import not found")

        return all_ok
    else:
        print(f"[FAIL] main.py not found: {main_file}")
        return False


def print_solution():
    """해결 방법 출력"""
    print("\n" + "=" * 60)
    print("SOLUTION: How to make specimen detection work")
    print("=" * 60)

    print("""
SpaceClaim에서 시편 생성 시 specimen.yaml이 자동 생성되지만,
**Document가 저장되어 있어야만** 생성됩니다!

해결 방법:

Option 1: SpaceClaim에서 Document 저장 후 시편 생성 (권장)
  1. SpaceClaim 실행
  2. File → Save As → d:/test_specimen.scdoc
  3. 인장시편 버튼 → ASTM E8 Standard → 생성
  4. Ctrl+S (저장)
  5. 확인: ls d:/specimen.yaml

Option 2: Workbench 통합 (자동)
  1. Workbench → SpaceClaim Component 생성
  2. SpaceClaim에서 시편 생성
  3. Ctrl+S (저장!)
  4. Update → Mechanical Component 연결
  5. Material Twin → Detect Specimen ✓

Option 3: 수동 Workbench Parameters (Fallback)
  1. Workbench → Parameters 탭
  2. New Input Parameter:
     - P1_GaugeLength = 50 mm
     - P2_GaugeWidth = 12.5 mm
     - P3_Thickness = 3.0 mm
     - P4_SpecimenType = "ASTM E8"
  3. Mechanical → Material Twin → Detect Specimen ✓

자세한 내용: diagnose_specimen_detection.md 참조
""")


def main():
    """메인 진단 루틴"""
    print("\n" + "=" * 60)
    print("Specimen Detection Diagnostic Tool")
    print("=" * 60)
    print("\nThis tool checks:")
    print("  1. SpaceClaim YAML file generation")
    print("  2. YAML parser implementation")
    print("  3. Mechanical detection code")
    print()

    results = []

    # 1. YAML 파일 찾기
    found_yamls = check_yaml_files()
    results.append(("YAML files found", len(found_yamls) > 0))

    # 2. YAML parser 테스트
    parser_ok = check_yaml_parser()
    results.append(("YAML parser", parser_ok))

    # 3. SpaceClaim 코드 확인
    spaceclaim_ok = check_spaceclaim_code()
    results.append(("SpaceClaim YAML generation", spaceclaim_ok))

    # 4. Mechanical detection 코드 확인
    mechanical_ok = check_mechanical_detection()
    results.append(("Mechanical detection", mechanical_ok))

    # Summary
    print("\n" + "=" * 60)
    print("DIAGNOSTIC SUMMARY")
    print("=" * 60)

    for name, passed in results:
        status = "[OK]" if passed else "[FAIL]"
        print(f"{status} {name}")

    # Overall status
    all_passed = all(p for _, p in results)

    if all_passed and len(found_yamls) > 0:
        print("\n✓ All checks passed!")
        print("  If detection still doesn't work, check:")
        print("  1. SpaceClaim document is saved")
        print("  2. Mechanical is reading from correct geometry path")
    else:
        print("\n✗ Some checks failed or no YAML files found")
        print_solution()

    return 0 if all_passed else 1


if __name__ == '__main__':
    sys.exit(main())
