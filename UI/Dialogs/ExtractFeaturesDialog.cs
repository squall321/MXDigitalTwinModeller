using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer;

#if V251
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    /// <summary>
    /// Stage 1 Extract Features 다이얼로그.
    /// 활성 Part 의 모든 Body 를 분석해 FeatureGraph (JSON) 표시 + 저장.
    /// </summary>
    public class ExtractFeaturesDialog : Form
    {
        private readonly Part _part;
        private TextBox txtJson;
        private TextBox txtSummary;
        private Button btnReanalyze;
        private Button btnSaveJson;
        private Button btnCopy;
        private Button btnClose;
        private Label lblStatus;

        private List<FeatureGraph> _graphs = new List<FeatureGraph>();

        public ExtractFeaturesDialog(Part part)
        {
            _part = part;
            InitializeLayout();
            RunExtraction();
        }

        private void InitializeLayout()
        {
            SuspendLayout();
            Text = "Extract Features (Stage 1 — Plane / Wall / Fillet)";
            Width = 900;
            Height = 700;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.Sizable;

            // ---- 상단 status ----
            lblStatus = new Label
            {
                Location = new Point(12, 12),
                Width = 860,
                Height = 24,
                AutoSize = false,
                Text = "분석 중...",
                Font = new Font(Font.FontFamily, 9.0f, FontStyle.Regular),
            };

            // ---- summary 박스 ----
            var lblSummary = new Label { Text = "요약 (per body):", Location = new Point(12, 42), AutoSize = true };
            txtSummary = new TextBox
            {
                Location = new Point(12, 62),
                Width = 860,
                Height = 120,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9.0f),
            };

            // ---- JSON ----
            var lblJson = new Label { Text = "FeatureGraph JSON:", Location = new Point(12, 192), AutoSize = true };
            txtJson = new TextBox
            {
                Location = new Point(12, 212),
                Width = 860,
                Height = 380,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new Font("Consolas", 9.0f),
            };

            // ---- 버튼들 ----
            btnReanalyze = new Button
            {
                Text = "재분석",
                Location = new Point(12, 612),
                Width = 100,
                Height = 32,
            };
            btnReanalyze.Click += (s, e) => RunExtraction();

            btnSaveJson = new Button
            {
                Text = "JSON 저장",
                Location = new Point(122, 612),
                Width = 120,
                Height = 32,
            };
            btnSaveJson.Click += BtnSaveJson_Click;

            btnCopy = new Button
            {
                Text = "JSON 복사",
                Location = new Point(252, 612),
                Width = 100,
                Height = 32,
            };
            btnCopy.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(txtJson.Text))
                {
                    Clipboard.SetText(txtJson.Text);
                    lblStatus.Text = "JSON 이 클립보드에 복사됨.";
                }
            };

            btnClose = new Button
            {
                Text = "닫기",
                Location = new Point(772, 612),
                Width = 100,
                Height = 32,
                DialogResult = DialogResult.OK,
            };
            btnClose.Click += (s, e) => Close();

            Controls.Add(lblStatus);
            Controls.Add(lblSummary);
            Controls.Add(txtSummary);
            Controls.Add(lblJson);
            Controls.Add(txtJson);
            Controls.Add(btnReanalyze);
            Controls.Add(btnSaveJson);
            Controls.Add(btnCopy);
            Controls.Add(btnClose);

            ResumeLayout(false);
        }

        private void RunExtraction()
        {
            _graphs.Clear();
            txtSummary.Clear();
            txtJson.Clear();
            lblStatus.Text = "분석 중...";
            System.Windows.Forms.Application.DoEvents();

            try
            {
                var bodies = CollectBodies(_part);
                if (bodies.Count == 0)
                {
                    lblStatus.Text = "활성 Part 에 Body 가 없습니다.";
                    return;
                }

                var extractor = new FeatureExtractor();
                var sbSum = new StringBuilder();
                var sbJson = new StringBuilder();

                int idx = 0;
                foreach (var db in bodies)
                {
                    var graph = extractor.Extract(db);
                    _graphs.Add(graph);

                    sbSum.AppendLine(string.Format(
                        "[{0}] {1}",
                        idx,
                        graph.BodyName));
                    sbSum.AppendLine(string.Format(
                        "    bbox = {0:F2} × {1:F2} × {2:F2} mm",
                        graph.BboxSizeMm[0], graph.BboxSizeMm[1], graph.BboxSizeMm[2]));

                    var counts = new List<string>();
                    foreach (var kv in graph.FaceTypeCounts)
                        counts.Add(string.Format("{0}={1}", kv.Key, kv.Value));
                    sbSum.AppendLine(string.Format("    faces = {0} ({1})", graph.Faces.Count, string.Join(", ", counts)));

                    sbSum.AppendLine(string.Format("    walls = {0}", graph.Walls.Count));
                    foreach (var w in graph.Walls)
                        sbSum.AppendLine(string.Format("        {0}: face[{1}↔{2}] thickness = {3:F3} mm",
                            w.Id, w.FaceA, w.FaceB, w.ThicknessMm));

                    sbSum.AppendLine(string.Format("    fillets = {0}", graph.Fillets.Count));
                    foreach (var f in graph.Fillets)
                        sbSum.AppendLine(string.Format("        {0}: face[{1}] R={2:F3} mm ({3})",
                            f.Id, f.FaceIndex, f.RadiusMm, f.BlendType));

                    sbSum.AppendLine();

                    // JSON: 여러 body 인 경우 각각 분리해서 출력
                    if (idx > 0) sbJson.AppendLine().AppendLine("// ---- next body ----");
                    sbJson.Append(FeatureGraphJsonWriter.ToJson(graph));
                    sbJson.AppendLine();

                    idx++;
                }

                txtSummary.Text = sbSum.ToString();
                txtJson.Text = sbJson.ToString();
                lblStatus.Text = string.Format("{0}개 body 분석 완료.", _graphs.Count);
            }
            catch (Exception ex)
            {
                lblStatus.Text = "오류: " + ex.Message;
                txtJson.Text = ex.ToString();
            }
        }

        /// <summary>
        /// Part 안의 모든 Body 를 재귀적으로 수집 (Components 포함).
        /// </summary>
        private List<DesignBody> CollectBodies(IPart part)
        {
            var list = new List<DesignBody>();
            CollectBodiesRecursive(part, list);
            return list;
        }

        private void CollectBodiesRecursive(IPart part, List<DesignBody> list)
        {
            if (part == null) return;
            foreach (var b in part.Bodies)
                list.Add((DesignBody)b);
            foreach (var c in part.Components)
                CollectBodiesRecursive(c.Content, list);
        }

        private void BtnSaveJson_Click(object sender, EventArgs e)
        {
            if (_graphs.Count == 0)
            {
                MessageBox.Show("저장할 데이터가 없습니다. 먼저 분석을 실행하세요.", "안내",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var sfd = new SaveFileDialog
            {
                Title = "FeatureGraph JSON 저장",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = "json",
                FileName = "feature_graph.json",
            })
            {
                if (sfd.ShowDialog() != DialogResult.OK) return;

                try
                {
                    string path = sfd.FileName;

                    if (_graphs.Count == 1)
                    {
                        FeatureGraphJsonWriter.WriteToFile(_graphs[0], path);
                    }
                    else
                    {
                        // 여러 body 면 각각 별도 파일로 저장
                        string baseName = Path.GetFileNameWithoutExtension(path);
                        string dir = Path.GetDirectoryName(path);
                        string ext = Path.GetExtension(path);
                        for (int i = 0; i < _graphs.Count; i++)
                        {
                            string p = Path.Combine(dir, string.Format("{0}_{1:00}{2}", baseName, i, ext));
                            FeatureGraphJsonWriter.WriteToFile(_graphs[i], p);
                        }
                    }

                    lblStatus.Text = "저장 완료: " + path;
                }
                catch (Exception ex)
                {
                    ValidationHelper.ShowError("저장 실패: " + ex.Message, "오류");
                }
            }
        }
    }
}
