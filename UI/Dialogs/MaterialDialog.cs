using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Material;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    public class MaterialDialog : Form
    {
        private readonly Part _part;

        // 왼쪽: 물성 요약 테이블
        private DataGridView dgvSummary;

        // 오른쪽: 입력 컨트롤
        private TextBox txtKeyword;
        private Button btnFilter;
        private TextBox txtMaterialName;
        private ComboBox cmbPreset;
        private TextBox[] txtProps;
        private CheckedListBox clbBodies;
        private Button btnSelectAll;
        private Button btnDeselectAll;
        private Button btnApply;
        private Button btnCopy;
        private Button btnClose;
        private TextBox txtResult;

        // 바디별 적용된 물성 추적 (mm-tonne-s 값)
        private readonly Dictionary<DesignBody, AppliedMaterialInfo> _appliedMaterials
            = new Dictionary<DesignBody, AppliedMaterialInfo>();

        private class AppliedMaterialInfo
        {
            public string MaterialName;
            public double[] Values; // density, E, nu, G, sigma, cte, k, cp
        }

        private const int LeftPanelWidth = 520;
        private const int RightX = 536;

        public MaterialDialog(Part part)
        {
            _part = part;
            InitializeLayout();
            LoadBodies();
            ApplyPreset("Steel");
        }

        private void InitializeLayout()
        {
            Text = "Material Properties (재료 물성) - mm/tonne/s";
            Width = 1060;
            Height = 680;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;

            // ═══════════════════════════════════════
            // 왼쪽: 물성 요약 테이블
            // ═══════════════════════════════════════
            var lblSummary = new Label
            {
                Text = "바디별 적용 물성 (mm-tonne-s)",
                Location = new Point(12, 8),
                AutoSize = true,
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold)
            };

            dgvSummary = new DataGridView
            {
                Location = new Point(12, 28),
                Size = new Size(LeftPanelWidth, ClientSize.Height - 38),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8.5f),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
            };
            dgvSummary.DefaultCellStyle.SelectionBackColor = Color.LightSteelBlue;
            dgvSummary.DefaultCellStyle.SelectionForeColor = Color.Black;
            dgvSummary.ColumnHeadersDefaultCellStyle.Font = new Font("Consolas", 8f, FontStyle.Bold);
            dgvSummary.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            // 컬럼 정의
            dgvSummary.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colBodyName", HeaderText = "Body", Width = 110
            });
            dgvSummary.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colMatName", HeaderText = "Material", Width = 80
            });
            dgvSummary.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDensity", HeaderText = "ρ", Width = 55,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            dgvSummary.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colE", HeaderText = "E(MPa)", Width = 60,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            dgvSummary.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colNu", HeaderText = "ν", Width = 38,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            dgvSummary.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colG", HeaderText = "G(MPa)", Width = 55,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            dgvSummary.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colSigma", HeaderText = "σ_u", Width = 40,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            dgvSummary.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colCTE", HeaderText = "CTE", Width = 50,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            // ═══════════════════════════════════════
            // 오른쪽: 입력 컨트롤
            // ═══════════════════════════════════════
            int rx = RightX;
            int y = 12;

            // ─── 키워드 필터 ───
            var lblKeyword = new Label
            {
                Text = "키워드:",
                Location = new Point(rx, y + 3),
                AutoSize = true
            };

            txtKeyword = new TextBox
            {
                Location = new Point(rx + 74, y),
                Width = 200
            };
            txtKeyword.TextChanged += txtKeyword_TextChanged;

            btnFilter = new Button
            {
                Text = "전체 리셋",
                Location = new Point(rx + 284, y),
                Width = 80,
                Height = 23
            };
            btnFilter.Click += (s, e) =>
            {
                txtKeyword.Text = "";
                for (int i = 0; i < clbBodies.Items.Count; i++)
                    clbBodies.SetItemChecked(i, true);
            };

            var lblFilterHint = new Label
            {
                Text = "바디 이름에 키워드가 포함된 것만 선택됩니다",
                Location = new Point(rx + 74, y + 24),
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Font = new Font(Font.FontFamily, 8f)
            };

            y += 48;

            // ─── 프리셋 선택 ───
            var lblPreset = new Label
            {
                Text = "프리셋:",
                Location = new Point(rx, y + 3),
                AutoSize = true
            };

            cmbPreset = new ComboBox
            {
                Location = new Point(rx + 74, y),
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbPreset.Items.AddRange(new object[] { "Steel", "Aluminum", "CFRP", "Custom" });
            cmbPreset.SelectedIndex = 0;
            cmbPreset.SelectedIndexChanged += cmbPreset_SelectedIndexChanged;

            y += 28;

            // ─── 재료 이름 ───
            var lblName = new Label
            {
                Text = "재료명:",
                Location = new Point(rx, y + 3),
                AutoSize = true
            };

            txtMaterialName = new TextBox
            {
                Location = new Point(rx + 74, y),
                Width = 200,
                Text = "Steel"
            };

            y += 28;

            // ─── 물성 입력 필드 ───
            string[] labels = MaterialService.PropertyDisplayNames;
            string[] units = MaterialService.PropertyUnits;
            txtProps = new TextBox[labels.Length];

            for (int i = 0; i < labels.Length; i++)
            {
                string unitText = string.IsNullOrEmpty(units[i]) ? "" : string.Format(" [{0}]", units[i]);

                var lbl = new Label
                {
                    Text = labels[i] + unitText,
                    Location = new Point(rx, y + 3),
                    AutoSize = true
                };

                txtProps[i] = new TextBox
                {
                    Location = new Point(rx + 284, y),
                    Width = 150,
                    Text = "0"
                };

                Controls.Add(lbl);
                Controls.Add(txtProps[i]);

                y += 26;
            }

            y += 4;

            // ─── 바디 선택 ───
            var lblBodies = new Label
            {
                Text = "적용 바디:",
                Location = new Point(rx, y + 3),
                AutoSize = true
            };

            clbBodies = new CheckedListBox
            {
                Location = new Point(rx + 74, y),
                Size = new Size(290, 90),
                CheckOnClick = true
            };

            btnSelectAll = new Button
            {
                Text = "전체선택",
                Location = new Point(rx + 374, y),
                Width = 60,
                Height = 25
            };
            btnSelectAll.Click += (s, e) =>
            {
                for (int i = 0; i < clbBodies.Items.Count; i++)
                    clbBodies.SetItemChecked(i, true);
            };

            btnDeselectAll = new Button
            {
                Text = "전체해제",
                Location = new Point(rx + 374, y + 30),
                Width = 60,
                Height = 25
            };
            btnDeselectAll.Click += (s, e) =>
            {
                for (int i = 0; i < clbBodies.Items.Count; i++)
                    clbBodies.SetItemChecked(i, false);
            };

            y += 96;

            // ─── 버튼 ───
            btnApply = new Button
            {
                Text = "적용",
                Location = new Point(rx, y),
                Width = 100,
                Height = 30
            };
            btnApply.Click += btnApply_Click;

            btnCopy = new Button
            {
                Text = "결과 복사",
                Location = new Point(rx + 110, y),
                Width = 100,
                Height = 30
            };
            btnCopy.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(txtResult.Text))
                    Clipboard.SetText(txtResult.Text);
            };

            btnClose = new Button
            {
                Text = "닫기",
                Location = new Point(rx + 220, y),
                Width = 80,
                Height = 30
            };
            btnClose.Click += (s, e) => Close();

            y += 38;

            // ─── 결과 텍스트박스 ───
            txtResult = new TextBox
            {
                Location = new Point(rx, y),
                Size = new Size(480, ClientSize.Height - y - 10),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = new Font("Consolas", 9f),
                BackColor = Color.White
            };

            // ─── 컨트롤 추가 ───
            Controls.AddRange(new Control[]
            {
                lblSummary, dgvSummary,
                lblKeyword, txtKeyword, btnFilter, lblFilterHint,
                lblPreset, cmbPreset,
                lblName, txtMaterialName,
                lblBodies, clbBodies, btnSelectAll, btnDeselectAll,
                btnApply, btnCopy, btnClose,
                txtResult
            });
        }

        private readonly List<DesignBody> _bodies = new List<DesignBody>();

        private void LoadBodies()
        {
            _bodies.Clear();
            clbBodies.Items.Clear();
            dgvSummary.Rows.Clear();
            _appliedMaterials.Clear();

            var allBodies = MaterialService.GetAllDesignBodies(_part);
            foreach (var body in allBodies)
            {
                _bodies.Add(body);
                string name = body.Name ?? "Unnamed";
                clbBodies.Items.Add(name);
                clbBodies.SetItemChecked(clbBodies.Items.Count - 1, true);

                // 바디의 현재 재료 읽기
                double densityMTS;
                string matName = MaterialService.ReadBodyMaterialName(body, out densityMTS);

                if (matName != null)
                {
                    // 캐시에서 전체 물성값 조회
                    double[] cached = MaterialService.GetCachedValues(matName);
                    if (cached != null)
                    {
                        _appliedMaterials[body] = new AppliedMaterialInfo
                        {
                            MaterialName = matName,
                            Values = cached
                        };
                        int rowIdx = dgvSummary.Rows.Add(
                            name, matName,
                            FormatCell(cached[0]), FormatCell(cached[1]),
                            FormatCell(cached[2]), FormatCell(cached[3]),
                            FormatCell(cached[4]), FormatCell(cached[5]));
                        dgvSummary.Rows[rowIdx].Tag = body;
                        dgvSummary.Rows[rowIdx].DefaultCellStyle.ForeColor = Color.Black;
                    }
                    else
                    {
                        // 캐시에 없으면 밀도만 표시
                        _appliedMaterials[body] = new AppliedMaterialInfo
                        {
                            MaterialName = matName,
                            Values = new double[] { densityMTS, 0, 0, 0, 0, 0, 0, 0 }
                        };
                        int rowIdx = dgvSummary.Rows.Add(
                            name, matName, FormatCell(densityMTS),
                            "", "", "", "", "");
                        dgvSummary.Rows[rowIdx].Tag = body;
                        dgvSummary.Rows[rowIdx].DefaultCellStyle.ForeColor = Color.DarkBlue;
                    }
                }
                else
                {
                    // 재료 없음
                    int rowIdx = dgvSummary.Rows.Add(name, "-", "", "", "", "", "", "");
                    dgvSummary.Rows[rowIdx].Tag = body;
                    dgvSummary.Rows[rowIdx].DefaultCellStyle.ForeColor = SystemColors.GrayText;
                }
            }
        }

        private void txtKeyword_TextChanged(object sender, EventArgs e)
        {
            string keyword = txtKeyword.Text.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                for (int i = 0; i < clbBodies.Items.Count; i++)
                    clbBodies.SetItemChecked(i, true);
                return;
            }

            for (int i = 0; i < _bodies.Count; i++)
            {
                string name = _bodies[i].Name ?? "";
                bool match = name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
                clbBodies.SetItemChecked(i, match);
            }
        }

        private void cmbPreset_SelectedIndexChanged(object sender, EventArgs e)
        {
            string preset = cmbPreset.SelectedItem as string;
            if (preset != null && preset != "Custom")
            {
                ApplyPreset(preset);
                string keyword = txtKeyword.Text.Trim();
                if (!string.IsNullOrEmpty(keyword))
                    txtMaterialName.Text = string.Format("{0}_{1}", preset, keyword);
                else
                    txtMaterialName.Text = preset;
            }
        }

        private void ApplyPreset(string preset)
        {
            double[] vals;
            switch (preset)
            {
                case "Steel":
                    vals = MaterialService.GetSteelDefaults();
                    break;
                case "Aluminum":
                    vals = MaterialService.GetAluminumDefaults();
                    break;
                case "CFRP":
                    vals = MaterialService.GetCFRPDefaults();
                    break;
                default:
                    return;
            }

            for (int i = 0; i < vals.Length && i < txtProps.Length; i++)
            {
                txtProps[i].Text = FormatValue(vals[i]);
            }
        }

        private static string FormatValue(double v)
        {
            if (v == 0) return "0";
            double abs = Math.Abs(v);
            if (abs >= 1 && abs < 1e7)
                return v.ToString("G6");
            return v.ToString("E4");
        }

        private static string FormatCell(double v)
        {
            if (v == 0) return "";
            double abs = Math.Abs(v);
            if (abs >= 0.01 && abs < 1e6)
                return v.ToString("G4");
            return v.ToString("E2");
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            txtResult.Clear();

            // 선택된 바디 수집
            var selectedBodies = new List<DesignBody>();
            for (int i = 0; i < clbBodies.Items.Count; i++)
            {
                if (clbBodies.GetItemChecked(i) && i < _bodies.Count)
                    selectedBodies.Add(_bodies[i]);
            }

            if (selectedBodies.Count == 0)
            {
                txtResult.Text = "선택된 바디가 없습니다.";
                return;
            }

            // 값 파싱
            double density, eMod, nu, gMod, sigma, cte, k, cp;
            if (!TryParseField(0, out density) ||
                !TryParseField(1, out eMod) ||
                !TryParseField(2, out nu))
            {
                return;
            }

            TryParseField(3, out gMod);
            TryParseField(4, out sigma);
            TryParseField(5, out cte);
            TryParseField(6, out k);
            TryParseField(7, out cp);

            string matName = txtMaterialName.Text.Trim();
            if (string.IsNullOrEmpty(matName))
            {
                txtResult.Text = "재료명을 입력하세요.";
                return;
            }

            var log = new List<string>();
            string keyword = txtKeyword.Text.Trim();
            if (!string.IsNullOrEmpty(keyword))
                log.Add(string.Format("키워드: \"{0}\" → {1}개 바디 매칭", keyword, selectedBodies.Count));

            try
            {
                WriteBlock.ExecuteTask("Apply Material", () =>
                {
                    MaterialService.ApplyMaterial(_part, matName,
                        density, eMod, nu, gMod, sigma, cte, k, cp,
                        selectedBodies, log);
                });

                // 성공 시 적용 정보 저장
                double[] vals = { density, eMod, nu, gMod, sigma, cte, k, cp };
                foreach (var body in selectedBodies)
                {
                    _appliedMaterials[body] = new AppliedMaterialInfo
                    {
                        MaterialName = matName,
                        Values = (double[])vals.Clone()
                    };
                }

                UpdateSummaryTable();
            }
            catch (Exception ex)
            {
                log.Add(string.Format("\n오류: {0}", ex.Message));
            }

            txtResult.Text = string.Join("\r\n", log.ToArray());
        }

        private void UpdateSummaryTable()
        {
            foreach (DataGridViewRow row in dgvSummary.Rows)
            {
                var body = row.Tag as DesignBody;
                if (body == null) continue;

                AppliedMaterialInfo info;
                if (_appliedMaterials.TryGetValue(body, out info))
                {
                    row.Cells["colMatName"].Value = info.MaterialName;
                    row.Cells["colDensity"].Value = FormatCell(info.Values[0]);
                    row.Cells["colE"].Value = FormatCell(info.Values[1]);
                    row.Cells["colNu"].Value = FormatCell(info.Values[2]);
                    row.Cells["colG"].Value = FormatCell(info.Values[3]);
                    row.Cells["colSigma"].Value = FormatCell(info.Values[4]);
                    row.Cells["colCTE"].Value = FormatCell(info.Values[5]);
                    row.DefaultCellStyle.ForeColor = Color.Black;
                }
                else
                {
                    row.Cells["colMatName"].Value = "-";
                    row.Cells["colDensity"].Value = "";
                    row.Cells["colE"].Value = "";
                    row.Cells["colNu"].Value = "";
                    row.Cells["colG"].Value = "";
                    row.Cells["colSigma"].Value = "";
                    row.Cells["colCTE"].Value = "";
                    row.DefaultCellStyle.ForeColor = SystemColors.GrayText;
                }
            }
        }

        private bool TryParseField(int index, out double value)
        {
            value = 0;
            if (index >= txtProps.Length) return true;

            string text = txtProps[index].Text.Trim();
            if (string.IsNullOrEmpty(text))
                return true;

            if (double.TryParse(text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value))
                return true;

            txtResult.Text = string.Format("'{0}' 값이 올바르지 않습니다: {1}",
                MaterialService.PropertyDisplayNames[index], text);
            return false;
        }
    }
}
