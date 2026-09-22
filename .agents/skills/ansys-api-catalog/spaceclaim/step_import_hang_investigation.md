# STEP Import Hang Investigation — SpaceClaim 2025 R2 (Ansys Student)

Date: 2026-06-03
Context: `StepRoundTripRunner.Format="stp"` triggers `Document.Open(*.stp, null)`
in our `/RunScript`-driven launcher (`Test\RE_SelfTest\run_headless.ps1`).
Symptom: the call blocks indefinitely waiting on a modal SC translator dialog
that never receives focus because the run is wrapped (no /Headless flag — SC
25.2 Student doesn't support /Headless; we just don't click anything).

Conclusion (TL;DR): **no working programmatic suppression was found**.
`Format` default stays `"scdocx"` (5/5 PASS, 79/79 preserved). `StepRoundTripRunner.cs`
was NOT modified.

---

## Host environment (verified)

- Launcher: `D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.exe`
  (FileVersion `2025.2.51200.39553`, OriginalFilename `SpaceClaim.exe`).
- Headless host: `D:\Program Files\ANSYS Inc\ANSYS Student\v252\Discovery\Discovery.exe`
  (FileVersion `25.2.2557.29619`) — NOT used by self-test wrapper.
- Self-test launches `scdm\SpaceClaim.exe /RunScript=...` (no `/Headless`,
  no `/Exit`), polls `__done__`, then force-kills SC.

---

## PATH A — User options / registry

### A.1 HKCU\Software\SpaceClaim
All v25.2 entries are empty placeholders. Only meaningful subkey:
`HKCU\Software\SpaceClaim\DiscoveryEdition25.2` → property `Edition = "DiscoverySpaceClaim25.2"`. No `AskBeforeImport`, no `PromptOnTranslate`, no `AP203Default`.
**Negative.**

### A.2 %APPDATA%\SpaceClaim\DiscoverySpaceClaim25.2\user.config
Searched for `(?i)ask|prompt|override|interactive|dialog`.
Step section (line 1090–1097):
```xml
<Step>
  <ExportVersion>V214</ExportVersion>
  <ImportStepMethod>UseFacewiseConnections</ImportStepMethod>
  <OverrideUnits>false</OverrideUnits>
  <NewUnits>MM</NewUnits>
  <ExportFaceAndEdgeObjectIds>true</ExportFaceAndEdgeObjectIds>
  <ImportPmiData>false</ImportPmiData>
</Step>
```
There is NO `AskBeforeImport`/`PromptOnTranslate`/`AP203Default` key — neither
absent (already-default-suppressed) nor controllable. `CheckGeometryOnImport`
exists (line 433) but is unrelated to dialog suppression.
**Negative.**

---

## PATH B — Command-line args

### B.1 `/?` on `scdm\SpaceClaim.exe`
Process launched in the background did not exit; never returned help text
within 60 s. `SpaceClaim.exe` does NOT print Usage in this build.

### B.2 Binary-string scan of `scdm\SpaceClaim.exe`
Searched Unicode + ASCII for `Headless`, `/Quiet`, `/Silent`, `/Batch`,
`/NoDialog`, `/RunScript`, `/Exit`, `/AutoExit`, `AskBeforeImport`,
`NonInteractive`, `SuppressDialog`. Only `Headless` matched — as a debug
log fragment (`Entering ShouldShowQAForm: ..., FromAPI: ..., Headless:`),
not a CLI token. The remaining flags are NOT present as recognized switches.

This matches the comment in our launcher (`run_headless.ps1` line 5):
> `/Headless` 옵션은 SC 25.2 Discovery/Student 에서 미지원 → 빼야 모달 안 뜸.

The `/RunScript=path` and `/Exit` tokens ARE recognized (used in production)
but offer no per-import dialog suppression.
**Negative.**

---

## PATH C — API ImportOptions / StepImportOptions

### C.1 `SpaceClaim.Api.V252.xml`, type `T:SpaceClaim.Api.V252.ImportOptions`
(lines 23140–23346) lists the FULL property surface:

```
CreateExternalAssembly, UseMatchingDocuments, Completeness, CleanUpBodies,
StitchSurfaces, StitchTolerance, ImportCurves, ImportPoints, ImportNames,
ImportAsLightweight, ImportPlanes, ImportAxes, ImportCoordinateSystems,
ImportComponentProperties, ImportHiddenComponentsAndGeometry,
ImproveImportedData, HealBodies, ImportMaterials, ImportEmptyComponents,
LargeImportFileSize, LargeImportEmptyComponents, LargeImportMinimumSize,
Acis, Catia, Stl, Obj, Workbench
```

