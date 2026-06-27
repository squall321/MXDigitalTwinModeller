# encoding: utf-8
# _warm.py — trivial warm-up: load the add-in DLL + SC modeling stack so the OS
# file cache is hot before the matrix runs (eliminates the cold-launch first-cell
# timeouts that caused the ±3 MISSING-cell non-determinism). Writes a done marker.
import os
ADDIN_DLL = r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll"
DONE = r"D:\MXDigitalTwinModeller\Test\RealCAD\warm_done.txt"
try:
    import clr
    clr.AddReferenceToFileAndPath(ADDIN_DLL)
    from SpaceClaim.Api.V252 import Document, Window
    from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import ModificationService  # noqa
    try:
        if Window.ActiveWindow is None: Document.Create()
    except Exception: pass
    from System.IO import File
    from System.Text import UTF8Encoding
    File.WriteAllText(DONE, "warm\n", UTF8Encoding(False))
except Exception:
    try:
        from System.IO import File
        from System.Text import UTF8Encoding
        File.WriteAllText(DONE, "warm-exc\n", UTF8Encoding(False))
    except Exception: pass
