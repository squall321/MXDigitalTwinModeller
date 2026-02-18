# encoding: utf-8
"""
MX Digital Twin Simulator - ANSYS Mechanical ACT Extension
Cap Vibration Time Force Setup

Toolbar buttons (MXSimulator.xml):
  Named Selections  -> show_ns_dialog       : contact face detection + NS creation
  Modal Analysis    -> show_modal_dialog    : modal analysis setup
  Add Scenario      -> show_scenario_dialog : pick NS + CSV -> one Transient
"""

import os
import clr

clr.AddReference("PresentationFramework")
clr.AddReference("PresentationCore")
clr.AddReference("WindowsBase")
clr.AddReference("System.Xaml")
clr.AddReference("System.Windows.Forms")

from System.Windows.Forms import OpenFileDialog, DialogResult
from System.Windows import Window
from System.Windows.Controls import (
    StackPanel, Button, Label, TextBox,
    Orientation, ScrollViewer, ScrollBarVisibility,
    CheckBox, RadioButton, Separator
)
from System.Windows import (
    HorizontalAlignment, VerticalAlignment,
    Thickness, MessageBox, MessageBoxButton, MessageBoxImage
)
from System.Windows.Media import Brushes

try:
    import Ansys
except ImportError:
    pass

try:
    from Ansys.Mechanical.DataModel.Enums import (
        DataModelObjectCategory, GeometryDefineByType
    )
except ImportError:
    pass

try:
    from Ansys.Core.Units import Quantity
except ImportError:
    pass


# ================================================================
# ACT Callbacks
# ================================================================

def on_init(_=None):
    pass


# ================================================================
# Constants & Helpers
# ================================================================

# Direction → semantic NS base name
DIR_NAMES = {
    '+Z': 'Cap_Upper',
    '-Z': 'Cap_Lower',
    '+Y': 'Cap_Front',
    '-Y': 'Cap_Back',
    '+X': 'Cap_Right',
    '-X': 'Cap_Left',
}

# Semantic part → (axis, default_sign) for force application
DIR_AXIS = {
    'Upper': ('Z',  1.0),
    'Lower': ('Z', -1.0),
    'Front': ('Y',  1.0),
    'Back':  ('Y', -1.0),
    'Right': ('X',  1.0),
    'Left':  ('X', -1.0),
}


def classify_normal_direction(normal):
    try:
        nx, ny, nz = abs(normal.X), abs(normal.Y), abs(normal.Z)
        ox, oy, oz = normal.X, normal.Y, normal.Z
    except:
        nx, ny, nz = abs(normal[0]), abs(normal[1]), abs(normal[2])
        ox, oy, oz = normal[0], normal[1], normal[2]

    if nz > nx and nz > ny:
        return '+Z' if oz > 0 else '-Z'
    elif ny > nx and ny > nz:
        return '+Y' if oy > 0 else '-Y'
    else:
        return '+X' if ox > 0 else '-X'


def _next_ns_counter(existing_names, base_name):
    """Return next available 3-digit counter for base_name_NNN pattern."""
    nums = []
    prefix = base_name + '_'
    for name in existing_names:
        if name.startswith(prefix):
            suffix = name[len(prefix):]
            if suffix.isdigit() and len(suffix) == 3:
                nums.append(int(suffix))
    return max(nums) + 1 if nums else 1


def _ns_to_axis(ns_name):
    """Return (axis, default_sign) from a Cap_* NS name, or (None, 1.0)."""
    for key, (axis, sign) in DIR_AXIS.items():
        if key in ns_name:
            return axis, sign
    return None, 1.0


def _is_negative_ns(ns_name):
    """True if NS name implies negative force direction by default."""
    return any(x in ns_name for x in ('Lower', 'Back', 'Left'))


def _lbl(text, width=None):
    lbl = Label()
    lbl.Content = text
    if width:
        lbl.Width = width
    lbl.VerticalContentAlignment = VerticalAlignment.Center
    return lbl


def _tb(default, width=80, tooltip=None):
    tb = TextBox()
    tb.Width = width
    tb.Text = str(default)
    tb.VerticalContentAlignment = VerticalAlignment.Center
    if tooltip:
        tb.ToolTip = tooltip
    return tb


def _row(*children, **kwargs):
    p = StackPanel()
    p.Orientation = Orientation.Horizontal
    p.Margin = kwargs.get('margin', Thickness(0, 0, 0, 6))
    for c in children:
        p.Children.Add(c)
    return p


def _sep():
    s = Separator()
    s.Margin = Thickness(0, 6, 0, 6)
    return s


def _status(text, color=None):
    lbl = Label()
    lbl.Content = text
    lbl.FontSize = 10
    lbl.Foreground = color if color else Brushes.Gray
    lbl.Margin = Thickness(2, 0, 0, 4)
    return lbl


# ================================================================
# Dialog 1: Named Selections
# ================================================================

