# encoding: utf-8
"""
ANSYS Mechanical ACT Python Environment Tester

Mechanical Scripting Console에서 실행:
  File → Scripting → Open Script → test_python_env.py → Run
"""

import sys

print("=" * 60)
print("Python Environment Information")
print("=" * 60)

# 1. Python version
print("\n[1] Python Version:")
print("  ", sys.version)
print("  Version info:", sys.version_info)

# 2. Python implementation
print("\n[2] Implementation:")
try:
    import platform
    print("  ", platform.python_implementation())
except:
    print("   platform module not available")

# 3. Check if IronPython
print("\n[3] IronPython Check:")
try:
    import clr
    print("   clr module: Available (likely IronPython or Python.NET)")
    print("   clr.FullName:", clr.FullName if hasattr(clr, 'FullName') else 'N/A')
except:
    print("   clr module: Not available (CPython)")

# 4. Check sys.platform
print("\n[4] Platform:")
print("   sys.platform:", sys.platform)

# 5. Executable path
print("\n[5] Executable:")
print("   sys.executable:", sys.executable)

# 6. Can we import numpy/scipy?
print("\n[6] Scientific Libraries Test:")
try:
    import numpy
    print("   ✓ numpy:", numpy.__version__)
except Exception as ex:
    print("   ✗ numpy:", str(ex))

try:
    import scipy
    print("   ✓ scipy:", scipy.__version__)
except Exception as ex:
    print("   ✗ scipy:", str(ex))

# 7. Can we add venv to sys.path and import?
print("\n[7] Venv Module Import Test:")
try:
    import os
    script_dir = os.path.dirname(__file__)
    venv_site = os.path.join(script_dir, 'calibration_env', 'Lib', 'site-packages')

    print("   Venv site-packages:", venv_site)
    print("   Exists:", os.path.exists(venv_site))

    if os.path.exists(venv_site):
        # Try adding to sys.path
        if venv_site not in sys.path:
            sys.path.insert(0, venv_site)

        print("   Added to sys.path")

        # Try importing scipy from venv
        try:
            import scipy as scipy_venv
            print("   ✓ scipy from venv:", scipy_venv.__version__)
            print("   scipy location:", scipy_venv.__file__)
        except Exception as ex:
            print("   ✗ scipy from venv:", str(ex))
except Exception as ex:
    print("   ERROR:", str(ex))

print("\n" + "=" * 60)
print("Test Complete")
print("=" * 60)
