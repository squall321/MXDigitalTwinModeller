using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Odb;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Odb;

#if V251
using SpaceClaim.Api.V251;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    /// <summary>
    /// ECAD(ODB++) → MCAD 임포트 다이얼로그: 압축 해제된 ODB++ 폴더(또는 .tgz 자동
    /// 해제) 선택 → step/파싱 요약(외곽/컷아웃/패키지/부품/핀) → 옵션(보드 두께,
    /// 패드, 풋프린트 필터) → 보드+부품+패드 솔리드 스택 생성. MCP import_odbpp 와
    /// 동일한 OdbImportService 를 쓰는 내부 UI 기능.
    /// </summary>
    public class OdbImportDialog : Form
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private readonly Part _part;
        private OdbDesign _design;
        private string _root;

        private TextBox _pathBox;
        private Label _summary;
        private ComboBox _stepCombo;
        private NumericUpDown _thick, _padThick, _padDia, _compH, _minFoot, _maxComp, _maxPads;
        private CheckBox _pads;
        private Button _importBtn;
        private bool _stepComboReady;

        public OdbImportDialog(Part part)
        {
            _part = part;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "Import ODB++ (ECAD → MCAD 솔리드)";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(560, 428);

            var pathLabel = new Label { Text = "ODB++ 경로:", Location = new Point(12, 15), AutoSize = true };
            _pathBox = new TextBox { Location = new Point(100, 12), Size = new Size(280, 23), ReadOnly = true };
            var browseDir = new Button { Text = "폴더...", Location = new Point(388, 11), Size = new Size(75, 25) };
            browseDir.Click += OnBrowseDir;
            var browseTgz = new Button { Text = ".tgz...", Location = new Point(468, 11), Size = new Size(75, 25) };
            browseTgz.Click += OnBrowseTgz;

            _summary = new Label
            {
                Location = new Point(12, 45),
                Size = new Size(531, 128),
                BorderStyle = BorderStyle.FixedSingle,
                Text = "압축 해제된 ODB++ 트리(steps/·matrix/ 포함) 폴더 또는 .tgz 파일을 선택하면\n" +
                       "보드 외곽 / 컷아웃 / 패키지 / 부품 / 핀 요약이 표시됩니다.\n" +
                       "(구리 배선은 제외 — 보드·부품·솔더패드 솔리드만 생성됩니다)",
            };

            var stepLabel = new Label { Text = "Step:", Location = new Point(12, 186), AutoSize = true };
            _stepCombo = new ComboBox
            {
                Location = new Point(100, 183), Size = new Size(200, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            _stepCombo.SelectedIndexChanged += OnStepChanged;

            int y1 = 220;
            var tLabel = new Label { Text = "보드 두께 mm:", Location = new Point(12, y1 + 3), AutoSize = true };
            _thick = Num(120, y1, 0.05m, 20m, 1.0m, 2);
            var hLabel = new Label { Text = "기본 부품높이 mm:", Location = new Point(200, y1 + 3), AutoSize = true };
            _compH = Num(322, y1, 0.05m, 20m, 1.0m, 2);
            var fLabel = new Label { Text = "최소 풋프린트 mm:", Location = new Point(400, y1 + 3), AutoSize = true };
            _minFoot = Num(517, y1, 0m, 50m, 0m, 1);

            int y2 = 252;
            _pads = new CheckBox { Text = "솔더패드 생성", Location = new Point(12, y2), AutoSize = true, Checked = true };
            _pads.CheckedChanged += (s, e) => { _padThick.Enabled = _pads.Checked; _padDia.Enabled = _pads.Checked; };
            var ptLabel = new Label { Text = "패드 두께 mm:", Location = new Point(140, y2 + 3), AutoSize = true };
            _padThick = Num(232, y2, 0.005m, 1m, 0.05m, 3);
            var pdLabel = new Label { Text = "패드 직경 mm (0=자동):", Location = new Point(310, y2 + 3), AutoSize = true };
            _padDia = Num(457, y2, 0m, 5m, 0m, 2);

            int y3 = 284;
            var mcLabel = new Label { Text = "최대 부품수:", Location = new Point(12, y3 + 3), AutoSize = true };
            _maxComp = Num(120, y3, 1m, 100000m, 2000m, 0);
            var mpLabel = new Label { Text = "최대 패드수:", Location = new Point(200, y3 + 3), AutoSize = true };
            _maxPads = Num(285, y3, 1m, 200000m, 8000m, 0);
            var hint = new Label
            {
                Text = "부품/패드 한도 초과 시 아무것도 생성하지 않고 명확한 오류로 중단합니다.",
                Location = new Point(12, y3 + 32), AutoSize = true, ForeColor = Color.DimGray,
            };

            _importBtn = new Button
            {
                Text = "Import", Location = new Point(337, 380), Size = new Size(100, 32),
                Enabled = false,
            };
            _importBtn.Click += OnImport;
            var closeBtn = new Button { Text = "닫기", Location = new Point(443, 380), Size = new Size(100, 32) };
            closeBtn.Click += (s, e) => Close();

            Controls.AddRange(new Control[]
            {
                pathLabel, _pathBox, browseDir, browseTgz, _summary,
                stepLabel, _stepCombo,
                tLabel, _thick, hLabel, _compH, fLabel, _minFoot,
                _pads, ptLabel, _padThick, pdLabel, _padDia,
                mcLabel, _maxComp, mpLabel, _maxPads, hint,
                _importBtn, closeBtn,
            });
        }

        private NumericUpDown Num(int x, int y, decimal min, decimal max, decimal val, int dec)
        {
            var n = new NumericUpDown
            {
                Location = new Point(x, y), Size = new Size(dec == 0 ? 70 : 64, 23),
                Minimum = min, Maximum = max, Value = val, DecimalPlaces = dec,
                Increment = dec == 0 ? 100 : (dec >= 3 ? 0.005m : 0.1m),
            };
            return n;
        }

        private void OnBrowseDir(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog
            {
                Description = "압축 해제된 ODB++ 트리 폴더 선택 (steps/ 와 matrix/ 포함)",
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                LoadRoot(dlg.SelectedPath, null);
            }
        }

        private void OnBrowseTgz(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog
            {
                Filter = "ODB++ archives (*.tgz;*.tar.gz)|*.tgz;*.tar.gz|All files (*.*)|*.*",
                Title = "ODB++ .tgz 선택 (자동 압축 해제)",
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                string extracted;
                try { extracted = ExtractTgz(dlg.FileName); }
                catch (Exception ex)
                {
                    _summary.Text = ".tgz 압축 해제 실패: " + ex.Message;
                    return;
                }
                LoadRoot(extracted, null);
            }
        }

        /// <summary>Windows 내장 tar.exe 로 .tgz 를 %TEMP% 에 풀고 루트를 돌려준다.</summary>
        internal static string ExtractTgz(string tgzPath)
        {
            string tar = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "tar.exe");
            if (!File.Exists(tar))
                throw new InvalidOperationException(
                    "tar.exe 를 찾을 수 없습니다 - .tgz 를 직접 압축 해제한 뒤 폴더로 선택하세요");
            string dest = Path.Combine(Path.GetTempPath(),
                "MXDTM_odb_" + Path.GetFileNameWithoutExtension(tgzPath) + "_" +
                DateTime.Now.Ticks.ToString(Inv));
            Directory.CreateDirectory(dest);
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = tar,
                Arguments = "-xzf \"" + tgzPath + "\" -C \"" + dest + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
            using (var p = System.Diagnostics.Process.Start(psi))
            {
                string err = p.StandardError.ReadToEnd();
                if (!p.WaitForExit(120000))
                { try { p.Kill(); } catch { } throw new InvalidOperationException("tar 시간 초과"); }
                if (p.ExitCode != 0)
                    throw new InvalidOperationException("tar 실패: " + err);
            }
            // 루트 판정: dest 자체 또는 단일 자식 폴더가 steps/ 를 가짐
            if (Directory.Exists(Path.Combine(dest, "steps"))) return dest;
            var kids = Directory.GetDirectories(dest);
            foreach (var k in kids)
                if (Directory.Exists(Path.Combine(k, "steps"))
                    || Directory.Exists(Path.Combine(k, "odb", "steps"))) return k;
            return dest; // 파서가 명확한 오류를 낸다
        }

        private void LoadRoot(string root, string step)
        {
            _root = root;
            _pathBox.Text = root;
            try
            {
                _design = OdbPlusPlusParser.Parse(root, step);
            }
            catch (Exception ex)
            {
                _design = null;
                _summary.Text = "파싱 실패: " + ex.Message;
                _importBtn.Enabled = false;
                return;
            }

            _stepComboReady = false;
            _stepCombo.Items.Clear();
            foreach (var s in _design.StepNames) _stepCombo.Items.Add(s);
            _stepCombo.SelectedIndex = _design.StepNames.IndexOf(_design.Step.Name);
            _stepComboReady = true;

            var st = _design.Step;
            int pins = 0, bottom = 0;
            foreach (var c in st.Components)
            {
                pins += st.Packages[c.PkgIndex].Pins.Count;
                if (c.Mirrored) bottom++;
            }
            double area = Math.Abs(Models.Pcb.PcbAssemblySpec.ShoelaceArea(st.OutlineMm.ToArray()));
            var sb = new System.Text.StringBuilder();
            sb.AppendFormat(Inv, "step '{0}'  |  보드 외곽 {1}점, {2:0.#} mm², 컷아웃 {3}\n",
                st.Name, st.OutlineMm.Count, area, st.CutoutsMm.Count);
            sb.AppendFormat(Inv, "패키지 {0}  |  부품 {1} (바텀 {2})  |  핀 {3}\n",
                st.Packages.Count, st.Components.Count, bottom, pins);
            sb.AppendFormat(Inv, "경고 {0}", _design.Warnings.Count);
            int shown = 0;
            foreach (var w in _design.Warnings)
            {
                if (shown++ >= 3) { sb.Append("\n..."); break; }
                sb.Append("\n· ").Append(w.Length > 90 ? w.Substring(0, 90) + "..." : w);
            }
            _summary.Text = sb.ToString();
            _importBtn.Enabled = st.OutlineMm.Count >= 3;
        }

        private void OnStepChanged(object sender, EventArgs e)
        {
            if (!_stepComboReady || _root == null || _stepCombo.SelectedItem == null) return;
            LoadRoot(_root, (string)_stepCombo.SelectedItem);
        }

        private void OnImport(object sender, EventArgs e)
        {
            if (_design == null || _part == null) return;
            var opt = new OdbImportOptions
            {
                BoardThicknessMm = (double)_thick.Value,
                DefaultCompHeightMm = (double)_compH.Value,
                MinFootprintMm = (double)_minFoot.Value,
                IncludePads = _pads.Checked,
                PadThicknessMm = (double)_padThick.Value,
                PadDiaMm = (double)_padDia.Value,
                MaxComponents = (int)_maxComp.Value,
                MaxTotalPads = (int)_maxPads.Value,
            };

            _importBtn.Enabled = false;
            Cursor = Cursors.WaitCursor;
            OdbImportResult res = null;
            try
            {
                WriteBlock.ExecuteTask("Import ODB++", () =>
                {
                    res = new OdbImportService().Build(_part, _design, opt);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "ODB++ 임포트 중 오류:\n\n" + ex.Message, "오류",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                Cursor = Cursors.Default;
                _importBtn.Enabled = true;
            }

            if (res != null && res.Success)
            {
                double bv;
                res.DimsMm.TryGetValue("board_v_mm3", out bv);
                var log = new System.Text.StringBuilder();
                int shown = 0;
                foreach (var l in res.Log)
                {
                    if (shown++ >= 8) { log.Append("\n... 외 ").Append(res.Log.Count - 8).Append("건"); break; }
                    log.Append("\n").Append(l);
                }
                MessageBox.Show(this, string.Format(Inv,
                    "ODB++ → MCAD 변환 완료\n\n바디 {0}개 (부품 {1}, 스킵 {2}, 패드 {3})\n" +
                    "보드 부피 {4:0.##} mm³{5}",
                    res.BodiesCreated.Count, res.ComponentsBuilt, res.ComponentsSkipped,
                    res.PadsBuilt, bv, log.ToString()),
                    "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
                MessageBox.Show(this, "ODB++ 임포트 실패: " +
                    (res != null ? res.Error : "(no result)"), "오류",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