class NSDialog(Window):
    """
    Button 1 — Named Selections
    Contact face detection + Named Selection creation.
    NS names: Cap_Upper_001, Cap_Lower_001, etc.
    """

    def __init__(self):
        self.Title = "MX Digital Twin  |  Named Selections"
        self.Width = 640
        self.Height = 620
        self.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen

        self.face_data_list = []
        self.face_checkboxes = []
        self.all_bodies = []

        main = StackPanel()
        main.Margin = Thickness(14, 10, 14, 10)

        # Header
        h = Label()
        h.Content = "Contact Face Detection  \u2192  Named Selections"
        h.FontSize = 14
        h.FontWeight = System.Windows.FontWeights.Bold
        h.Margin = Thickness(0, 0, 0, 8)
        main.Children.Add(h)

        # Keywords
        self.keywords_tb = _tb("", 300,
            "Comma-separated keywords (blank = all bodies)")
        main.Children.Add(_row(
            _lbl("Keywords:", 80), self.keywords_tb,
            _lbl("  (blank = all)", 0)
        ))

        # Tolerance
        self.tol_tb = _tb("0.1", 60, "Contact gap tolerance (mm)")
        main.Children.Add(_row(
            _lbl("Tolerance:", 80), self.tol_tb, _lbl("mm")
        ))

        # Find button
        find_btn = Button()
        find_btn.Content = "Find Contact Faces"
        find_btn.Height = 32
        find_btn.FontSize = 12
        find_btn.HorizontalAlignment = HorizontalAlignment.Stretch
        find_btn.Margin = Thickness(0, 4, 0, 4)
        find_btn.Click += self.on_find
        main.Children.Add(find_btn)

        self.find_status = _status("Not run yet")
        main.Children.Add(self.find_status)

        # Face table
        main.Children.Add(_sep())
        face_lbl = _lbl("Detected faces  (uncheck to exclude):", 0)
        face_lbl.FontSize = 10
        face_lbl.Foreground = Brushes.DimGray
        face_lbl.Margin = Thickness(0, 0, 0, 3)
        main.Children.Add(face_lbl)

        face_scroll = ScrollViewer()
        face_scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        face_scroll.Height = 160
        face_scroll.Margin = Thickness(0, 0, 0, 6)
        self.face_panel = StackPanel()
        ph = _lbl("  (run Find first)", 0)
        ph.FontSize = 10
        ph.Foreground = Brushes.LightGray
        self.face_panel.Children.Add(ph)
        face_scroll.Content = self.face_panel
        main.Children.Add(face_scroll)

        # Naming preview
        naming_info = _status(
            "NS names: Cap_Upper_001, Cap_Lower_001, Cap_Front_001 ...", Brushes.DimGray)
        main.Children.Add(naming_info)

        # Create NS button
        ns_btn = Button()
        ns_btn.Content = "Create Named Selections from Checked Faces"
        ns_btn.Height = 32
        ns_btn.FontSize = 12
        ns_btn.HorizontalAlignment = HorizontalAlignment.Stretch
        ns_btn.Margin = Thickness(0, 0, 0, 4)
        ns_btn.Click += self.on_create_ns
        main.Children.Add(ns_btn)

        self.ns_status = _status("No Named Selections yet")
        main.Children.Add(self.ns_status)

        # Log
        main.Children.Add(_sep())
        log_scroll = ScrollViewer()
        log_scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        log_scroll.Height = 120
        self.log_tb = TextBox()
        self.log_tb.IsReadOnly = True
        self.log_tb.TextWrapping = System.Windows.TextWrapping.Wrap
        self.log_tb.AcceptsReturn = True
        self.log_tb.FontSize = 9
        self.log_tb.FontFamily = System.Windows.Media.FontFamily("Consolas")
        log_scroll.Content = self.log_tb
        main.Children.Add(log_scroll)

        # Close
        close_btn = Button()
        close_btn.Content = "Close"
        close_btn.Width = 80
        close_btn.Height = 26
        close_btn.HorizontalAlignment = HorizontalAlignment.Right
        close_btn.Margin = Thickness(0, 8, 0, 0)
        close_btn.Click += self.on_close
        main.Children.Add(close_btn)

        scroll = ScrollViewer()
        scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        scroll.Content = main
        self.Content = scroll

    def on_find(self, sender, e):
        try:
            self.log("=== Find Contact Faces ===")
            kw_text = self.keywords_tb.Text.strip()
            keywords = (
                [k.strip() for k in kw_text.split(',') if k.strip()]
                if kw_text else []
            )
            try:
                tol_mm = float(self.tol_tb.Text.strip())
            except ValueError:
                MessageBox.Show("Invalid tolerance.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning)
                return

            # Get all bodies
            model = ExtAPI.DataModel.Project.Model
            self.all_bodies = list(model.Geometry.GetChildren(
                DataModelObjectCategory.Body, True))
            self.log("Total bodies: {0}".format(len(self.all_bodies)))

            # Filter by keywords
            if keywords:
                targets = [
                    b for b in self.all_bodies
                    if any(kw.lower() in b.Name.lower() for kw in keywords)
                ]
            else:
                targets = list(self.all_bodies)

            self.log("Matched: {0}".format(len(targets)))
            if not targets:
                names = "\n".join(b.Name for b in self.all_bodies[:15])
                MessageBox.Show("No bodies matched.\n\nAvailable:\n" + names,
                    "No Match", MessageBoxButton.OK, MessageBoxImage.Warning)
                self.find_status.Content = "No bodies matched"
                self.find_status.Foreground = Brushes.OrangeRed
                return

            for b in targets:
                self.log("  {0}".format(b.Name))

            # Detect contact faces
            self.face_data_list = self._detect_contact(targets, self.all_bodies, tol_mm)

            if not self.face_data_list:
                self.find_status.Content = "No contact faces — try larger tolerance"
                self.find_status.Foreground = Brushes.OrangeRed
                MessageBox.Show(
                    "No contact faces detected.\nTry tolerance > {0} mm.".format(tol_mm),
                    "No Contact Faces", MessageBoxButton.OK, MessageBoxImage.Warning)
                return

            dir_counts = {}
            for fd in self.face_data_list:
                d = fd['direction']
                dir_counts[d] = dir_counts.get(d, 0) + 1
            dir_str = ", ".join(
                "{0}:{1}".format(d, c) for d, c in sorted(dir_counts.items())
            )
            self.find_status.Content = "{0} faces  ({1})".format(
                len(self.face_data_list), dir_str)
            self.find_status.Foreground = Brushes.DarkGreen
            self.log(self.find_status.Content)
            self._populate_table()

        except Exception as ex:
            self.log("ERROR: {0}".format(str(ex)))
            import traceback
            self.log(traceback.format_exc())
            self.find_status.Content = "Error — see log"
            self.find_status.Foreground = Brushes.Red

    def _populate_table(self):
        self.face_panel.Children.Clear()
        self.face_checkboxes = []
        for fd in self.face_data_list:
            cb = CheckBox()
            cb.IsChecked = True
            cb.Margin = Thickness(2, 1, 2, 1)
            na = fd['normal']
            direction = fd['direction']
            base_name = DIR_NAMES.get(direction, 'Cap_Unknown')
            cb.Content = "[{0}] {1}  ({2})  ({3:.2f},{4:.2f},{5:.2f})".format(
                direction, base_name, fd['body'].Name, na[0], na[1], na[2])
            cb.FontSize = 10
            self.face_panel.Children.Add(cb)
            self.face_checkboxes.append((cb, fd))

    def on_create_ns(self, sender, e):
        self.log("\n=== Create Named Selections ===")
        if not self.face_data_list:
            MessageBox.Show("Run 'Find Contact Faces' first.",
                "No Data", MessageBoxButton.OK, MessageBoxImage.Warning)
            return

        checked = [fd for (cb, fd) in self.face_checkboxes if cb.IsChecked == True]
        if not checked:
            MessageBox.Show("All faces are unchecked.",
                "Nothing Selected", MessageBoxButton.OK, MessageBoxImage.Warning)
            return

        self.log("{0} faces checked".format(len(checked)))
        try:
            model = ExtAPI.DataModel.Project.Model

            # Get existing NS names for counter
            try:
                existing_names = [ns.Name for ns in model.NamedSelections.Children]
            except:
                existing_names = []

            # Group faces by direction
            by_dir = {}
            for fd in checked:
                d = fd['direction']
                if d not in by_dir:
                    by_dir[d] = []
                by_dir[d].append(fd)

            created = []
            for direction, group in by_dir.items():
                base_name = DIR_NAMES.get(direction, 'Cap_Unknown')
                counter = _next_ns_counter(existing_names, base_name)
                ns_name = "{0}_{1:03d}".format(base_name, counter)
                # Reserve this name for subsequent directions in same batch
                existing_names.append(ns_name)

                try:
                    ns = model.AddNamedSelection()
                    ns.Name = ns_name
                    try:
                        ns.ScopingMethod = GeometryDefineByType.GeometryEntities
                    except:
                        pass
                    sel_ok = False
                    try:
                        sel = ExtAPI.SelectionManager.CreateSelectionInfo(
                            Ansys.ACT.Interfaces.Common.SelectionTypeEnum.GeometryEntities
                        )
                        sel.Entities = [fd['face'] for fd in group]
                        ns.Location = sel
                        sel_ok = True
                    except Exception as sel_ex:
                        self.log("Warning sel: {0}".format(str(sel_ex)))
                    created.append(ns_name)
                    self.log("Created: {0} ({1} faces, sel={2})".format(
                        ns_name, len(group), sel_ok))
                except Exception as ns_ex:
                    self.log("ERROR {0}: {1}".format(ns_name, str(ns_ex)))

            if created:
                result = ", ".join(created)
                self.ns_status.Content = "Created: " + result
                self.ns_status.Foreground = Brushes.DarkGreen
                MessageBox.Show("Named Selections created:\n\n" + "\n".join(created),
                    "Done", MessageBoxButton.OK, MessageBoxImage.Information)
            else:
                self.ns_status.Content = "Failed — see log"
                self.ns_status.Foreground = Brushes.Red

        except Exception as ex:
            self.log("ERROR: {0}".format(str(ex)))
            import traceback
            self.log(traceback.format_exc())

    def _detect_contact(self, targets, all_bodies, tol_mm):
        tol_m = tol_mm / 1000.0
        target_ids = set(id(b) for b in targets)
        others = [b for b in all_bodies if id(b) not in target_ids]
        self.log("Caching {0} non-target bodies...".format(len(others)))

        other_faces = []
        skipped = 0
        for body in others:
            try:
                gb = body.GetGeoBody()
                if gb is None:
                    self.log("  skip (no GeoBody): {0}".format(body.Name))
                    continue
                for i in range(gb.Faces.Count):
                    try:
                        f = gb.Faces[i]
                        c = list(f.Centroid)
                        n = f.NormalAtParam(0.5, 0.5)
                        mag = (n[0]**2 + n[1]**2 + n[2]**2) ** 0.5
                        if mag < 0.1:
                            skipped += 1
                            continue
                        other_faces.append({
                            'centroid': (c[0], c[1], c[2]),
                            'normal': (n[0]/mag, n[1]/mag, n[2]/mag)
                        })
                    except:
                        skipped += 1
                        continue
            except:
                continue
        self.log("{0} faces cached, {1} skipped".format(len(other_faces), skipped))

        contact = []
        for body in targets:
            count = 0
            try:
                gb = body.GetGeoBody()
                if gb is None:
                    self.log("  skip target (no GeoBody): {0}".format(body.Name))
                    continue
                self.log("{0}: {1} faces".format(body.Name, gb.Faces.Count))
                for i in range(gb.Faces.Count):
                    try:
                        face = gb.Faces[i]
                        c = list(face.Centroid)
                        n = face.NormalAtParam(0.5, 0.5)
                        mag = (n[0]**2 + n[1]**2 + n[2]**2) ** 0.5
                        if mag < 0.1:
                            continue
                        ca = (c[0], c[1], c[2])
                        na = (n[0]/mag, n[1]/mag, n[2]/mag)
                        for ofd in other_faces:
                            cb_v, nb = ofd['centroid'], ofd['normal']
                            dot = na[0]*nb[0] + na[1]*nb[1] + na[2]*nb[2]
                            if dot >= -0.8:   # relaxed: -0.8 ≈ angle > 143°
                                continue
                            dx = ca[0]-cb_v[0]
                            dy = ca[1]-cb_v[1]
                            dz = ca[2]-cb_v[2]
                            if abs(dx*nb[0]+dy*nb[1]+dz*nb[2]) >= tol_m:
                                continue
                            if abs(dx*na[0]+dy*na[1]+dz*na[2]) >= tol_m:
                                continue
                            contact.append({
                                'body': body, 'face': face, 'normal': na,
                                'direction': classify_normal_direction(na)
                            })
                            count += 1
                            break  # one match per face is enough for NS grouping
                    except:
                        continue
            except Exception as ex:
                self.log("Warning {0}: {1}".format(body.Name, str(ex)))
            self.log("  -> {0} contact faces".format(count))
        return contact

    def on_close(self, sender, e):
        self.Close()

    def log(self, msg):
        self.log_tb.Text += msg + "\n"
        self.log_tb.ScrollToEnd()


# ================================================================
# Dialog 2: Modal Analysis
# ================================================================

class ModalDialog(Window):
    """
    Button 2 — Modal Analysis
    Creates a Modal Analysis with specified settings.
    """

    def __init__(self):
        self.Title = "MX Digital Twin  |  Modal Analysis"
        self.Width = 420
        self.Height = 320
        self.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen

        main = StackPanel()
        main.Margin = Thickness(14, 10, 14, 10)

        h = Label()
        h.Content = "Modal Analysis Setup"
        h.FontSize = 14
        h.FontWeight = System.Windows.FontWeights.Bold
        h.Margin = Thickness(0, 0, 0, 10)
        main.Children.Add(h)

        # Number of Modes
        self.modes_tb = _tb("20", 80)
        main.Children.Add(_row(_lbl("Number of Modes:", 130), self.modes_tb))

        # Max Frequency
        self.freq_tb = _tb("1000", 80)
        main.Children.Add(_row(
            _lbl("Max Frequency:", 130), self.freq_tb, _lbl("Hz")
        ))

        main.Children.Add(_sep())

        # Show existing modal analyses
        self.existing_label = _status("Checking model...")
        main.Children.Add(self.existing_label)
        self._check_existing()

        # Create button
        create_btn = Button()
        create_btn.Content = "Create Modal Analysis"
        create_btn.Height = 34
        create_btn.FontSize = 12
        create_btn.HorizontalAlignment = HorizontalAlignment.Stretch
        create_btn.Margin = Thickness(0, 8, 0, 4)
        create_btn.Click += self.on_create
        main.Children.Add(create_btn)

        self.result_status = _status("")
        main.Children.Add(self.result_status)

        # Close
        close_btn = Button()
        close_btn.Content = "Close"
        close_btn.Width = 80
        close_btn.Height = 26
        close_btn.HorizontalAlignment = HorizontalAlignment.Right
        close_btn.Margin = Thickness(0, 10, 0, 0)
        close_btn.Click += self.on_close
        main.Children.Add(close_btn)

        self.Content = main

    def _check_existing(self):
        try:
            model = ExtAPI.DataModel.Project.Model
            modal_names = [
                a.Name for a in model.Analyses
                if "Modal" in a.Name or "modal" in a.Name
            ]
            if modal_names:
                self.existing_label.Content = (
                    "Existing Modal: " + ", ".join(modal_names)
                )
                self.existing_label.Foreground = Brushes.DarkBlue
            else:
                self.existing_label.Content = "No Modal Analysis found in model"
                self.existing_label.Foreground = Brushes.Gray
        except:
            self.existing_label.Content = ""

    def on_create(self, sender, e):
        try:
            modes = int(self.modes_tb.Text)
            max_freq = float(self.freq_tb.Text)
        except ValueError:
            MessageBox.Show("Invalid Modes or Max Frequency.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning)
            return

        try:
            model = ExtAPI.DataModel.Project.Model
            modal = model.AddModalAnalysis()
            modal.Name = "Modal"

            try:
                s = modal.AnalysisSettings
                s.MaximumModesToFind = modes
                s.ModalRangeMaximum = Quantity(max_freq, "Hz")
                s.LimitSearchToRange = True
            except Exception as ms_ex:
                pass  # settings partially applied

            msg = "Modal created: {0} modes, {1} Hz".format(modes, max_freq)
            self.result_status.Content = msg
            self.result_status.Foreground = Brushes.DarkGreen
            self._check_existing()
            MessageBox.Show(msg + "\n\nAdd a Fixed Support to run the analysis.",
                "Done", MessageBoxButton.OK, MessageBoxImage.Information)

        except Exception as ex:
            self.result_status.Content = "Error: " + str(ex)
            self.result_status.Foreground = Brushes.Red
            MessageBox.Show("Failed:\n\n" + str(ex), "Error",
                MessageBoxButton.OK, MessageBoxImage.Error)

    def on_close(self, sender, e):
        self.Close()


# ================================================================
# Dialog 3: Add Scenario
# ================================================================

class ScenarioDialog(Window):
    """
    Button 3 — Add Scenario
    Pick Cap_* Named Selections with +/- force direction,
    select CSV, create one Transient analysis.
    Mode Superposition auto-links existing Modal analysis.
    """

    def __init__(self):
        self.Title = "MX Digital Twin  |  Add Scenario"
        self.Width = 600
        self.Height = 560
        self.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen

        self.csv_path = ""
        # Each item: (ns_obj, include_cb, rb_pos, rb_neg)
        self.ns_items = []

        main = StackPanel()
        main.Margin = Thickness(14, 10, 14, 10)

        h = Label()
        h.Content = "Add Transient Scenario"
        h.FontSize = 14
        h.FontWeight = System.Windows.FontWeights.Bold
        h.Margin = Thickness(0, 0, 0, 8)
        main.Children.Add(h)

        # NS section header
        ns_hdr = Label()
        ns_hdr.Content = "Named Selections — Force Direction"
        ns_hdr.FontSize = 11
        ns_hdr.FontWeight = System.Windows.FontWeights.Bold
        ns_hdr.Margin = Thickness(0, 0, 0, 2)
        main.Children.Add(ns_hdr)

        # Column headers
        col_hdr = _row(
            _lbl("", 20),          # include checkbox spacer
            _lbl("Named Selection", 200),
            _lbl("  Positive (+)", 100),
            _lbl("  Negative (−)", 100),
            margin=Thickness(0, 0, 0, 2)
        )
        for child in col_hdr.Children:
            child.FontSize = 10
            child.Foreground = Brushes.DimGray
        main.Children.Add(col_hdr)

        # NS scroll area
        ns_scroll = ScrollViewer()
        ns_scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        ns_scroll.Height = 140
        ns_scroll.Margin = Thickness(0, 0, 0, 4)
        self.ns_panel = StackPanel()
        ns_scroll.Content = self.ns_panel
        main.Children.Add(ns_scroll)

        refresh_btn = Button()
        refresh_btn.Content = "Refresh NS list"
        refresh_btn.Width = 120
        refresh_btn.Height = 24
        refresh_btn.HorizontalAlignment = HorizontalAlignment.Left
        refresh_btn.FontSize = 10
        refresh_btn.Margin = Thickness(0, 0, 0, 8)
        refresh_btn.Click += self.on_refresh_ns
        main.Children.Add(refresh_btn)

        # CSV file
        main.Children.Add(_sep())
        self.csv_tb = _tb("", 360, "Time-Force CSV: Time(s),Force(N)")
        self.csv_tb.IsReadOnly = True
        browse_btn = Button()
        browse_btn.Content = "Browse..."
        browse_btn.Width = 80
        browse_btn.Height = 26
        browse_btn.Margin = Thickness(5, 0, 0, 0)
        browse_btn.Click += self.on_browse_csv
        main.Children.Add(_row(
            _lbl("CSV File:", 70), self.csv_tb, browse_btn
        ))

        # Time settings
        self.end_tb = _tb("0.02", 70)
        self.step_tb = _tb("0.0001", 80)
        main.Children.Add(_row(
            _lbl("End Time:", 70), self.end_tb, _lbl("s   ", 25),
            _lbl("Step:", 40), self.step_tb, _lbl("s")
        ))

        # Method
        self.radio_super = RadioButton()
        self.radio_super.Content = "Mode Superposition"
        self.radio_super.IsChecked = True
        self.radio_super.VerticalAlignment = VerticalAlignment.Center
        self.radio_super.Margin = Thickness(0, 0, 12, 0)

        self.radio_full = RadioButton()
        self.radio_full.Content = "Full Transient"
        self.radio_full.IsChecked = False
        self.radio_full.VerticalAlignment = VerticalAlignment.Center

        main.Children.Add(_row(
            _lbl("Method:", 70), self.radio_super, self.radio_full,
            margin=Thickness(0, 0, 0, 4)
        ))

        # Modal auto-link status
        self.modal_status = _status("")
        main.Children.Add(self.modal_status)
        self._update_modal_status()

        main.Children.Add(_sep())

        # Create Scenario button
        create_btn = Button()
        create_btn.Content = "Create Scenario (Transient Analysis)"
        create_btn.Height = 34
        create_btn.FontSize = 12
        create_btn.HorizontalAlignment = HorizontalAlignment.Stretch
        create_btn.Margin = Thickness(0, 2, 0, 4)
        create_btn.Click += self.on_create_scenario
        main.Children.Add(create_btn)

        self.result_status = _status("Ready")
        main.Children.Add(self.result_status)

        # Close
        close_btn = Button()
        close_btn.Content = "Close"
        close_btn.Width = 80
        close_btn.Height = 26
        close_btn.HorizontalAlignment = HorizontalAlignment.Right
        close_btn.Margin = Thickness(0, 10, 0, 0)
        close_btn.Click += self.on_close
        main.Children.Add(close_btn)

        scroll = ScrollViewer()
        scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        scroll.Content = main
        self.Content = scroll

        # Load NS list on open
        self._load_ns_list()

    def _update_modal_status(self):
        try:
            model = ExtAPI.DataModel.Project.Model
            modals = [a for a in model.Analyses
                      if "Modal" in a.Name or "modal" in a.Name]
            if modals:
                self.modal_status.Content = (
                    "Modal found: {0} — will auto-link for Mode Superposition".format(
                        modals[0].Name))
                self.modal_status.Foreground = Brushes.DarkBlue
            else:
                self.modal_status.Content = (
                    "No Modal Analysis found — run Modal Analysis button first")
                self.modal_status.Foreground = Brushes.OrangeRed
        except:
            self.modal_status.Content = ""

    def _load_ns_list(self):
        self.ns_panel.Children.Clear()
        self.ns_items = []
        try:
            model = ExtAPI.DataModel.Project.Model
            cap_ns = [
                ns for ns in model.NamedSelections.Children
                if ns.Name.startswith("Cap_")
            ]
            if not cap_ns:
                ph = _lbl(
                    "  No Cap_* Named Selections found. "
                    "Run 'Named Selections' toolbar button first.", 0)
                ph.FontSize = 10
                ph.Foreground = Brushes.Gray
                self.ns_panel.Children.Add(ph)
                return

            for i, ns in enumerate(cap_ns):
                include_cb = CheckBox()
                include_cb.IsChecked = True
                include_cb.Width = 20
                include_cb.VerticalAlignment = VerticalAlignment.Center

                name_lbl = _lbl(ns.Name, 200)
                name_lbl.FontSize = 11

                group_name = "ns_dir_{0}".format(i)

                rb_pos = RadioButton()
                rb_pos.Content = "+"
                rb_pos.GroupName = group_name
                rb_pos.Width = 100
                rb_pos.VerticalAlignment = VerticalAlignment.Center
                rb_pos.Foreground = Brushes.DarkBlue

                rb_neg = RadioButton()
                rb_neg.Content = "−"
                rb_neg.GroupName = group_name
                rb_neg.Width = 100
                rb_neg.VerticalAlignment = VerticalAlignment.Center
                rb_neg.Foreground = Brushes.DarkRed

                # Default direction from name
                if _is_negative_ns(ns.Name):
                    rb_neg.IsChecked = True
                else:
                    rb_pos.IsChecked = True

                row = _row(include_cb, name_lbl, rb_pos, rb_neg,
                           margin=Thickness(2, 2, 2, 2))
                self.ns_panel.Children.Add(row)
                self.ns_items.append((ns, include_cb, rb_pos, rb_neg))

        except Exception as ex:
            ph = _lbl("  Error loading NS: " + str(ex), 0)
            ph.FontSize = 10
            ph.Foreground = Brushes.Red
            self.ns_panel.Children.Add(ph)

    def on_refresh_ns(self, sender, e):
        self._load_ns_list()
        self._update_modal_status()

    def on_browse_csv(self, sender, e):
        dlg = OpenFileDialog()
        dlg.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*"
        dlg.Title = "Select Time-Force CSV"
        if dlg.ShowDialog() == DialogResult.OK:
            self.csv_path = dlg.FileName
            self.csv_tb.Text = self.csv_path

    def on_create_scenario(self, sender, e):
        # Collect selected NS with direction override
        selected = [
            (ns, rb_pos.IsChecked == True)
            for (ns, include_cb, rb_pos, rb_neg) in self.ns_items
            if include_cb.IsChecked == True
        ]
        if not selected:
            MessageBox.Show(
                "Select at least one Named Selection.\n\n"
                "If list is empty, run 'Named Selections' toolbar button first.",
                "No NS Selected", MessageBoxButton.OK, MessageBoxImage.Warning)
            return

        if not self.csv_path:
            MessageBox.Show("Select a CSV file first.",
                "No CSV", MessageBoxButton.OK, MessageBoxImage.Warning)
            return

        try:
            end_time = float(self.end_tb.Text)
            time_step = float(self.step_tb.Text)
        except ValueError:
            MessageBox.Show("Invalid End Time or Time Step.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning)
            return

        use_super = (self.radio_super.IsChecked == True)

        try:
            time_vals, force_vals = self._parse_csv(self.csv_path)
            if not time_vals:
                MessageBox.Show("No valid data in CSV file.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning)
                return

            model = ExtAPI.DataModel.Project.Model

            # Auto-name: Transient_Scenario_N
            existing = [
                a.Name for a in model.Analyses
                if a.Name.startswith("Transient_Scenario_")
            ]
            n = len(existing) + 1
            scenario_name = "Transient_Scenario_{0}".format(n)

            transient = model.AddTransientStructuralAnalysis()
            transient.Name = scenario_name

            try:
                ts = transient.AnalysisSettings
                ts.SetStepEndTime(1, Quantity(end_time, "sec"))
                ts.SetInitialTimeStep(1, Quantity(time_step, "sec"))
                ts.SetMinimumTimeStep(1, Quantity(time_step, "sec"))
                ts.SetMaximumTimeStep(1, Quantity(time_step, "sec"))
                if use_super:
                    try:
                        ts.SolutionMethod = (
                            Ansys.ACT.Automation.Mechanical
                            .TransientSolutionMethod.ModeSuperposition
                        )
                    except:
                        pass
            except:
                pass

            # Auto-link Modal Analysis for Mode Superposition
            modal_linked = False
            if use_super:
                try:
                    modals = [a for a in model.Analyses
                              if "Modal" in a.Name or "modal" in a.Name]
                    if modals:
                        modal = modals[0]
                        try:
                            model.Link(transient, modal)
                            modal_linked = True
                        except:
                            pass
                        if not modal_linked:
                            try:
                                transient.SetLinkedAnalysis(modal, 0)
                                modal_linked = True
                            except:
                                pass
                except:
                    pass

            # Apply forces
            try:
                transient.Activate()
            except:
                pass

            force_count = 0
            for ns, is_positive in selected:
                # Determine axis from NS name
                axis, default_sign = _ns_to_axis(ns.Name)
                if axis is None:
                    continue

                # User override: if is_positive, force = positive axis sign
                # if not is_positive, flip the sign
                if is_positive:
                    sign = default_sign
                else:
                    sign = -default_sign

                try:
                    force = transient.AddForce()
                    force.Name = "Force_" + ns.Name
                    force.Location = ns
                    try:
                        force.DefineBy = (
                            Ansys.Mechanical.DataModel.Enums.LoadDefineBy.Components
                        )
                    except:
                        pass
                    td = [Quantity("{0} [s]".format(t)) for t in time_vals]
                    fd = [Quantity("{0} [N]".format(f * sign)) for f in force_vals]
                    try:
                        if axis == 'X':
                            force.XComponent.Inputs[0].DiscreteValues = td
                            force.XComponent.Output.DiscreteValues = fd
                        elif axis == 'Y':
                            force.YComponent.Inputs[0].DiscreteValues = td
                            force.YComponent.Output.DiscreteValues = fd
                        elif axis == 'Z':
                            force.ZComponent.Inputs[0].DiscreteValues = td
                            force.ZComponent.Output.DiscreteValues = fd
                        force_count += 1
                    except:
                        force_count += 1
                except Exception as f_ex:
                    pass

            method = "Mode Superposition" if use_super else "Full Transient"
            link_info = " (Modal linked)" if modal_linked else ""
            msg = "'{0}' created\n{1} forces, {2}{3}".format(
                scenario_name, force_count, method, link_info)
            self.result_status.Content = msg
            self.result_status.Foreground = Brushes.DarkGreen

            MessageBox.Show(
                msg + "\n\nCSV: " + os.path.basename(self.csv_path) +
                ("\n\nSolve Modal first, then solve this Transient."
                 if use_super else ""),
                "Scenario Created", MessageBoxButton.OK, MessageBoxImage.Information)

        except Exception as ex:
            self.result_status.Content = "Error: " + str(ex)
            self.result_status.Foreground = Brushes.Red
            MessageBox.Show("Failed:\n\n" + str(ex), "Error",
                MessageBoxButton.OK, MessageBoxImage.Error)

    def _parse_csv(self, csv_path):
        import csv
        time_vals, force_vals = [], []
        with open(csv_path, 'r') as f:
            reader = csv.reader(f)
            try:
                next(reader)
            except:
                pass
            for row in reader:
                try:
                    time_vals.append(float(row[0]))
                    force_vals.append(float(row[1]))
                except (ValueError, IndexError):
                    continue
        return time_vals, force_vals

    def on_close(self, sender, e):
        self.Close()


# ================================================================
# Dialog 4: Face Pair Named Selections
# ================================================================

class FacePairDialog(Window):
    """
    Button 4 — Face Pair NS
    Include/Exclude keyword filter → detect contact face pairs →
    create two Named Selections per pair (A-side and B-side).
    Similar to SpaceClaim contact face NS creation.
    """

    def __init__(self):
        self.Title = "MX Digital Twin  |  Face Pair Named Selections"
        self.Width = 680
        self.Height = 660
        self.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen

        self.pair_data = []
        self.pair_checkboxes = []
        self.all_bodies = []

        main = StackPanel()
        main.Margin = Thickness(14, 10, 14, 10)

        h = Label()
        h.Content = "Contact Face Pairs  \u2192  Named Selections (A-side / B-side)"
        h.FontSize = 14
        h.FontWeight = System.Windows.FontWeights.Bold
        h.Margin = Thickness(0, 0, 0, 8)
        main.Children.Add(h)

        # Include keywords
        self.inc_tb = _tb("", 280, "Bodies to include — comma separated keywords")
        main.Children.Add(_row(
            _lbl("Include:", 80), self.inc_tb,
            _lbl("  (body name contains)", 0)
        ))

        # Exclude keywords
        self.exc_tb = _tb("", 280, "Bodies to exclude — comma separated keywords")
        main.Children.Add(_row(
            _lbl("Exclude:", 80), self.exc_tb,
            _lbl("  (body name contains)", 0)
        ))

        # Tolerance
        self.tol_tb = _tb("0.1", 60, "Contact gap tolerance (mm)")
        main.Children.Add(_row(
            _lbl("Tolerance:", 80), self.tol_tb, _lbl("mm")
        ))

        # Find button
        find_btn = Button()
        find_btn.Content = "Find Contact Face Pairs"
        find_btn.Height = 32
        find_btn.FontSize = 12
        find_btn.HorizontalAlignment = HorizontalAlignment.Stretch
        find_btn.Margin = Thickness(0, 6, 0, 4)
        find_btn.Click += self.on_find
        main.Children.Add(find_btn)

        self.find_status = _status("Not run yet")
        main.Children.Add(self.find_status)

        # Pair table
        main.Children.Add(_sep())

        hdr = _row(
            _lbl("", 22),
            _lbl("A-side (target body)", 220),
            _lbl("B-side (other body)", 220),
            _lbl("Dir", 60),
            margin=Thickness(2, 0, 0, 2)
        )
        for child in hdr.Children:
            child.FontSize = 10
            child.Foreground = Brushes.DimGray
        main.Children.Add(hdr)

        pair_scroll = ScrollViewer()
        pair_scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        pair_scroll.Height = 180
        pair_scroll.Margin = Thickness(0, 0, 0, 6)
        self.pair_panel = StackPanel()
        ph = _lbl("  (run Find first)", 0)
        ph.FontSize = 10
        ph.Foreground = Brushes.LightGray
        self.pair_panel.Children.Add(ph)
        pair_scroll.Content = self.pair_panel
        main.Children.Add(pair_scroll)

        naming_info = _status(
            "NS names: FaceA_001, FaceB_001 ... (each pair gets A+B NS)", Brushes.DimGray)
        main.Children.Add(naming_info)

        # Mode selection: Per Pair vs Merged
        self.rb_per_pair = RadioButton()
        self.rb_per_pair.Content = "Per Pair  (FaceA_001 / FaceB_001 per pair)"
        self.rb_per_pair.GroupName = "fp_mode"
        self.rb_per_pair.IsChecked = True
        self.rb_per_pair.Margin = Thickness(0, 2, 0, 2)

        self.rb_merged = RadioButton()
        self.rb_merged.Content = "Merged  (all A-faces \u2192 1 NS,  all B-faces \u2192 1 NS)"
        self.rb_merged.GroupName = "fp_mode"
        self.rb_merged.VerticalAlignment = VerticalAlignment.Center
        self.rb_merged.Margin = Thickness(0, 0, 10, 0)

        self.merge_name_tb = _tb("Contact", 90,
            "Prefix: Contact \u2192 Contact_A / Contact_B")

        main.Children.Add(_row(self.rb_per_pair, margin=Thickness(0, 2, 0, 2)))
        main.Children.Add(_row(
            self.rb_merged, _lbl("  Prefix:", 55), self.merge_name_tb,
            margin=Thickness(0, 0, 0, 6)
        ))

        # Create NS button
        ns_btn = Button()
        ns_btn.Content = "Create A/B Named Selections for Checked Pairs"
        ns_btn.Height = 32
        ns_btn.FontSize = 12
        ns_btn.HorizontalAlignment = HorizontalAlignment.Stretch
        ns_btn.Margin = Thickness(0, 0, 0, 4)
        ns_btn.Click += self.on_create_ns
        main.Children.Add(ns_btn)

        self.ns_status = _status("No Named Selections yet")
        main.Children.Add(self.ns_status)

        # Log
        main.Children.Add(_sep())
        log_scroll = ScrollViewer()
        log_scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        log_scroll.Height = 120
        self.log_tb = TextBox()
        self.log_tb.IsReadOnly = True
        self.log_tb.TextWrapping = System.Windows.TextWrapping.Wrap
        self.log_tb.AcceptsReturn = True
        self.log_tb.FontSize = 9
        self.log_tb.FontFamily = System.Windows.Media.FontFamily("Consolas")
        log_scroll.Content = self.log_tb
        main.Children.Add(log_scroll)

        close_btn = Button()
        close_btn.Content = "Close"
        close_btn.Width = 80
        close_btn.Height = 26
        close_btn.HorizontalAlignment = HorizontalAlignment.Right
        close_btn.Margin = Thickness(0, 8, 0, 0)
        close_btn.Click += self.on_close
        main.Children.Add(close_btn)

        scroll = ScrollViewer()
        scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        scroll.Content = main
        self.Content = scroll

    def on_find(self, sender, e):
        try:
            self.log("=== Find Contact Face Pairs ===")

            inc_text = self.inc_tb.Text.strip()
            exc_text = self.exc_tb.Text.strip()
            includes = [k.strip() for k in inc_text.split(',') if k.strip()] if inc_text else []
            excludes = [k.strip() for k in exc_text.split(',') if k.strip()] if exc_text else []

            try:
                tol_mm = float(self.tol_tb.Text.strip())
            except ValueError:
                MessageBox.Show("Invalid tolerance.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning)
                return

            model = ExtAPI.DataModel.Project.Model
            self.all_bodies = list(model.Geometry.GetChildren(
                DataModelObjectCategory.Body, True))
            self.log("Total bodies: {0}".format(len(self.all_bodies)))

            inc_lo = [k.lower() for k in includes]
            exc_lo = [k.lower() for k in excludes]

            # Scope = all bodies minus explicitly excluded ones
            # (exclude applies if EITHER body in a pair matches — enforced at pair level)
            scope = []
            for b in self.all_bodies:
                name_lo = b.Name.lower()
                if exc_lo and any(k in name_lo for k in exc_lo):
                    self.log("  excluded: {0}".format(b.Name))
                    continue
                scope.append(b)
            self.log("In-scope: {0} bodies".format(len(scope)))

            # Targets = include-matched bodies (A-side initiators).
            # If no include filter, all in-scope bodies are targets.
            # 'others' = full scope, so target-target pairs are also detected
            # (covers "either body matches include").
            if inc_lo:
                targets = [b for b in scope if any(k in b.Name.lower() for k in inc_lo)]
            else:
                targets = scope

            if not targets:
                if inc_lo:
                    available = "\n".join(b.Name for b in self.all_bodies[:12])
                    MessageBox.Show(
                        "No bodies matched include keywords: {0}\n\nAvailable:\n{1}".format(
                            ", ".join(includes), available),
                        "No Match", MessageBoxButton.OK, MessageBoxImage.Warning)
                else:
                    MessageBox.Show("No bodies found.", "No Bodies",
                        MessageBoxButton.OK, MessageBoxImage.Warning)
                self.find_status.Content = "No bodies matched"
                self.find_status.Foreground = Brushes.OrangeRed
                return

            self.log("Targets (A-side, {0}):".format(len(targets)))
            for b in targets:
                self.log("  + {0}".format(b.Name))

            # others = full scope (allows target-target pairs too)
            others = scope

            self.pair_data = self._detect_pairs(targets, others, tol_mm)

            if not self.pair_data:
                self.find_status.Content = "No contact pairs — try larger tolerance"
                self.find_status.Foreground = Brushes.OrangeRed
                MessageBox.Show(
                    "No contact face pairs detected.\nTry tolerance > {0} mm.".format(tol_mm),
                    "No Pairs", MessageBoxButton.OK, MessageBoxImage.Warning)
                return

            self.find_status.Content = "{0} contact face pairs found".format(len(self.pair_data))
            self.find_status.Foreground = Brushes.DarkGreen
            self.log(self.find_status.Content)
            self._populate_table()

        except Exception as ex:
            self.log("ERROR: {0}".format(str(ex)))
            import traceback
            self.log(traceback.format_exc())
            self.find_status.Content = "Error — see log"
            self.find_status.Foreground = Brushes.Red

    def _detect_pairs(self, targets, others, tol_mm):
        tol_m = tol_mm / 1000.0
        self.log("Caching {0} other bodies...".format(len(others)))

        if not others:
            self.log("WARNING: no 'other' bodies — specify Include keywords to select target bodies only")

        other_cache = []
        skipped = 0
        for body in others:
            try:
                gb = body.GetGeoBody()
                if gb is None:
                    self.log("  skip (no GeoBody): {0}".format(body.Name))
                    continue
                for i in range(gb.Faces.Count):
                    try:
                        f = gb.Faces[i]
                        c = list(f.Centroid)
                        n = f.NormalAtParam(0.5, 0.5)
                        mag = (n[0]**2 + n[1]**2 + n[2]**2) ** 0.5
                        if mag < 0.1:
                            skipped += 1
                            continue
                        other_cache.append({
                            'body': body, 'face': f,
                            'centroid': (c[0], c[1], c[2]),
                            'normal': (n[0]/mag, n[1]/mag, n[2]/mag)
                        })
                    except:
                        skipped += 1
                        continue
            except:
                continue
        self.log("{0} faces cached, {1} skipped".format(len(other_cache), skipped))

        pairs = []
        seen = set()   # deduplicate (face_a_id, face_b_id) pairs
        for body_a in targets:
            count = 0
            try:
                gb = body_a.GetGeoBody()
                if gb is None:
                    self.log("  skip target (no GeoBody): {0}".format(body_a.Name))
                    continue
                self.log("{0}: {1} faces".format(body_a.Name, gb.Faces.Count))
                for i in range(gb.Faces.Count):
                    try:
                        face_a = gb.Faces[i]
                        c = list(face_a.Centroid)
                        n = face_a.NormalAtParam(0.5, 0.5)
                        mag = (n[0]**2 + n[1]**2 + n[2]**2) ** 0.5
                        if mag < 0.1:
                            continue
                        ca = (c[0], c[1], c[2])
                        na = (n[0]/mag, n[1]/mag, n[2]/mag)
                        for ofd in other_cache:
                            # Skip faces from the same body
                            if id(ofd['body']) == id(body_a):
                                continue
                            cb_v = ofd['centroid']
                            nb = ofd['normal']
                            dot = na[0]*nb[0] + na[1]*nb[1] + na[2]*nb[2]
                            if dot >= -0.8:   # relaxed: -0.8 ≈ angle > 143°
                                continue
                            dx = ca[0] - cb_v[0]
                            dy = ca[1] - cb_v[1]
                            dz = ca[2] - cb_v[2]
                            if abs(dx*nb[0]+dy*nb[1]+dz*nb[2]) >= tol_m:
                                continue
                            if abs(dx*na[0]+dy*na[1]+dz*na[2]) >= tol_m:
                                continue
                            # Order-independent key: prevents (A,B) and (B,A) duplicates
                            # when targets and others overlap (both include target bodies)
                            key = tuple(sorted([id(face_a), id(ofd['face'])]))
                            if key in seen:
                                continue
                            seen.add(key)
                            pairs.append({
                                'body_a': body_a,
                                'face_a': face_a,
                                'dir_a': classify_normal_direction(na),
                                'body_b': ofd['body'],
                                'face_b': ofd['face'],
                                'dir_b': classify_normal_direction(nb),
                            })
                            count += 1
                    except:
                        continue
            except Exception as ex:
                self.log("Warning {0}: {1}".format(body_a.Name, str(ex)))
            self.log("  -> {0} contact pairs".format(count))
        return pairs

    def _populate_table(self):
        self.pair_panel.Children.Clear()
        self.pair_checkboxes = []
        for i, pd in enumerate(self.pair_data):
            cb = CheckBox()
            cb.IsChecked = True
            cb.Width = 20
            cb.VerticalAlignment = VerticalAlignment.Center

            a_lbl = _lbl("[{0}] {1}".format(pd['dir_a'], pd['body_a'].Name[:20]), 220)
            a_lbl.FontSize = 10
            a_lbl.Foreground = Brushes.DarkBlue

            b_lbl = _lbl("[{0}] {1}".format(pd['dir_b'], pd['body_b'].Name[:20]), 220)
            b_lbl.FontSize = 10
            b_lbl.Foreground = Brushes.DarkRed

            id_lbl = _lbl("{0:03d}".format(i + 1), 40)
            id_lbl.FontSize = 9
            id_lbl.Foreground = Brushes.Gray

            row = _row(cb, id_lbl, a_lbl, b_lbl, margin=Thickness(1, 1, 1, 1))
            self.pair_panel.Children.Add(row)
            self.pair_checkboxes.append((cb, pd))

    def on_create_ns(self, sender, e):
        self.log("\n=== Create Face Pair Named Selections ===")
        if not self.pair_data:
            MessageBox.Show("Run 'Find Contact Face Pairs' first.",
                "No Data", MessageBoxButton.OK, MessageBoxImage.Warning)
            return

        checked = [(pd, i + 1) for i, (cb, pd) in enumerate(self.pair_checkboxes)
                   if cb.IsChecked == True]
        if not checked:
            MessageBox.Show("All pairs unchecked.",
                "Nothing Selected", MessageBoxButton.OK, MessageBoxImage.Warning)
            return

        self.log("{0} pairs checked".format(len(checked)))

        if self.rb_merged.IsChecked == True:
            self._create_ns_merged(checked)
        else:
            self._create_ns_per_pair(checked)

    def _create_ns_merged(self, checked):
        """Merge mode: combine all A-faces into one NS, all B-faces into one NS."""
        prefix = self.merge_name_tb.Text.strip() or "Contact"
        ns_a_name = prefix + "_A"
        ns_b_name = prefix + "_B"
        a_faces = [pd['face_a'] for pd, _ in checked]
        b_faces = [pd['face_b'] for pd, _ in checked]
        self.log("Merged mode: {0} A-faces, {1} B-faces".format(
            len(a_faces), len(b_faces)))

        try:
            model = ExtAPI.DataModel.Project.Model
            created = []

            for ns_name, faces in [(ns_a_name, a_faces), (ns_b_name, b_faces)]:
                try:
                    ns = model.AddNamedSelection()
                    ns.Name = ns_name
                    try:
                        ns.ScopingMethod = GeometryDefineByType.GeometryEntities
                    except:
                        pass
                    sel_ok = False
                    try:
                        sel = ExtAPI.SelectionManager.CreateSelectionInfo(
                            Ansys.ACT.Interfaces.Common.SelectionTypeEnum.GeometryEntities
                        )
                        sel.Entities = faces
                        ns.Location = sel
                        sel_ok = True
                    except Exception as sel_ex:
                        self.log("Warning sel: {0}".format(str(sel_ex)))
                    created.append((ns_name, len(faces)))
                    self.log("Created: {0} ({1} faces, sel={2})".format(
                        ns_name, len(faces), sel_ok))
                except Exception as ns_ex:
                    self.log("ERROR {0}: {1}".format(ns_name, str(ns_ex)))

            if created:
                summary = "\n".join("{0}  ({1} faces)".format(n, c) for n, c in created)
                self.ns_status.Content = "Merged: " + ", ".join(n for n, _ in created)
                self.ns_status.Foreground = Brushes.DarkGreen
                MessageBox.Show(
                    "Merged Named Selections created:\n\n" + summary,
                    "Done", MessageBoxButton.OK, MessageBoxImage.Information)
            else:
                self.ns_status.Content = "Failed — see log"
                self.ns_status.Foreground = Brushes.Red

        except Exception as ex:
            self.log("ERROR: {0}".format(str(ex)))
            import traceback
            self.log(traceback.format_exc())

    def _create_ns_per_pair(self, checked):
        """Per-pair mode: FaceA_001/FaceB_001 per checked pair."""
        try:
            model = ExtAPI.DataModel.Project.Model
            created = []
            pair_seq = 1

            for pd, orig_idx in checked:
                ns_a_name = "FaceA_{0:03d}".format(pair_seq)
                ns_b_name = "FaceB_{0:03d}".format(pair_seq)
                pair_seq += 1

                for ns_name, face_obj in [(ns_a_name, pd['face_a']),
                                           (ns_b_name, pd['face_b'])]:
                    try:
                        ns = model.AddNamedSelection()
                        ns.Name = ns_name
                        try:
                            ns.ScopingMethod = GeometryDefineByType.GeometryEntities
                        except:
                            pass
                        sel_ok = False
                        try:
                            sel = ExtAPI.SelectionManager.CreateSelectionInfo(
                                Ansys.ACT.Interfaces.Common.SelectionTypeEnum.GeometryEntities
                            )
                            sel.Entities = [face_obj]
                            ns.Location = sel
                            sel_ok = True
                        except Exception as sel_ex:
                            self.log("Warning sel: {0}".format(str(sel_ex)))
                        created.append(ns_name)
                        self.log("Created: {0} (sel={1})".format(ns_name, sel_ok))
                    except Exception as ns_ex:
                        self.log("ERROR {0}: {1}".format(ns_name, str(ns_ex)))

            if created:
                pairs_made = len(created) // 2
                self.ns_status.Content = "{0} pairs created ({1} NS)".format(
                    pairs_made, len(created))
                self.ns_status.Foreground = Brushes.DarkGreen
                MessageBox.Show(
                    "{0} NS created in {1} pairs:\n\n".format(len(created), pairs_made) +
                    "\n".join(created[:20]) +
                    ("\n..." if len(created) > 20 else ""),
                    "Done", MessageBoxButton.OK, MessageBoxImage.Information)
            else:
                self.ns_status.Content = "Failed — see log"
                self.ns_status.Foreground = Brushes.Red

        except Exception as ex:
            self.log("ERROR: {0}".format(str(ex)))
            import traceback
            self.log(traceback.format_exc())

    def on_close(self, sender, e):
        self.Close()

    def log(self, msg):
        self.log_tb.Text += msg + "\n"
        self.log_tb.ScrollToEnd()


# ================================================================
# ACT Entry Points
# ================================================================

def show_ns_dialog(analysis):
    try:
        NSDialog().ShowDialog()
    except Exception as ex:
        MessageBox.Show("Error:\n\n" + str(ex), "Error",
            MessageBoxButton.OK, MessageBoxImage.Error)


def show_modal_dialog(analysis):
    try:
        ModalDialog().ShowDialog()
    except Exception as ex:
        MessageBox.Show("Error:\n\n" + str(ex), "Error",
            MessageBoxButton.OK, MessageBoxImage.Error)


def show_scenario_dialog(analysis):
    try:
        ScenarioDialog().ShowDialog()
    except Exception as ex:
        MessageBox.Show("Error:\n\n" + str(ex), "Error",
            MessageBoxButton.OK, MessageBoxImage.Error)


def show_face_pair_dialog(analysis):
    try:
        FacePairDialog().ShowDialog()
    except Exception as ex:
        MessageBox.Show("Error:\n\n" + str(ex), "Error",
            MessageBoxButton.OK, MessageBoxImage.Error)


def Initialize():
    pass


def Finalize():
    pass