There is NO `Step` sub-options property. The set covers Acis/Catia/Stl/Obj
only. No `SuppressDialogs` / `Quiet` / `Interactive` / `ProtocolDefault`.

### C.2 `StepExportOptions` (line 15530)
Only one property: `ExportIdentifiers` (bool). No protocol selector (AP203/214/242).

### C.3 `Document.Open` overloads
Five overloads — all take either `null`, `ImportOptions`, `WorkbenchSettings`,
`IFileSettings`, or a config-file path. The `configFilePath` overload
(`DocumentOpen.Execute(System.String, System.String)`) is described as
`"path to a settings config file"` but the XML gives no schema or example.
This is the one untested lever — but its schema is undocumented and writing
it speculatively risks worse failure modes than the current `null`-options
path.

**Negative for clean API suppression.** A config-file probe could be tried in
a future cycle but is not a high-confidence fix.

---

## PATH D — Pre-dismiss the dialog (Win32 / SendKeys)

Architecturally feasible: spawn a polling thread before `Document.Open`,
`FindWindow` for the SC translator dialog title, send `WM_KEYDOWN(VK_RETURN)`
or `WM_CLOSE`. However:

1. The dialog title in 25.2 is unknown — we have no fresh repro that lets us
   capture it (the call hangs before we can attach an inspector, and the
   wrapper force-kills SC at timeout).
2. The dialog is raised on SC's UI thread, which is the SAME thread our
   `WriteBlock` / `Document.Open` runs on. SendKeys from inside the add-in
   would deadlock — we'd need an external watchdog process.
3. External watchdog needs to be plumbed through `run_headless.ps1`, target
   one process by PID, and survive the existing force-kill timing — non-trivial.
4. Even if dismissed once, the dialog re-appears per `Document.Open` call
   (5 specs in `SpecFilter`), multiplying flakiness.

**Not pursued. Documented as a fallback only.** Decision: cost > benefit until
PATH A/C produces a registry-level "don't ask again" flag we can pre-seed
into `user.config`. We don't even know what option that would be.

---

## PATH E — Standalone `SpaceClaimIopVisTranslator.exe`

Located: `D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaimIopVisTranslator.exe` (271 KB,
FileDescription `SpaceClaimIopVisTranslator`).

Binary string scan reveals C++ exports: `SPAIConverter::Convert`, `SPAIFile`,
`SaveFacetBodyFile`, `SaveFacetXamlFile`, `ScIopVisTranslatorExitCode`.

This is the **IOP→VIS facet writer** used by Workbench Discovery viewer
(produces facet XAML / facet body files for visualization-only consumption).
It does NOT round-trip STEP to a SpaceClaim-readable BREP document. Useless
for the round-trip test which needs `extractor.Extract(DesignBody)`.

Direct invocation: `& SpaceClaimIopVisTranslator.exe /?` fails with
`STATUS_DLL_NOT_FOUND` (0xC0000135) — the binary expects to be loaded by the
SC host with its dependent DLLs already in-process. Confirmed it is NOT a
standalone CLI.

**Negative.** Even if it worked standalone, wrong output format.

---

## Decision

`StepRoundTripRunner.cs` is NOT modified. `Format` default remains `"scdocx"`,
`SpecFilter` remains the documented 5-spec subset, 79/79 preserved.

The existing comment block (lines 79–83) already documents the hang. I did
NOT replace it with a "we tried X and it failed" essay — the negative findings
live HERE so the runner code stays clean.

### What WOULD unblock STP RT in the future

1. A fresh interactive repro with `Format="stp"` to capture the actual dialog
   title (then PATH D becomes feasible via an out-of-process watchdog).
2. Discovery of an undocumented `user.config` Step option in a newer SC
   release notes / ACT KB (e.g. `<SuppressTranslatorDialog>true</...>`).
3. The `Document.Open(path, configFilePath)` overload + reverse-engineering
   the config-file schema — but this is undocumented and may not exist for
   STEP at all (the documented `IFileSettings` overload uses an internal
   `IScriptObject<IFileSettings>` that the public API doesn't expose a
   constructor for).

---

## Files touched

- This file (created).
- `StepRoundTripRunner.cs`: **unchanged**.
- `StepImportService.cs`: **unchanged**.

## Tests

- 79/79 preserved by NOT modifying `Format` default or `SpecFilter`.
- Last green run: `Test\RE_SelfTest\headless_20260603_075924\step_roundtrip\00_step_roundtrip_summary.md` — 5/5 PASS @ scdocx.
