using System;
using System.Drawing;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Package;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Package;

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
    /// 패키지 파일(*Layer 스택 + 솔더볼 볼맵) 임포트 다이얼로그:
    /// 파일 선택 → 파싱 요약(레이어/볼/다이, Mesh* 키는 필터링) → 옵션(볼 형상
    /// Cylinder/Barrel, 벌지 비율, 수지 매트릭스) → CAD 스택 생성.
    /// </summary>
    public class PackageImportDialog : Form
    {
        private readonly Part _part;
        private PackageSpec _spec;
        private string _path;

        private TextBox _pathBox;
        private Label _summary;
        private ComboBox _shapeCombo;
        private NumericUpDown _bulge;
        private NumericUpDown _slices;
        private CheckBox _fillMatrix;
        private Button _generateBtn;

        public PackageImportDialog(Part part)
        {
            _part = part;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "Import Package (볼맵 → CAD 스택)";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(520, 320);

            var pathLabel = new Label { Text = "패키지 파일:", Location = new Point(12, 15), AutoSize = true };
            _pathBox = new TextBox { Location = new Point(100, 12), Size = new Size(320, 23), ReadOnly = true };
            var browse = new Button { Text = "찾기...", Location = new Point(428, 11), Size = new Size(78, 25) };
            browse.Click += OnBrowse;

            _summary = new Label
            {
                Location = new Point(12, 45),
                Size = new Size(494, 120),
                BorderStyle = BorderStyle.FixedSingle,
                Text = "파일을 선택하면 레이어 스택 요약이 표시됩니다.\n" +
                       "(*Layer / Location / Length / Thickness / Cylinder 볼맵 / Box 다이 — " +
                       "Mesh* 설정은 CAD에 사용되지 않습니다)",
            };

            var shapeLabel = new Label { Text = "솔더볼 형상:", Location = new Point(12, 180), AutoSize = true };
            _shapeCombo = new ComboBox
            {
                Location = new Point(100, 177), Size = new Size(110, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            _shapeCombo.Items.AddRange(new object[] { "Cylinder", "Barrel" });
            _shapeCombo.SelectedIndex = 0;
            _shapeCombo.SelectedIndexChanged += (s, e) => UpdateEnable();

            var bulgeLabel = new Label { Text = "벌지 비율:", Location = new Point(230, 180), AutoSize = true };
            _bulge = new NumericUpDown
            {
                Location = new Point(300, 177), Size = new Size(64, 23),
                Minimum = 1.05m, Maximum = 2.0m, Value = 1.25m,
                DecimalPlaces = 2, Increment = 0.05m,
            };
            var slicesLabel = new Label { Text = "슬라이스:", Location = new Point(380, 180), AutoSize = true };
            _slices = new NumericUpDown
            {
                Location = new Point(444, 177), Size = new Size(56, 23),
                Minimum = 4, Maximum = 24, Value = 8,
            };

            _fillMatrix = new CheckBox
            {
                Text = "수지 매트릭스 생성 (슬래브 − 볼/다이 Boolean: 언더필/EMC)",
                Location = new Point(12, 212), AutoSize = true, Checked = true,
            };

            _generateBtn = new Button
            {
                Text = "Generate", Location = new Point(300, 250), Size = new Size(100, 32),
                Enabled = false,
            };
            _generateBtn.Click += OnGenerate;
            var closeBtn = new Button { Text = "닫기", Location = new Point(406, 250), Size = new Size(100, 32) };
            closeBtn.Click += (s, e) => Close();

            Controls.AddRange(new Control[]
            {
                pathLabel, _pathBox, browse, _summary,
                shapeLabel, _shapeCombo, bulgeLabel, _bulge, slicesLabel, _slices,
                _fillMatrix, _generateBtn, closeBtn,
            });
            UpdateEnable();
        }

        private void UpdateEnable()
        {
            bool barrel = _shapeCombo.SelectedIndex == 1;
            _bulge.Enabled = barrel;
            _slices.Enabled = barrel;
            _generateBtn.Enabled = _spec != null && _spec.Layers.Count > 0;
        }

        private void OnBrowse(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog
            {
                Filter = "Package files (*.txt)|*.txt|All files (*.*)|*.*",
                Title = "패키지 파일 선택",
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _path = dlg.FileName;
                _pathBox.Text = _path;
                try
                {
                    _spec = PackageFileParser.ParseFile(_path);
                }
                catch (Exception ex)
                {
                    _spec = null;
                    _summary.Text = "파싱 실패: " + ex.Message;
                    UpdateEnable();
                    return;
                }

                int balls = 0, boxes = 0;
                var lines = new System.Text.StringBuilder();
                var zb = _spec.ComputeZBasesMm();
                for (int i = 0; i < _spec.Layers.Count && i < 9; i++)
                {
                    var l = _spec.Layers[i];
                    lines.AppendFormat("{0}: t={1}mm z={2:0.###} {3}x{4}mm",
                        l.Name, l.ThicknessMm, zb[i], l.LenXMm, l.LenYMm);
                    if (l.Balls.Count > 0) lines.AppendFormat("  볼 {0}", l.Balls.Count);
                    if (l.Boxes.Count > 0) lines.AppendFormat("  다이 {0}", l.Boxes.Count);
                    lines.AppendLine();
                }
                foreach (var l in _spec.Layers) { balls += l.Balls.Count; boxes += l.Boxes.Count; }
                if (_spec.Layers.Count > 9)
                    lines.AppendLine("... 외 " + (_spec.Layers.Count - 9) + "개 레이어");
                lines.AppendFormat("합계: 레이어 {0}, 볼 {1}, 다이 {2}, 총 두께 {3:0.###}mm, 경고 {4}",
                    _spec.Layers.Count, balls, boxes, _spec.GetTotalThicknessMm(), _spec.Warnings.Count);
                _summary.Text = lines.ToString();
                UpdateEnable();
            }
        }

        private void OnGenerate(object sender, EventArgs e)
        {
            if (_spec == null || _part == null) return;
            var opt = new PackageGenOptions
            {
                BallShape = _shapeCombo.SelectedIndex == 1 ? "barrel" : "cylinder",
                BarrelBulgeRatio = (double)_bulge.Value,
                BarrelSlices = (int)_slices.Value,
                FillMatrix = _fillMatrix.Checked,
            };

            _generateBtn.Enabled = false;
            Cursor = Cursors.WaitCursor;
            PackageGenResult res = null;
            try
            {
                WriteBlock.ExecuteTask("Import Package", () =>
                {
                    DesignBody bound;
                    res = new PackageGenerationService().BuildStack(_part, _spec, opt, out bound);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "패키지 생성 중 오류:\n\n" + ex.Message, "오류",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                Cursor = Cursors.Default;
                _generateBtn.Enabled = true;
            }

            if (res != null && res.Success)
                MessageBox.Show(this, string.Format(
                    "패키지 스택 생성 완료\n\n바디 {0}개 / 총 두께 {1:0.###}mm\n\n{2}",
                    res.TotalBodies, res.TotalThicknessMm, string.Join("\n", res.Log.ToArray())),
                    "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                MessageBox.Show(this, "패키지 생성 실패: " +
                    (res != null ? res.Error : "(no result)"), "오류",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
