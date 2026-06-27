using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.GmshMesher
{
    /// <summary>
    /// CLI-based Gmsh engine that invokes gmsh.exe as an external process.
    /// Searches for gmsh.exe in Libs/gmsh/ relative to the add-in assembly.
    /// </summary>
    // @lat: [[mesh#Gmsh Mesher#Gmsh CLI 엔진]]
    public class GmshCliEngine : IGmshEngine
    {
        private const int TimeoutMs = 300000; // 5 minutes
        private string _gmshExePath;
        private string _version;

        public bool IsAvailable
        {
            get
            {
                FindGmshExe();
                return _gmshExePath != null && File.Exists(_gmshExePath);
            }
        }

        public string Version
        {
            get
            {
                if (_version != null) return _version;
                if (!IsAvailable) return "N/A";

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = _gmshExePath,
                        Arguments = "--version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    using (var proc = Process.Start(psi))
                    {
                        string output = proc.StandardOutput.ReadToEnd().Trim();
                        string err = proc.StandardError.ReadToEnd().Trim();
                        proc.WaitForExit(5000);
                        _version = string.IsNullOrEmpty(output) ? err : output;
                    }
                }
                catch
                {
                    _version = "unknown";
                }
                return _version;
            }
        }

        public bool RunMesh(string geoPath, string mshPath, List<string> log)
        {
            if (!IsAvailable)
            {
                if (log != null) log.Add("[GmshCli] ERROR: gmsh.exe를 찾을 수 없습니다.");
                return false;
            }

            string args = string.Format("\"{0}\" -3 -o \"{1}\" -format msh4 -v 3", geoPath, mshPath);

            if (log != null)
                log.Add(string.Format("[GmshCli] 실행: {0} {1}", _gmshExePath, args));

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _gmshExePath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    string stdout = proc.StandardOutput.ReadToEnd();
                    string stderr = proc.StandardError.ReadToEnd();

                    bool exited = proc.WaitForExit(TimeoutMs);
                    if (!exited)
                    {
                        try { proc.Kill(); } catch { }
                        if (log != null) log.Add("[GmshCli] ERROR: 타임아웃 (300초 초과)");
                        return false;
                    }

                    // Gmsh outputs info to stderr
                    if (!string.IsNullOrEmpty(stderr) && log != null)
                    {
                        string[] errLines = stderr.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string line in errLines)
                        {
                            if (line.Contains("Error") || line.Contains("Warning") ||
                                line.Contains("nodes") || line.Contains("elements") ||
                                line.Contains("Done"))
                            {
                                log.Add("[Gmsh] " + line.Trim());
                            }
                        }
                    }

                    if (proc.ExitCode != 0)
                    {
                        if (log != null)
                            log.Add(string.Format("[GmshCli] ERROR: 종료 코드 {0}", proc.ExitCode));
                        return false;
                    }
                }

                if (!File.Exists(mshPath))
                {
                    if (log != null) log.Add("[GmshCli] ERROR: .msh 출력 파일 없음");
                    return false;
                }

                if (log != null) log.Add("[GmshCli] 메쉬 생성 완료");
                return true;
            }
            catch (Exception ex)
            {
                if (log != null)
                    log.Add(string.Format("[GmshCli] ERROR: {0}", ex.Message));
                return false;
            }
        }

        private void FindGmshExe()
        {
            if (_gmshExePath != null) return;

            // Search relative to add-in assembly
            string asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string[] searchPaths = new[]
            {
                Path.Combine(asmDir, "Libs", "gmsh", "gmsh.exe"),
                Path.Combine(asmDir, "gmsh", "gmsh.exe"),
                Path.Combine(asmDir, "gmsh.exe"),
            };

            foreach (string path in searchPaths)
            {
                if (File.Exists(path))
                {
                    _gmshExePath = path;
                    return;
                }
            }

            // Fallback: check PATH
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "gmsh.exe",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(psi))
                {
                    proc.WaitForExit(5000);
                    if (proc.ExitCode == 0)
                        _gmshExePath = "gmsh.exe";
                }
            }
            catch { }
        }
    }
}
