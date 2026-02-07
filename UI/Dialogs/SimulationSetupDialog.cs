using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Simulation;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Simulation;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    public class SimulationSetupDialog : Form
    {
        // ─── General ───
        private ComboBox cmbType;
        private TextBox txtTitle;

        // ─── Eigenvalue ───
        private TextBox txtNumModes;
        private TextBox txtMinFreq;
        private TextBox txtMaxFreq;
        private ComboBox cmbEigenMethod;

        // ─── Solver ───
        private ComboBox cmbSolver;
        private CheckBox chkAutoSPC;
        private ComboBox cmbNegEigen;
        private CheckBox chkGeoStiff;
        private ComboBox cmbImForm;

        // ─── Output ───
        private CheckBox chkEigout;
        private CheckBox chkD3plot;
        private CheckBox chkNodeout;
        private CheckBox chkElout;

        // ─── Additional ───
        private CheckBox chkEnergy;
        private CheckBox chkHourglass;
        private CheckBox chkAccuracy;

        // ─── Preview ───
        private TextBox txtPreview;

        // ─── Buttons ───
        private Button btnPreview;
        private Button btnCopy;
        private Button btnExport;
        private Button btnClose;

        public SimulationSetupDialog()
        {
            InitializeLayout();
            LoadCurrentParameters();
        }

        private void InitializeLayout()
        {
            Text = "Simulation Setup (\uc2dc\ubbac\ub808\uc774\uc158 \uc124\uc815)";
            Width = 780;
            Height = 760;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;

            var font = new Font("Segoe UI", 9f);
            var monoFont = new Font("Consolas", 8.5f);
            var boldFont = new Font("Segoe UI", 9f, FontStyle.Bold);
            var sectionColor = Color.FromArgb(50, 80, 140);

            int y = 12;
            int col1 = 12;
            int col2 = 140;

            // ═══ Row 1: Type + Title ═══
            var lblType = new Label
            {
                Text = "Analysis Type:",
                Location = new Point(col1, y + 3),
                AutoSize = true,
                Font = font
            };

            cmbType = new ComboBox
            {
                Location = new Point(col2, y),
                Width = 220,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = font
            };
            cmbType.Items.AddRange(new object[]
            {
                "Modal Analysis (\ubaa8\ub4dc\ud574\uc11d)"
            });
            cmbType.SelectedIndex = 0;

            var lblTitle = new Label
            {
                Text = "Title:",
                Location = new Point(400, y + 3),
                AutoSize = true,
                Font = font
            };

            txtTitle = new TextBox
            {
                Location = new Point(450, y),
                Width = 290,
                Font = font,
                Text = "Modal_Analysis"
            };

            y += 34;

            // ═══ Section: Eigenvalue Settings ═══
            var lblEigenSec = new Label
            {
                Text = "\u2500\u2500\u2500 Eigenvalue Settings (\uace0\uc720\uac12 \uc124\uc815) \u2500\u2500\u2500",
                Location = new Point(col1, y),
                AutoSize = true,
                Font = boldFont,
                ForeColor = sectionColor
            };
            y += 24;

            var lblNumModes = new Label
            {
                Text = "Number of Modes:",
                Location = new Point(col1, y + 3),
                AutoSize = true,
                Font = font
            };

            txtNumModes = new TextBox
            {
                Location = new Point(col2, y),
                Width = 80,
                Font = font,
                Text = "10"
            };

            var lblMinFreq = new Label
            {
                Text = "Min Freq (Hz):",
                Location = new Point(260, y + 3),
                AutoSize = true,
                Font = font
            };

            txtMinFreq = new TextBox
            {
                Location = new Point(370, y),
                Width = 80,
                Font = font,
                Text = "0"
            };

            var lblMaxFreq = new Label
            {
                Text = "Max Freq (Hz):",
                Location = new Point(490, y + 3),
                AutoSize = true,
                Font = font
            };

            txtMaxFreq = new TextBox
            {
                Location = new Point(600, y),
                Width = 80,
                Font = font,
                Text = "0"
            };

            y += 28;

            var lblMethod = new Label
            {
                Text = "Eigen Method:",
                Location = new Point(col1, y + 3),
                AutoSize = true,
                Font = font
            };

            cmbEigenMethod = new ComboBox
            {
                Location = new Point(col2, y),
                Width = 340,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = font
            };
            cmbEigenMethod.Items.AddRange(new object[]
            {
                "2: Block Shift Invert Lanczos (\uad8c\uc7a5)",
                "3: Inverse Power Method",
                "101: BCSLIB-EXT"
            });
            cmbEigenMethod.SelectedIndex = 0;

            y += 34;

            // ═══ Section: Solver Settings ═══
            var lblSolverSec = new Label
            {
                Text = "\u2500\u2500\u2500 Solver Settings (\uc194\ubc84 \uc124\uc815) \u2500\u2500\u2500",
                Location = new Point(col1, y),
                AutoSize = true,
                Font = boldFont,
                ForeColor = sectionColor
            };
            y += 24;

            var lblSolver = new Label
            {
                Text = "Solver:",
                Location = new Point(col1, y + 3),
                AutoSize = true,
                Font = font
            };

            cmbSolver = new ComboBox
            {
                Location = new Point(col2, y),
                Width = 280,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = font
            };
            cmbSolver.Items.AddRange(new object[]
            {
                "2: Multi-frontal Sparse (\uae30\ubcf8)",
                "4: Intel PARDISO",
                "6: MUMPS Distributed"
            });
            cmbSolver.SelectedIndex = 0;

            chkAutoSPC = new CheckBox
            {
                Text = "Auto SPC (\uc790\ub3d9 \ub2e8\uc810 \uad6c\uc18d)",
                Location = new Point(470, y + 1),
                AutoSize = true,
                Font = font,
                Checked = true
            };

            y += 28;

            var lblNegEigen = new Label
            {
                Text = "Negative Eigen:",
                Location = new Point(col1, y + 3),
                AutoSize = true,
                Font = font
            };

            cmbNegEigen = new ComboBox
            {
                Location = new Point(col2, y),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = font
            };
            cmbNegEigen.Items.AddRange(new object[]
            {
                "0: Stop on negative",
                "1: Warn only",
                "2: Allow (\ud5c8\uc6a9)"
            });
            cmbNegEigen.SelectedIndex = 2;

            chkGeoStiff = new CheckBox
            {
                Text = "Geometric Stiffness (\uae30\ud558 \uac15\uc131)",
                Location = new Point(380, y + 1),
                AutoSize = true,
                Font = font
            };

            y += 28;

            var lblImForm = new Label
            {
                Text = "Formulation:",
                Location = new Point(col1, y + 3),
                AutoSize = true,
                Font = font
            };

            cmbImForm = new ComboBox
            {
                Location = new Point(col2, y),
                Width = 340,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = font
            };
            cmbImForm.Items.AddRange(new object[]
            {
                "2: Eigenvalue only (\uace0\uc720\uac12\ub9cc)",
                "12: Static preload + Eigenvalue (\uc815\uc801 \ud504\ub9ac\ub85c\ub4dc + \uace0\uc720\uac12)"
            });
            cmbImForm.SelectedIndex = 0;

            y += 34;

            // ═══ Section: Output Settings ═══
            var lblOutputSec = new Label
            {
                Text = "\u2500\u2500\u2500 Output Settings (\ucd9c\ub825 \uc124\uc815) \u2500\u2500\u2500",
                Location = new Point(col1, y),
                AutoSize = true,
                Font = boldFont,
                ForeColor = sectionColor
            };
            y += 24;

            chkEigout = new CheckBox
            {
                Text = "D3EIGV (\uace0\uc720\uac12 \ubc14\uc774\ub108\ub9ac)",
                Location = new Point(col1, y),
                AutoSize = true,
                Font = font,
                Checked = true
            };

            chkD3plot = new CheckBox
            {
                Text = "D3PLOT (\ubc14\uc774\ub108\ub9ac \ud50c\ub86f)",
                Location = new Point(220, y),
                AutoSize = true,
                Font = font,
                Checked = true
            };

            chkNodeout = new CheckBox
            {
                Text = "NODOUT (\uc808\uc810 \ucd9c\ub825)",
                Location = new Point(420, y),
                AutoSize = true,
                Font = font
            };

            chkElout = new CheckBox
            {
                Text = "ELOUT (\uc694\uc18c \ucd9c\ub825)",
                Location = new Point(590, y),
                AutoSize = true,
                Font = font
            };

            y += 30;

            // ═══ Section: Additional Controls ═══
            var lblAdditionalSec = new Label
            {
                Text = "\u2500\u2500\u2500 Additional Controls (\ucd94\uac00 \uc81c\uc5b4) \u2500\u2500\u2500",
                Location = new Point(col1, y),
                AutoSize = true,
                Font = boldFont,
                ForeColor = sectionColor
            };
            y += 24;

            chkEnergy = new CheckBox
            {
                Text = "CONTROL_ENERGY",
                Location = new Point(col1, y),
                AutoSize = true,
                Font = font,
                Checked = true
            };

            chkHourglass = new CheckBox
            {
                Text = "CONTROL_HOURGLASS",
                Location = new Point(200, y),
                AutoSize = true,
                Font = font,
                Checked = true
            };

            chkAccuracy = new CheckBox
            {
                Text = "CONTROL_ACCURACY",
                Location = new Point(420, y),
                AutoSize = true,
                Font = font,
                Checked = true
            };

            y += 32;

            // ═══ Section: Keyword Preview ═══
            var lblPreviewSec = new Label
            {
                Text = "\u2500\u2500\u2500 Keyword Preview (\ud0a4\uc6cc\ub4dc \ubbf8\ub9ac\ubcf4\uae30) \u2500\u2500\u2500",
                Location = new Point(col1, y),
                AutoSize = true,
                Font = boldFont,
                ForeColor = sectionColor
            };
            y += 22;

            txtPreview = new TextBox
            {
                Location = new Point(col1, y),
                Size = new Size(738, 250),
                Font = monoFont,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                ReadOnly = true,
                WordWrap = false,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(200, 220, 200)
            };

            y += 258;

            // ═══ Buttons ═══
            btnPreview = new Button
            {
                Text = "Generate Preview",
                Location = new Point(col1, y),
                Size = new Size(130, 30),
                Font = font
            };
            btnPreview.Click += BtnPreview_Click;

            btnCopy = new Button
            {
                Text = "Copy to Clipboard",
                Location = new Point(152, y),
                Size = new Size(130, 30),
                Font = font
            };
            btnCopy.Click += BtnCopy_Click;

            btnExport = new Button
            {
                Text = "Export .k File",
                Location = new Point(292, y),
                Size = new Size(120, 30),
                Font = font
            };
            btnExport.Click += BtnExport_Click;

            btnClose = new Button
            {
                Text = "Close",
                Location = new Point(660, y),
                Size = new Size(90, 30),
                Font = font
            };
            btnClose.Click += (s, ev) => Close();

            // ═══ Add all controls ═══
            Controls.AddRange(new Control[]
            {
                lblType, cmbType, lblTitle, txtTitle,
                lblEigenSec, lblNumModes, txtNumModes, lblMinFreq, txtMinFreq, lblMaxFreq, txtMaxFreq,
                lblMethod, cmbEigenMethod,
                lblSolverSec, lblSolver, cmbSolver, chkAutoSPC,
                lblNegEigen, cmbNegEigen, chkGeoStiff,
                lblImForm, cmbImForm,
                lblOutputSec, chkEigout, chkD3plot, chkNodeout, chkElout,
                lblAdditionalSec, chkEnergy, chkHourglass, chkAccuracy,
                lblPreviewSec, txtPreview,
                btnPreview, btnCopy, btnExport, btnClose
            });
        }

        // ═══════════════════════════════════════
        //  Build / Load Parameters
        // ═══════════════════════════════════════

        private SimulationParameters BuildParameters()
        {
            var p = new SimulationParameters();
            p.Title = txtTitle.Text.Trim();
            p.Type = SimulationType.ModalAnalysis;

            int numModes;
            if (int.TryParse(txtNumModes.Text, out numModes) && numModes > 0)
                p.NumModes = numModes;

            double minFreq;
            if (double.TryParse(txtMinFreq.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out minFreq))
                p.MinFrequency = Math.Max(0, minFreq);

            double maxFreq;
            if (double.TryParse(txtMaxFreq.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out maxFreq))
                p.MaxFrequency = Math.Max(0, maxFreq);

            // Eigenvalue method
            string eigenSel = cmbEigenMethod.SelectedItem?.ToString() ?? "";
            if (eigenSel.StartsWith("3")) p.EigenvalueMethod = 3;
            else if (eigenSel.StartsWith("101")) p.EigenvalueMethod = 101;
            else p.EigenvalueMethod = 2;

            // Solver
            string solverSel = cmbSolver.SelectedItem?.ToString() ?? "";
            if (solverSel.StartsWith("4")) p.SolverType = 4;
            else if (solverSel.StartsWith("6")) p.SolverType = 6;
            else p.SolverType = 2;

            p.AutoSPC = chkAutoSPC.Checked;

            string negSel = cmbNegEigen.SelectedItem?.ToString() ?? "";
            if (negSel.StartsWith("0")) p.NegativeEigenvalue = 0;
            else if (negSel.StartsWith("1")) p.NegativeEigenvalue = 1;
            else p.NegativeEigenvalue = 2;

            p.GeometricStiffness = chkGeoStiff.Checked;

            string imSel = cmbImForm.SelectedItem?.ToString() ?? "";
            p.ImplicitFormulation = imSel.StartsWith("12") ? 12 : 2;

            p.OutputEigout = chkEigout.Checked;
            p.OutputD3plot = chkD3plot.Checked;
            p.OutputNodeout = chkNodeout.Checked;
            p.OutputElout = chkElout.Checked;

            p.ControlEnergy = chkEnergy.Checked;
            p.ControlHourglass = chkHourglass.Checked;
            p.ControlAccuracy = chkAccuracy.Checked;

            return p;
        }

        private void LoadCurrentParameters()
        {
            var p = SimulationKeywordService.Current;
            txtTitle.Text = p.Title ?? "Modal_Analysis";
            txtNumModes.Text = p.NumModes.ToString();
            txtMinFreq.Text = p.MinFrequency.ToString(CultureInfo.InvariantCulture);
            txtMaxFreq.Text = p.MaxFrequency.ToString(CultureInfo.InvariantCulture);

            switch (p.EigenvalueMethod)
            {
                case 3: cmbEigenMethod.SelectedIndex = 1; break;
                case 101: cmbEigenMethod.SelectedIndex = 2; break;
                default: cmbEigenMethod.SelectedIndex = 0; break;
            }

            switch (p.SolverType)
            {
                case 4: cmbSolver.SelectedIndex = 1; break;
                case 6: cmbSolver.SelectedIndex = 2; break;
                default: cmbSolver.SelectedIndex = 0; break;
            }

            chkAutoSPC.Checked = p.AutoSPC;

            switch (p.NegativeEigenvalue)
            {
                case 0: cmbNegEigen.SelectedIndex = 0; break;
                case 1: cmbNegEigen.SelectedIndex = 1; break;
                default: cmbNegEigen.SelectedIndex = 2; break;
            }

            chkGeoStiff.Checked = p.GeometricStiffness;
            cmbImForm.SelectedIndex = p.ImplicitFormulation == 12 ? 1 : 0;

            chkEigout.Checked = p.OutputEigout;
            chkD3plot.Checked = p.OutputD3plot;
            chkNodeout.Checked = p.OutputNodeout;
            chkElout.Checked = p.OutputElout;

            chkEnergy.Checked = p.ControlEnergy;
            chkHourglass.Checked = p.ControlHourglass;
            chkAccuracy.Checked = p.ControlAccuracy;
        }

        // ═══════════════════════════════════════
        //  Button Handlers
        // ═══════════════════════════════════════

        private void BtnPreview_Click(object sender, EventArgs e)
        {
            try
            {
                var p = BuildParameters();
                SimulationKeywordService.Current = p;
                txtPreview.Text = SimulationKeywordService.GenerateKeywords(p);
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError("Preview failed: " + ex.Message, "Error");
            }
        }

        private void BtnCopy_Click(object sender, EventArgs e)
        {
            try
            {
                var p = BuildParameters();
                SimulationKeywordService.Current = p;
                string keywords = SimulationKeywordService.GenerateKeywords(p);
                Clipboard.SetText(keywords);
                MessageBox.Show("Keywords copied to clipboard.",
                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError("Copy failed: " + ex.Message, "Error");
            }
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            try
            {
                var p = BuildParameters();
                SimulationKeywordService.Current = p;
                string keywords = SimulationKeywordService.GenerateKeywords(p);

                var sfd = new SaveFileDialog
                {
                    Title = "Export LS-DYNA Keyword File",
                    Filter = "LS-DYNA Keyword (*.k)|*.k|LS-DYNA Keyword (*.key)|*.key|All files (*.*)|*.*",
                    DefaultExt = "k",
                    FileName = (p.Title ?? "simulation") + ".k"
                };

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    System.IO.File.WriteAllText(sfd.FileName, keywords, System.Text.Encoding.ASCII);
                    MessageBox.Show("Exported to:\n" + sfd.FileName,
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError("Export failed: " + ex.Message, "Error");
            }
        }
    }
}
