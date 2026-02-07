using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Load;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Load;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    public class LoadDefinitionDialog : Form
    {
        // ─── Left: Common controls ───
        private ComboBox cmbGroup;
        private TextBox txtName;
        private TextBox txtEndTime;
        private TextBox txtDt;
        private RadioButton rbExpression;
        private RadioButton rbTabular;
        private RadioButton rbPwm;
        private Button btnGenerate;

        // ─── Expression / Tabular inputs ───
        private TextBox txtExpression;
        private TextBox txtExprHelp;
        private TextBox txtPasteData;

        // ─── PWM inputs ───
        private System.Windows.Forms.Panel pnlPwmSettings;
        private TextBox txtCarrierFreq;
        private TextBox txtOutputAmp;
        private CheckBox chkBipolar;
        private TextBox txtTargetFreq;
        private Button btnOptimize;
        private DataGridView dgvHarmonics;
        private Button btnAddHarmonic;
        private Button btnRemoveHarmonic;

        // ─── Graph panels ───
        private System.Windows.Forms.Panel pnlTimeHistory;
        private System.Windows.Forms.Panel pnlFFT;

        // ─── Right: Load list ───
        private ListBox lstLoads;
        private Button btnAdd;
        private Button btnUpdate;
        private Button btnDelete;
        private Button btnClose;

        // ─── Graph data ───
        private double[] _graphTimeX;
        private double[] _graphTimeY;       // primary (PWM output or amplitude)
        private double[] _graphTargetY;     // secondary (PWM target sine, null if non-PWM)
        private double[] _graphFftX;
        private double[] _graphFftY;

        // ─── Zoom state (X-axis drag) ───
        private bool _isDragging;
        private int _dragStartX;
        private int _dragEndX;
        private double _zoomXMin = double.NaN;
        private double _zoomXMax = double.NaN;

        private const int LeftW = 680;
        private const int RightX = 700;
        private const int RightW = 360;

        // Layout Y positions for input area
        private int _inputAreaY;

        public LoadDefinitionDialog()
        {
            InitializeLayout();
            LoadGroups();
            RefreshLoadList();
        }

        private void InitializeLayout()
        {
            Text = "Load Definition (하중 정의)";
            Width = 1100;
            Height = 790;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;

            var font = new Font("Segoe UI", 9f);
            var monoFont = new Font("Consolas", 9f);

            int y = 12;

            // ═══ Row 1: Named Selection + Load Name ═══
            var lblGroup = new Label
            {
                Text = "Named Selection:",
                Location = new Point(12, y + 3),
                AutoSize = true,
                Font = font
            };

            cmbGroup = new ComboBox
            {
                Location = new Point(130, y),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDown,
                Font = font
            };

            var lblName = new Label
            {
                Text = "Load Name:",
                Location = new Point(350, y + 3),
                AutoSize = true,
                Font = font
            };

            txtName = new TextBox
            {
                Location = new Point(440, y),
                Width = 220,
                Font = font,
                Text = "Load_1"
            };

            y += 30;

            // ═══ Row 2: End Time + dt ═══
            var lblEndTime = new Label
            {
                Text = "End Time (s):",
                Location = new Point(12, y + 3),
                AutoSize = true,
                Font = font
            };

            txtEndTime = new TextBox
            {
                Location = new Point(130, y),
                Width = 100,
                Font = font,
                Text = "0.01"
            };

            var lblDt = new Label
            {
                Text = "dt (s):",
                Location = new Point(250, y + 3),
                AutoSize = true,
                Font = font
            };

            txtDt = new TextBox
            {
                Location = new Point(300, y),
                Width = 100,
                Font = font,
                Text = "0.0001"
            };

            y += 30;

            // ═══ Row 3: Input mode ═══
            rbExpression = new RadioButton
            {
                Text = "Expression",
                Location = new Point(12, y),
                AutoSize = true,
                Font = font,
                Checked = true
            };
            rbExpression.CheckedChanged += InputModeChanged;

            rbTabular = new RadioButton
            {
                Text = "Tabular",
                Location = new Point(130, y),
                AutoSize = true,
                Font = font
            };
            rbTabular.CheckedChanged += InputModeChanged;

            rbPwm = new RadioButton
            {
                Text = "PWM Generator",
                Location = new Point(230, y),
                AutoSize = true,
                Font = font
            };
            rbPwm.CheckedChanged += InputModeChanged;

            btnGenerate = new Button
            {
                Text = "Generate",
                Location = new Point(564, y - 2),
                Size = new Size(100, 26),
                Font = font
            };
            btnGenerate.Click += BtnGenerate_Click;

            y += 28;
            _inputAreaY = y;

            // ═══ Expression input ═══
            txtExpression = new TextBox
            {
                Location = new Point(12, y),
                Width = 540,
                Font = new Font("Consolas", 9.5f),
                Text = "1000*sin(2*pi*100*t)"
            };

            txtExprHelp = new TextBox
            {
                Location = new Point(12, y + 24),
                Size = new Size(652, 130),
                Font = new Font("Consolas", 8f),
                ForeColor = Color.FromArgb(80, 80, 80),
                BackColor = Color.FromArgb(248, 248, 248),
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                TabStop = false,
                Text =
                    "[수학] sin cos tan exp log sqrt abs asin acos atan ceil floor  min(a,b) max(a,b) pow(a,b)\r\n"
                  + "\r\n"
                  + "[파형 - 2\u03C0 주기]  sin처럼 사용, 주파수는 인자로 조절\r\n"
                  + "  square(x [,duty])   사각파  duty=ON비율 0~1 (기본0.5)     Ex) 1000*square(2*pi*50*t, 0.3)\r\n"
                  + "  saw(x)              톱니파                                Ex) 1000*saw(2*pi*100*t)\r\n"
                  + "  tri(x)              삼각파                                Ex) 1000*tri(2*pi*100*t)\r\n"
                  + "\r\n"
                  + "[파형 - 초 단위]  period=주기(초), duty=ON비율, delay=시작지연(초)\r\n"
                  + "  square(t, period, duty [,delay])       Ex) 1000*square(t, 0.02, 0.5)       \u2192 50Hz, duty50%\r\n"
                  + "  saw(t, period [,delay])                Ex) 1000*saw(t, 0.01)                \u2192 100Hz 톱니파\r\n"
                  + "  tri(t, period [,delay])                Ex) 1000*tri(t, 0.01, 0.002)         \u2192 100Hz 삼각파, 2ms 지연\r\n"
                  + "\r\n"
                  + "[펄스/계단]  비주기 단발 신호\r\n"
                  + "  pulse(t, start, width)   start~start+width 구간만 1      Ex) 500*pulse(t, 0.001, 0.003)\r\n"
                  + "  step(t [,t0])            t\u2265t0 이면 1, 아니면 0            Ex) 1000*step(t, 0.005)\r\n"
                  + "\r\n"
                  + "[상수] pi, e   [변수] t   [연산] + - * / ^   Ex) 1000*square(t,0.02,0.3)*step(t,0.001)*(1-step(t,0.01))"
            };

            // ═══ Tabular input ═══
            txtPasteData = new TextBox
            {
                Location = new Point(12, y),
                Size = new Size(540, 80),
                Font = monoFont,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Visible = false,
                AcceptsReturn = true
            };

            // ═══ PWM settings panel ═══
            pnlPwmSettings = new System.Windows.Forms.Panel
            {
                Location = new Point(12, y),
                Size = new Size(652, 178),
                Visible = false
            };
            BuildPwmSettingsPanel(font, monoFont);

            y += 184;

            // ═══ Time History graph ═══
            var lblTimeGraph = new Label
            {
                Text = "Time History  (drag to zoom X, double-click to reset)",
                Location = new Point(12, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };

            y += 18;

            pnlTimeHistory = new System.Windows.Forms.Panel
            {
                Location = new Point(12, y),
                Size = new Size(LeftW - 30, 200),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };
            pnlTimeHistory.Paint += PnlTimeHistory_Paint;
            pnlTimeHistory.MouseDown += PnlTimeHistory_MouseDown;
            pnlTimeHistory.MouseMove += PnlTimeHistory_MouseMove;
            pnlTimeHistory.MouseUp += PnlTimeHistory_MouseUp;
            pnlTimeHistory.DoubleClick += PnlTimeHistory_DoubleClick;

            y += 206;

            // ═══ FFT graph ═══
            var lblFftGraph = new Label
            {
                Text = "FFT Spectrum",
                Location = new Point(12, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };

            y += 18;

            pnlFFT = new System.Windows.Forms.Panel
            {
                Location = new Point(12, y),
                Size = new Size(LeftW - 30, 200),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };
            pnlFFT.Paint += PnlFFT_Paint;

            // ═══ Right side: Load list ═══
            int ry = 12;

            var lblList = new Label
            {
                Text = "Defined Loads (정의된 하중)",
                Location = new Point(RightX, ry),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };

            ry += 22;

            lstLoads = new ListBox
            {
                Location = new Point(RightX, ry),
                Size = new Size(RightW, 500),
                Font = font
            };
            lstLoads.SelectedIndexChanged += LstLoads_SelectedIndexChanged;

            ry += 508;

            btnAdd = new Button
            {
                Text = "Add",
                Location = new Point(RightX, ry),
                Size = new Size(80, 30),
                Font = font
            };
            btnAdd.Click += BtnAdd_Click;

            btnUpdate = new Button
            {
                Text = "Update",
                Location = new Point(RightX + 90, ry),
                Size = new Size(80, 30),
                Font = font
            };
            btnUpdate.Click += BtnUpdate_Click;

            btnDelete = new Button
            {
                Text = "Delete",
                Location = new Point(RightX + 180, ry),
                Size = new Size(80, 30),
                Font = font
            };
            btnDelete.Click += BtnDelete_Click;

            ry += 40;

            btnClose = new Button
            {
                Text = "Close",
                Location = new Point(RightX + 280, ry),
                Size = new Size(80, 30),
                Font = font
            };
            btnClose.Click += (s, e) => Close();

            // ═══ Add all controls ═══
            Controls.AddRange(new Control[]
            {
                lblGroup, cmbGroup, lblName, txtName,
                lblEndTime, txtEndTime, lblDt, txtDt,
                rbExpression, rbTabular, rbPwm, btnGenerate,
                txtExpression, txtExprHelp, txtPasteData, pnlPwmSettings,
                lblTimeGraph, pnlTimeHistory,
                lblFftGraph, pnlFFT,
                lblList, lstLoads,
                btnAdd, btnUpdate, btnDelete, btnClose
            });
        }

        // ═══════════════════════════════════════
        //  PWM Settings Panel
        // ═══════════════════════════════════════

        private void BuildPwmSettingsPanel(Font font, Font monoFont)
        {
            int y = 0;

            // Row 1: Carrier freq + Output amp + Bipolar
            var lblCarrier = new Label
            {
                Text = "Carrier (Hz):",
                Location = new Point(0, y + 3),
                AutoSize = true,
                Font = font
            };

            txtCarrierFreq = new TextBox
            {
                Location = new Point(90, y),
                Width = 80,
                Font = monoFont,
                Text = "10000"
            };

            var lblOutAmp = new Label
            {
                Text = "Output Amp:",
                Location = new Point(185, y + 3),
                AutoSize = true,
                Font = font
            };

            txtOutputAmp = new TextBox
            {
                Location = new Point(275, y),
                Width = 80,
                Font = monoFont,
                Text = "1000"
            };

            chkBipolar = new CheckBox
            {
                Text = "Bipolar (+/-)",
                Location = new Point(375, y + 2),
                AutoSize = true,
                Font = font,
                Checked = true
            };

            y += 28;

            // Row 2: Target frequency + Optimize
            var lblTarget = new Label
            {
                Text = "Target (Hz):",
                Location = new Point(0, y + 3),
                AutoSize = true,
                Font = font
            };

            txtTargetFreq = new TextBox
            {
                Location = new Point(90, y),
                Width = 80,
                Font = monoFont,
                Text = "50"
            };

            btnOptimize = new Button
            {
                Text = "Optimize",
                Location = new Point(185, y - 1),
                Size = new Size(90, 24),
                Font = font
            };
            btnOptimize.Click += BtnOptimize_Click;

            y += 28;

            // Row 3: Harmonics table
            var lblHarmonics = new Label
            {
                Text = "Source Harmonics:",
                Location = new Point(0, y + 3),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };

            btnAddHarmonic = new Button
            {
                Text = "+",
                Location = new Point(130, y),
                Size = new Size(28, 22),
                Font = font
            };
            btnAddHarmonic.Click += BtnAddHarmonic_Click;

            btnRemoveHarmonic = new Button
            {
                Text = "-",
                Location = new Point(162, y),
                Size = new Size(28, 22),
                Font = font
            };
            btnRemoveHarmonic.Click += BtnRemoveHarmonic_Click;

            y += 24;

            dgvHarmonics = new DataGridView
            {
                Location = new Point(0, y),
                Size = new Size(640, 95),
                Font = monoFont,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                ScrollBars = ScrollBars.Vertical
            };
            dgvHarmonics.DefaultCellStyle.SelectionBackColor = Color.LightSteelBlue;
            dgvHarmonics.DefaultCellStyle.SelectionForeColor = Color.Black;

            dgvHarmonics.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colFreq",
                HeaderText = "Frequency (Hz)",
                Width = 140
            });
            dgvHarmonics.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colAmp",
                HeaderText = "Amplitude",
                Width = 140
            });
            dgvHarmonics.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPhase",
                HeaderText = "Phase (deg)",
                Width = 120
            });

            // Default harmonics
            dgvHarmonics.Rows.Add("50", "1000", "0");
            dgvHarmonics.Rows.Add("150", "200", "0");

            pnlPwmSettings.Controls.AddRange(new Control[]
            {
                lblCarrier, txtCarrierFreq,
                lblOutAmp, txtOutputAmp,
                chkBipolar,
                lblTarget, txtTargetFreq, btnOptimize,
                lblHarmonics, btnAddHarmonic, btnRemoveHarmonic,
                dgvHarmonics
            });
        }

        private void BtnAddHarmonic_Click(object sender, EventArgs e)
        {
            dgvHarmonics.Rows.Add("100", "500", "0");
        }

        private void BtnRemoveHarmonic_Click(object sender, EventArgs e)
        {
            if (dgvHarmonics.SelectedRows.Count > 0 && dgvHarmonics.Rows.Count > 1)
                dgvHarmonics.Rows.Remove(dgvHarmonics.SelectedRows[0]);
        }

        private void BtnOptimize_Click(object sender, EventArgs e)
        {
            try
            {
                var ld = BuildLoadDefinition();

                double targetFreq;
                if (!double.TryParse(txtTargetFreq.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out targetFreq) || targetFreq <= 0)
                    throw new ArgumentException("Target frequency must be positive.");
                ld.PwmTargetFrequency = targetFreq;

                int nHarmonics = dgvHarmonics.Rows.Count;
                if (nHarmonics < 1) nHarmonics = 1;

                btnOptimize.Enabled = false;
                btnOptimize.Text = "...";
                System.Windows.Forms.Application.DoEvents();

                LoadService.OptimizePwmHarmonics(ld, nHarmonics);

                // Update DataGridView with optimized harmonics
                dgvHarmonics.Rows.Clear();
                foreach (var h in ld.PwmHarmonics)
                {
                    dgvHarmonics.Rows.Add(
                        h.Frequency.ToString("F2", CultureInfo.InvariantCulture),
                        h.Amplitude.ToString("F2", CultureInfo.InvariantCulture),
                        h.Phase.ToString("F1", CultureInfo.InvariantCulture));
                }

                // Update graphs
                _graphTimeX = ld.ComputedTime;
                _graphTimeY = ld.ComputedAmplitude;
                _graphTargetY = ld.ComputedTarget;
                _graphFftX = ld.FftFrequency;
                _graphFftY = ld.FftMagnitude;
                _zoomXMin = double.NaN;
                _zoomXMax = double.NaN;
                pnlTimeHistory.Invalidate();
                pnlFFT.Invalidate();
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError("Optimize failed: " + ex.Message, "Error");
            }
            finally
            {
                btnOptimize.Enabled = true;
                btnOptimize.Text = "Optimize";
            }
        }

        // ─── Groups ───

        private void LoadGroups()
        {
            cmbGroup.Items.Clear();
            try
            {
                var window = Window.ActiveWindow;
                if (window != null)
                {
                    foreach (Group group in window.Groups)
                        cmbGroup.Items.Add(group.Name);
                }
            }
            catch { }

            if (cmbGroup.Items.Count > 0)
                cmbGroup.SelectedIndex = 0;
        }

        // ─── Input mode toggle ───

        private void InputModeChanged(object sender, EventArgs e)
        {
            bool expr = rbExpression.Checked;
            bool tab = rbTabular.Checked;
            bool pwm = rbPwm.Checked;

            txtExpression.Visible = expr;
            txtExprHelp.Visible = expr;
            txtPasteData.Visible = tab;
            pnlPwmSettings.Visible = pwm;

            // Reset zoom on mode change
            _zoomXMin = double.NaN;
            _zoomXMax = double.NaN;
        }

        // ─── Generate ───

        private void BtnGenerate_Click(object sender, EventArgs e)
        {
            try
            {
                var ld = BuildLoadDefinition();
                LoadService.GenerateTimeSeries(ld);
                LoadService.ComputeFFT(ld);

                _graphTimeX = ld.ComputedTime;
                _graphTimeY = ld.ComputedAmplitude;
                _graphTargetY = ld.ComputedTarget;
                _graphFftX = ld.FftFrequency;
                _graphFftY = ld.FftMagnitude;

                // Reset zoom
                _zoomXMin = double.NaN;
                _zoomXMax = double.NaN;

                pnlTimeHistory.Invalidate();
                pnlFFT.Invalidate();
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError("Generate failed: " + ex.Message, "Error");
            }
        }

        private LoadDefinition BuildLoadDefinition()
        {
            var ld = new LoadDefinition();
            ld.Name = txtName.Text.Trim();
            ld.GroupName = cmbGroup.Text.Trim();

            double endTime, dt;
            if (!double.TryParse(txtEndTime.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out endTime) || endTime <= 0)
                throw new ArgumentException("End Time must be a positive number.");
            if (!double.TryParse(txtDt.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out dt) || dt <= 0)
                throw new ArgumentException("dt must be a positive number.");

            ld.EndTime = endTime;
            ld.DeltaTime = dt;

            if (rbExpression.Checked)
            {
                ld.InputMode = LoadInputMode.Expression;
                ld.Expression = txtExpression.Text.Trim();
                if (string.IsNullOrEmpty(ld.Expression))
                    throw new ArgumentException("Expression cannot be empty.");
            }
            else if (rbTabular.Checked)
            {
                ld.InputMode = LoadInputMode.Tabular;
                double[] tVals, aVals;
                string parseErr;
                if (!LoadService.ParseTabularData(txtPasteData.Text, out tVals, out aVals, out parseErr))
                    throw new ArgumentException("Tabular data error: " + parseErr);
                ld.TimeValues = tVals;
                ld.AmplitudeValues = aVals;
            }
            else if (rbPwm.Checked)
            {
                ld.InputMode = LoadInputMode.Pwm;

                double carrierFreq;
                if (!double.TryParse(txtCarrierFreq.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out carrierFreq) || carrierFreq <= 0)
                    throw new ArgumentException("Carrier frequency must be positive.");
                ld.PwmCarrierFrequency = carrierFreq;

                double outAmp;
                if (!double.TryParse(txtOutputAmp.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out outAmp))
                    throw new ArgumentException("Output amplitude is invalid.");
                ld.PwmOutputAmplitude = outAmp;
                ld.PwmBipolar = chkBipolar.Checked;

                double tgtFreq;
                if (double.TryParse(txtTargetFreq.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out tgtFreq))
                    ld.PwmTargetFrequency = tgtFreq;

                // Read harmonics from DataGridView
                ld.PwmHarmonics = new List<PwmHarmonic>();
                foreach (DataGridViewRow row in dgvHarmonics.Rows)
                {
                    double freq, amp, phase;
                    string sFreq = (row.Cells["colFreq"].Value ?? "").ToString().Trim();
                    string sAmp = (row.Cells["colAmp"].Value ?? "").ToString().Trim();
                    string sPhase = (row.Cells["colPhase"].Value ?? "").ToString().Trim();

                    if (!double.TryParse(sFreq, NumberStyles.Float, CultureInfo.InvariantCulture, out freq))
                        throw new ArgumentException("Invalid frequency in row " + (row.Index + 1));
                    if (!double.TryParse(sAmp, NumberStyles.Float, CultureInfo.InvariantCulture, out amp))
                        throw new ArgumentException("Invalid amplitude in row " + (row.Index + 1));
                    if (!double.TryParse(sPhase, NumberStyles.Float, CultureInfo.InvariantCulture, out phase))
                        phase = 0;

                    ld.PwmHarmonics.Add(new PwmHarmonic(freq, amp, phase));
                }

                if (ld.PwmHarmonics.Count == 0)
                    throw new ArgumentException("At least one harmonic is required.");
            }

            return ld;
        }

        // ─── Add / Update / Delete ───

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            try
            {
                var ld = BuildLoadDefinition();
                if (string.IsNullOrEmpty(ld.Name))
                {
                    ValidationHelper.ShowError("Load Name is required.", "Error");
                    return;
                }

                LoadService.GenerateTimeSeries(ld);
                LoadService.ComputeFFT(ld);
                LoadService.Add(ld);

                RefreshLoadList();
                lstLoads.SelectedIndex = lstLoads.Items.Count - 1;
                UpdateGraphFromLoad(ld);
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError("Add failed: " + ex.Message, "Error");
            }
        }

        private void BtnUpdate_Click(object sender, EventArgs e)
        {
            int idx = lstLoads.SelectedIndex;
            if (idx < 0)
            {
                ValidationHelper.ShowError("Select a load to update.", "Error");
                return;
            }

            try
            {
                var ld = BuildLoadDefinition();
                LoadService.GenerateTimeSeries(ld);
                LoadService.ComputeFFT(ld);
                LoadService.Update(idx, ld);

                RefreshLoadList();
                if (idx < lstLoads.Items.Count)
                    lstLoads.SelectedIndex = idx;
                UpdateGraphFromLoad(ld);
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError("Update failed: " + ex.Message, "Error");
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            int idx = lstLoads.SelectedIndex;
            if (idx < 0) return;

            LoadService.RemoveAt(idx);
            RefreshLoadList();

            _graphTimeX = null; _graphTimeY = null; _graphTargetY = null;
            _graphFftX = null; _graphFftY = null;
            pnlTimeHistory.Invalidate();
            pnlFFT.Invalidate();
        }

        private void LstLoads_SelectedIndexChanged(object sender, EventArgs e)
        {
            int idx = lstLoads.SelectedIndex;
            if (idx < 0 || idx >= LoadService.Count) return;

            var ld = LoadService.Get(idx);
            LoadToUI(ld);
            UpdateGraphFromLoad(ld);
        }

        private void RefreshLoadList()
        {
            lstLoads.Items.Clear();
            var all = LoadService.GetAll();
            foreach (var ld in all)
            {
                string mode = ld.InputMode == LoadInputMode.Pwm ? "PWM" :
                              ld.InputMode == LoadInputMode.Tabular ? "Tab" : "Expr";
                string display = string.Format("{0}  [{1}]  ({2})", ld.Name, ld.GroupName, mode);
                lstLoads.Items.Add(display);
            }
        }

        private void LoadToUI(LoadDefinition ld)
        {
            txtName.Text = ld.Name ?? "";
            cmbGroup.Text = ld.GroupName ?? "";
            txtEndTime.Text = ld.EndTime.ToString(CultureInfo.InvariantCulture);
            txtDt.Text = ld.DeltaTime.ToString(CultureInfo.InvariantCulture);

            if (ld.InputMode == LoadInputMode.Expression)
            {
                rbExpression.Checked = true;
                txtExpression.Text = ld.Expression ?? "";
            }
            else if (ld.InputMode == LoadInputMode.Tabular)
            {
                rbTabular.Checked = true;
                if (ld.TimeValues != null && ld.AmplitudeValues != null)
                {
                    var sb = new System.Text.StringBuilder();
                    int n = Math.Min(ld.TimeValues.Length, ld.AmplitudeValues.Length);
                    for (int i = 0; i < n; i++)
                        sb.AppendFormat("{0}\t{1}\r\n",
                            ld.TimeValues[i].ToString(CultureInfo.InvariantCulture),
                            ld.AmplitudeValues[i].ToString(CultureInfo.InvariantCulture));
                    txtPasteData.Text = sb.ToString();
                }
            }
            else if (ld.InputMode == LoadInputMode.Pwm)
            {
                rbPwm.Checked = true;
                txtCarrierFreq.Text = ld.PwmCarrierFrequency.ToString(CultureInfo.InvariantCulture);
                txtOutputAmp.Text = ld.PwmOutputAmplitude.ToString(CultureInfo.InvariantCulture);
                chkBipolar.Checked = ld.PwmBipolar;
                txtTargetFreq.Text = ld.PwmTargetFrequency.ToString(CultureInfo.InvariantCulture);

                dgvHarmonics.Rows.Clear();
                if (ld.PwmHarmonics != null)
                {
                    foreach (var h in ld.PwmHarmonics)
                    {
                        dgvHarmonics.Rows.Add(
                            h.Frequency.ToString(CultureInfo.InvariantCulture),
                            h.Amplitude.ToString(CultureInfo.InvariantCulture),
                            h.Phase.ToString(CultureInfo.InvariantCulture));
                    }
                }
            }
        }

        private void UpdateGraphFromLoad(LoadDefinition ld)
        {
            _graphTimeX = ld.ComputedTime;
            _graphTimeY = ld.ComputedAmplitude;
            _graphTargetY = ld.ComputedTarget;
            _graphFftX = ld.FftFrequency;
            _graphFftY = ld.FftMagnitude;
            _zoomXMin = double.NaN;
            _zoomXMax = double.NaN;
            pnlTimeHistory.Invalidate();
            pnlFFT.Invalidate();
        }

        // ═══════════════════════════════════════
        //  Drag Zoom (Time History panel)
        // ═══════════════════════════════════════

        private void PnlTimeHistory_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = true;
                _dragStartX = e.X;
                _dragEndX = e.X;
            }
        }

        private void PnlTimeHistory_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                _dragEndX = e.X;
                pnlTimeHistory.Invalidate();
            }
        }

        private void PnlTimeHistory_MouseUp(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;
            _isDragging = false;

            int dx = Math.Abs(_dragEndX - _dragStartX);
            if (dx < 5) return; // too small, ignore

            // Convert pixel range to data range
            int ml = 65, mr = 15;
            int plotW = pnlTimeHistory.ClientSize.Width - ml - mr;
            if (plotW < 10 || _graphTimeX == null || _graphTimeX.Length < 2) return;

            // Current X range (considering existing zoom)
            double dataXMin, dataXMax;
            GetTimeXRange(out dataXMin, out dataXMax);
            double xRange = dataXMax - dataXMin;
            if (xRange <= 0) return;

            int pxLeft = Math.Min(_dragStartX, _dragEndX);
            int pxRight = Math.Max(_dragStartX, _dragEndX);

            double newXMin = dataXMin + xRange * (pxLeft - ml) / plotW;
            double newXMax = dataXMin + xRange * (pxRight - ml) / plotW;

            if (newXMax > newXMin)
            {
                _zoomXMin = newXMin;
                _zoomXMax = newXMax;
                pnlTimeHistory.Invalidate();
            }
        }

        private void PnlTimeHistory_DoubleClick(object sender, EventArgs e)
        {
            // Reset zoom
            _zoomXMin = double.NaN;
            _zoomXMax = double.NaN;
            pnlTimeHistory.Invalidate();
        }

        private void GetTimeXRange(out double xMin, out double xMax)
        {
            if (!double.IsNaN(_zoomXMin) && !double.IsNaN(_zoomXMax))
            {
                xMin = _zoomXMin;
                xMax = _zoomXMax;
                return;
            }

            xMin = 0; xMax = 1;
            if (_graphTimeX != null && _graphTimeX.Length >= 2)
            {
                xMin = _graphTimeX[0];
                xMax = _graphTimeX[_graphTimeX.Length - 1];
            }
        }

        // ═══════════════════════════════════════
        //  Graph Rendering
        // ═══════════════════════════════════════

        private void PnlTimeHistory_Paint(object sender, PaintEventArgs e)
        {
            double xMin, xMax;
            GetTimeXRange(out xMin, out xMax);

            PaintGraphDual(e.Graphics, pnlTimeHistory.ClientRectangle,
                _graphTimeX, _graphTimeY, _graphTargetY,
                xMin, xMax,
                "Time (s)", "Amplitude",
                Color.RoyalBlue, Color.FromArgb(180, 220, 60, 60));

            // Draw drag selection rectangle
            if (_isDragging)
            {
                int left = Math.Min(_dragStartX, _dragEndX);
                int right = Math.Max(_dragStartX, _dragEndX);
                using (var brush = new SolidBrush(Color.FromArgb(40, 70, 130, 230)))
                using (var pen = new Pen(Color.FromArgb(120, 70, 130, 230), 1))
                {
                    int mt = 12;
                    int plotH = pnlTimeHistory.ClientSize.Height - mt - 32;
                    e.Graphics.FillRectangle(brush, left, mt, right - left, plotH);
                    e.Graphics.DrawRectangle(pen, left, mt, right - left, plotH);
                }
            }
        }

        private void PnlFFT_Paint(object sender, PaintEventArgs e)
        {
            PaintGraphSingle(e.Graphics, pnlFFT.ClientRectangle,
                _graphFftX, _graphFftY,
                "Frequency (Hz)", "Magnitude", Color.OrangeRed);
        }

        /// <summary>
        /// Draw graph with optional secondary data series (for PWM target overlay)
        /// </summary>
        private void PaintGraphDual(Graphics g, Rectangle bounds,
            double[] xData, double[] yData, double[] y2Data,
            double forceXMin, double forceXMax,
            string xLabel, string yLabel,
            Color lineColor, Color line2Color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.White);

            var axisFont = new Font("Segoe UI", 7.5f);
            var labelFont = new Font("Segoe UI", 8f, FontStyle.Bold);
            var axisPen = new Pen(Color.DimGray, 1f);
            var gridPen = new Pen(Color.FromArgb(230, 230, 230), 1f);

            int ml = 65, mr = 15, mt = 12, mb = 32;
            int plotW = bounds.Width - ml - mr;
            int plotH = bounds.Height - mt - mb;

            if (plotW < 10 || plotH < 10) { axisFont.Dispose(); labelFont.Dispose(); axisPen.Dispose(); gridPen.Dispose(); return; }

            // Axis labels
            g.DrawString(xLabel, labelFont, Brushes.DimGray, ml + plotW / 2 - 30, bounds.Height - mb + 16);
            var state = g.Save();
            g.TranslateTransform(12, mt + plotH / 2);
            g.RotateTransform(-90);
            g.DrawString(yLabel, labelFont, Brushes.DimGray, -30, 0);
            g.Restore(state);

            if (xData == null || yData == null || xData.Length < 2)
            {
                g.DrawString("No data", axisFont, Brushes.Gray, ml + plotW / 2 - 20, mt + plotH / 2 - 6);
                g.DrawLine(axisPen, ml, mt, ml, mt + plotH);
                g.DrawLine(axisPen, ml, mt + plotH, ml + plotW, mt + plotH);
                axisFont.Dispose(); labelFont.Dispose(); axisPen.Dispose(); gridPen.Dispose();
                return;
            }

            double xMin = forceXMin, xMax = forceXMax;
            double xRange = xMax - xMin;
            if (xRange <= 0) xRange = 1;

            // Compute Y range from visible data
            double yMin = double.MaxValue, yMax = double.MinValue;
            int n = Math.Min(xData.Length, yData.Length);
            for (int i = 0; i < n; i++)
            {
                if (xData[i] < xMin || xData[i] > xMax) continue;
                if (yData[i] < yMin) yMin = yData[i];
                if (yData[i] > yMax) yMax = yData[i];
            }
            // Also consider y2 for Y range
            if (y2Data != null)
            {
                int n2 = Math.Min(xData.Length, y2Data.Length);
                for (int i = 0; i < n2; i++)
                {
                    if (xData[i] < xMin || xData[i] > xMax) continue;
                    if (y2Data[i] < yMin) yMin = y2Data[i];
                    if (y2Data[i] > yMax) yMax = y2Data[i];
                }
            }

            if (yMin == double.MaxValue) { yMin = -1; yMax = 1; }
            double yRange = yMax - yMin;
            if (yRange == 0) { yRange = 1; yMin -= 0.5; yMax += 0.5; }
            else { yMin -= yRange * 0.08; yMax += yRange * 0.08; yRange = yMax - yMin; }

            // Grid + ticks
            DrawGridAndTicks(g, ml, mt, plotW, plotH, xMin, xRange, yMin, yRange, axisFont, axisPen, gridPen);

            // Draw secondary line first (target sine) - thinner, behind
            if (y2Data != null)
            {
                DrawDataLine(g, xData, y2Data, xMin, xRange, yMin, yRange, ml, mt, plotW, plotH, line2Color, 1.2f);
            }

            // Draw primary line (PWM output or amplitude)
            DrawDataLine(g, xData, yData, xMin, xRange, yMin, yRange, ml, mt, plotW, plotH, lineColor, 1.5f);

            // Legend if dual
            if (y2Data != null)
            {
                int lx = ml + plotW - 160;
                int ly = mt + 4;
                using (var p1 = new Pen(lineColor, 2f))
                using (var p2 = new Pen(line2Color, 2f))
                {
                    g.DrawLine(p1, lx, ly + 6, lx + 20, ly + 6);
                    g.DrawString("PWM Output", axisFont, Brushes.DimGray, lx + 24, ly);
                    g.DrawLine(p2, lx, ly + 18, lx + 20, ly + 18);
                    g.DrawString("Target Signal", axisFont, Brushes.DimGray, lx + 24, ly + 12);
                }
            }

            axisFont.Dispose(); labelFont.Dispose(); axisPen.Dispose(); gridPen.Dispose();
        }

        private void PaintGraphSingle(Graphics g, Rectangle bounds,
            double[] xData, double[] yData,
            string xLabel, string yLabel, Color lineColor)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.White);

            var axisFont = new Font("Segoe UI", 7.5f);
            var labelFont = new Font("Segoe UI", 8f, FontStyle.Bold);
            var axisPen = new Pen(Color.DimGray, 1f);
            var gridPen = new Pen(Color.FromArgb(230, 230, 230), 1f);

            int ml = 65, mr = 15, mt = 12, mb = 32;
            int plotW = bounds.Width - ml - mr;
            int plotH = bounds.Height - mt - mb;

            if (plotW < 10 || plotH < 10) { axisFont.Dispose(); labelFont.Dispose(); axisPen.Dispose(); gridPen.Dispose(); return; }

            g.DrawString(xLabel, labelFont, Brushes.DimGray, ml + plotW / 2 - 30, bounds.Height - mb + 16);
            var state = g.Save();
            g.TranslateTransform(12, mt + plotH / 2);
            g.RotateTransform(-90);
            g.DrawString(yLabel, labelFont, Brushes.DimGray, -30, 0);
            g.Restore(state);

            if (xData == null || yData == null || xData.Length < 2)
            {
                g.DrawString("No data", axisFont, Brushes.Gray, ml + plotW / 2 - 20, mt + plotH / 2 - 6);
                g.DrawLine(axisPen, ml, mt, ml, mt + plotH);
                g.DrawLine(axisPen, ml, mt + plotH, ml + plotW, mt + plotH);
                axisFont.Dispose(); labelFont.Dispose(); axisPen.Dispose(); gridPen.Dispose();
                return;
            }

            int n = Math.Min(xData.Length, yData.Length);
            double xMin = xData[0], xMax = xData[0];
            double yMin = yData[0], yMax = yData[0];
            for (int i = 1; i < n; i++)
            {
                if (xData[i] < xMin) xMin = xData[i];
                if (xData[i] > xMax) xMax = xData[i];
                if (yData[i] < yMin) yMin = yData[i];
                if (yData[i] > yMax) yMax = yData[i];
            }

            double xRange = xMax - xMin;
            double yRange = yMax - yMin;
            if (xRange == 0) xRange = 1;
            if (yRange == 0) { yRange = 1; yMin -= 0.5; yMax += 0.5; }
            else { yMin -= yRange * 0.08; yMax += yRange * 0.08; yRange = yMax - yMin; }

            DrawGridAndTicks(g, ml, mt, plotW, plotH, xMin, xRange, yMin, yRange, axisFont, axisPen, gridPen);
            DrawDataLine(g, xData, yData, xMin, xRange, yMin, yRange, ml, mt, plotW, plotH, lineColor, 1.5f);

            axisFont.Dispose(); labelFont.Dispose(); axisPen.Dispose(); gridPen.Dispose();
        }

        private void DrawGridAndTicks(Graphics g, int ml, int mt, int plotW, int plotH,
            double xMin, double xRange, double yMin, double yRange,
            Font axisFont, Pen axisPen, Pen gridPen)
        {
            int tickCount = 5;
            for (int i = 0; i <= tickCount; i++)
            {
                int px = ml + (int)(plotW * i / (double)tickCount);
                double xVal = xMin + xRange * i / tickCount;
                g.DrawLine(gridPen, px, mt, px, mt + plotH);
                g.DrawString(FormatTick(xVal), axisFont, Brushes.DimGray, px - 16, mt + plotH + 2);

                int py = mt + plotH - (int)(plotH * i / (double)tickCount);
                double yVal = yMin + yRange * i / tickCount;
                g.DrawLine(gridPen, ml, py, ml + plotW, py);
                string yStr = FormatTick(yVal);
                SizeF sz = g.MeasureString(yStr, axisFont);
                g.DrawString(yStr, axisFont, Brushes.DimGray, ml - sz.Width - 3, py - 6);
            }

            g.DrawLine(axisPen, ml, mt, ml, mt + plotH);
            g.DrawLine(axisPen, ml, mt + plotH, ml + plotW, mt + plotH);
        }

        private void DrawDataLine(Graphics g, double[] xData, double[] yData,
            double xMin, double xRange, double yMin, double yRange,
            int ml, int mt, int plotW, int plotH,
            Color color, float penWidth)
        {
            int n = Math.Min(xData.Length, yData.Length);
            int maxPts = plotW * 3;
            int step = n > maxPts ? n / maxPts : 1;

            var points = new List<PointF>();
            for (int i = 0; i < n; i += step)
            {
                double relX = (xData[i] - xMin) / xRange;
                if (relX < -0.01 || relX > 1.01) continue;

                float px = (float)(ml + plotW * relX);
                float py = (float)(mt + plotH - plotH * (yData[i] - yMin) / yRange);
                points.Add(new PointF(px, py));
            }

            if (points.Count >= 2)
            {
                using (var pen = new Pen(color, penWidth))
                {
                    g.DrawLines(pen, points.ToArray());
                }
            }
        }

        private static string FormatTick(double v)
        {
            double abs = Math.Abs(v);
            if (abs == 0) return "0";
            if (abs >= 1000 || abs < 0.01)
                return v.ToString("0.##E+0", CultureInfo.InvariantCulture);
            if (abs >= 1)
                return v.ToString("0.##", CultureInfo.InvariantCulture);
            return v.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}
