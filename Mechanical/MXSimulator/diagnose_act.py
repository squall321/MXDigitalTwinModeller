# encoding: utf-8
"""
ACT Extension Diagnostic Script
Run this in Mechanical Scripting Console to diagnose why extension isn't loading
"""

import sys
import os
import clr

print("=" * 70)
print("ANSYS Mechanical ACT Extension Diagnostic")
print("=" * 70)

# 1. Check ANSYS version
print("\n[1] ANSYS Version:")
try:
    print("ExtAPI Version: {}".format(ExtAPI.ExtensionManager.ProductVersion))
except Exception as e:
    print("ERROR getting version: {}".format(e))

# 2. Check extension search paths
print("\n[2] Extension Search Paths:")
try:
    import System
    app_data = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData)
    local_app_data = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData)

    print("APPDATA: {}".format(app_data))
    print("LOCALAPPDATA: {}".format(local_app_data))

    # Check both possible paths
    paths_to_check = [
        os.path.join(app_data, "ANSYS", "v252", "ACT", "extensions", "MXSimulator"),
        os.path.join(local_app_data, "ANSYS", "v252", "ACT", "extensions", "MXSimulator"),
        os.path.join(app_data, "Ansys", "v252", "ACT", "extensions", "MXSimulator"),
        os.path.join(local_app_data, "Ansys", "v252", "ACT", "extensions", "MXSimulator"),
    ]

    print("\n[3] Checking deployment paths:")
    for path in paths_to_check:
        exists = os.path.exists(path)
        print("  {} ... {}".format(path, "EXISTS" if exists else "NOT FOUND"))
        if exists:
            print("    Files:")
            for item in os.listdir(path):
                full_path = os.path.join(path, item)
                if os.path.isfile(full_path):
                    size = os.path.getsize(full_path)
                    print("      - {} ({} bytes)".format(item, size))
                else:
                    print("      - {} (directory)".format(item))

except Exception as e:
    print("ERROR: {}".format(e))

# 4. Check loaded extensions
print("\n[4] Loaded Extensions:")
try:
    ext_manager = ExtAPI.ExtensionManager
    print("Total extensions: {}".format(ext_manager.Extensions.Count))
    for ext in ext_manager.Extensions:
        print("  - {} (Version: {})".format(ext.Name, ext.Version))
except Exception as e:
    print("ERROR: {}".format(e))

# 5. Check if MXSimulator is loaded
print("\n[5] MXSimulator Extension Status:")
try:
    found = False
    for ext in ExtAPI.ExtensionManager.Extensions:
        if "MXSimulator" in ext.Name or "MX" in ext.Name:
            found = True
            print("  FOUND: {}".format(ext.Name))
            print("    Version: {}".format(ext.Version))
            print("    Location: {}".format(ext.Location if hasattr(ext, 'Location') else 'N/A'))
    if not found:
        print("  NOT LOADED")
except Exception as e:
    print("ERROR: {}".format(e))

# 6. Python environment
print("\n[6] Python Environment:")
print("Python version: {}".format(sys.version))
print("IronPython: {}".format(sys.platform))

# 7. Check ACT API availability
print("\n[7] ACT API Check:")
try:
    print("ExtAPI available: {}".format(ExtAPI is not None))
    print("ExtAPI.ExtensionManager available: {}".format(hasattr(ExtAPI, 'ExtensionManager')))
except Exception as e:
    print("ERROR: {}".format(e))

print("\n" + "=" * 70)
print("Diagnostic Complete")
print("=" * 70)
