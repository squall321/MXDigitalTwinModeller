using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using SpaceClaim.Api.V252.Analysis;
using SpaceClaim.Api.V252.Scripting.Commands;
using SpaceClaim.Api.V252.Scripting.Commands.CommandOptions;
using SpaceClaim.Api.V252.Scripting.Commands.CommandResults;
using SpaceClaim.Api.V252.Scripting.Selection;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Mesh;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    public partial class MeshSettingsDialog : Form
    {
        private readonly Part _part;
        private readonly List<DesignBody> _allBodies;

        // 바디별 마지막 적용된 메쉬 설정 (변경 감지용)
        private readonly Dictionary<DesignBody, string> _appliedSettings
            = new Dictionary<DesignBody, string>();

        // 테이블 + 일괄 적용 공통 항목
        private static readonly string[] ShapeItems = { "Tet", "Hex", "Quad", "Tri" };
        private static readonly ElementShapeType[] ShapeValues = {
            ElementShapeType.Tetrahedral,
            ElementShapeType.Hexahedral,
            ElementShapeType.QuadDominant,
            ElementShapeType.Triangle
        };

        private static readonly string[] MidsideItems = { "Dropped", "Kept", "Auto" };
        private static readonly MidsideNodesType[] MidsideValues = {
            MidsideNodesType.Dropped,
            MidsideNodesType.Kept,
            MidsideNodesType.BasedOnPhysics
        };

        private static readonly string[] SizeFuncItems = { "Curv+Prox", "Curv", "Prox", "Fixed" };
        private static readonly SizeFunctionType[] SizeFuncValues = {
            SizeFunctionType.CurvatureAndProximity,
            SizeFunctionType.Curvature,
            SizeFunctionType.Proximity,
            SizeFunctionType.Fixed
        };

        public MeshSettingsDialog(Part part)
        {
            _part = part;
            _allBodies = MeshSettingsService.GetAllDesignBodies(part);

            InitializeComponent();
            InitializeComboBoxes();
            LoadBodies();

            dgvBodies.CellValueChanged += dgvBodies_CellValueChanged;
            dgvBodies.CurrentCellDirtyStateChanged += dgvBodies_CurrentCellDirtyStateChanged;
        }

        private void InitializeComboBoxes()
        {
            cmbElementShape.Items.AddRange(ShapeItems);
            cmbElementShape.SelectedIndex = 0;

            cmbMidsideNodes.Items.AddRange(MidsideItems);
            cmbMidsideNodes.SelectedIndex = 0;

            cmbSizeFunction.Items.AddRange(SizeFuncItems);
            cmbSizeFunction.SelectedIndex = 0;
        }

        private void LoadBodies()
        {
            dgvBodies.Rows.Clear();

            if (_allBodies.Count == 0)
            {
                ValidationHelper.ShowError("현재 문서에 솔리드 바디가 없습니다.", "알림");
                return;
            }

            foreach (DesignBody body in _allBodies)
            {
                double xMm, yMm, zMm;
                MeshSettingsService.ComputeDefaultSizes(body, out xMm, out yMm, out zMm);
                double elemMm = Math.Min(xMm, Math.Min(yMm, zMm));

                bool isComp = body.Parent != null && !ReferenceEquals(body.Parent, _part);
                string name = (isComp ? "[C] " : "") + (body.Name ?? "Unnamed");
                int rowIdx = dgvBodies.Rows.Add(
                    true,
                    name,
                    xMm.ToString("F2"),
                    yMm.ToString("F2"),
                    zMm.ToString("F2"),
                    elemMm.ToString("F2"),
                    "Tet",
                    "Dropped",
                    "1.20",
                    "Curv+Prox"
                );
                dgvBodies.Rows[rowIdx].Tag = body;
            }
        }

        /// <summary>
        /// CheckBox/ComboBox 즉시 커밋
        /// </summary>
        private void dgvBodies_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dgvBodies.IsCurrentCellDirty)
                dgvBodies.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        /// <summary>
        /// X/Y/Z 값 변경 시 ElemSize(=Min) 자동 갱신
        /// </summary>
        private void dgvBodies_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            int col = e.ColumnIndex;

            // X, Y, Z 컬럼 인덱스 = 2, 3, 4
            if (col >= 2 && col <= 4)
            {
                UpdateElemSize(e.RowIndex);
            }
        }

        private void UpdateElemSize(int rowIndex)
        {
            var row = dgvBodies.Rows[rowIndex];
            double x = ParseCell(row.Cells["colMeshX"].Value);
            double y = ParseCell(row.Cells["colMeshY"].Value);
            double z = ParseCell(row.Cells["colMeshZ"].Value);
            double elem = Math.Min(x, Math.Min(y, z));
            row.Cells["colElemSize"].Value = elem.ToString("F2");
        }

        /// <summary>
        /// 키워드 필터: 일치하는 행 하이라이트 + 체크
        /// </summary>
        private void txtKeyword_TextChanged(object sender, EventArgs e)
        {
            string keyword = txtKeyword.Text.Trim();
            bool hasKeyword = !string.IsNullOrEmpty(keyword);

            foreach (DataGridViewRow row in dgvBodies.Rows)
            {
                string bodyName = row.Cells["colBodyName"].Value?.ToString() ?? "";
                bool match = !hasKeyword ||
                    bodyName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;

                row.Cells["colCheck"].Value = match;
                row.DefaultCellStyle.BackColor = match && hasKeyword
                    ? Color.LightYellow
                    : SystemColors.Window;
            }
        }

        /// <summary>
        /// 업데이트 버튼: 체크된 바디에 메쉬크기 + 요소옵션 일괄 적용
        /// </summary>
        private void btnUpdate_Click(object sender, EventArgs e)
        {
            double batchSize = (double)numBatchSize.Value;
            string batchStr = batchSize.ToString("F2");

            // 일괄 적용 기본값에서 읽기
            string batchShape = ShapeItems[Math.Max(0, cmbElementShape.SelectedIndex)];
            string batchMidside = MidsideItems[Math.Max(0, cmbMidsideNodes.SelectedIndex)];
            string batchGrowth = ((double)numGrowthRate.Value).ToString("F2");
            string batchSizeFunc = SizeFuncItems[Math.Max(0, cmbSizeFunction.SelectedIndex)];

            int count = 0;

            foreach (DataGridViewRow row in dgvBodies.Rows)
            {
                bool isChecked = Convert.ToBoolean(row.Cells["colCheck"].Value);
                if (!isChecked) continue;

                row.Cells["colMeshX"].Value = batchStr;
                row.Cells["colMeshY"].Value = batchStr;
                row.Cells["colMeshZ"].Value = batchStr;
                row.Cells["colElemSize"].Value = batchStr;
                row.Cells["colShape"].Value = batchShape;
                row.Cells["colMidside"].Value = batchMidside;
                row.Cells["colGrowth"].Value = batchGrowth;
                row.Cells["colSizeFunc"].Value = batchSizeFunc;
                count++;
            }

            if (count == 0)
                ValidationHelper.ShowError("선택된 바디가 없습니다.", "알림");
        }

        /// <summary>
        /// 전체 적용 (메쉬 생성) - 바디별 개별 옵션 사용
        /// </summary>
        private void btnApplyAll_Click(object sender, EventArgs e)
        {
            var checkedBodies = new List<DesignBody>();

            foreach (DataGridViewRow row in dgvBodies.Rows)
            {
                bool isChecked = Convert.ToBoolean(row.Cells["colCheck"].Value);
                if (!isChecked) continue;

                DesignBody body = row.Tag as DesignBody;
                if (body == null) continue;

                checkedBodies.Add(body);
            }

            if (checkedBodies.Count == 0)
            {
                ValidationHelper.ShowError("선택된 바디가 없습니다.", "알림");
                return;
            }

            // 바디별 행 매핑 + 변경된 바디만 필터
            var bodyRowMap = new Dictionary<DesignBody, DataGridViewRow>();
            var changedBodies = new List<DesignBody>();
            int skipCount = 0;

            foreach (DataGridViewRow row in dgvBodies.Rows)
            {
                bool isChecked = Convert.ToBoolean(row.Cells["colCheck"].Value);
                if (!isChecked) continue;
                DesignBody b = row.Tag as DesignBody;
                if (b == null) continue;
                bodyRowMap[b] = row;

                string key = GetSettingsKey(row);
                string prevKey;
                if (_appliedSettings.TryGetValue(b, out prevKey) && prevKey == key)
                {
                    skipCount++;
                }
                else
                {
                    changedBodies.Add(b);
                }
            }

            if (changedBodies.Count == 0 && skipCount > 0)
            {
                ValidationHelper.ShowError(
                    string.Format("설정이 변경된 바디가 없습니다. ({0}개 동일)", skipCount),
                    "알림");
                return;
            }

            string lastStep = "";
            var log = new List<string>();
            int successCount = 0;
            int failCount = 0;

            if (skipCount > 0)
                log.Add(string.Format("※ 설정 미변경 {0}개 바디 스킵", skipCount));

            try
            {
                Cursor = Cursors.WaitCursor;
                btnApplyAll.Enabled = false;

                // Step 1: InitMeshSettings (Structural physics)
                lastStep = "InitMeshSettings";
                log.Add("Step 1: InitMeshSettings.Execute(Structural)");
                InitMeshSettingsResult initResult = InitMeshSettings.Execute(PhysicsType.Structural, null);
                log.Add(string.Format("  Success={0}", initResult.Success));

                // Step 2: 방향별 에지 사이징 적용 (변경된 바디만)
                lastStep = "DirectionalSizing";
                log.Add(string.Format("Step 2: 방향별 에지 사이징 ({0}개)", changedBodies.Count));
                foreach (DesignBody sBody in changedBodies)
                {
                    DataGridViewRow row = bodyRowMap[sBody];

                    double xMm = ParseCell(row.Cells["colMeshX"].Value);
                    double yMm = ParseCell(row.Cells["colMeshY"].Value);
                    double zMm = ParseCell(row.Cells["colMeshZ"].Value);

                    MeshSettingsService.ApplyDirectionalSizing(sBody, xMm, yMm, zMm, log);
                }

                // Step 3: SetBodyMeshType (변경된 바디만)
                lastStep = "SetBodyMeshType";
                log.Add(string.Format("Step 3: SetBodyMeshType ({0}개)", changedBodies.Count));
                foreach (DesignBody mBody in changedBodies)
                {
                    DataGridViewRow mRow = bodyRowMap[mBody];
                    string mName = mBody.Name ?? "Unnamed";
                    bool isComp = mBody.Parent != null && !ReferenceEquals(mBody.Parent, _part);
                    string src = isComp ? "[C]" : "[R]";
                    ElementShapeType mShape = ParseShape(mRow.Cells["colShape"].Value);

                    try
                    {
                        var bodySel = BodySelection.Create(mBody);
                        var emptySel = Selection.Empty();

                        BlockingDecompositionType? decomp = null;
                        ElementShapeType? elemShape = null;

                        if (mShape == ElementShapeType.Tetrahedral)
                        {
                            decomp = BlockingDecompositionType.Free;
                            elemShape = ElementShapeType.Tetrahedral;
                        }
                        else if (mShape == ElementShapeType.Hexahedral)
                        {
                            decomp = BlockingDecompositionType.Automatic;
                            elemShape = ElementShapeType.Hexahedral;
                        }

                        var result = SetBodyMeshType.Execute(bodySel, emptySel, decomp, elemShape, null);
                        log.Add(string.Format("  {0} {1}: Decomp={2} Shape={3} Success={4}",
                            src, mName, decomp, elemShape, result.Success));
                    }
                    catch (Exception ex)
                    {
                        log.Add(string.Format("  [WARN] {0} {1}: {2}", src, mName, ex.Message));
                    }
                }

                // Step 4: 바디별 메쉬 생성 (변경된 바디만)
                lastStep = "CreateMesh";
                log.Add(string.Format("Step 4: 바디별 메쉬 생성 ({0}개)", changedBodies.Count));
                var emptySelection = Selection.Empty();

                foreach (DesignBody body in changedBodies)
                {
                    string bodyName = body.Name ?? "Unnamed";
                    bool isComp = body.Parent != null && !ReferenceEquals(body.Parent, _part);
                    string src = isComp ? "[C]" : "[R]";
                    DataGridViewRow bRow = bodyRowMap[body];

                    double elemMm = ParseCell(bRow.Cells["colElemSize"].Value);
                    double elemM = elemMm / 1000.0;

                    ElementShapeType shape = ParseShape(bRow.Cells["colShape"].Value);
                    MidsideNodesType midside = ParseMidside(bRow.Cells["colMidside"].Value);
                    double growthRate = ParseGrowthRate(bRow.Cells["colGrowth"].Value);
                    SizeFunctionType sizeFunc = ParseSizeFunc(bRow.Cells["colSizeFunc"].Value);

                    try
                    {
                        var bodySelection = BodySelection.Create(body);

                        var options = new CreateMeshOptions();
                        options.ElementSize = elemM;
                        options.SolidElementShape = shape;
                        options.MidsideNodes = midside;
                        options.SizeFunctionType = sizeFunc;
                        options.GrowthRate = growthRate;

                        if (shape == ElementShapeType.Tetrahedral)
                            options.MeshMethod = MeshMethod.Prime;

                        CreateMeshResult meshResult = CreateMesh.Execute(bodySelection, emptySelection, options, null);

                        if (meshResult.Success)
                        {
                            successCount++;
                            // 성공 시 설정 저장
                            _appliedSettings[body] = GetSettingsKey(bRow);
                            log.Add(string.Format("  [OK] {0} {1}: Elem={2:F2}mm Shape={3} Midside={4} Growth={5:F2} SizeFunc={6}",
                                src, bodyName, elemMm, shape, midside, growthRate, sizeFunc));
                        }
                        else
                        {
                            failCount++;
                            log.Add(string.Format("  [FAIL] {0} {1}: Success=false", src, bodyName));
                        }
                    }
                    catch (Exception bodyEx)
                    {
                        failCount++;
                        log.Add(string.Format("  [ERR] {0} {1}: {2}", src, bodyName, bodyEx.Message));
                    }
                }

                // 결과 종합
                string msg;
                if (failCount == 0)
                {
                    msg = string.Format("메쉬 생성 완료!\r\n\r\n생성: {0}개", successCount);
                }
                else
                {
                    msg = string.Format("메쉬 생성 일부 완료\r\n\r\n성공: {0}개, 실패: {1}개",
                        successCount, failCount);
                }
                if (skipCount > 0)
                    msg += string.Format(", 스킵(미변경): {0}개", skipCount);
                msg += "\r\n\r\n--- 상세 로그 ---\r\n" + string.Join("\r\n", log);

                ShowCopyableResult("Mesh Settings", msg);
            }
            catch (Exception ex)
            {
                string detail = string.Format("Step: {0}\r\n\r\n{1}", lastStep, ex.Message);
                if (ex.InnerException != null)
                    detail += string.Format("\r\n\r\nInner: {0}: {1}",
                        ex.InnerException.GetType().Name, ex.InnerException.Message);
                detail += string.Format("\r\n\r\nType: {0}", ex.GetType().FullName);
                detail += "\r\n\r\n--- 로그 ---\r\n" + string.Join("\r\n", log);

                ShowCopyableResult("오류",
                    string.Format("메쉬 생성 중 오류가 발생했습니다:\r\n\r\n{0}", detail));
            }
            finally
            {
                Cursor = Cursors.Default;
                btnApplyAll.Enabled = true;
            }
        }

        private static void ShowCopyableResult(string title, string message)
        {
            var dlg = new Form
            {
                Text = title,
                Width = 520,
                Height = 320,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false,
                FormBorderStyle = FormBorderStyle.FixedDialog
            };
            var txt = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Text = message,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9f),
                BackColor = SystemColors.Window,
                SelectionStart = 0,
                SelectionLength = 0
            };
            var btnCopy = new Button { Text = "복사", Width = 80, Height = 30, DialogResult = DialogResult.None };
            var btnOk = new Button { Text = "확인", Width = 80, Height = 30, DialogResult = DialogResult.OK };
            var pnl = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(4)
            };
            btnCopy.Click += (s, ev) =>
            {
                Clipboard.SetText(message);
                btnCopy.Text = "복사됨!";
            };
            pnl.Controls.Add(btnOk);
            pnl.Controls.Add(btnCopy);
            dlg.Controls.Add(txt);
            dlg.Controls.Add(pnl);
            dlg.AcceptButton = btnOk;
            dlg.ShowDialog();
            dlg.Dispose();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        // ─── Parse helpers ───

        private static double ParseCell(object value)
        {
            if (value == null) return 2.0;
            double result;
            if (double.TryParse(value.ToString(), NumberStyles.Any,
                CultureInfo.InvariantCulture, out result))
                return Math.Max(0.01, result);
            return 2.0;
        }

        private static ElementShapeType ParseShape(object value)
        {
            string s = value != null ? value.ToString() : "";
            int idx = Array.IndexOf(ShapeItems, s);
            return idx >= 0 ? ShapeValues[idx] : ElementShapeType.Tetrahedral;
        }

        private static MidsideNodesType ParseMidside(object value)
        {
            string s = value != null ? value.ToString() : "";
            int idx = Array.IndexOf(MidsideItems, s);
            return idx >= 0 ? MidsideValues[idx] : MidsideNodesType.Dropped;
        }

        private static double ParseGrowthRate(object value)
        {
            if (value == null) return 1.2;
            double result;
            if (double.TryParse(value.ToString(), NumberStyles.Any,
                CultureInfo.InvariantCulture, out result))
                return Math.Max(1.0, Math.Min(5.0, result));
            return 1.2;
        }

        private static SizeFunctionType ParseSizeFunc(object value)
        {
            string s = value != null ? value.ToString() : "";
            int idx = Array.IndexOf(SizeFuncItems, s);
            return idx >= 0 ? SizeFuncValues[idx] : SizeFunctionType.CurvatureAndProximity;
        }

        /// <summary>
        /// 행의 메쉬 설정을 단일 문자열 키로 만듦 (변경 감지용)
        /// </summary>
        private static string GetSettingsKey(DataGridViewRow row)
        {
            return string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}",
                row.Cells["colMeshX"].Value,
                row.Cells["colMeshY"].Value,
                row.Cells["colMeshZ"].Value,
                row.Cells["colElemSize"].Value,
                row.Cells["colShape"].Value,
                row.Cells["colMidside"].Value,
                row.Cells["colGrowth"].Value + "|" + row.Cells["colSizeFunc"].Value);
        }

        // ─── Analysis API enum 변환 ───

        private static ElementShape ToAnalysisShape(ElementShapeType t)
        {
            switch (t)
            {
                case ElementShapeType.Hexahedral: return ElementShape.Hexahedral;
                case ElementShapeType.QuadDominant: return ElementShape.QuadDominant;
                case ElementShapeType.Triangle: return ElementShape.Triangle;
                default: return ElementShape.Tetrahedral;
            }
        }

        private static Analysis.MidsideNodes ToAnalysisMidside(MidsideNodesType t)
        {
            switch (t)
            {
                case MidsideNodesType.Kept: return Analysis.MidsideNodes.Kept;
                case MidsideNodesType.BasedOnPhysics: return Analysis.MidsideNodes.BasedOnPhysics;
                default: return Analysis.MidsideNodes.Dropped;
            }
        }

        private static Analysis.SizeFunction ToAnalysisSizeFunc(SizeFunctionType t)
        {
            switch (t)
            {
                case SizeFunctionType.Curvature: return Analysis.SizeFunction.Curvature;
                case SizeFunctionType.Proximity: return Analysis.SizeFunction.Proximity;
                case SizeFunctionType.Fixed: return Analysis.SizeFunction.Fixed;
                default: return Analysis.SizeFunction.CurvatureAndProximity;
            }
        }
    }
}
