using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Commands;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer;

#if V251
using SpaceClaim.Api.V251;
using SpaceClaim.Api.V251.Extensibility;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252;
using SpaceClaim.Api.V252.Extensibility;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.ReverseEngineer
{
    /// <summary>
    /// "Change Dimension..." ribbon command. Lets the user pick a single feature
    /// (Hole / Boss / FilletChain / Wall) from the active body and edit its
    /// primary dimension via ModificationService.
    /// </summary>
    // @lat: [[reverse-engineer#ChangeDimension]]
    public class ChangeDimensionCommand : BaseCommandCapsule
    {
        public const string CommandName = "MXDigitalTwinModeller.ChangeDimension";

        public ChangeDimensionCommand()
            : base(CommandName, "Change Dimension...", IconHelper.MeshIcon,
                   "활성 Body 의 feature 를 선택하고 치수 (D / H / R / T) 를 변경")
        {
        }

        protected override void OnUpdate(Command command)
        {
            command.IsEnabled = IsWindowActive();
        }

        protected override void OnExecute(Command command, ExecutionContext context, Rectangle buttonRect)
        {
            try
            {
                Part part = GetActivePart();
                if (part == null)
                {
                    ValidationHelper.ShowError("활성 Part 가 없습니다.", "오류");
                    return;
                }

                DesignBody body = null;
                foreach (var ib in part.Bodies)
                {
                    var db = ib as DesignBody;
                    if (db != null) { body = db; break; }
                }
                if (body == null)
                {
                    ValidationHelper.ShowError("활성 Part 에 DesignBody 가 없습니다.", "오류");
                    return;
                }

                var extractor = new FeatureExtractor();
                FeatureGraph graph = extractor.Extract(body);
                if (graph == null)
                {
                    ValidationHelper.ShowError("FeatureGraph 추출에 실패했습니다.", "오류");
                    return;
                }

                using (var dlg = new ChangeDimensionDialog(body, graph))
                {
                    dlg.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    string.Format("Change Dimension 중 오류:\n\n{0}", ex.Message),
                    "오류");
            }
        }
    }

    /// <summary>
    /// Inline WinForms dialog — kept in this file to match the lightweight
    /// pattern used by AskClaudeCommand / VisualizeFeatureGraphCommand.
    /// </summary>
    internal class ChangeDimensionDialog : Form
    {
        // -------------------------------------------------------------------
        // Feature-row tag (carried inside ListBox items).
        // -------------------------------------------------------------------
        private enum FeatureKind { Hole, BossDiameter, BossHeight, FilletChain, Wall }

        private class FeatureRow
        {
            public FeatureKind Kind;
            public string Id;                // H1 / B1 / FC1 / W1
            public string ParamLabel;        // D / H / R / T
            public double CurrentMm;
            public string DisplayText;

            public override string ToString()
            {
                return DisplayText;
            }
        }

        private readonly DesignBody _body;
        private readonly FeatureGraph _graph;

        private ListBox _list;
        private Label _currentLabel;
        private TextBox _newValueBox;
        private Button _applyBtn;
        private Button _cancelBtn;
        private Label _statusLabel;

        // Cycle 32 additions
        private CheckBox _autoFilletBox;
        private Label _patternLabel;
        private CheckBox _patternBulkBox;
        // Pattern membership for currently-selected feature (null if none).
        private object _selectedPattern;  // HolePatternFeature or BossPatternFeature
        private List<string> _selectedPatternMembers;

        // Cycle 33 additions — symmetry-aware partners
        private Label _symmetryLabel;
        private CheckBox _symmetryApplyBox;
        private List<string> _symmetryPartners;  // ids of mirror-partner features

        // Improvement C — modification preview
        private Label _previewLabel;

        // Improvement A — filter box for large feature lists (100+ rows).
        private TextBox _filterBox;
        private Label _filterLabel;
        // Full unfiltered set of rows; ListBox is rebuilt from this when filter changes.
        private List<FeatureRow> _allRows = new List<FeatureRow>();

        public ChangeDimensionDialog(DesignBody body, FeatureGraph graph)
        {
            _body = body;
            _graph = graph;

            Text = "Change Dimension — CAD Reverse Engineer";
            Width = 660;
            Height = 590;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(540, 490);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;

            // Improvement A — filter box for large feature lists.
            _filterLabel = new Label
            {
                Text = "Filter:",
                Location = new Point(10, 12),
                Size = new Size(45, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            _filterBox = new TextBox
            {
                Location = new Point(55, 10),
                Size = new Size(300, 22),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            _filterBox.TextChanged += OnFilterChanged;

            var listLabel = new Label
            {
                Text = "Feature 선택 (단일 항목):",
                Location = new Point(10, 38),
                Size = new Size(600, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _list = new ListBox
            {
                Location = new Point(10, 60),
                Size = new Size(600, 315),
                SelectionMode = SelectionMode.One,
                Font = new Font(FontFamily.GenericMonospace, 9f),
                IntegralHeight = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
                         AnchorStyles.Left | AnchorStyles.Right
            };
            _list.SelectedIndexChanged += OnFeatureSelected;

            _currentLabel = new Label
            {
                Text = "Current value: (feature 를 선택하세요)",
                Location = new Point(10, 385),
                Size = new Size(380, 20),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var newLabel = new Label
            {
                Text = "New value (mm):",
                Location = new Point(10, 410),
                Size = new Size(110, 22),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            _newValueBox = new TextBox
            {
                Location = new Point(125, 408),
                Size = new Size(120, 22),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            _newValueBox.TextChanged += OnNewValueChanged;

            _applyBtn = new Button
            {
                Text = "Apply",
                Location = new Point(420, 405),
                Size = new Size(90, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            _applyBtn.Click += OnApply;

            _cancelBtn = new Button
            {
                Text = "Cancel",
                Location = new Point(520, 405),
                Size = new Size(90, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.Cancel
            };

            _statusLabel = new Label
            {
                Text = "",
                Location = new Point(10, 495),
                Size = new Size(600, 20),
                ForeColor = Color.DarkSlateGray,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            // Cycle 32 — AutoFillet integration
            _autoFilletBox = new CheckBox
            {
                Text = "Auto-fillet new sharp edges after change",
                Location = new Point(260, 408),
                Size = new Size(280, 22),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Checked = false,
            };

            // Cycle 32 — Pattern bulk
            _patternLabel = new Label
            {
                Text = "",
                Location = new Point(10, 432),
                Size = new Size(400, 18),
                ForeColor = Color.DarkBlue,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Visible = false,
            };
            _patternBulkBox = new CheckBox
            {
                Text = "Apply to all pattern members",
                Location = new Point(420, 432),
                Size = new Size(220, 18),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Visible = false,
                Checked = false,
            };

            // Cycle 33 — Symmetry partner
            _symmetryLabel = new Label
            {
                Text = "",
                Location = new Point(10, 452),
                Size = new Size(400, 18),
                ForeColor = Color.DarkMagenta,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Visible = false,
            };
            _symmetryApplyBox = new CheckBox
            {
                Text = "Also apply to symmetric partner(s)",
                Location = new Point(420, 452),
                Size = new Size(220, 18),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Visible = false,
                Checked = false,
            };

            // Improvement C — modification preview
            _previewLabel = new Label
            {
                Text = "",
                Location = new Point(10, 472),
                Size = new Size(630, 20),
                ForeColor = Color.DarkGreen,
                Font = new Font(FontFamily.GenericSansSerif, 8.5f, FontStyle.Bold),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            };

            Controls.Add(_filterLabel);
            Controls.Add(_filterBox);
            Controls.Add(listLabel);
            Controls.Add(_list);
            Controls.Add(_currentLabel);
            Controls.Add(newLabel);
            Controls.Add(_newValueBox);
            Controls.Add(_applyBtn);
            Controls.Add(_cancelBtn);
            Controls.Add(_autoFilletBox);
            Controls.Add(_patternLabel);
            Controls.Add(_patternBulkBox);
            Controls.Add(_symmetryLabel);
            Controls.Add(_symmetryApplyBox);
            Controls.Add(_previewLabel);
            Controls.Add(_statusLabel);

            _patternBulkBox.CheckedChanged += (s, ev) => UpdatePreview();
            _symmetryApplyBox.CheckedChanged += (s, ev) => UpdatePreview();

            CancelButton = _cancelBtn;

            PopulateFeatures();
        }

        // -------------------------------------------------------------------
        // Populate the listbox from the FeatureGraph.
        // Improvement A: rows are built into _allRows once, then the visible
        // ListBox is filled via ApplyFilter() so the user-typed Filter: text
        // can be re-applied after a successful Apply triggers re-extraction.
        // -------------------------------------------------------------------
        private void PopulateFeatures()
        {
            _allRows.Clear();

            // Holes
            if (_graph.Holes != null)
            {
                foreach (var h in _graph.Holes)
                {
                    string axisStr = AxisLabel(h.Axis);
                    string thru = h.IsThrough ? "through" : ("blind " + h.DepthMm.ToString("F2") + "mm");
                    string disp = string.Format(CultureInfo.InvariantCulture,
                        "{0}: hole  D={1:F2}  ({2}, axis={3})",
                        h.Id, h.DiameterMm, thru, axisStr);

                    // Counterbore-aware label additions.
                    if (h.HasCounterbore)
                    {
                        // This hole is the inner-through part; find the outer step hole
                        // that references it via ParentHoleId.
                        string outerId = null;
                        foreach (var other in _graph.Holes)
                        {
                            if (other.IsCounterboreStep && other.ParentHoleId == h.Id)
                            {
                                outerId = other.Id;
                                break;
                            }
                        }
                        disp += string.Format(CultureInfo.InvariantCulture,
                            " [cb-inner; outer step: {0}]",
                            outerId ?? "?");
                    }
                    else if (h.IsCounterboreStep)
                    {
                        disp += string.Format(CultureInfo.InvariantCulture,
                            " [cb-outer-step; inner: {0}]",
                            string.IsNullOrEmpty(h.ParentHoleId) ? "?" : h.ParentHoleId);
                    }

                    // Conical (countersink / chamfered) hole label.
                    if (h.IsConical)
                    {
                        disp += string.Format(CultureInfo.InvariantCulture,
                            " [conical; half_angle={0:F1}deg]",
                            h.HalfAngleDeg);
                    }

                    _allRows.Add(new FeatureRow
                    {
                        Kind = FeatureKind.Hole,
                        Id = h.Id,
                        ParamLabel = "D",
                        CurrentMm = h.DiameterMm,
                        DisplayText = disp,
                    });
                }
            }

            // Bosses — two rows each (change D / change H).
            if (_graph.Bosses != null)
            {
                foreach (var b in _graph.Bosses)
                {
                    string axisStr = AxisLabel(b.Axis);
                    string dDisp = string.Format(CultureInfo.InvariantCulture,
                        "{0}: boss  D={1:F2}  H={2:F2}   (change D, axis={3})",
                        b.Id, b.DiameterMm, b.HeightMm, axisStr);
                    _allRows.Add(new FeatureRow
                    {
                        Kind = FeatureKind.BossDiameter,
                        Id = b.Id,
                        ParamLabel = "D",
                        CurrentMm = b.DiameterMm,
                        DisplayText = dDisp,
                    });
                    string hDisp = string.Format(CultureInfo.InvariantCulture,
                        "{0}: boss  D={1:F2}  H={2:F2}   (change H, axis={3})",
                        b.Id, b.DiameterMm, b.HeightMm, axisStr);
                    _allRows.Add(new FeatureRow
                    {
                        Kind = FeatureKind.BossHeight,
                        Id = b.Id,
                        ParamLabel = "H",
                        CurrentMm = b.HeightMm,
                        DisplayText = hDisp,
                    });
                }
            }

            // Fillet chains
            if (_graph.FilletChains != null)
            {
                foreach (var fc in _graph.FilletChains)
                {
                    int faceCount = fc.FaceIndices != null ? fc.FaceIndices.Count : 0;
                    string disp = string.Format(CultureInfo.InvariantCulture,
                        "{0}: fillet  R={1:F2}  (chain, {2} faces)",
                        fc.Id, fc.RadiusMm, faceCount);
                    _allRows.Add(new FeatureRow
                    {
                        Kind = FeatureKind.FilletChain,
                        Id = fc.Id,
                        ParamLabel = "R",
                        CurrentMm = fc.RadiusMm,
                        DisplayText = disp,
                    });
                }
            }

            // Walls
            if (_graph.Walls != null)
            {
                foreach (var w in _graph.Walls)
                {
                    string disp = string.Format(CultureInfo.InvariantCulture,
                        "{0}: wall  T={1:F2}  (faces {2}+{3})",
                        w.Id, w.ThicknessMm, w.FaceA, w.FaceB);
                    _allRows.Add(new FeatureRow
                    {
                        Kind = FeatureKind.Wall,
                        Id = w.Id,
                        ParamLabel = "T",
                        CurrentMm = w.ThicknessMm,
                        DisplayText = disp,
                    });
                }
            }

            // Push _allRows through current filter (preserves typed filter text
            // when re-populating after a successful Apply).
            ApplyFilter();
        }

        // -------------------------------------------------------------------
        // Improvement A: rebuild the ListBox from _allRows, honoring the
        // user-typed filter (substring, case-insensitive). Empty filter shows
        // all rows. Selects first visible row to keep the dialog usable.
        // -------------------------------------------------------------------
        private void ApplyFilter()
        {
            string filter = _filterBox != null ? (_filterBox.Text ?? "").Trim() : "";

            _list.BeginUpdate();
            try
            {
                _list.Items.Clear();
                if (filter.Length == 0)
                {
                    foreach (var r in _allRows) _list.Items.Add(r);
                }
                else
                {
                    foreach (var r in _allRows)
                    {
                        if (r.DisplayText != null &&
                            r.DisplayText.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            _list.Items.Add(r);
                        }
                    }
                }
            }
            finally
            {
                _list.EndUpdate();
            }

            if (_allRows.Count == 0)
            {
                _statusLabel.Text = "이 body 에서 변경 가능한 feature (hole/boss/fillet/wall) 가 발견되지 않았습니다.";
                _applyBtn.Enabled = false;
            }
            else if (_list.Items.Count == 0)
            {
                // Filter excluded everything; keep dialog responsive without
                // clobbering the global "no features" message.
                _statusLabel.Text = string.Format(CultureInfo.InvariantCulture,
                    "Filter '{0}' 에 매칭되는 row 가 없습니다 ({1} 개 숨겨짐).",
                    filter, _allRows.Count);
                _applyBtn.Enabled = false;
            }
            else
            {
                _applyBtn.Enabled = true;
                _list.SelectedIndex = 0;
            }
        }

        private void OnFilterChanged(object sender, EventArgs e)
        {
            ApplyFilter();
        }

        private static string AxisLabel(double[] axis)
        {
            if (axis == null || axis.Length < 3) return "?";
            // Identify whichever component dominates.
            double ax = Math.Abs(axis[0]);
            double ay = Math.Abs(axis[1]);
            double az = Math.Abs(axis[2]);
            if (az >= ax && az >= ay) return axis[2] >= 0 ? "+Z" : "-Z";
            if (ay >= ax) return axis[1] >= 0 ? "+Y" : "-Y";
            return axis[0] >= 0 ? "+X" : "-X";
        }

        private void OnFeatureSelected(object sender, EventArgs e)
        {
            var row = _list.SelectedItem as FeatureRow;
            if (row == null)
            {
                _currentLabel.Text = "Current value: (선택 없음)";
                _newValueBox.Text = "";
                _patternLabel.Visible = false;
                _patternBulkBox.Visible = false;
                _selectedPattern = null;
                _selectedPatternMembers = null;
                if (_previewLabel != null) _previewLabel.Text = "";
                return;
            }

            _currentLabel.Text = string.Format(CultureInfo.InvariantCulture,
                "Current value: {0} = {1:F3} mm",
                row.ParamLabel, row.CurrentMm);
            _newValueBox.Text = row.CurrentMm.ToString("F3", CultureInfo.InvariantCulture);
            _newValueBox.SelectAll();
            _newValueBox.Focus();
            _statusLabel.Text = "";

            // Cycle 32: pattern membership lookup
            _selectedPattern = null;
            _selectedPatternMembers = null;
            _patternLabel.Visible = false;
            _patternBulkBox.Visible = false;
            _patternBulkBox.Checked = false;

            // Cycle 33: symmetry partner search
            _symmetryPartners = null;
            _symmetryLabel.Visible = false;
            _symmetryApplyBox.Visible = false;
            _symmetryApplyBox.Checked = false;

            if (_graph.Symmetries != null && _graph.Symmetries.Count > 0)
            {
                double[] selPos = null;
                if (row.Kind == FeatureKind.Hole && _graph.Holes != null)
                {
                    var h = _graph.Holes.Find(hh => hh.Id == row.Id);
                    if (h != null) selPos = h.PositionMm;
                }
                else if ((row.Kind == FeatureKind.BossDiameter || row.Kind == FeatureKind.BossHeight) &&
                         _graph.Bosses != null)
                {
                    var b = _graph.Bosses.Find(bb => bb.Id == row.Id);
                    if (b != null) selPos = b.BasePositionMm;
                }

                if (selPos != null && selPos.Length == 3)
                {
                    var partners = new List<string>();
                    foreach (var sym in _graph.Symmetries)
                    {
                        if (sym.ScorePercent < 80.0) continue;  // only reliable mirror planes
                        if (sym.MirrorPlaneNormal_mm == null || sym.MirrorPlaneOrigin_mm == null) continue;
                        // p_mirror = p - 2 * dot(p - origin, normal) * normal
                        double[] n = sym.MirrorPlaneNormal_mm;
                        double[] o = sym.MirrorPlaneOrigin_mm;
                        double t = (selPos[0] - o[0]) * n[0]
                                 + (selPos[1] - o[1]) * n[1]
                                 + (selPos[2] - o[2]) * n[2];
                        double[] pm = new[]
                        {
                            selPos[0] - 2 * t * n[0],
                            selPos[1] - 2 * t * n[1],
                            selPos[2] - 2 * t * n[2],
                        };
                        // find a hole/boss whose position matches pm (within 5mm)
                        if (row.Kind == FeatureKind.Hole && _graph.Holes != null)
                        {
                            foreach (var hh in _graph.Holes)
                            {
                                if (hh.Id == row.Id) continue;
                                if (hh.PositionMm == null || hh.PositionMm.Length < 3) continue;
                                double dx = hh.PositionMm[0] - pm[0];
                                double dy = hh.PositionMm[1] - pm[1];
                                double dz = hh.PositionMm[2] - pm[2];
                                if (dx*dx + dy*dy + dz*dz <= 25.0)  // 5mm tol
                                {
                                    if (!partners.Contains(hh.Id)) partners.Add(hh.Id);
                                }
                            }
                        }
                        else if ((row.Kind == FeatureKind.BossDiameter || row.Kind == FeatureKind.BossHeight) &&
                                 _graph.Bosses != null)
                        {
                            foreach (var bb in _graph.Bosses)
                            {
                                if (bb.Id == row.Id) continue;
                                if (bb.BasePositionMm == null || bb.BasePositionMm.Length < 3) continue;
                                double dx = bb.BasePositionMm[0] - pm[0];
                                double dy = bb.BasePositionMm[1] - pm[1];
                                double dz = bb.BasePositionMm[2] - pm[2];
                                if (dx*dx + dy*dy + dz*dz <= 25.0)
                                {
                                    if (!partners.Contains(bb.Id)) partners.Add(bb.Id);
                                }
                            }
                        }
                    }

                    if (partners.Count > 0)
                    {
                        _symmetryPartners = partners;
                        _symmetryLabel.Text = string.Format(CultureInfo.InvariantCulture,
                            "Symmetric partner(s): {0}", string.Join(",", partners.ToArray()));
                        _symmetryLabel.Visible = true;
                        _symmetryApplyBox.Visible = true;
                    }
                }
            }

            UpdatePreview();

            if (row.Kind == FeatureKind.Hole && _graph.HolePatterns != null)
            {
                foreach (var hp in _graph.HolePatterns)
                {
                    if (hp.MemberHoleIds != null && hp.MemberHoleIds.Contains(row.Id))
                    {
                        _selectedPattern = hp;
                        _selectedPatternMembers = new List<string>(hp.MemberHoleIds);
                        int n = hp.MemberHoleIds.Count;
                        string preview = string.Join(",", hp.MemberHoleIds.GetRange(0, Math.Min(n, 6)).ToArray());
                        if (n > 6) preview += "...";
                        _patternLabel.Text = string.Format(CultureInfo.InvariantCulture,
                            "Part of pattern {0} ({1} {2}, {3} members: {4})",
                            hp.Id, hp.PatternType, "hole", n, preview);
                        _patternLabel.Visible = true;
                        _patternBulkBox.Visible = true;
                        break;
                    }
                }
            }
            else if ((row.Kind == FeatureKind.BossDiameter || row.Kind == FeatureKind.BossHeight) &&
                     _graph.BossPatterns != null)
            {
                foreach (var bp in _graph.BossPatterns)
                {
                    if (bp.MemberBossIds != null && bp.MemberBossIds.Contains(row.Id))
                    {
                        _selectedPattern = bp;
                        _selectedPatternMembers = new List<string>(bp.MemberBossIds);
                        int n = bp.MemberBossIds.Count;
                        string preview = string.Join(",", bp.MemberBossIds.GetRange(0, Math.Min(n, 6)).ToArray());
                        if (n > 6) preview += "...";
                        _patternLabel.Text = string.Format(CultureInfo.InvariantCulture,
                            "Part of pattern {0} ({1} {2}, {3} members: {4})",
                            bp.Id, bp.PatternType, "boss", n, preview);
                        _patternLabel.Visible = true;
                        _patternBulkBox.Visible = true;
                        break;
                    }
                }
            }

            UpdatePreview();
        }

        // -------------------------------------------------------------------
        // Improvement C — modification preview
        // -------------------------------------------------------------------
        private void OnNewValueChanged(object sender, EventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (_previewLabel == null) return;

            var row = _list != null ? _list.SelectedItem as FeatureRow : null;
            if (row == null)
            {
                _previewLabel.Text = "";
                return;
            }

            double newVal;
            if (!double.TryParse(_newValueBox.Text, NumberStyles.Float,
                                  CultureInfo.InvariantCulture, out newVal) &&
                !double.TryParse(_newValueBox.Text, out newVal))
            {
                _previewLabel.Text = "Preview: (enter a numeric value)";
                _previewLabel.ForeColor = Color.Gray;
                return;
            }

            double cur = row.CurrentMm;
            double delta = newVal - cur;
            string preview;
            Color color = Color.DarkGreen;

            switch (row.Kind)
            {
                case FeatureKind.Hole:
                {
                    double areaPctFrac = 0.0;
                    if (cur > 1e-9)
                    {
                        double aOld = cur * cur;
                        double aNew = newVal * newVal;
                        areaPctFrac = (aNew - aOld) / aOld;
                    }
                    preview = string.Format(CultureInfo.InvariantCulture,
                        "Preview: hole D {0:F2} → {1:F2} mm (ΔD = {2:+0.00;-0.00;0.00}, area {3:+0%;-0%;0%})",
                        cur, newVal, delta, areaPctFrac);
                    break;
                }
                case FeatureKind.BossDiameter:
                {
                    double areaPctFrac = 0.0;
                    if (cur > 1e-9)
                    {
                        double aOld = cur * cur;
                        double aNew = newVal * newVal;
                        areaPctFrac = (aNew - aOld) / aOld;
                    }
                    preview = string.Format(CultureInfo.InvariantCulture,
                        "Preview: boss D {0:F2} → {1:F2} mm (ΔD = {2:+0.00;-0.00;0.00}, area {3:+0%;-0%;0%})",
                        cur, newVal, delta, areaPctFrac);
                    break;
                }
                case FeatureKind.BossHeight:
                {
                    double pctFrac = cur > 1e-9 ? (delta / cur) : 0.0;
                    preview = string.Format(CultureInfo.InvariantCulture,
                        "Preview: boss H {0:F2} → {1:F2} mm (ΔH = {2:+0.00;-0.00;0.00} = {3:+0%;-0%;0%})",
                        cur, newVal, delta, pctFrac);
                    break;
                }
                case FeatureKind.FilletChain:
                {
                    double pctFrac = cur > 1e-9 ? (delta / cur) : 0.0;
                    preview = string.Format(CultureInfo.InvariantCulture,
                        "Preview: R {0:F2} → {1:F2} mm (ΔR = {2:+0.00;-0.00;0.00} = {3:+0%;-0%;0%})",
                        cur, newVal, delta, pctFrac);
                    break;
                }
                case FeatureKind.Wall:
                {
                    string dir = delta >= 0 ? "grows" : "shrinks";
                    preview = string.Format(CultureInfo.InvariantCulture,
                        "Preview: T {0:F2} → {1:F2} mm (ΔT = {2:+0.00;-0.00;0.00}, body Z {3} by ~{4:F2} mm)",
                        cur, newVal, delta, dir, Math.Abs(delta));
                    break;
                }
                default:
                    preview = string.Format(CultureInfo.InvariantCulture,
                        "Preview: {0} {1:F2} → {2:F2} mm (Δ = {3:+0.00;-0.00;0.00})",
                        row.ParamLabel, cur, newVal, delta);
                    break;
            }

            // Bulk / symmetry annotation — count unique features that will be touched.
            bool isBulk = _patternBulkBox != null && _patternBulkBox.Visible && _patternBulkBox.Checked &&
                          _selectedPatternMembers != null;
            bool isSym = _symmetryApplyBox != null && _symmetryApplyBox.Visible && _symmetryApplyBox.Checked &&
                         _symmetryPartners != null;
            if (isBulk || isSym)
            {
                var seen = new HashSet<string>();
                seen.Add(row.Id);
                if (isBulk)
                {
                    foreach (var m in _selectedPatternMembers) seen.Add(m);
                }
                if (isSym)
                {
                    foreach (var sp in _symmetryPartners) seen.Add(sp);
                }
                if (seen.Count > 1)
                {
                    preview += string.Format(CultureInfo.InvariantCulture,
                        "  ({0} members will change)", seen.Count);
                }
            }

            // Warning colors for unusual changes
            if (newVal <= 0 && row.Kind != FeatureKind.FilletChain)
            {
                color = Color.Firebrick;
            }
            else if (Math.Abs(delta) > 1e-9 && cur > 1e-9 && Math.Abs(delta / cur) > 0.5)
            {
                color = Color.DarkOrange;
            }

            _previewLabel.ForeColor = color;
            _previewLabel.Text = preview;
        }

        // -------------------------------------------------------------------
        // Apply: dispatch to the right ModificationService method.
        // -------------------------------------------------------------------
        private void OnApply(object sender, EventArgs e)
        {
            var row = _list.SelectedItem as FeatureRow;
            if (row == null)
            {
                _statusLabel.Text = "Feature 를 선택하세요.";
                return;
            }

            double newVal;
            if (!double.TryParse(_newValueBox.Text, NumberStyles.Float,
                                  CultureInfo.InvariantCulture, out newVal))
            {
                // Try the user's locale as a fallback.
                if (!double.TryParse(_newValueBox.Text, out newVal))
                {
                    ValidationHelper.ShowError(
                        "New value 가 숫자가 아닙니다. (예: 5.0)",
                        "입력 오류");
                    return;
                }
            }
            if (newVal <= 0 && row.Kind != FeatureKind.FilletChain)
            {
                ValidationHelper.ShowError(
                    string.Format(CultureInfo.InvariantCulture,
                        "{0} 는 0 보다 커야 합니다.", row.ParamLabel),
                    "입력 오류");
                return;
            }
            if (newVal < 0)
            {
                ValidationHelper.ShowError("R 는 음수일 수 없습니다.", "입력 오류");
                return;
            }

            _applyBtn.Enabled = false;
            _statusLabel.Text = "Applying...";
            System.Windows.Forms.Application.DoEvents();

            // Capture pre-modify graph snapshot for AutoFillet target R determination.
            FeatureGraph graphBeforeModify = _graph;

            // Determine ID list to mutate: pattern members if bulk-apply checked, else single.
            // Cycle 33: also include symmetric partners if that checkbox is on.
            List<string> idsToApply = new List<string>();
            bool isBulk = _patternBulkBox.Visible && _patternBulkBox.Checked && _selectedPatternMembers != null;
            bool isSym  = _symmetryApplyBox.Visible && _symmetryApplyBox.Checked && _symmetryPartners != null;
            if (isBulk)
            {
                idsToApply.AddRange(_selectedPatternMembers);
            }
            else
            {
                idsToApply.Add(row.Id);
            }
            if (isSym)
            {
                foreach (var sp in _symmetryPartners)
                    if (!idsToApply.Contains(sp)) idsToApply.Add(sp);
            }

            ModificationResult result = null;
            int succCount = 0;
            int failCount = 0;
            string lastHint = null;
            string lastError = null;
            try
            {
                foreach (var id in idsToApply)
                {
                    ModificationResult r = null;
                    switch (row.Kind)
                    {
                        case FeatureKind.Hole:
                            r = ModificationService.ChangeHoleDiameter(_body, _graph, id, newVal);
                            break;
                        case FeatureKind.BossDiameter:
                            r = ModificationService.ChangeBossDiameter(_body, _graph, id, newVal);
                            break;
                        case FeatureKind.BossHeight:
                            r = ModificationService.ChangeBossHeight(_body, _graph, id, newVal);
                            break;
                        case FeatureKind.FilletChain:
                            r = ModificationService.ChangeFilletRadius(_body, _graph, id, newVal);
                            break;
                        case FeatureKind.Wall:
                            r = ModificationService.ChangeWallThickness(_body, _graph, id, newVal);
                            break;
                        default:
                            ValidationHelper.ShowError("알 수 없는 feature 종류: " + row.Kind, "오류");
                            _applyBtn.Enabled = true;
                            return;
                    }
                    if (r != null && r.Success) { succCount++; result = r; }
                    else
                    {
                        failCount++;
                        if (r != null)
                        {
                            lastError = r.ErrorMessage;
                            if (!string.IsNullOrEmpty(r.HintMessage)) lastHint = r.HintMessage;
                        }
                    }
                    // Cycle 33: log every attempt (success or fail) into in-memory history.
                    ModificationHistory.Record(
                        r != null ? r.Operation : ("Change" + row.Kind),
                        id, row.ParamLabel, row.CurrentMm, newVal, r);
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Exception.";
                ValidationHelper.ShowError(
                    string.Format("ModificationService 호출 중 예외 발생:\n\n{0}\n\n{1}",
                        ex.Message, ex.StackTrace),
                    "Change Dimension — Exception");
                _applyBtn.Enabled = true;
                return;
            }

            if (succCount == 0)
            {
                _statusLabel.Text = "Failed: " + (lastError ?? "(no message)");
                string errMsg = string.Format("[{0}] 실패:\n\n{1}",
                        row.Kind, lastError ?? "(no message)");
                if (!string.IsNullOrEmpty(lastHint))
                {
                    errMsg += "\n\n--- HINT ---\n" + lastHint;
                }
                ValidationHelper.ShowError(errMsg, "Change Dimension — Failed");
                _applyBtn.Enabled = true;
                return;
            }

            if (failCount > 0)
            {
                _statusLabel.Text = string.Format(CultureInfo.InvariantCulture,
                    "Partial: {0}/{1} OK", succCount, idsToApply.Count);
            }

            _statusLabel.Text = string.Format(CultureInfo.InvariantCulture,
                "OK — {0} ({1} → {2:F3} mm).",
                result.Operation, row.ParamLabel, newVal);

            // Cycle 32: optional AutoFillet on new sharp edges
            string autoFilletNote = "";
            if (_autoFilletBox.Checked && result.ModifiedFaceIndices != null &&
                result.ModifiedFaceIndices.Count > 0)
            {
                try
                {
                    double targetR = AutoFilletService.DetermineTargetRadius(graphBeforeModify, 0.5);
                    var afResult = AutoFilletService.RoundEdgesNearFaces(
                        _body, result.ModifiedFaceIndices, targetR);
                    if (afResult != null && afResult.EdgesRounded > 0)
                    {
                        autoFilletNote = string.Format(CultureInfo.InvariantCulture,
                            "\nAutoFillet: {0} edges rounded at R={1:F2} mm",
                            afResult.EdgesRounded, targetR);
                    }
                    else
                    {
                        autoFilletNote = "\nAutoFillet: no new sharp edges found";
                    }
                }
                catch (Exception afEx)
                {
                    autoFilletNote = "\nAutoFillet 실패 (warning, modification 유지됨): " + afEx.Message;
                }
            }

            string bulkNote = isBulk
                ? string.Format(CultureInfo.InvariantCulture, "\nBulk: {0}/{1} members modified",
                                succCount, idsToApply.Count)
                : "";

            ValidationHelper.ShowInfo(
                string.Format(CultureInfo.InvariantCulture,
                    "성공: {0}\n\n{1} {2}: {3:F3} → {4:F3} mm\nModified faces: {5}{6}{7}",
                    result.Operation, row.Id, row.ParamLabel,
                    row.CurrentMm, newVal,
                    result.ModifiedFaceIndices != null ? result.ModifiedFaceIndices.Count : 0,
                    bulkNote, autoFilletNote),
                "Change Dimension — Success");

            // Optionally save as a new .scdocx so the user can keep the modified result.
            if (ValidationHelper.ShowConfirm(
                "수정된 모델을 새 .scdocx 파일로 저장하시겠습니까?",
                "Save Modified Model?"))
            {
                TrySaveAsScdocx();
            }

            // Re-extract so the dialog reflects the new dimensions for follow-up edits.
            try
            {
                var extractor = new FeatureExtractor();
                FeatureGraph fresh = extractor.Extract(_body);
                if (fresh != null)
                {
                    // Replace the underlying graph fields (we keep the same instance ref
                    // so ModificationService dispatch keeps working without restart).
                    _graph.Holes = fresh.Holes;
                    _graph.Bosses = fresh.Bosses;
                    _graph.FilletChains = fresh.FilletChains;
                    _graph.Walls = fresh.Walls;
                    _graph.Faces = fresh.Faces;
                    PopulateFeatures();
                }
            }
            catch
            {
                // best-effort re-extract; original graph still usable for further edits.
            }
            finally
            {
                _applyBtn.Enabled = true;
            }
        }

        private void TrySaveAsScdocx()
        {
            try
            {
                using (var sfd = new SaveFileDialog())
                {
                    sfd.Title = "Save Modified Model";
                    sfd.Filter = "SpaceClaim Document (*.scdocx)|*.scdocx|All Files (*.*)|*.*";
                    sfd.DefaultExt = "scdocx";
                    sfd.AddExtension = true;
                    sfd.OverwritePrompt = true;

                    if (sfd.ShowDialog(this) != DialogResult.OK) return;

                    Document doc = _body != null ? Window.ActiveWindow?.Document : null;
                    if (doc == null)
                    {
                        ValidationHelper.ShowError("활성 Document 를 찾을 수 없습니다.", "Save Failed");
                        return;
                    }
                    doc.SaveAs(sfd.FileName);
                    _statusLabel.Text = "Saved: " + sfd.FileName;
                }
            }
            catch (Exception ex)
            {
                ValidationHelper.ShowError(
                    string.Format("저장 중 오류:\n\n{0}", ex.Message),
                    "Save Failed");
            }
        }
    }
}
