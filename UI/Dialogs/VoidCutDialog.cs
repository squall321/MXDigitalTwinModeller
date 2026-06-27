using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.VoidCut;
using Point = System.Drawing.Point;

#if V251
using SpaceClaim.Api.V251.Extensibility;
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Extensibility;
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    /// <summary>
    /// Void Cut 확장 다이얼로그
    /// 기능: 커터/타겟 분리, 형상 정의(Cuboid/Cylinder/Sphere),
    /// Boolean 모드(Subtract/Unite/Intersect), 오프셋, NS 생성, 체적 표시, 배치 모드
    /// </summary>
    public class VoidCutDialog : Form
    {
        private readonly Part _part;
        private List<DesignBody> _bodyList;

        // 모드 선택
        private RadioButton rdoBodyMode;
        private RadioButton rdoShapeMode;

        // 모드 1: 커터 바디 선택
        private GroupBox grpCutterBody;
        private CheckedListBox clbCutters;
        private Button btnCutterAll;
        private Label lblCutterInfo;

        // 모드 2: 형상 정의
        private GroupBox grpShape;
        private ComboBox cmbShapeType;
        private Label lblDim1, lblDim2, lblDim3;
        private NumericUpDown numDim1, numDim2, numDim3;
        private NumericUpDown numPosX, numPosY, numPosZ;

        // 타겟 바디 (공통)
        private GroupBox grpTarget;
        private CheckedListBox clbTargets;
        private Button btnTargetAll;
        private Button btnRefresh;
        private Label lblTargetInfo;

        // 옵션
        private GroupBox grpOptions;
        private ComboBox cmbBooleanMode;
        private NumericUpDown numOffset;
        private CheckBox chkDeleteCutter;
        private CheckBox chkCreateNS;
        private CheckBox chkShowVolume;

        // 실행 버튼
        private Button btnPreview;
        private Button btnExecute;
        private Button btnBatch;
        private Button btnCopy;
        private Button btnClose;

        // 결과
        private TextBox txtResult;

        public VoidCutDialog(Part part)
        {
            _part = part;
            _bodyList = new List<DesignBody>();
            InitializeLayout();
            PopulateBodyList();
        }

        private void InitializeLayout()
        {
            SuspendLayout();
            Text = "Void Cut (보이드 컷)";
            Width = 570;
            Height = 920;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            TopMost = true;
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            AutoScaleMode = AutoScaleMode.Font;

            int y = 12;
            int formW = 540;

            // ============================================
            // 1. 커터 모드 선택
            // ============================================
            rdoBodyMode = new RadioButton
            {
                Text = "기존 바디 선택 (Select Body)",
                Location = new Point(16, y),
                AutoSize = true,
                Checked = true
            };
            rdoBodyMode.CheckedChanged += rdoMode_CheckedChanged;

            rdoShapeMode = new RadioButton
            {
                Text = "형상 정의 (Define Shape)",
                Location = new Point(260, y),
                AutoSize = true
            };
            rdoShapeMode.CheckedChanged += rdoMode_CheckedChanged;
            y += 28;

            // ============================================
            // 2. 모드 1: 커터 바디 선택 그룹
            // ============================================
            grpCutterBody = new GroupBox
            {
                Text = "커터 바디 (Cutter Bodies)",
                Location = new Point(12, y),
                Width = formW,
                Height = 160
            };

            clbCutters = new CheckedListBox
            {
                Location = new Point(10, 20),
                Width = 420,
                Height = 100,
                CheckOnClick = true
            };
            clbCutters.SelectedIndexChanged += clbCutters_SelectedIndexChanged;

            btnCutterAll = new Button
            {
                Text = "전체선택",
                Location = new Point(440, 20),
                Width = 85,
                Height = 26
            };
            btnCutterAll.Click += btnCutterAll_Click;

            lblCutterInfo = new Label
            {
                Text = "",
                Location = new Point(10, 130),
                Width = 515,
                AutoSize = false,
                ForeColor = SystemColors.GrayText
            };

            grpCutterBody.Controls.AddRange(new Control[]
            {
                clbCutters, btnCutterAll, lblCutterInfo
            });
            y += 168;

            // ============================================
            // 3. 모드 2: 형상 정의 그룹
            // ============================================
            grpShape = new GroupBox
            {
                Text = "커터 형상 정의 (Shape Definition)",
                Location = new Point(12, y - 168),
                Width = formW,
                Height = 160,
                Visible = false
            };

            var lblShape = new Label
            {
                Text = "Shape:",
                Location = new Point(10, 24),
                AutoSize = true
            };
            cmbShapeType = new ComboBox
            {
                Location = new Point(65, 20),
                Width = 130,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbShapeType.Items.AddRange(new object[] { "Cuboid", "Cylinder", "Sphere" });
            cmbShapeType.SelectedIndex = 0;
            cmbShapeType.SelectedIndexChanged += cmbShapeType_SelectedIndexChanged;

            // 치수 행 (동적 레이블)
            lblDim1 = new Label { Text = "Width:", Location = new Point(10, 56), AutoSize = true };
            numDim1 = CreateNumeric(new Point(65, 52), 10, 0.01m, 10000m);
            lblDim2 = new Label { Text = "Height:", Location = new Point(190, 56), AutoSize = true };
            numDim2 = CreateNumeric(new Point(245, 52), 10, 0.01m, 10000m);
            lblDim3 = new Label { Text = "Depth:", Location = new Point(370, 56), AutoSize = true };
            numDim3 = CreateNumeric(new Point(420, 52), 10, 0.01m, 10000m);

            // 위치 행
            var lblPX = new Label { Text = "Pos X:", Location = new Point(10, 86), AutoSize = true };
            numPosX = CreateNumeric(new Point(65, 82), 0, -10000m, 10000m);
            var lblPY = new Label { Text = "Pos Y:", Location = new Point(190, 86), AutoSize = true };
            numPosY = CreateNumeric(new Point(245, 82), 0, -10000m, 10000m);
            var lblPZ = new Label { Text = "Pos Z:", Location = new Point(370, 86), AutoSize = true };
            numPosZ = CreateNumeric(new Point(420, 82), 0, -10000m, 10000m);

            var lblShapeHint = new Label
            {
                Text = "치수/위치: mm 단위. 위치는 형상 중심 기준.",
                Location = new Point(10, 115),
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Font = new Font(Font.FontFamily, 8f)
            };

            grpShape.Controls.AddRange(new Control[]
            {
                lblShape, cmbShapeType,
                lblDim1, numDim1, lblDim2, numDim2, lblDim3, numDim3,
                lblPX, numPosX, lblPY, numPosY, lblPZ, numPosZ,
                lblShapeHint
            });

            // ============================================
            // 4. 타겟 바디 (항상 표시)
            // ============================================
            grpTarget = new GroupBox
            {
                Text = "타겟 바디 (Target Bodies)",
                Location = new Point(12, y),
                Width = formW,
                Height = 160
            };

            clbTargets = new CheckedListBox
            {
                Location = new Point(10, 20),
                Width = 420,
                Height = 100,
                CheckOnClick = true
            };
            clbTargets.SelectedIndexChanged += clbTargets_SelectedIndexChanged;

            btnTargetAll = new Button
            {
                Text = "전체선택",
                Location = new Point(440, 20),
                Width = 85,
                Height = 26
            };
            btnTargetAll.Click += btnTargetAll_Click;

            btnRefresh = new Button
            {
                Text = "새로고침",
                Location = new Point(440, 54),
                Width = 85,
                Height = 26
            };
            btnRefresh.Click += (s, e) => PopulateBodyList();

            lblTargetInfo = new Label
            {
                Text = "",
                Location = new Point(10, 130),
                Width = 515,
                AutoSize = false,
                ForeColor = SystemColors.GrayText
            };

            grpTarget.Controls.AddRange(new Control[]
            {
                clbTargets, btnTargetAll, btnRefresh, lblTargetInfo
            });
            y += 168;

            // ============================================
            // 5. 옵션 그룹
            // ============================================
            grpOptions = new GroupBox
            {
                Text = "옵션 (Options)",
                Location = new Point(12, y),
                Width = formW,
                Height = 100
            };

            // Operation 모드
            var lblOp = new Label
            {
                Text = "Operation:",
                Location = new Point(10, 24),
                AutoSize = true
            };
            cmbBooleanMode = new ComboBox
            {
                Location = new Point(80, 20),
                Width = 120,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbBooleanMode.Items.AddRange(new object[] { "Subtract", "Unite", "Intersect" });
            cmbBooleanMode.SelectedIndex = 0;

            // 오프셋
            var lblOffset = new Label
            {
                Text = "Offset (mm):",
                Location = new Point(220, 24),
                AutoSize = true
            };
            numOffset = new NumericUpDown
            {
                Location = new Point(310, 20),
                Width = 80,
                DecimalPlaces = 2,
                Minimum = 0m,
                Maximum = 100m,
                Value = 0m,
                Increment = 0.1m
            };

            // 체크박스 행
            chkDeleteCutter = new CheckBox
            {
                Text = "커터 삭제",
                Location = new Point(10, 55),
                AutoSize = true,
                Checked = true
            };
            chkCreateNS = new CheckBox
            {
                Text = "NS 자동 생성",
                Location = new Point(130, 55),
                AutoSize = true,
                Checked = false
            };
            chkShowVolume = new CheckBox
            {
                Text = "체적 표시",
                Location = new Point(280, 55),
                AutoSize = true,
                Checked = false
            };

            var lblOptionsHint = new Label
            {
                Text = "Subtract=빼기, Unite=합치기, Intersect=교집합  /  Offset=커터 확장량",
                Location = new Point(10, 78),
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Font = new Font(Font.FontFamily, 7.5f)
            };

            grpOptions.Controls.AddRange(new Control[]
            {
                lblOp, cmbBooleanMode,
                lblOffset, numOffset,
                chkDeleteCutter, chkCreateNS, chkShowVolume,
                lblOptionsHint
            });
            y += 108;

            // ============================================
            // 6. 실행 버튼
            // ============================================
            btnPreview = new Button
            {
                Text = "미리보기",
                Location = new Point(16, y),
                Width = 90,
                Height = 32
            };
            btnPreview.Click += btnPreview_Click;

            btnExecute = new Button
            {
                Text = "실행",
                Location = new Point(114, y),
                Width = 90,
                Height = 32
            };
            btnExecute.Click += btnExecute_Click;

            btnBatch = new Button
            {
                Text = "배치 실행...",
                Location = new Point(212, y),
                Width = 100,
                Height = 32
            };
            btnBatch.Click += btnBatch_Click;

            btnCopy = new Button
            {
                Text = "복사",
                Location = new Point(388, y),
                Width = 65,
                Height = 32,
                Enabled = false
            };
            btnCopy.Click += btnCopy_Click;

            btnClose = new Button
            {
                Text = "닫기",
                Location = new Point(465, y),
                Width = 65,
                Height = 32
            };
            btnClose.Click += (s, e) => Close();
            y += 42;

            // ============================================
            // 7. 결과
            // ============================================
            var lblResult = new Label
            {
                Text = "결과:",
                Location = new Point(16, y),
                AutoSize = true
            };
            y += 18;

            txtResult = new TextBox
            {
                Location = new Point(16, y),
                Width = formW - 4,
                Height = 180,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9f),
                BackColor = SystemColors.Window,
                Text = "커터/타겟을 선택하고 \"미리보기\" 또는 \"실행\"을 클릭하세요.\r\n" +
                       "형상 정의 모드에서는 Cuboid/Cylinder/Sphere를 생성할 수 있습니다.\r\n" +
                       "\"배치 실행\"으로 CSV 파일에서 일괄 처리 가능합니다."
            };

            // ============================================
            // Controls 등록
            // ============================================
            Controls.AddRange(new Control[]
            {
                rdoBodyMode, rdoShapeMode,
                grpCutterBody, grpShape,
                grpTarget,
                grpOptions,
                btnPreview, btnExecute, btnBatch, btnCopy, btnClose,
                lblResult, txtResult
            });

            CancelButton = btnClose;

            ResumeLayout(false);
            PerformLayout();
        }

        private NumericUpDown CreateNumeric(Point loc, decimal defaultVal,
            decimal min, decimal max)
        {
            return new NumericUpDown
            {
                Location = loc,
                Width = 100,
                DecimalPlaces = 2,
                Minimum = min,
                Maximum = max,
                Value = defaultVal,
                Increment = 1m
            };
        }

        #region Mode / Shape Change

        private void rdoMode_CheckedChanged(object sender, EventArgs e)
        {
            grpCutterBody.Visible = rdoBodyMode.Checked;
            grpShape.Visible = rdoShapeMode.Checked;
        }

        private void cmbShapeType_SelectedIndexChanged(object sender, EventArgs e)
        {
            string shape = cmbShapeType.SelectedItem as string ?? "Cuboid";

            switch (shape)
            {
                case "Cylinder":
                    lblDim1.Text = "Radius:";
                    lblDim2.Text = "Height:";
                    lblDim2.Visible = true;
                    numDim2.Visible = true;
                    lblDim3.Visible = false;
                    numDim3.Visible = false;
                    break;
                case "Sphere":
                    lblDim1.Text = "Radius:";
                    lblDim2.Visible = false;
                    numDim2.Visible = false;
                    lblDim3.Visible = false;
                    numDim3.Visible = false;
                    break;
                default: // Cuboid
                    lblDim1.Text = "Width:";
                    lblDim2.Text = "Height:";
                    lblDim3.Text = "Depth:";
                    lblDim2.Visible = true;
                    numDim2.Visible = true;
                    lblDim3.Visible = true;
                    numDim3.Visible = true;
                    break;
            }
        }

        #endregion

        #region Body List

        private void PopulateBodyList()
        {
            clbCutters.Items.Clear();
            clbTargets.Items.Clear();
            _bodyList.Clear();

            foreach (DesignBody body in _part.Bodies)
            {
                string name = string.IsNullOrEmpty(body.Name) ? body.ToString() : body.Name;
                clbCutters.Items.Add(name);
                clbTargets.Items.Add(name);
                _bodyList.Add(body);
            }

            if (_bodyList.Count == 0)
            {
                lblCutterInfo.Text = "Part에 바디가 없습니다.";
                lblTargetInfo.Text = "Part에 바디가 없습니다.";
            }
            else
            {
                // 기본: 커터=전체 선택, 타겟=전체 선택
                for (int i = 0; i < clbCutters.Items.Count; i++)
                    clbCutters.SetItemChecked(i, true);
                for (int i = 0; i < clbTargets.Items.Count; i++)
                    clbTargets.SetItemChecked(i, true);

                btnCutterAll.Text = "전체해제";
                btnTargetAll.Text = "전체해제";
                lblCutterInfo.Text = string.Format("{0}개 바디 전체 선택됨", _bodyList.Count);
                lblTargetInfo.Text = string.Format("{0}개 바디 전체 선택됨", _bodyList.Count);
            }
        }

        private void btnCutterAll_Click(object sender, EventArgs e)
        {
            ToggleAll(clbCutters, btnCutterAll);
        }

        private void btnTargetAll_Click(object sender, EventArgs e)
        {
            ToggleAll(clbTargets, btnTargetAll);
        }

        private void ToggleAll(CheckedListBox clb, Button btn)
        {
            bool allChecked = clb.CheckedIndices.Count == clb.Items.Count;
            for (int i = 0; i < clb.Items.Count; i++)
                clb.SetItemChecked(i, !allChecked);
            btn.Text = allChecked ? "전체선택" : "전체해제";
        }

        private void clbCutters_SelectedIndexChanged(object sender, EventArgs e)
        {
            ShowBodyInfo(clbCutters, lblCutterInfo);
        }

        private void clbTargets_SelectedIndexChanged(object sender, EventArgs e)
        {
            ShowBodyInfo(clbTargets, lblTargetInfo);
        }

        private void ShowBodyInfo(CheckedListBox clb, Label lbl)
        {
            int idx = clb.SelectedIndex;
            if (idx < 0 || idx >= _bodyList.Count) return;

            var body = _bodyList[idx];
            try
            {
                var bb = body.Shape.GetBoundingBox(Matrix.Identity);
                var min = bb.MinCorner;
                var max = bb.MaxCorner;
                lbl.Text = string.Format(
                    "BBox: ({0:F1}, {1:F1}, {2:F1}) ~ ({3:F1}, {4:F1}, {5:F1}) mm",
                    GeometryUtils.MetersToMm(min.X), GeometryUtils.MetersToMm(min.Y),
                    GeometryUtils.MetersToMm(min.Z),
                    GeometryUtils.MetersToMm(max.X), GeometryUtils.MetersToMm(max.Y),
                    GeometryUtils.MetersToMm(max.Z));
            }
            catch
            {
                lbl.Text = "바운딩 박스 계산 실패";
            }
        }

        private List<DesignBody> GetCheckedBodies(CheckedListBox clb)
        {
            var list = new List<DesignBody>();
            foreach (int idx in clb.CheckedIndices)
            {
                if (idx >= 0 && idx < _bodyList.Count)
                    list.Add(_bodyList[idx]);
            }
            return list;
        }

        #endregion

        #region Options Helpers

        private BooleanMode GetSelectedMode()
        {
            switch (cmbBooleanMode.SelectedIndex)
            {
                case 1: return BooleanMode.Unite;
                case 2: return BooleanMode.Intersect;
                default: return BooleanMode.Subtract;
            }
        }

        private CutterShape GetSelectedShape()
        {
            string shape = cmbShapeType.SelectedItem as string ?? "Cuboid";
            switch (shape)
            {
                case "Cylinder": return CutterShape.Cylinder;
                case "Sphere": return CutterShape.Sphere;
                default: return CutterShape.Cuboid;
            }
        }

        #endregion

        #region Preview

        private void btnPreview_Click(object sender, EventArgs e)
        {
            try
            {
                var targets = GetCheckedBodies(clbTargets);
                if (targets.Count == 0)
                {
                    ValidationHelper.ShowError("타겟 바디를 1개 이상 체크하세요.", "입력 오류");
                    return;
                }

                if (rdoBodyMode.Checked)
                {
                    var cutters = GetCheckedBodies(clbCutters);
                    if (cutters.Count == 0)
                    {
                        ValidationHelper.ShowError("커터 바디를 1개 이상 체크하세요.", "입력 오류");
                        return;
                    }

                    var info = VoidCutService.GetCandidateInfo(_part, cutters, targets);
                    info.Add("");
                    info.Add("\"실행\" 버튼으로 Boolean 연산을 수행합니다.");
                    txtResult.Text = string.Join("\r\n", info);
                }
                else
                {
                    txtResult.Text = "형상 정의 모드: 실행 시 자동으로 커터를 생성하여 교차 감지합니다.\r\n" +
                                     string.Format("Shape: {0}, 타겟: {1}개",
                                         cmbShapeType.SelectedItem, targets.Count);
                }
                btnCopy.Enabled = true;
            }
            catch (Exception ex)
            {
                txtResult.Text = string.Format("미리보기 오류:\r\n{0}", ex.Message);
                btnCopy.Enabled = true;
            }
        }

        #endregion

        #region Execute

        private void btnExecute_Click(object sender, EventArgs e)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                btnExecute.Enabled = false;
                txtResult.Text = "Void Cut 실행 중...";
                txtResult.Refresh();

                // UI 값 캡처 (WriteBlock 밖에서)
                bool isBodyMode = rdoBodyMode.Checked;
                var mode = GetSelectedMode();
                double offsetMm = (double)numOffset.Value;
                bool deleteCutter = chkDeleteCutter.Checked;
                bool createNS = chkCreateNS.Checked;
                bool showVolume = chkShowVolume.Checked;

                var targets = GetCheckedBodies(clbTargets);
                if (targets.Count == 0)
                {
                    ValidationHelper.ShowError("타겟 바디를 1개 이상 체크하세요.", "입력 오류");
                    return;
                }

                VoidCutResult result = null;

                if (isBodyMode)
                {
                    var cutters = GetCheckedBodies(clbCutters);
                    if (cutters.Count == 0)
                    {
                        ValidationHelper.ShowError("커터 바디를 1개 이상 체크하세요.", "입력 오류");
                        return;
                    }

                    WriteBlock.ExecuteTask("Void Cut", () =>
                    {
                        result = VoidCutService.Execute(
                            _part, cutters, targets, mode,
                            offsetMm, deleteCutter, createNS, showVolume);
                    });
                }
                else
                {
                    // 형상 정의 모드
                    var shape = GetSelectedShape();
                    double d1 = (double)numDim1.Value;
                    double d2 = (double)numDim2.Value;
                    double d3 = (double)numDim3.Value;
                    double px = (double)numPosX.Value;
                    double py = (double)numPosY.Value;
                    double pz = (double)numPosZ.Value;

                    // 치수 유효성 검사
                    if (d1 <= 0)
                    {
                        ValidationHelper.ShowError("첫 번째 치수(Width/Radius)는 0보다 커야 합니다.", "입력 오류");
                        return;
                    }
                    if (shape == CutterShape.Cuboid && (d2 <= 0 || d3 <= 0))
                    {
                        ValidationHelper.ShowError("Cuboid: Width, Height, Depth 모두 0보다 커야 합니다.", "입력 오류");
                        return;
                    }
                    if (shape == CutterShape.Cylinder && d2 <= 0)
                    {
                        ValidationHelper.ShowError("Cylinder: Radius, Height 모두 0보다 커야 합니다.", "입력 오류");
                        return;
                    }

                    WriteBlock.ExecuteTask("Void Cut (Shape)", () =>
                    {
                        result = VoidCutService.ExecuteWithShape(
                            _part, shape, d1, d2, d3, px, py, pz,
                            targets, mode, offsetMm, deleteCutter, createNS, showVolume);
                    });
                }

                DisplayResult(result, deleteCutter);
            }
            catch (Exception ex)
            {
                txtResult.Text = string.Format("오류 발생:\r\n\r\n{0}\r\n\r\n{1}",
                    ex.Message, ex.GetType().FullName);
                if (ex.InnerException != null)
                    txtResult.Text += string.Format("\r\n\r\nInner: {0}",
                        ex.InnerException.Message);
                btnCopy.Enabled = true;
            }
            finally
            {
                Cursor = Cursors.Default;
                btnExecute.Enabled = true;
            }
        }

        #endregion

        #region Batch

        private void btnBatch_Click(object sender, EventArgs e)
        {
            try
            {
                // CSV 파일 선택
                using (var ofd = new OpenFileDialog())
                {
                    ofd.Title = "배치 CSV 파일 선택";
                    ofd.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                    ofd.FilterIndex = 1;

                    if (ofd.ShowDialog() != DialogResult.OK)
                        return;

                    string csvPath = ofd.FileName;

                    var targets = GetCheckedBodies(clbTargets);
                    if (targets.Count == 0)
                    {
                        ValidationHelper.ShowError("타겟 바디를 1개 이상 체크하세요.", "입력 오류");
                        return;
                    }

                    Cursor = Cursors.WaitCursor;
                    btnBatch.Enabled = false;
                    txtResult.Text = "배치 실행 중...";
                    txtResult.Refresh();

                    var mode = GetSelectedMode();
                    double offsetMm = (double)numOffset.Value;
                    bool createNS = chkCreateNS.Checked;
                    bool showVolume = chkShowVolume.Checked;

                    VoidCutResult result = null;

                    WriteBlock.ExecuteTask("Void Cut (Batch)", () =>
                    {
                        result = VoidCutService.ExecuteBatch(
                            _part, csvPath, targets, mode, offsetMm, createNS, showVolume);
                    });

                    DisplayResult(result, true);
                }
            }
            catch (Exception ex)
            {
                txtResult.Text = string.Format("배치 오류:\r\n{0}", ex.Message);
                btnCopy.Enabled = true;
            }
            finally
            {
                Cursor = Cursors.Default;
                btnBatch.Enabled = true;
            }
        }

        #endregion

        #region Result Display

        private void DisplayResult(VoidCutResult result, bool refreshList)
        {
            if (result == null) return;

            var lines = new List<string>();
            lines.Add("=== Void Cut 결과 ===");
            lines.Add(string.Format("교차 후보: {0}개", result.CandidateCount));
            lines.Add(string.Format("성공: {0}개", result.SubtractedCount));
            lines.Add(string.Format("스킵: {0}개", result.SkippedCount));
            if (result.FailedCount > 0)
                lines.Add(string.Format("실패: {0}개", result.FailedCount));
            if (result.NamedSelectionsCreated > 0)
                lines.Add(string.Format("NS 생성: {0}개", result.NamedSelectionsCreated));
            lines.Add("");
            lines.AddRange(result.Log);
            txtResult.Text = string.Join("\r\n", lines);
            btnCopy.Enabled = true;

            if (refreshList)
                PopulateBodyList();
        }

        #endregion

        #region Copy

        private void btnCopy_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtResult.Text))
            {
                Clipboard.SetText(txtResult.Text);
                btnCopy.Text = "복사됨!";
            }
        }

        #endregion
    }
}
