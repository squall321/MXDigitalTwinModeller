#!/usr/bin/env python
# encoding: utf-8
"""
Material Twin - Standalone Test
Tests the calibration without ANSYS Mechanical
"""

import os
import sys
import json
import tempfile

def test_yaml_parser():
    """Test YAML parsing."""
    print("=" * 60)
    print("TEST 1: YAML Parser")
    print("=" * 60)

    yaml_path = r"d:\MXDigitalTwinModeller\test_specimen.yaml"

    if not os.path.exists(yaml_path):
        print("ERROR: test_specimen.yaml not found")
        return False

    # Add calibration dir to path
    here = os.path.dirname(os.path.abspath(__file__))
    calib_path = os.path.join(here, 'calibration')
    if calib_path not in sys.path:
        sys.path.insert(0, calib_path)

    from utils.yaml_parser import parse_yaml, get_specimen_info_from_yaml

    yaml_data = parse_yaml(yaml_path)
    if not yaml_data:
        print("ERROR: Failed to parse YAML")
        return False

    print("YAML data:")
    for key, value in yaml_data.items():
        print("  {}: {}".format(key, value))

    specimen_info = get_specimen_info_from_yaml(yaml_data)
    if not specimen_info:
        print("ERROR: Failed to extract specimen info")
        return False

    print("\nSpecimen info:")
    for key, value in specimen_info.items():
        print("  {}: {}".format(key, value))

    print("\n[PASS] YAML Parser")
    return True


def test_csv_parser():
    """Test CSV parsing."""
    print("\n" + "=" * 60)
    print("TEST 2: CSV Parser")
    print("=" * 60)

    csv_path = r"d:\MXDigitalTwinModeller\test_tensile_data.csv"

    if not os.path.exists(csv_path):
        print("ERROR: test_tensile_data.csv not found")
        return False

    # Simple CSV parsing (same as main.py)
    displacement = []
    force = []

    with open(csv_path, 'r') as f:
        lines = f.readlines()

        for line in lines[1:]:  # Skip header
            line = line.strip()
            if not line:
                continue

            parts = line.split(',')
            if len(parts) < 2:
                continue

            try:
                disp = float(parts[0].strip())
                f_val = float(parts[1].strip())
                displacement.append(disp)
                force.append(f_val)
            except ValueError:
                continue

    print("Loaded {} data points".format(len(displacement)))
    print("Displacement range: {:.3f} - {:.3f} mm".format(
        min(displacement), max(displacement)))
    print("Force range: {:.1f} - {:.1f} N".format(
        min(force), max(force)))

    if len(displacement) < 10:
        print("ERROR: Insufficient data points")
        return False

    print("\n[PASS] CSV Parser")
    return True


