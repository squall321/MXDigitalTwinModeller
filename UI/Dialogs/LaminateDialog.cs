using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Laminate;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Laminate;

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
    /// 적층 모델 생성 다이얼로그
    /// 직사각형 모드 + 면 기반 모드 지원
    /// </summary>
    public partial class LaminateDialog : Form
    {
        private readonly RectangularLaminateService rectService;
        private readonly SurfaceLaminateService surfaceService;
        private Part activePart;
        private List<DesignBody> previewBodies;

        // 면 기반 모드 - 선택된 면
        private DesignFace selectedFace;

        // DataGridView 데이터 소스
        private BindingList<LaminateLayerDefinition> layerBindingList;

        private int nextLayerNumber = 4; // 초기 3개 이후부터

        /// <summary>
        /// 현재 직사각형 모드인지 여부
        /// </summary>
        private bool IsRectangularMode
        {
            get { return rdoRectangular.Checked; }
        }

        public LaminateDialog(Part part)
        {
            InitializeComponent();
            rectService = new RectangularLaminateService();
            surfaceService = new SurfaceLaminateService();
            activePart = part;
            previewBodies = new List<DesignBody>();

            InitializeLayerGrid();
            UpdateTotalThickness();

            this.TopMost = true;
            this.FormClosing += LaminateDialog_FormClosing;
        }

        // =============================================
        //  모드 전환
        // =============================================

        private void rdoMode_CheckedChanged(object sender, EventArgs e)
        {
            bool isRect = IsRectangularMode;

            grpDimensions.Visible = isRect;
            grpSurface.Visible = !isRect;

            // Share Topology 옵션은 직사각형 모드에서만 표시
            chkShareTopology.Visible = isRect;

            // 면 모드 전환 시 미리보기 정리
            CleanupPreview();
        }

        // =============================================
        //  면 선택 (서피스 모드)
        // =============================================

        private void btnSelectFace_Click(object sender, EventArgs e)
        {
            try
            {
                // 현재 SpaceClaim 선택에서 면 가져오기
                var window = Window.ActiveWindow;
                if (window == null)
                {
                    ValidationHelper.ShowError("활성 윈도우가 없습니다.", "오류");
                    return;
                }

                // 현재 선택된 객체에서 DesignFace 찾기
                DesignFace face = null;
                foreach (var obj in window.ActiveContext.Selection)
                {
                    face = obj as DesignFace;
                    if (face != null) break;
                }

                if (face == null)
                {
                    ValidationHelper.ShowError(
                        "면이 선택되지 않았습니다.\n\n" +
                        "SpaceClaim에서 평면(Face)을 먼저 선택한 후\n" +
                        "\"면 선택 (Pick)\" 버튼을 다시 눌러주세요.",
                        "면 선택");
                    return;
                }

                // 평면인지 확인
                if (!(face.Shape.Geometry is Plane))
                {
                    ValidationHelper.ShowError(
                        "선택된 면이 평면이 아닙니다.\n평면(Planar Face)만 지원됩니다.",
                        "면 선택 오류");
                    return;
                }

                selectedFace = face;

                // 면 정보 표시
                Plane plane = (Plane)face.Shape.Geometry;
                Vector normal = plane.Frame.DirZ.UnitVector;
                lblFaceInfo.Text = string.Format("법선: ({0:F2}, {1:F2}, {2:F2})",
                    normal.X, normal.Y, normal.Z);
                lblFaceInfo.ForeColor = System.Drawing.Color.Black;
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    $"면 선택 중 오류가 발생했습니다:\n\n{ex.Message}", "오류");
            }
        }

        // =============================================
        //  레이어 그리드 초기화
        // =============================================

        private void InitializeLayerGrid()
        {
            layerBindingList = new BindingList<LaminateLayerDefinition>
            {
                new LaminateLayerDefinition("Layer_1", 0.25),
                new LaminateLayerDefinition("Layer_2", 0.25),
                new LaminateLayerDefinition("Layer_3", 0.25),
            };

            RefreshGrid();

            dgvLayers.CellValueChanged += dgvLayers_CellValueChanged;
            dgvLayers.CellEndEdit += dgvLayers_CellEndEdit;
        }

        /// <summary>
        /// DataGridView를 layerBindingList로부터 새로고침
        /// </summary>
        private void RefreshGrid()
        {
            dgvLayers.Rows.Clear();
            for (int i = 0; i < layerBindingList.Count; i++)
            {
                var layer = layerBindingList[i];
                dgvLayers.Rows.Add((i + 1).ToString(), layer.Name, layer.ThicknessMm.ToString("F4"));
            }
        }

        // =============================================
        //  레이어 관리 버튼
        // =============================================

        private void btnAddLayer_Click(object sender, EventArgs e)
        {
            string name = "Layer_" + nextLayerNumber++;
            layerBindingList.Add(new LaminateLayerDefinition(name, 0.25));
            RefreshGrid();
            UpdateTotalThickness();

            // 새로 추가된 행 선택
            if (dgvLayers.Rows.Count > 0)
                dgvLayers.CurrentCell = dgvLayers.Rows[dgvLayers.Rows.Count - 1].Cells[1];
        }

        private void btnRemoveLayer_Click(object sender, EventArgs e)
        {
            if (dgvLayers.SelectedRows.Count == 0) return;
            if (layerBindingList.Count <= 1)
            {
                ValidationHelper.ShowError("최소 1개의 레이어가 필요합니다.", "오류");
                return;
            }

            int idx = dgvLayers.SelectedRows[0].Index;
            if (idx >= 0 && idx < layerBindingList.Count)
            {
                layerBindingList.RemoveAt(idx);
                RefreshGrid();
                UpdateTotalThickness();
            }
        }

        private void btnMoveUp_Click(object sender, EventArgs e)
        {
            if (dgvLayers.SelectedRows.Count == 0) return;
            int idx = dgvLayers.SelectedRows[0].Index;
            if (idx <= 0) return;

            var item = layerBindingList[idx];
            layerBindingList.RemoveAt(idx);
            layerBindingList.Insert(idx - 1, item);
            RefreshGrid();

            dgvLayers.ClearSelection();
            dgvLayers.Rows[idx - 1].Selected = true;
            dgvLayers.CurrentCell = dgvLayers.Rows[idx - 1].Cells[1];
        }

        private void btnMoveDown_Click(object sender, EventArgs e)
        {
            if (dgvLayers.SelectedRows.Count == 0) return;
            int idx = dgvLayers.SelectedRows[0].Index;
            if (idx >= layerBindingList.Count - 1) return;

            var item = layerBindingList[idx];
            layerBindingList.RemoveAt(idx);
            layerBindingList.Insert(idx + 1, item);
            RefreshGrid();

            dgvLayers.ClearSelection();
            dgvLayers.Rows[idx + 1].Selected = true;
            dgvLayers.CurrentCell = dgvLayers.Rows[idx + 1].Cells[1];
        }

        // =============================================
        //  그리드 셀 편집 이벤트
        // =============================================

        private void dgvLayers_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            SyncGridToBindingList();
            UpdateTotalThickness();
        }

        private void dgvLayers_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            SyncGridToBindingList();
            UpdateTotalThickness();
        }

        /// <summary>
        /// DataGridView 내용을 BindingList에 동기화
        /// </summary>
        private void SyncGridToBindingList()
        {
            for (int i = 0; i < dgvLayers.Rows.Count && i < layerBindingList.Count; i++)
            {
                var row = dgvLayers.Rows[i];
                var layer = layerBindingList[i];

                // 이름
                string name = row.Cells[1].Value?.ToString();
                if (!string.IsNullOrEmpty(name))
                    layer.Name = name;

                // 두께
                string thicknessStr = row.Cells[2].Value?.ToString();
                double thickness;
                if (double.TryParse(thicknessStr, out thickness) && thickness > 0)
                    layer.ThicknessMm = thickness;
            }
        }

        private void UpdateTotalThickness()
        {
            double total = layerBindingList.Sum(l => l.ThicknessMm);
            lblTotalThickness.Text = string.Format("총 두께: {0:F4} mm  /  총 {1}층",
                total, layerBindingList.Count);
        }

        // =============================================
        //  파라미터 읽기 / 검증
        // =============================================

        private RectangularLaminateParameters ReadRectangularParams()
        {
            SyncGridToBindingList();

            var p = new RectangularLaminateParameters();
            p.WidthMm = (double)numWidth.Value;
            p.LengthMm = (double)numLength.Value;
            p.Direction = (StackingDirection)cmbStackDir.SelectedIndex;
            p.EnableShareTopology = chkShareTopology.Checked;
            p.CreateInterfaceNamedSelections = chkInterfaceNS.Checked;

            p.Layers = new List<LaminateLayerDefinition>();
            foreach (var layer in layerBindingList)
            {
                p.Layers.Add(new LaminateLayerDefinition(layer.Name, layer.ThicknessMm));
            }

            return p;
        }

        private SurfaceLaminateParameters ReadSurfaceParams()
        {
            SyncGridToBindingList();

            var p = new SurfaceLaminateParameters();
            p.Direction = (OffsetDirection)cmbOffsetDir.SelectedIndex;
            p.CreateInterfaceNamedSelections = chkInterfaceNS.Checked;

            p.Layers = new List<LaminateLayerDefinition>();
            foreach (var layer in layerBindingList)
            {
                p.Layers.Add(new LaminateLayerDefinition(layer.Name, layer.ThicknessMm));
            }

            return p;
        }

        private bool ValidateInputs()
        {
            string errorMessage;

            if (IsRectangularMode)
            {
                var p = ReadRectangularParams();
                if (!p.Validate(out errorMessage))
                {
                    ValidationHelper.ShowError(errorMessage, "입력 오류");
                    return false;
                }
            }
            else
            {
                // 면 선택 확인
                if (selectedFace == null)
                {
                    ValidationHelper.ShowError(
                        "면이 선택되지 않았습니다.\n" +
                        "SpaceClaim에서 평면을 선택한 후 \"면 선택 (Pick)\" 버튼을 눌러주세요.",
                        "입력 오류");
                    return false;
                }

                var p = ReadSurfaceParams();
                if (!p.Validate(out errorMessage))
                {
                    ValidationHelper.ShowError(errorMessage, "입력 오류");
                    return false;
                }
            }

            return true;
        }

        // =============================================
        //  미리보기 / 생성 / 취소
        // =============================================

        private void btnPreview_Click(object sender, EventArgs e)
        {
            if (!ValidateInputs()) return;

            try
            {
                CleanupPreview();

                // WriteBlock 밖에서 UI 파라미터 읽기
                bool isRect = IsRectangularMode;
                RectangularLaminateParameters rectParams = isRect ? ReadRectangularParams() : null;
                SurfaceLaminateParameters surfParams = isRect ? null : ReadSurfaceParams();
                DesignFace face = selectedFace;

                WriteBlock.ExecuteTask("Laminate Preview", () =>
                {
                    List<DesignBody> bodies;

                    if (isRect)
                    {
                        bodies = rectService.CreateRectangularLaminate(activePart, rectParams);
                    }
                    else
                    {
                        bodies = surfaceService.CreateSurfaceLaminate(activePart, face, surfParams);
                    }

                    previewBodies.AddRange(bodies);
                });
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    $"미리보기 생성 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "미리보기 오류");
            }
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            if (!ValidateInputs()) return;

            try
            {
                if (previewBodies.Count > 0)
                {
                    // 미리보기가 있으면 확정
                    previewBodies.Clear();
                }
                else
                {
                    // WriteBlock 밖에서 UI 파라미터 읽기
                    bool isRect = IsRectangularMode;
                    RectangularLaminateParameters rectParams = isRect ? ReadRectangularParams() : null;
                    SurfaceLaminateParameters surfParams = isRect ? null : ReadSurfaceParams();
                    DesignFace face = selectedFace;

                    WriteBlock.ExecuteTask("Create Laminate", () =>
                    {
                        if (isRect)
                        {
                            rectService.CreateRectangularLaminate(activePart, rectParams);
                        }
                        else
                        {
                            surfaceService.CreateSurfaceLaminate(activePart, face, surfParams);
                        }
                    });
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    $"적층 모델 생성 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "생성 오류");
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        // =============================================
        //  미리보기 정리
        // =============================================

        private void CleanupPreview()
        {
            if (previewBodies.Count > 0)
            {
                try
                {
                    WriteBlock.ExecuteTask("Cleanup Laminate Preview", () =>
                    {
                        foreach (var body in previewBodies)
                        {
                            if (body != null)
                                body.Delete();
                        }
                    });
                    previewBodies.Clear();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Laminate preview cleanup error: {ex.Message}");
                }
            }
        }

        private void LaminateDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (DialogResult != DialogResult.OK)
            {
                CleanupPreview();
            }
        }
    }
}
