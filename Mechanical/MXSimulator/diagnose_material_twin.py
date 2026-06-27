# encoding: utf-8
"""
Material Twin Diagnostic Script

Mechanical Scripting Console에서 실행:
  File → Scripting → Open Script → diagnose_material_twin.py → Run
"""

import os
import sys

print("=" * 60)
print("Material Twin Diagnostics")
print("=" * 60)

# 1. Check script location
script_dir = os.path.dirname(__file__)
print("\n[1] Script Location:")
print("  __file__ =", __file__)
print("  dirname  =", script_dir)

# 2. Check calibration module path
calib_path = os.path.join(script_dir, 'calibration')
print("\n[2] Calibration Module:")
print("  Path:", calib_path)
print("  Exists:", os.path.exists(calib_path))
if os.path.exists(calib_path):
    print("  Contents:", os.listdir(calib_path))

# 3. Check venv
venv_path = os.path.join(script_dir, 'calibration_env')
print("\n[3] Virtual Environment:")
print("  Path:", venv_path)
print("  Exists:", os.path.exists(venv_path))
if os.path.exists(venv_path):
    python_exe = os.path.join(venv_path, 'Scripts', 'python.exe')
    print("  Python.exe:", os.path.exists(python_exe))

# 4. Try importing calibration modules
print("\n[4] Module Import Test:")
try:
    if calib_path not in sys.path:
        sys.path.insert(0, calib_path)

    print("  Attempting: from utils.yaml_parser import parse_yaml")
    from utils.yaml_parser import parse_yaml
    print("    ✓ SUCCESS")

    print("  Attempting: from utils.csv_parser import parse_tensile_csv")
    from utils.csv_parser import parse_tensile_csv
    print("    ✓ SUCCESS")

    print("  Attempting: from elastic_calibrator import calibrate_elastic")
    from elastic_calibrator import calibrate_elastic
    print("    ✓ SUCCESS")

except Exception as ex:
    print("    ✗ FAILED:", str(ex))
    import traceback
    traceback.print_exc()

# 5. Check Geometry source
print("\n[5] Geometry Detection Test:")
try:
    geometry = ExtAPI.DataModel.Project.Model.Geometry
    if geometry:
        print("  Geometry object: EXISTS")

        # Try different attributes
        attrs = ['Source', 'SourceFile', 'DefinitionFile']
        for attr in attrs:
            if hasattr(geometry, attr):
                val = getattr(geometry, attr)
                print(f"  geometry.{attr} = {val}")
                if val and os.path.exists(str(val)):
                    print(f"    → File exists: YES")
                    yaml_dir = os.path.dirname(str(val))
                    yaml_path = os.path.join(yaml_dir, 'specimen.yaml')
                    print(f"    → specimen.yaml: {os.path.exists(yaml_path)}")
            else:
                print(f"  geometry.{attr} = NOT FOUND")

        # List all attributes
        print("\n  All Geometry attributes:")
        for attr in dir(geometry):
            if not attr.startswith('_'):
                print(f"    - {attr}")
    else:
        print("  Geometry object: NULL")

except Exception as ex:
    print("  ERROR:", str(ex))
    import traceback
    traceback.print_exc()

# 6. Check Parameters
print("\n[6] Workbench Parameters Test:")
try:
    params = ExtAPI.DataModel.Project.Model.Parameters
    if params:
        param_count = len([p for p in params])
        print(f"  Parameter count: {param_count}")

        if param_count > 0:
            print("  Parameter names:")
            for p in params:
                print(f"    - {p.Name}")
    else:
        print("  No parameters found")

except Exception as ex:
    print("  ERROR:", str(ex))

# 7. Test CSV parsing
print("\n[7] CSV Parser Test:")
try:
    test_csv = os.path.join(script_dir, '..', '..', 'Examples', 'Steel_ASTM_E8_Elastic.csv')
    print(f"  Test file: {test_csv}")
    print(f"  Exists: {os.path.exists(test_csv)}")

    if os.path.exists(test_csv):
        from utils.csv_parser import parse_tensile_csv
        disp, force = parse_tensile_csv(test_csv)
        print(f"  ✓ Parsed: {len(disp)} points")
        print(f"    Displacement: {min(disp):.6f} ~ {max(disp):.6f} mm")
        print(f"    Force: {min(force):.2f} ~ {max(force):.2f} N")

except Exception as ex:
    print(f"  ✗ FAILED: {str(ex)}")
    import traceback
    traceback.print_exc()

print("\n" + "=" * 60)
print("Diagnostic Complete")
print("=" * 60)