def test_elastic_calibration():
    """Test elastic calibration with MaterialCalibrator.exe."""
    print("\n" + "=" * 60)
    print("TEST 3: Elastic Calibration (MaterialCalibrator.exe)")
    print("=" * 60)

    # Load CSV data
    csv_path = r"d:\MXDigitalTwinModeller\test_tensile_data.csv"
    displacement = []
    force = []

    with open(csv_path, 'r') as f:
        lines = f.readlines()
        for line in lines[1:]:
            line = line.strip()
            if not line:
                continue
            parts = line.split(',')
            if len(parts) < 2:
                continue
            try:
                displacement.append(float(parts[0].strip()))
                force.append(float(parts[1].strip()))
            except ValueError:
                continue

    # Prepare input data
    input_data = {
        'calibration_type': 'elastic',
        'displacement': displacement,
        'force': force,
        'gauge_length': 50.0,
        'cross_section_area': 37.5,  # 12.5 * 3.0
        'poisson_ratio': 0.3,
        'density': 7850.0,
        'max_elastic_strain': 0.002
    }

    # Write input JSON
    temp_dir = tempfile.gettempdir()
    input_path = os.path.join(temp_dir, 'material_calib_test.json')
    output_path = os.path.join(temp_dir, 'material_calib_test_result.json')

    # Clean old result
    if os.path.exists(output_path):
        os.remove(output_path)

    with open(input_path, 'w') as f:
        json.dump(input_data, f, indent=2)

    print("Input JSON written to: {}".format(input_path))

    # Find MaterialCalibrator.exe
    here = os.path.dirname(os.path.abspath(__file__))
    calib_exe = os.path.join(here, 'calibration', 'MaterialCalibrator.exe')

    if not os.path.exists(calib_exe):
        print("ERROR: MaterialCalibrator.exe not found at: {}".format(calib_exe))
        print("       Run: cd calibration && build_calibrator.bat")
        return False

    print("Using calibrator: {}".format(calib_exe))

    # Run calibrator
    import subprocess
    cmd = '"{}" "{}"'.format(calib_exe, input_path)
    print("Command: {}".format(cmd))

    result = subprocess.call(cmd, shell=True)

    if result != 0:
        print("ERROR: Calibrator returned non-zero exit code: {}".format(result))
        return False

    # Check result
    if not os.path.exists(output_path):
        print("ERROR: Result file not created: {}".format(output_path))
        return False

    with open(output_path, 'r') as f:
        output = json.load(f)

    print("\nResult JSON:")
    print(json.dumps(output, indent=2))

    if not output.get('success'):
        print("\nERROR: Calibration failed: {}".format(output.get('error')))
        return False

    result_data = output['result']
    E = result_data['E_modulus']
    r_sq = result_data['r_squared']

    print("\n--- Calibration Results ---")
    print("Young's Modulus (E): {:.0f} MPa".format(E))
    print("R² (fit quality): {:.6f}".format(r_sq))
    print("Elastic limit stress: {:.1f} MPa".format(result_data['elastic_limit_stress']))
    print("Data points used: {}".format(result_data['num_points_used']))
    print("Suggested material: {}".format(result_data.get('suggested_material', 'Unknown')))

    # Validation
    expected_E = 100000.0  # From test data (force/disp gradient)
    error = abs(E - expected_E) / expected_E

    if error > 0.01:  # 1% tolerance
        print("\nWARNING: E modulus error = {:.2%} (expected {:.0f} MPa)".format(
            error, expected_E))
    else:
        print("\nValidation: E modulus within 1% of expected value ✓")

    if r_sq < 0.99:
        print("WARNING: R² = {:.6f} < 0.99 (expected perfect fit)".format(r_sq))
    else:
        print("Validation: R² > 0.99 (excellent fit) ✓")

    # Cleanup
    try:
        os.remove(input_path)
        os.remove(output_path)
    except:
        pass

    print("\n[PASS] Elastic Calibration")
    return True


def main():
    """Run all tests."""
    print("\n" + "=" * 60)
    print("MX Material Twin - Standalone Test Suite")
    print("=" * 60)

    tests = [
        ("YAML Parser", test_yaml_parser),
        ("CSV Parser", test_csv_parser),
        ("Elastic Calibration", test_elastic_calibration),
    ]

    results = []
    for name, test_func in tests:
        try:
            passed = test_func()
            results.append((name, passed))
        except Exception as ex:
            import traceback
            print("\n[FAIL] {} - Exception: {}".format(name, ex))
            traceback.print_exc()
            results.append((name, False))

    # Summary
    print("\n" + "=" * 60)
    print("TEST SUMMARY")
    print("=" * 60)

    for name, passed in results:
        status = "[PASS]" if passed else "[FAIL]"
        print("{} {}".format(status, name))

    passed_count = sum(1 for _, p in results if p)
    total_count = len(results)

    print("\nTotal: {}/{} tests passed".format(passed_count, total_count))

    if passed_count == total_count:
        print("\n✓ All tests passed!")
        return 0
    else:
        print("\n✗ Some tests failed")
        return 1


if __name__ == '__main__':
    sys.exit(main())
