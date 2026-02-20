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

import System

from System.Windows.Forms import OpenFileDialog, FolderBrowserDialog, DialogResult
from System.Windows import Window
from System.Windows.Controls import (
    StackPanel, Button, Label, TextBox, ListBox,
    Orientation, ScrollViewer, ScrollBarVisibility,
    CheckBox, RadioButton, Separator, ComboBox
)
from System.Windows import (
    HorizontalAlignment, VerticalAlignment,
    Thickness, MessageBox, MessageBoxButton, MessageBoxImage, MessageBoxResult
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


def _hdr(text):
    """Section header label"""
    lbl = Label()
    lbl.Content = text
    lbl.FontSize = 11
    lbl.FontWeight = System.Windows.FontWeights.Bold
    lbl.Foreground = Brushes.DarkBlue
    lbl.Margin = Thickness(0, 8, 0, 2)
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
        self.inc_tb = _tb("", 280, "Use 'all' or comma-separated keywords (e.g., all,ASTM)")
        main.Children.Add(_row(
            _lbl("Include:", 80), self.inc_tb,
            _lbl("  ('all' = all bodies)", 0)
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
            # If no include filter or "all" keyword, all in-scope bodies are targets.
            # 'others' = full scope, so target-target pairs are also detected
            # (covers "either body matches include").
            if inc_lo and 'all' not in inc_lo:
                targets = [b for b in scope if any(k in b.Name.lower() for k in inc_lo)]
            else:
                targets = scope  # "all" keyword or no filter → use all in-scope bodies

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
        dup_count = 0  # DEBUG: count duplicates
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

                            # Order-independent key using geometric identity (centroid rounded to 6 decimals)
                            # Prevents (A,B) and (B,A) duplicates when targets and others overlap
                            def geo_key(centroid, normal):
                                # Round to 1 micron precision to handle floating point errors
                                c = tuple(round(x, 6) for x in centroid)
                                n = tuple(round(x, 6) for x in normal)
                                return (c, n)

                            key_a = geo_key(ca, na)
                            key_b = geo_key(ofd['centroid'], ofd['normal'])
                            key = tuple(sorted([key_a, key_b]))  # Order-independent

                            if key in seen:
                                dup_count += 1
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

        self.log("DEBUG: {0} duplicates skipped".format(dup_count))
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
        a_faces_raw = [pd['face_a'] for pd, _ in checked]
        b_faces_raw = [pd['face_b'] for pd, _ in checked]

        # Deduplicate faces by ID (one face may appear in multiple pairs)
        a_seen = set()
        a_faces = []
        for f in a_faces_raw:
            fid = id(f)
            if fid not in a_seen:
                a_seen.add(fid)
                a_faces.append(f)

        b_seen = set()
        b_faces = []
        for f in b_faces_raw:
            fid = id(f)
            if fid not in b_seen:
                b_seen.add(fid)
                b_faces.append(f)

        # DEBUG: Check for face ID overlap
        overlap = a_seen & b_seen

        self.log("Merged mode: {0} pairs -> {1} unique A-faces, {2} unique B-faces".format(
            len(checked), len(a_faces), len(b_faces)))
        self.log("  Overlap (same face in both): {0}".format(len(overlap)))

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
# PostProcessDialog
# ================================================================

class PostProcessDialog(Window):
    """
    Post-Process Viewer.
    Step 1: Adds TotalDeformation + EquivalentStress results per Named Selection,
            evaluates all, ranks Top-N bodies by MaximumOfMaximumOverTime.
    Step 2: Exports time-history CSVs, writes metadata.json, launches PyQt5 viewer.
    """

    _NS_PATTERNS_DEFAULT = "Cap_, FaceA_, FaceB_, Contact_"
    _POSTPROCESS_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "postprocess")

    def __init__(self):
        self.Title = "MX Post-Process Viewer"
        self.Width = 580
        self.Height = 750
        self._analyses = []    # list of analysis objects
        self._ranked = []      # list of dicts with ranked body data

        root = StackPanel()
        root.Orientation = Orientation.Vertical
        root.Margin = Thickness(10)

        sv = ScrollViewer()
        sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        sv.Content = root
        self.Content = sv

        # ── local UI helpers ──────────────────────────────────────────────
        def _hdr(text):
            lbl = Label()
            lbl.Content = text
            lbl.FontSize = 11
            lbl.FontWeight = System.Windows.FontWeights.Bold
            lbl.Foreground = Brushes.DarkBlue
            lbl.Margin = Thickness(0, 8, 0, 2)
            return lbl

        def _row(*widgets):
            p = StackPanel()
            p.Orientation = Orientation.Horizontal
            p.Margin = Thickness(0, 2, 0, 2)
            for w in widgets:
                p.Children.Add(w)
            return p

        def _lbl(text, w=130):
            l = Label()
            l.Content = text
            l.Width = w
            l.VerticalAlignment = VerticalAlignment.Center
            return l

        def _tb(default='', w=140):
            t = TextBox()
            t.Text = str(default)
            t.Width = w
            t.VerticalAlignment = VerticalAlignment.Center
            t.Margin = Thickness(4, 0, 4, 0)
            return t

        def _btn(text, handler, w=None):
            b = Button()
            b.Content = text
            b.Margin = Thickness(4, 0, 4, 0)
            b.Padding = Thickness(6, 2, 6, 2)
            if w:
                b.Width = w
            b.Click += handler
            return b

        # ── 1. Analysis ───────────────────────────────────────────────────
        root.Children.Add(_hdr("1. Analysis"))

        self.analysis_cb = ComboBox()
        self.analysis_cb.Width = 280
        self.analysis_cb.Margin = Thickness(4, 0, 4, 0)
        self.analysis_cb.VerticalAlignment = VerticalAlignment.Center

        root.Children.Add(_row(
            _lbl("Transient analysis:"),
            self.analysis_cb,
            _btn("Refresh", lambda s, e: self._populate_analyses(), 65),
        ))

        # ── 2. Settings ───────────────────────────────────────────────────
        root.Children.Add(_hdr("2. Settings"))

        self.topn_tb = _tb("10", 55)
        self.freq_tb = _tb("100", 70)
        root.Children.Add(_row(
            _lbl("Top-N bodies:"), self.topn_tb,
            _lbl("Op. freq [Hz]:", 110), self.freq_tb,
        ))

        self.red_tb = _tb("0.30", 60)
        self.yellow_tb = _tb("0.10", 60)
        root.Children.Add(_row(
            _lbl("Thresh red [mm]:"), self.red_tb,
            _lbl("yellow [mm]:", 90), self.yellow_tb,
        ))

        # 바디 자동 감지 안내
        info_lbl = _lbl("※ All bodies in model will be analyzed automatically.", 400)
        info_lbl.Foreground = Brushes.DarkGreen
        root.Children.Add(info_lbl)

        # ── 3. Paths ──────────────────────────────────────────────────────
        root.Children.Add(_hdr("3. Paths"))

        self.outdir_tb = _tb("", 320)
        root.Children.Add(_row(
            _lbl("Output folder:"),
            self.outdir_tb,
            _btn("Browse...", lambda s, e: self.on_browse_outdir(s, e), 70),
        ))

        self.force_tb = _tb("", 320)
        self.force_tb.ToolTip = "Optional: input force CSV for FRF computation in viewer"
        root.Children.Add(_row(
            _lbl("Force CSV (opt.):"),
            self.force_tb,
            _btn("Browse...", lambda s, e: self.on_browse_force(s, e), 70),
        ))

        # ── 4. Actions ────────────────────────────────────────────────────
        root.Children.Add(_hdr("4. Actions"))

        self.status_lbl = Label()
        self.status_lbl.Content = "Ready. Select an analysis and click ①."
        self.status_lbl.Foreground = Brushes.DimGray
        self.status_lbl.Margin = Thickness(0, 2, 0, 4)
        root.Children.Add(self.status_lbl)

        root.Children.Add(_row(
            _btn("① Add Results & Evaluate", lambda s, e: self.on_add_results(s, e), 200),
            _btn("② Export CSV + Launch Viewer", lambda s, e: self.on_export_launch(s, e), 200),
            _btn("Close", lambda s, e: self.on_close(s, e), 60),
        ))

        # ── Log ───────────────────────────────────────────────────────────
        root.Children.Add(_hdr("Log"))

        self.log_tb = TextBox()
        self.log_tb.IsReadOnly = True
        self.log_tb.TextWrapping = System.Windows.TextWrapping.Wrap
        self.log_tb.Height = 250
        self.log_tb.VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        self.log_tb.Margin = Thickness(0, 2, 0, 2)
        self.log_tb.FontFamily = System.Windows.Media.FontFamily("Consolas")
        self.log_tb.FontSize = 10
        root.Children.Add(self.log_tb)

        # Populate on open
        self._populate_analyses()

    # ── helpers ──────────────────────────────────────────────────────────

    def _populate_analyses(self):
        """Find all analyses (prefer Transient) in the model."""
        self.analysis_cb.Items.Clear()
        self._analyses = []
        try:
            model = ExtAPI.DataModel.Project.Model
            analyses = model.GetChildren(DataModelObjectCategory.Analysis, True)
            for a in analyses:
                try:
                    atype = str(a.AnalysisType)
                    if 'Transient' in atype:
                        self._analyses.append(a)
                        self.analysis_cb.Items.Add("[Transient] " + a.Name)
                except Exception:
                    self._analyses.append(a)
                    self.analysis_cb.Items.Add(a.Name)
            if self._analyses:
                self.analysis_cb.SelectedIndex = len(self._analyses) - 1
                self.log("Found {0} analysis(es).".format(len(self._analyses)))
            else:
                self.log("No analyses found. Run a Scenario first.")
        except Exception as ex:
            self.log("Error loading analyses: {0}".format(str(ex)))

    def _get_selected_analysis(self):
        idx = self.analysis_cb.SelectedIndex
        if idx < 0 or idx >= len(self._analyses):
            return None
        return self._analyses[idx]

    def _get_ns_patterns(self):
        raw = self.ns_filter_tb.Text.strip()
        if not raw:
            return []
        return [k.strip() for k in raw.split(',') if k.strip()]

    def _safe_float(self, result_obj, attr):
        """Safely extract a float from a Quantity-like result attribute."""
        try:
            val = getattr(result_obj, attr)
            # Try direct float conversion first
            return float(val)
        except Exception:
            pass
        try:
            # Quantity.ToString() usually gives "2.34 [mm]" — grab first token
            return float(str(val).split()[0])
        except Exception:
            return 0.0

    # ── ① Add Results & Evaluate ─────────────────────────────────────────

    def on_add_results(self, sender, e):
        self.log("=" * 55)
        self.log("① Adding results & evaluating...")
        self.status_lbl.Content = "Working — please wait..."
        self.status_lbl.Foreground = Brushes.DarkOrange
        self._ranked = []

        try:
            analysis = self._get_selected_analysis()
            if analysis is None:
                self.log("ERROR: No analysis selected.")
                self.status_lbl.Content = "Select an analysis first."
                self.status_lbl.Foreground = Brushes.Red
                return

            model = ExtAPI.DataModel.Project.Model
            top_n = int(self.topn_tb.Text.strip() or "10")

            # Get solution
            solutions = analysis.GetChildren(DataModelObjectCategory.Solution, True)
            if not solutions:
                self.log("ERROR: No solution found in analysis.")
                self.status_lbl.Content = "No solution found."
                self.status_lbl.Foreground = Brushes.Red
                return
            sol = solutions[0]
            self.log("Solution: {0}".format(sol.Name))

            # Check solution status — must be solved before post-processing
            sol_status = str(sol.Status)
            self.log("Status: {0}".format(sol_status))
            if 'Done' not in sol_status and 'Solved' not in sol_status:
                self.log("ERROR: Analysis not solved yet.")
                self.log("  -> Solve the analysis in Mechanical first, then run ①.")
                self.status_lbl.Content = "Not solved yet. Run Solve in Mechanical first."
                self.status_lbl.Foreground = Brushes.Red
                MessageBox.Show(
                    "The selected analysis has not been solved yet.\n\n"
                    "Please solve it in Mechanical first:\n"
                    "  Home tab -> Solve (or press F5)\n\n"
                    "Then come back and run ① again.",
                    "Solve Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning)
                return

            # Get all bodies (바디별 자동 가시화)
            bodies = model.Geometry.GetChildren(DataModelObjectCategory.Body, True)

            if not bodies:
                self.log("ERROR: No bodies found in model.")
                self.status_lbl.Content = "No bodies found."
                self.status_lbl.Foreground = Brushes.Red
                return

            self.log("Found {0} bodies.".format(len(bodies)))

            # Import SelectionTypeEnum once (before loop)
            from Ansys.ACT.Interfaces.Common import SelectionTypeEnum

            # ① Add Total Deformation for All Bodies (overview)
            self.log("Adding overview results for all bodies...")
            deform_all = sol.AddTotalDeformation()
            deform_all.Name = "Deform_All_Bodies"

            stress_all = sol.AddEquivalentStress()
            stress_all.Name = "VM_All_Bodies"
            self.log("  + All Bodies (overview)")

            # ② Add TotalDeformation + EquivalentStress per Body (for ranking)
            self.log("Adding per-body results...")
            pairs = []
            for body in bodies:
                try:
                    body_name = body.Name if hasattr(body, 'Name') else "Body_{0}".format(body.Id)

                    # Get GeoBody and create selection
                    geo_body = body.GetGeoBody()
                    sel = ExtAPI.SelectionManager.CreateSelectionInfo(
                        SelectionTypeEnum.GeometryEntities
                    )
                    sel.Entities = [geo_body]

                    deform = sol.AddTotalDeformation()
                    deform.Name = "Deform_" + body_name
                    deform.Location = sel

                    stress = sol.AddEquivalentStress()
                    stress.Name = "VM_" + body_name
                    stress.Location = sel

                    pairs.append((body_name, deform, stress))
                    self.log("  + {0}".format(body_name))
                except Exception as ex:
                    self.log("  WARN {0}: {1}".format(body_name, str(ex)))

            if not pairs:
                self.log("ERROR: Could not add any results.")
                self.status_lbl.Content = "Failed to add results."
                self.status_lbl.Foreground = Brushes.Red
                return

            # Evaluate
            self.log("Evaluating {0} result pair(s)...".format(len(pairs)))
            try:
                sol.EvaluateAllResults()
                self.log("Evaluation complete.")
            except Exception as ex:
                self.log("WARN during evaluate: {0}".format(str(ex)))

            # Rank by MaximumOfMaximumOverTime
            ranked = []
            for ns_name, deform, stress in pairs:
                max_def = self._safe_float(deform, 'MaximumOfMaximumOverTime')
                max_vm  = self._safe_float(stress,  'MaximumOfMaximumOverTime')
                ranked.append({
                    'name':    ns_name,
                    'max_def': max_def,
                    'max_vm':  max_vm,
                    'deform':  deform,
                    'stress':  stress,
                })

            ranked.sort(key=lambda x: x['max_def'], reverse=True)
            self._ranked = ranked[:top_n]

            self.log("\n=== Top-{0} by Max Deformation ===".format(top_n))
            for i, r in enumerate(self._ranked):
                r['rank'] = i + 1
                self.log("  #{0:2d}  {1:30s}  def={2:.4f}  vm={3:.2f}".format(
                    i + 1, r['name'], r['max_def'], r['max_vm']))

            self.status_lbl.Content = (
                "Done. Top-{0} ranked. Run ② to export & launch viewer.".format(
                    len(self._ranked)))
            self.status_lbl.Foreground = Brushes.DarkGreen

        except Exception as ex:
            self.log("ERROR: {0}".format(str(ex)))
            import traceback
            self.log(traceback.format_exc())
            self.status_lbl.Content = "Error — see log."
            self.status_lbl.Foreground = Brushes.Red

    # ── ② Export CSV + Launch Viewer ─────────────────────────────────────

    def on_export_launch(self, sender, e):
        self.log("=" * 55)
        self.log("② Exporting CSVs + launching viewer...")

        if not self._ranked:
            self.log("ERROR: Run ① first to add and rank results.")
            self.status_lbl.Content = "Run ① first."
            self.status_lbl.Foreground = Brushes.Red
            return

        out_dir = self.outdir_tb.Text.strip()
        if not out_dir:
            self.log("ERROR: Output folder not set.")
            self.status_lbl.Content = "Set output folder first."
            self.status_lbl.Foreground = Brushes.Red
            return

        if not os.path.isdir(out_dir):
            try:
                os.makedirs(out_dir)
                self.log("Created: {0}".format(out_dir))
            except Exception as ex:
                self.log("ERROR creating folder: {0}".format(str(ex)))
                return

        try:
            import json

            analysis = self._get_selected_analysis()
            analysis_name = analysis.Name if analysis else "Unknown"

            bodies_meta = []
            for r in self._ranked:
                ns_name = r['name']
                # Sanitise name for filename
                safe = ns_name.replace('/', '_').replace('\\', '_').replace(':', '_')
                csv_fname = safe + "_deform.txt"
                csv_path  = os.path.join(out_dir, csv_fname)
                try:
                    r['deform'].ExportToTextFile(csv_path)
                    self.log("  Exported: {0}".format(csv_fname))
                except Exception as ex:
                    self.log("  WARN export {0}: {1}".format(ns_name, str(ex)))

                bodies_meta.append({
                    'rank':    r.get('rank', 0),
                    'name':    ns_name,
                    'max_def': r['max_def'],
                    'max_vm':  r['max_vm'],
                    'csv':     csv_fname,
                })

            meta = {
                'analysis':        analysis_name,
                'operating_freq_hz': float(self.freq_tb.Text.strip() or "100"),
                'thresh_red_mm':   float(self.red_tb.Text.strip() or "0.30"),
                'thresh_yellow_mm': float(self.yellow_tb.Text.strip() or "0.10"),
                'force_csv':       self.force_tb.Text.strip(),
                'bodies':          bodies_meta,
            }

            meta_path = os.path.join(out_dir, "metadata.json")
            with open(meta_path, 'w', encoding='utf-8') as f:
                json.dump(meta, f, indent=2, ensure_ascii=False)
            self.log("Written: metadata.json")

            # Launch viewer (exe preferred, python fallback)
            if not self._launch_viewer(meta_path):
                return

            self.status_lbl.Content = "Viewer launched!"
            self.status_lbl.Foreground = Brushes.DarkGreen

        except Exception as ex:
            self.log("ERROR: {0}".format(str(ex)))
            import traceback
            self.log(traceback.format_exc())
            self.status_lbl.Content = "Export error — see log."
            self.status_lbl.Foreground = Brushes.Red

    def _launch_viewer(self, meta_path):
        """
        Launch the post-process viewer.
        Priority:
          1. MXPostViewer.exe  (PyInstaller bundle — no Python needed)
          2. venv python + runner.py
          3. system PATH python + runner.py
        Returns True if launched, False on failure.
        """
        # 1. Pre-built exe
        exe = os.path.join(self._POSTPROCESS_DIR, "MXPostViewer.exe")
        if os.path.isfile(exe):
            self.log("Launching: MXPostViewer.exe")
            System.Diagnostics.Process.Start(exe, '"{0}"'.format(meta_path))
            return True

        # 2/3. Python (venv → PATH → common paths)
        python_exe = self._find_python()
        if not python_exe:
            self.log("ERROR: Neither MXPostViewer.exe nor Python found.")
            self.log("  Option A — build the viewer: run postprocess/build_viewer.bat")
            self.log("  Option B — install Python: run postprocess/setup_venv.bat")
            self.status_lbl.Content = "Viewer not found — see log."
            self.status_lbl.Foreground = Brushes.Red
            return False

        runner = os.path.join(self._POSTPROCESS_DIR, "runner.py")
        args = '"{0}" "{1}"'.format(runner, meta_path)
        self.log("Launching: {0}".format(python_exe))
        self.log("  {0}".format(args))
        System.Diagnostics.Process.Start(python_exe, args)
        return True

    def _find_python(self):
        """Find python.exe: venv first, then PATH, then common install paths."""
        # 1. venv
        venv_py = os.path.join(self._POSTPROCESS_DIR, "venv", "Scripts", "python.exe")
        if os.path.isfile(venv_py):
            self.log("Python (venv): {0}".format(venv_py))
            return venv_py

        # 2. 'where python' via Process
        try:
            psi = System.Diagnostics.ProcessStartInfo("where", "python")
            psi.UseShellExecute = False
            psi.RedirectStandardOutput = True
            psi.CreateNoWindow = True
            p = System.Diagnostics.Process.Start(psi)
            p.WaitForExit(3000)
            out = p.StandardOutput.ReadToEnd().strip()
            if out:
                line = out.split('\n')[0].strip()
                if os.path.isfile(line):
                    self.log("Python (PATH): {0}".format(line))
                    return line
        except Exception:
            pass

        # 3. Common install paths
        for cand in [
            r"C:\Python312\python.exe",
            r"C:\Python311\python.exe",
            r"C:\Python310\python.exe",
            r"C:\Python39\python.exe",
        ]:
            if os.path.isfile(cand):
                self.log("Python (found): {0}".format(cand))
                return cand

        return None

    def on_browse_outdir(self, sender, e):
        dlg = FolderBrowserDialog()
        dlg.Description = "Select output folder for CSVs and metadata.json"
        if dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK:
            self.outdir_tb.Text = dlg.SelectedPath

    def on_browse_force(self, sender, e):
        dlg = OpenFileDialog()
        dlg.Title = "Select Force CSV"
        dlg.Filter = "CSV / text files (*.csv;*.txt)|*.csv;*.txt|All files (*.*)|*.*"
        if dlg.ShowDialog() == DialogResult.OK:
            self.force_tb.Text = dlg.FileName

    def on_close(self, sender, e):
        self.Close()

    def log(self, msg):
        self.log_tb.Text += msg + "\n"
        self.log_tb.ScrollToEnd()


# ================================================================
# Dialog 6: Export LS-DYNA K-File
# ================================================================

class ExportKFileDialog(Window):
    """
    Button 6 — Export K-File
    Export mesh to LS-DYNA .k format:
      *NODE, *ELEMENT_SOLID/SHELL, *PART, *SECTION, *MAT_ELASTIC
      *SET_NODE_TITLE (Named Selections)
    No simulation control cards or load curves.
    """

    def __init__(self):
        self.Title = "MX Digital Twin  |  Export LS-DYNA K-File"
        self.Width = 580
        self.Height = 540
        self.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen

        main = StackPanel()
        main.Margin = Thickness(14, 10, 14, 10)

        h = Label()
        h.Content = "Export LS-DYNA .k File  (Mesh + Named Selections)"
        h.FontSize = 13
        h.FontWeight = System.Windows.FontWeights.Bold
        h.Margin = Thickness(0, 0, 0, 8)
        main.Children.Add(h)

        # Output path
        self.path_tb = _tb("", 360)
        browse_btn = Button()
        browse_btn.Content = "Browse..."
        browse_btn.Width = 70
        browse_btn.Margin = Thickness(4, 0, 0, 0)
        browse_btn.Click += self.on_browse
        main.Children.Add(_row(_lbl("Output .k file:", 110), self.path_tb, browse_btn))

        # Unit system
        self.unit_mm_rb = RadioButton()
        self.unit_mm_rb.Content = "mm-tonne-s  (LS-DYNA standard)"
        self.unit_mm_rb.IsChecked = True
        self.unit_si_rb = RadioButton()
        self.unit_si_rb.Content = "SI  (m-kg-s)"
        main.Children.Add(_row(_lbl("Unit system:", 110), self.unit_mm_rb))
        main.Children.Add(_row(_lbl("", 110), self.unit_si_rb))

        # Options
        self.include_ns_cb = CheckBox()
        self.include_ns_cb.Content = "Named Selections \u2192 *SET_NODE_TITLE"
        self.include_ns_cb.IsChecked = True
        main.Children.Add(_row(_lbl("Options:", 110), self.include_ns_cb))

        self.include_mat_cb = CheckBox()
        self.include_mat_cb.Content = "Materials \u2192 *MAT_ELASTIC (default steel if not assigned)"
        self.include_mat_cb.IsChecked = True
        main.Children.Add(_row(_lbl("", 110), self.include_mat_cb))

        self.include_contact_cb = CheckBox()
        self.include_contact_cb.Content = "Contact Regions \u2192 *CONTACT_* + *SET_SEGMENT_TITLE (auto master/slave by mesh size)"
        self.include_contact_cb.IsChecked = True
        main.Children.Add(_row(_lbl("", 110), self.include_contact_cb))

        # Tolerance for geometry filtering
        self.tol_tb = _tb("0.1", 60)
        self.tol_tb.ToolTip = "Tolerance in mm for matching mesh nodes to geometry faces (Named Selections & Contact Regions)"
        tol_unit_lbl = Label()
        tol_unit_lbl.Content = "mm"
        tol_unit_lbl.VerticalAlignment = VerticalAlignment.Center
        tol_unit_lbl.Margin = Thickness(2, 0, 0, 0)
        main.Children.Add(_row(_lbl("Geo tolerance:", 110), self.tol_tb, tol_unit_lbl))

        main.Children.Add(_sep())

        # Buttons
        export_btn = Button()
        export_btn.Content = "Export .k File"
        export_btn.Height = 34
        export_btn.FontSize = 12
        export_btn.HorizontalAlignment = HorizontalAlignment.Stretch
        export_btn.Margin = Thickness(0, 0, 0, 4)
        export_btn.Click += self.on_export
        main.Children.Add(export_btn)
        main.Children.Add(_row(_btn("Close", lambda s, e: self.Close(), 60)))

        main.Children.Add(_sep())

        # Log
        log_hdr = Label()
        log_hdr.Content = "Log"
        log_hdr.FontWeight = System.Windows.FontWeights.SemiBold
        log_hdr.Margin = Thickness(0, 2, 0, 2)
        main.Children.Add(log_hdr)

        self.log_tb = TextBox()
        self.log_tb.IsReadOnly = True
        self.log_tb.TextWrapping = System.Windows.TextWrapping.Wrap
        self.log_tb.Height = 230
        self.log_tb.VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        self.log_tb.FontFamily = System.Windows.Media.FontFamily("Consolas")
        self.log_tb.FontSize = 10
        self.log_tb.Margin = Thickness(0, 2, 0, 2)
        main.Children.Add(self.log_tb)

        sv = ScrollViewer()
        sv.Content = main
        sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        self.Content = sv

    def log(self, msg):
        self.log_tb.Text += msg + "\n"
        self.log_tb.ScrollToEnd()

    # ── Browse ────────────────────────────────────────────────────────────

    def on_browse(self, sender, e):
        from System.Windows.Forms import SaveFileDialog as WFSaveDialog
        dlg = WFSaveDialog()
        dlg.Filter = "LS-DYNA K-File (*.k)|*.k"
        dlg.DefaultExt = "k"
        dlg.Title = "Export LS-DYNA K-File"
        if dlg.ShowDialog() == DialogResult.OK:
            self.path_tb.Text = dlg.FileName

    # ── Export ────────────────────────────────────────────────────────────

    def on_export(self, sender, e):
        path = self.path_tb.Text.strip()
        if not path:
            MessageBox.Show("Please specify an output file path.", "Missing Path",
                MessageBoxButton.OK, MessageBoxImage.Warning)
            return

        self.log_tb.Text = ""
        self.log("=" * 50)
        self.log("Export LS-DYNA K-File")
        self.log("Output: " + path)
        self.log("=" * 50)

        use_mm = bool(self.unit_mm_rb.IsChecked)
        scale = 1000.0 if use_mm else 1.0        # m → mm (coordinates)
        mat_e_scale = 1e-6 if use_mm else 1.0    # Pa → tonne/(mm·s²) = MPa
        mat_rho_scale = 1e-12 if use_mm else 1.0 # kg/m³ → tonne/mm³
        unit_label = "mm-tonne-s" if use_mm else "SI (m-kg-s)"
        self.log("Unit system: " + unit_label)

        try:
            model = ExtAPI.DataModel.Project.Model

            # ── mesh data ──────────────────────────────────────────────
            try:
                mesh_data = ExtAPI.DataModel.MeshDataByName('Global')
                if mesh_data is None:
                    raise Exception("MeshData not available")
                n_nodes = mesh_data.NodeCount
                n_elems = mesh_data.ElementCount
                self.log("MeshData: {0} nodes, {1} elements".format(n_nodes, n_elems))
                if n_nodes == 0:
                    raise Exception("Mesh has 0 nodes")
            except Exception as ex:
                self.log("ERROR: " + str(ex))
                self.log("  -> Generate mesh first (Mesh tab -> Generate Mesh)")
                MessageBox.Show(
                    "Cannot access mesh data.\n\nPlease generate the mesh first:\n"
                    "  Mesh -> Generate Mesh\n\nThen try again.",
                    "Mesh Required", MessageBoxButton.OK, MessageBoxImage.Warning)
                return

            # ── introspect MeshData API (logged for debugging) ─────────
            try:
                api_attrs = [a for a in dir(mesh_data) if not a.startswith('_') and
                             any(k in a for k in ('Node', 'Element', 'Part', 'Body'))]
                self.log("MeshData API: " + ", ".join(api_attrs[:12]))
            except Exception:
                pass

            lines = []
            lines.append("*KEYWORD")
            lines.append("$ Generated by MX Digital Twin Simulator")
            lines.append("$ Unit system: " + unit_label)
            lines.append("$")

            # ── *NODE ──────────────────────────────────────────────────
            node_coords_m, n_written = self._write_nodes(lines, mesh_data, scale)
            self.log("*NODE: {0} written".format(n_written))

            # ── *ELEMENT + *PART + *SECTION ────────────────────────────
            body_pid_map, n_elem_written = self._write_elements_parts(
                lines, mesh_data, model)
            self.log("*ELEMENT/*PART: {0} elems, {1} parts".format(
                n_elem_written, len(body_pid_map)))

            # ── *MAT_ELASTIC ───────────────────────────────────────────
            n_mats = 0
            if bool(self.include_mat_cb.IsChecked):
                n_mats = self._write_materials(
                    lines, model, body_pid_map, use_mm, mat_e_scale, mat_rho_scale)
                self.log("*MAT_ELASTIC: {0} materials".format(n_mats))

            # ── Geometry tolerance ─────────────────────────────────────
            tol_mm = 0.1
            try:
                tol_mm = float(self.tol_tb.Text.strip())
                if tol_mm <= 0:
                    tol_mm = 0.1
            except Exception:
                tol_mm = 0.1
            tol_m = tol_mm / 1000.0  # mm → m
            self.log("Geometry tolerance: {0} mm ({1} m)".format(tol_mm, tol_m))

            # ── *SET_NODE_TITLE ────────────────────────────────────────
            n_sets = 0
            if bool(self.include_ns_cb.IsChecked):
                n_sets = self._write_named_selections(
                    lines, mesh_data, model, node_coords_m, tol_m)
                self.log("*SET_NODE_TITLE: {0} sets".format(n_sets))

            # ── Contact Regions ────────────────────────────────────────
            n_contacts = 0
            if bool(self.include_contact_cb.IsChecked):
                all_elems     = self._collect_elements(mesh_data)
                face_map      = self._enumerate_element_faces(all_elems)
                surface_faces = self._extract_surface_faces(face_map)
                self.log("Surface faces (boundary): {0}".format(len(surface_faces)))
                n_contacts = self._write_contacts(
                    lines, surface_faces, node_coords_m, mesh_data)
                self.log("*CONTACT: {0} contact regions".format(n_contacts))

            lines.append("*END")

            # ── write file ─────────────────────────────────────────────
            with open(path, 'w') as f:
                f.write('\n'.join(lines))

            file_kb = os.path.getsize(path) / 1024.0
            self.log("")
            self.log("Saved: {0:.1f} KB  ->  {1}".format(file_kb, path))

            MessageBox.Show(
                "Export complete!\n\n"
                "Nodes:    {0}\nElements: {1}\n"
                "Parts:    {2}\nNS Sets:  {3}\nContacts: {4}\n\n"
                "File: {5:.1f} KB\n{6}".format(
                    n_written, n_elem_written, len(body_pid_map), n_sets, n_contacts,
                    file_kb, path),
                "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information)

        except Exception as ex:
            import traceback
            self.log("ERROR: " + str(ex))
            self.log(traceback.format_exc())
            MessageBox.Show("Export failed:\n\n" + str(ex), "Error",
                MessageBoxButton.OK, MessageBoxImage.Error)

    # ── *NODE writer ──────────────────────────────────────────────────────

    def _write_nodes(self, lines, mesh_data, scale):
        """
        Write *NODE section. Returns (node_coords_m dict, count).
        node_coords_m: {nid: (x_m, y_m, z_m)}  (unscaled, in meters)
        """
        lines.append("*NODE")
        # Standard format: NID(I8), X(F16), Y(F16), Z(F16) — see manual §2 (I8,3F16,2I8)
        lines.append("$#     nid             x               y               z")
        node_coords_m = {}
        count = 0

        def _try_node(node):
            return node.Id, node.X, node.Y, node.Z

        # Try NodeById first (1-based sequential IDs are common)
        try:
            n = mesh_data.NodeCount
            for nid in range(1, n + 1):
                try:
                    node = mesh_data.NodeById(nid)
                    nid_r, x, y, z = _try_node(node)
                    node_coords_m[nid_r] = (x, y, z)
                    # *NODE standard format: NID(I8), X(F16), Y(F16), Z(F16)
                    lines.append("{:8d}{:16.6E}{:16.6E}{:16.6E}".format(
                        nid_r, x * scale, y * scale, z * scale))
                    count += 1
                except Exception:
                    pass
            if count > 0:
                lines.append("$")
                return node_coords_m, count
        except Exception as ex:
            self.log("  [NODE] NodeById failed: " + str(ex))

        # Fallback: NodeByIndex (0-based)
        try:
            n = mesh_data.NodeCount
            for i in range(n):
                try:
                    node = mesh_data.NodeByIndex(i)
                    nid_r, x, y, z = _try_node(node)
                    node_coords_m[nid_r] = (x, y, z)
                    # *NODE standard format: NID(I8), X(F16), Y(F16), Z(F16)
                    lines.append("{:8d}{:16.6E}{:16.6E}{:16.6E}".format(
                        nid_r, x * scale, y * scale, z * scale))
                    count += 1
                except Exception:
                    pass
        except Exception as ex2:
            self.log("  [NODE] NodeByIndex also failed: " + str(ex2))

        lines.append("$")
        return node_coords_m, count

    # ── *ELEMENT + *PART + *SECTION writer ────────────────────────────────

    def _write_elements_parts(self, lines, mesh_data, model):
        """
        Write *SECTION + *PART + *ELEMENT_SOLID/SHELL blocks.
        Returns (body_name -> pid dict, total element count).
        """
        # Body name → PID mapping
        body_pid_map = {}
        try:
            bodies = list(model.Geometry.GetChildren(
                DataModelObjectCategory.Body, True))
            for i, body in enumerate(bodies):
                try:
                    body_pid_map[body.Name] = i + 1
                except Exception:
                    body_pid_map["Body_{0}".format(i)] = i + 1
        except Exception as ex:
            self.log("  [PART] Cannot read bodies: " + str(ex))
            body_pid_map["Model"] = 1

        # Group elements by part ID
        elem_by_part = {}   # pid -> [(eid, [nids], is_shell)]
        total = 0
        try:
            n = mesh_data.ElementCount
            for eid in range(1, n + 1):
                try:
                    elem = mesh_data.ElementById(eid)
                    # Determine part ID
                    pid = 1
                    for attr in ('PartId', 'BodyId', 'Part', 'Body'):
                        try:
                            pid = int(getattr(elem, attr))
                            break
                        except Exception:
                            pass
                    nids = list(elem.NodeIds)
                    etype = str(elem.ElementType) if hasattr(elem, 'ElementType') else ""
                    is_shell = "Shell" in etype or "Plate" in etype
                    if pid not in elem_by_part:
                        elem_by_part[pid] = []
                    elem_by_part[pid].append((elem.Id, nids, is_shell))
                except Exception:
                    pass
        except Exception as ex:
            self.log("  [ELEM] ElementById failed: " + str(ex))

        # Fallback: ElementByIndex
        if not elem_by_part:
            try:
                n = mesh_data.ElementCount
                for i in range(n):
                    try:
                        elem = mesh_data.ElementByIndex(i)
                        nids = list(elem.NodeIds)
                        etype = str(elem.ElementType) if hasattr(elem, 'ElementType') else ""
                        is_shell = "Shell" in etype or "Plate" in etype
                        if 1 not in elem_by_part:
                            elem_by_part[1] = []
                        elem_by_part[1].append((elem.Id, nids, is_shell))
                    except Exception:
                        pass
            except Exception as ex2:
                self.log("  [ELEM] ElementByIndex also failed: " + str(ex2))

        if not elem_by_part:
            self.log("  [ELEM] WARNING: No elements found")
            return body_pid_map, 0

        # ── LS-DYNA element type helpers ──────────────────────────────────
        # Returns (ELFORM for *SECTION_SOLID, 10-node Card-2 list) per manual:
        #   4-node Tet4    → ELFORM=10, Card2: N1,N2,N3,N4,N4,N4,N4,N4,0,0
        #   6-node Wedge6  → ELFORM=15, Card2: N1,N2,N3,N4,N5,N5,N6,N6,0,0
        #   8-node Hex8    → ELFORM= 1, Card2: N1..N8,0,0
        #   5-node Pyr5    → ELFORM= 1 (degenerate hex), Card2: N1..N5,N5,N5,N5,0,0
        #   Quadratic elements: degrade to linear using corner nodes only
        #   10-node Tet10  → treat as Tet4 (corners = N1-N4, indices 0-3)
        #   15-node Wedge15→ treat as Wedge6 (corners = N1-N6, indices 0-5)
        #   20-node Hex20  → treat as Hex8  (corners = N1-N8, indices 0-7)
        def _solid_card2(nids):
            n = len(nids)
            if n == 4:
                return 10, list(nids[:4]) + [nids[3]]*4 + [0, 0]
            elif n == 10:
                # Tet10 → degrade to Tet4 using corner nodes (indices 0-3)
                return 10, list(nids[:4]) + [nids[3]]*4 + [0, 0]
            elif n == 6:
                # Wedge6: LS-DYNA maps N5→N5,N5 and N6→N6,N6 (§19-123)
                return 15, [nids[0],nids[1],nids[2],nids[3],
                            nids[4],nids[4],nids[5],nids[5], 0, 0]
            elif n == 15:
                # Wedge15 → degrade to Wedge6 using corner nodes (indices 0-5)
                return 15, [nids[0],nids[1],nids[2],nids[3],
                            nids[4],nids[4],nids[5],nids[5], 0, 0]
            elif n == 5:
                return 1,  list(nids[:5]) + [nids[4]]*3 + [0, 0]
            elif n == 8:
                return 1,  list(nids[:8]) + [0, 0]
            elif n == 20:
                # Hex20 → degrade to Hex8 using corner nodes (indices 0-7)
                return 1,  list(nids[:8]) + [0, 0]
            else:   # unknown — use up to 8 nodes padded
                padded = (list(nids) + [nids[-1]]*8)[:8]
                return 1, padded + [0, 0]

        # Write blocks per part
        sec_id = 1
        for pid in sorted(elem_by_part.keys()):
            elems = elem_by_part[pid]
            if not elems:
                continue
            is_shell = elems[0][2]

            # *SECTION
            if is_shell:
                lines.append("*SECTION_SHELL")
                # ELFORM=2: Belytschko-Tsay (most common shell element)
                lines.append("$#   secid    elform      shrf       nip")
                lines.append("{:10d}{:10d}{:10.4f}{:10d}".format(sec_id, 2, 1.0, 3))
                lines.append("$#      t1        t2        t3        t4")
                lines.append("{:10.4f}{:10.4f}{:10.4f}{:10.4f}".format(1.0, 1.0, 1.0, 1.0))
            else:
                # Detect ELFORM from first element's node count
                elform, _ = _solid_card2(elems[0][1])
                lines.append("*SECTION_SOLID")
                lines.append("$#   secid    elform")
                lines.append("{:10d}{:10d}".format(sec_id, elform))

            # *PART
            part_name = next(
                (n for n, p in body_pid_map.items() if p == pid),
                "Part_{0}".format(pid))
            lines.append("*PART")
            lines.append(part_name)
            lines.append("$#    pid     secid       mid")
            lines.append("{:10d}{:10d}{:10d}".format(pid, sec_id, pid))

            # *ELEMENT
            if is_shell:
                lines.append("*ELEMENT_SHELL")
                lines.append("$#   eid     pid      n1      n2      n3      n4")
                for eid, nids, _ in elems:
                    n = len(nids)
                    if n in (3, 6):
                        # Tri3 or Tri6 (quadratic): corners=N1,N2,N3; N4=N3 per manual §19-104
                        nids4 = [nids[0], nids[1], nids[2], nids[2]]
                    elif n in (4, 8):
                        # Quad4 or Quad8 (quadratic): corners=N1-N4
                        nids4 = list(nids[:4])
                    else:
                        nids4 = (list(nids) + [nids[-1]] * 4)[:4]
                    lines.append("{:8d}{:8d}{:8d}{:8d}{:8d}{:8d}".format(
                        eid, pid, *nids4))
                    total += 1
            else:
                # Two-card format per LS-DYNA manual §19-119:
                #   Card 1: EID PID  (line 1)
                #   Card 2: N1..N8 N9 N10  (line 2, 10 × I8)
                lines.append("*ELEMENT_SOLID")
                lines.append("$#   eid     pid")
                lines.append("$#    n1      n2      n3      n4      n5      n6      n7      n8")
                for eid, nids, _ in elems:
                    _, c2 = _solid_card2(nids)
                    lines.append("{:8d}{:8d}".format(eid, pid))
                    lines.append("{:8d}{:8d}{:8d}{:8d}{:8d}{:8d}"
                                 "{:8d}{:8d}{:8d}{:8d}".format(*c2))
                    total += 1

            lines.append("$")
            sec_id += 1

        return body_pid_map, total

    # ── *MAT_ELASTIC writer ───────────────────────────────────────────────

    def _write_materials(self, lines, model, body_pid_map, use_mm,
                         mat_e_scale, mat_rho_scale):
        lines.append("$")
        lines.append("$" + "=" * 49)
        lines.append("$  MATERIALS")
        lines.append("$" + "=" * 49)

        # Default steel in mm-tonne-s: rho=7.85e-9, E=2e5, nu=0.3
        # Default steel in SI:         rho=7850,    E=2e11, nu=0.3
        def_rho = 7.85e-9 if use_mm else 7850.0
        def_E   = 2.0e5   if use_mm else 2.0e11
        def_nu  = 0.3

        count = 0
        written = set()
        try:
            bodies = list(model.Geometry.GetChildren(
                DataModelObjectCategory.Body, True))
            for body in bodies:
                pid = body_pid_map.get(body.Name, 1)
                if pid in written:
                    continue
                written.add(pid)

                rho, E, nu = def_rho, def_E, def_nu
                mat_label = "default steel"
                try:
                    mat_name = str(body.Material) if hasattr(body, 'Material') else ""
                    if mat_name:
                        mat_label = mat_name
                except Exception:
                    pass

                lines.append("*MAT_ELASTIC")
                lines.append("$ Part: {0}  Mat: {1}".format(
                    body.Name, mat_label))
                lines.append("$#     mid        ro         e        pr")
                lines.append("{:10d}{:10.3E}{:10.3E}{:10.4f}".format(
                    pid, rho, E, nu))
                lines.append("$")
                count += 1
        except Exception as ex:
            self.log("  [MAT] WARNING: " + str(ex))
            if not written:
                lines.append("*MAT_ELASTIC")
                lines.append("$ Default steel")
                lines.append("$#     mid        ro         e        pr")
                lines.append("{:10d}{:10.3E}{:10.3E}{:10.4f}".format(
                    1, def_rho, def_E, def_nu))
                lines.append("$")
                count = 1

        return count

    # ── *SET_NODE_TITLE writer ────────────────────────────────────────────

    def _write_named_selections(self, lines, mesh_data, model, node_coords_m, tol_m):
        """
        Write *SET_NODE_TITLE for each Named Selection.
        Uses geometric filtering with specified tolerance.
        node_coords_m: {nid: (x_m, y_m, z_m)}
        tol_m: tolerance in meters for geometry filtering
        """
        lines.append("$")
        lines.append("$" + "=" * 49)
        lines.append("$  NAMED SELECTIONS (NODE SETS)")
        lines.append("$" + "=" * 49)

        try:
            ns_list = list(model.NamedSelections.GetChildren(
                DataModelObjectCategory.NamedSelection, True))
        except Exception as ex:
            self.log("  [NS] Cannot read Named Selections: " + str(ex))
            return 0

        count = 0
        set_id = 1

        for ns in ns_list:
            try:
                ns_name = ns.Name
                node_ids = self._get_ns_node_ids(
                    ns, node_coords_m, tol_m, mesh_data)

                if not node_ids:
                    self.log("  [NS] {0}: 0 nodes (skipped)".format(ns_name))
                    continue

                lines.append("*SET_NODE_TITLE")
                lines.append(ns_name)
                lines.append("$#    nsid")
                lines.append("{:10d}".format(set_id))
                lines.append(
                    "$#      n1        n2        n3        n4"
                    "        n5        n6        n7        n8")

                row = []
                for nid in sorted(node_ids):
                    row.append("{:10d}".format(nid))
                    if len(row) == 8:
                        lines.append("".join(row))
                        row = []
                if row:
                    lines.append("".join(row))

                lines.append("$")
                self.log("  [NS] {0}: {1} nodes -> SET #{2}".format(
                    ns_name, len(node_ids), set_id))
                set_id += 1
                count += 1

            except Exception as ex:
                self.log("  [NS] ERROR on {0}: {1}".format(
                    getattr(ns, 'Name', '?'), str(ex)))

        return count

    def _get_ns_node_ids(self, ns, node_coords_m, tol_m, mesh_data):
        """
        Return set of node IDs belonging to this Named Selection.
        Uses MeshData.GetNodeIdsFromRegionIds API for direct region→node conversion.
        """
        ns_name = ns.Name

        try:
            # Generate NS to ensure it's up to date
            ns.Generate()

            # Get region IDs from Named Selection
            region_ids = list(ns.Location.Ids)
            self.log("    [MeshAPI] {0}: {1} region IDs from Location.Ids".format(
                ns_name, len(region_ids)))

            if not region_ids:
                return set()

            # Use MeshData API: region IDs → node IDs (returns Dictionary)
            node_dict = mesh_data.GetNodeIdsFromRegionIds(region_ids)
            result = set()

            # node_dict is KeyValuePair<int, IList<int>>
            for kvp in node_dict:
                region_id = kvp.Key
                node_list = kvp.Value
                for nid in node_list:
                    result.add(int(nid))

            self.log("    [MeshAPI] {0}: GetNodeIdsFromRegionIds → {1} nodes".format(
                ns_name, len(result)))
            return result

        except Exception as ex:
            self.log("    [MeshAPI] {0}: FAILED - {1}".format(
                ns_name, str(ex)[:80]))
            return set()

    def _get_ns_node_ids_geometry(self, ns, ns_name, node_coords_m, tol_m):
        """Extract node IDs from geometry faces using plane filtering.
        Gets face IDs from NS, then looks up SpaceClaim face entities from bodies."""
        face_entities = []

        try:
            face_ids = list(ns.Location.Ids)
            self.log("    [Geo] {0}: Location.Ids → {1} face IDs".format(
                ns_name, len(face_ids)))

            if not face_ids:
                self.log("    [Geo] {0}: no face IDs".format(ns_name))
                return set()

            model = ExtAPI.DataModel.Project.Model
            bodies = list(model.Geometry.GetChildren(
                DataModelObjectCategory.Body, True))
            self.log("    [Geo] {0}: searching {1} bodies for faces".format(
                ns_name, len(bodies)))

            for fid in face_ids:
                found = False
                for body in bodies:
                    try:
                        gb = body.GetGeoBody()
                        for f in gb.Faces:
                            if f.PersistentId == fid:
                                face_entities.append(f)
                                found = True
                                break
                    except Exception:
                        pass
                    if found:
                        break
                if not found:
                    self.log("    [Geo] face ID {0} NOT FOUND in bodies".format(fid))

            self.log("    [Geo] {0}: found {1} SpaceClaim faces".format(
                ns_name, len(face_entities)))
        except Exception as ex:
            self.log("    [Geo] {0}: lookup failed - {1}".format(
                ns_name, str(ex)[:60]))

        if not face_entities:
            self.log("    [Geo] {0}: NO FACES → 0 nodes".format(ns_name))
            return set()

        result = set()
        self.log("    [Filter] {0}: {1} nodes × {2} faces (tol={3:.5f} m)".format(
            ns_name, len(node_coords_m), len(face_entities), tol_m))

        for idx, face in enumerate(face_entities):
            try:
                # Get face normal and centroid (in meters, SpaceClaim geometry)
                raw_n = face.NormalAtParam(0.5, 0.5)
                raw_c = face.Centroid
                mag = (raw_n.X**2 + raw_n.Y**2 + raw_n.Z**2) ** 0.5
                if mag < 1e-10:
                    self.log("    [Filter] face {0}: zero normal (skip)".format(idx))
                    continue
                nx = raw_n.X / mag
                ny = raw_n.Y / mag
                nz = raw_n.Z / mag
                cx = raw_c.X
                cy = raw_c.Y
                cz = raw_c.Z

                self.log("    [Filter] face {0}: C=({1:.4f},{2:.4f},{3:.4f}) N=({4:.2f},{5:.2f},{6:.2f})".format(
                    idx, cx, cy, cz, nx, ny, nz))

                # Filter nodes on this face plane
                matched = 0
                for nid, (x, y, z) in node_coords_m.items():
                    dist = abs((x - cx) * nx + (y - cy) * ny + (z - cz) * nz)
                    if dist <= tol_m:
                        result.add(nid)
                        matched += 1

                self.log("    [Filter] face {0}: {1} nodes matched".format(idx, matched))
            except Exception as ex:
                self.log("    [Filter] face {0}: ERROR - {1}".format(idx, str(ex)[:50]))

        self.log("    [Geo] {0}: geometry-filter → {1} nodes TOTAL".format(
            ns_name, len(result)))
        return result

    # ── Contact: element face extraction ──────────────────────────────────

    # Face topology: node count → list of (local index tuples per face)
    # For quadratic solids, corner node indices only (mid-side nodes ignored).
    # ANSYS SOLID187 (Tet10):  corners=idx 0-3,  midsides=idx 4-9
    # ANSYS SOLID186 (Hex20):  corners=idx 0-7,  midsides=idx 8-19
    # ANSYS SOLID185/186 Wedge15: corners=idx 0-5, midsides=idx 6-14
    _FACE_TOPO = {
        # ── Linear solids ────────────────────────────────────────────────
        4: [(0,1,2), (0,2,3), (0,3,1), (1,3,2)],                          # Tet4
        8: [(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)], # Hex8
        6: [(0,1,2),(3,5,4),(0,3,4,1),(1,4,5,2),(0,2,5,3)],                # Wedge6
        5: [(0,3,2,1),(0,1,4),(1,2,4),(2,3,4),(3,0,4)],                    # Pyramid5
        3: [(0,1,2)],                                                       # Tri3 shell
        # ── Quadratic solids — corner faces only ─────────────────────────
        10: [(0,1,2), (0,2,3), (0,3,1), (1,3,2)],                         # Tet10  → Tet4 corners
        20: [(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],# Hex20  → Hex8 corners
        15: [(0,1,2),(3,5,4),(0,3,4,1),(1,4,5,2),(0,2,5,3)],               # Wedge15→ Wedge6 corners
        13: [(0,3,2,1),(0,1,4),(1,2,4),(2,3,4),(3,0,4)],                   # Pyr13  → Pyr5 corners
    }

    def _collect_elements(self, mesh_data):
        """Collect all elements. Returns {eid: ([nid,...], is_shell)}."""
        all_elems = {}
        try:
            n = mesh_data.ElementCount
            for eid in range(1, n + 1):
                try:
                    elem = mesh_data.ElementById(eid)
                    nids = list(elem.NodeIds)
                    etype = str(elem.ElementType) if hasattr(elem, 'ElementType') else ""
                    all_elems[elem.Id] = (nids, "Shell" in etype or "Plate" in etype)
                except Exception:
                    pass
        except Exception:
            pass
        if not all_elems:
            try:
                n = mesh_data.ElementCount
                for i in range(n):
                    try:
                        elem = mesh_data.ElementByIndex(i)
                        nids = list(elem.NodeIds)
                        etype = str(elem.ElementType) if hasattr(elem, 'ElementType') else ""
                        all_elems[elem.Id] = (nids, "Shell" in etype or "Plate" in etype)
                    except Exception:
                        pass
            except Exception:
                pass
        return all_elems

    def _enumerate_element_faces(self, all_elems):
        """Decompose elements into faces.
        Returns {frozenset(node_ids): [(eid, [ordered_nids]), ...]}."""
        face_map = {}
        for eid, (nids, is_shell) in all_elems.items():
            n = len(nids)
            if is_shell:
                # Shell elements: the element itself is the contact surface.
                # Use only corner nodes to avoid mid-side nodes in segment output.
                # Tri3/Tri6 → triangular face [n0,n1,n2]
                # Quad4/Quad8 → quad face [n0,n1,n2,n3]
                if n in (3, 6):
                    topo = [(0, 1, 2)]
                elif n in (4, 8):
                    topo = [(0, 1, 2, 3)]
                else:
                    topo = [tuple(range(min(n, 4)))]
            else:
                topo = self._FACE_TOPO.get(n, [])
            for fi in topo:
                ordered = [nids[k] for k in fi]
                key = frozenset(ordered)
                if key not in face_map:
                    face_map[key] = []
                face_map[key].append((eid, ordered))
        return face_map

    def _extract_surface_faces(self, face_map):
        """Return faces belonging to exactly one element (boundary faces)."""
        return [entries[0]
                for entries in face_map.values()
                if len(entries) == 1]

    def _average_elem_size(self, segment_faces, node_coords_m):
        """Average edge length (m) across segment faces. Returns 1.0 if empty."""
        import math
        total, count = 0.0, 0
        for (_eid, nodes) in segment_faces:
            coords = [node_coords_m[nid] for nid in nodes if nid in node_coords_m]
            m = len(coords)
            if m < 2:
                continue
            for i in range(m):
                a, b = coords[i], coords[(i + 1) % m]
                dx, dy, dz = a[0]-b[0], a[1]-b[1], a[2]-b[2]
                total += math.sqrt(dx*dx + dy*dy + dz*dz)
                count += 1
        return total / count if count > 0 else 1.0

    def _get_contact_segment_faces(self, selection_info, surface_faces, mesh_data):
        """Filter surface_faces to those belonging to SelectionInfo region.
        Uses MeshData.GetNodeIdsFromRegionIds for direct conversion."""
        result = []
        seen = set()

        try:
            # Get region IDs from SelectionInfo
            region_ids = list(selection_info.Ids)
            if not region_ids:
                return result

            # Convert region IDs to node IDs using MeshData API
            node_dict = mesh_data.GetNodeIdsFromRegionIds(region_ids)
            region_nodes = set()
            for kvp in node_dict:
                for nid in kvp.Value:
                    region_nodes.add(int(nid))

            if not region_nodes:
                return result

            # Filter surface faces: include if ALL nodes belong to region
            for (eid, nodes) in surface_faces:
                key = (eid, tuple(nodes))
                if key in seen:
                    continue

                # Check if all face nodes are in region
                if all(nid in region_nodes for nid in nodes):
                    result.append((eid, nodes))
                    seen.add(key)

            return result

        except Exception as ex:
            return result

    def _write_segment_set(self, lines, seg_id, title, segment_faces):
        """Write *SET_SEGMENT_TITLE card.
        Segment row format: n1 n2 n3 n4 (4 corner node IDs, no element ID).
        """
        lines.append("*SET_SEGMENT_TITLE")
        lines.append(title)
        lines.append("{:10d}".format(seg_id))
        lines.append("$#      n1        n2        n3        n4")
        for (_eid, nodes) in segment_faces:
            n1 = nodes[0]; n2 = nodes[1]; n3 = nodes[2]
            # LS-DYNA: triangular segment → N4 = N3 (per manual §43-54)
            n4 = nodes[3] if len(nodes) >= 4 else nodes[2]
            lines.append("{:10d}{:10d}{:10d}{:10d}".format(n1, n2, n3, n4))

    def _write_contacts(self, lines, surface_faces, node_coords_m, mesh_data):
        """Read Contact Regions, write *SET_SEGMENT_TITLE + *CONTACT_* cards.
        Returns number of contact regions written.
        Uses MeshData.GetNodeIdsFromRegionIds for direct conversion."""
        try:
            model = ExtAPI.DataModel.Project.Model
            connections = model.Connections
            cr_list = list(connections.GetChildren(
                DataModelObjectCategory.ContactRegion, True))
        except Exception as ex:
            self.log("  [CONTACT] Cannot read connections: " + str(ex))
            return 0
        if not cr_list:
            self.log("  [CONTACT] No contact regions found")
            return 0

        seg_id = 100  # Start well above NS set IDs
        written = 0
        for cr in cr_list:
            try:
                cr_name = cr.Name
            except Exception:
                cr_name = "Contact_{0}".format(seg_id // 2)
            try:
                contact_faces = self._get_contact_segment_faces(
                    cr.SourceLocation, surface_faces, mesh_data)
                target_faces  = self._get_contact_segment_faces(
                    cr.TargetLocation,  surface_faces, mesh_data)
            except Exception as ex:
                self.log("  [CONTACT] Geometry read failed for {0}: {1}".format(
                    cr_name, ex))
                seg_id += 2
                continue
            if not contact_faces and not target_faces:
                self.log("  [CONTACT] No segments matched: " + cr_name)
                seg_id += 2
                continue

            # Master/Slave: finer mesh (smaller avg edge length) = Slave
            size_c = self._average_elem_size(contact_faces, node_coords_m)
            size_t = self._average_elem_size(target_faces,  node_coords_m)
            if size_c <= size_t:
                slave_faces,  slave_id  = contact_faces, seg_id
                master_faces, master_id = target_faces,  seg_id + 1
                self.log("  {0}: Contact=Slave(size={1:.5f}), Target=Master".format(
                    cr_name, size_c))
            else:
                master_faces, master_id = contact_faces, seg_id
                slave_faces,  slave_id  = target_faces,  seg_id + 1
                self.log("  {0}: Contact=Master, Target=Slave(size={1:.5f})".format(
                    cr_name, size_t))

            self._write_segment_set(lines, slave_id,
                                    cr_name + "_SLAVE",  slave_faces)
            self._write_segment_set(lines, master_id,
                                    cr_name + "_MASTER", master_faces)

            ct = ""
            try:
                ct = str(cr.ContactType)
            except Exception:
                pass
            kw = ("*CONTACT_TIED_SURFACE_TO_SURFACE"
                  if 'Bonded' in ct
                  else "*CONTACT_AUTOMATIC_SURFACE_TO_SURFACE")
            lines.append(kw)
            # Card 1: SURFA(=SSID), SURFB(=MSID), SURFATYP(=SSTYP), SURFBTYP(=MSTYP)
            #   SURFATYP=0 → segment set ID (matches our *SET_SEGMENT_TITLE)
            lines.append("$#    ssid      msid     sstyp     mstyp")
            lines.append("{:10d}{:10d}{:10d}{:10d}".format(
                slave_id, master_id, 0, 0))
            # Card 2: FS, FD, DC, VC, VDC, PENCHK, BT, DT
            mu = 0.0
            if 'Frictional' in ct:
                try:
                    mu = float(cr.FrictionCoefficient)
                except Exception:
                    mu = 0.3
            lines.append("$#       fs        fd        dc        vc       vdc    penchk        bt")
            # DT omitted → LS-DYNA uses default (1e20); writing 0.0 risks early contact death
            lines.append("{:10.4f}{:10.4f}{:10.4f}{:10.4f}{:10.4f}{:10d}{:10.4f}".format(
                mu, mu, 0.0, 0.0, 0.0, 0, 0.0))
            # Card 3: SFSA, SFSB, SAST, SBST, SFSAT, SFSBT, FSF, VSF (mandatory per manual §11-33)
            lines.append("$#    sfsa      sfsb      sast      sbst     sfsat     sfsbt       fsf       vsf")
            lines.append("{:10.4f}{:10.4f}{:10.4f}{:10.4f}{:10.4f}{:10.4f}{:10.4f}{:10.4f}".format(
                1.0, 1.0, 0.0, 0.0, 1.0, 1.0, 1.0, 1.0))
            lines.append("$")
            self.log("  -> {0} (SSID={1} MSID={2})".format(kw, slave_id, master_id))
            seg_id += 2
            written += 1

        return written


def _btn(text, handler, width=None):
    """Helper: create a simple Button."""
    b = Button()
    b.Content = text
    if width:
        b.Width = width
    b.Margin = Thickness(0, 0, 4, 0)
    b.Click += handler
    return b


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


def show_postprocess_dialog(analysis):
    try:
        PostProcessDialog().ShowDialog()
    except Exception as ex:
        MessageBox.Show("Error:\n\n" + str(ex), "Error",
            MessageBoxButton.OK, MessageBoxImage.Error)


def show_export_kfile_dialog(analysis):
    try:
        ExportKFileDialog().ShowDialog()
    except Exception as ex:
        MessageBox.Show("Error:\n\n" + str(ex), "Error",
            MessageBoxButton.OK, MessageBoxImage.Error)


# ═════════════════════════════════════════════════════════════════════════════
# Tied Contact Check Dialog
# ═════════════════════════════════════════════════════════════════════════════

class TiedContactCheckDialog(Window):
    """
    Tied Contact Validation Tool
    - Creates Modal analysis with user-specified modes
    - Detects rigid body motion (disconnected parts)
    - Auto-creates face-to-face contacts or suppresses bodies
    """

    def __init__(self):
        self.Title = "MX Digital Twin  |  Tied Contact Check"
        self.Width = 580
        self.Height = 620
        self.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen
        self.Topmost = True  # Always on top - prevents hiding behind solver windows

        self._modal_analysis = None
        self._rigid_bodies = []  # [(body, modes_list, min_freq), ...]
        self._iteration = 0

        root = StackPanel()
        root.Margin = Thickness(14, 10, 14, 10)

        # ── Header ──
        hdr = Label()
        hdr.Content = "Tied Contact Validation"
        hdr.FontSize = 14
        hdr.FontWeight = System.Windows.FontWeights.Bold
        hdr.Margin = Thickness(0, 0, 0, 8)
        root.Children.Add(hdr)

        info = Label()
        info.Content = "Finds disconnected bodies by detecting rigid-body modes."
        info.Foreground = Brushes.Gray
        info.FontSize = 10
        info.Margin = Thickness(0, 0, 0, 10)
        root.Children.Add(info)

        # ── 1. Modal Settings ──
        root.Children.Add(_hdr("1. Modal Analysis"))

        self.modes_tb = _tb("20", 60)
        root.Children.Add(_row(_lbl("Max Modes:", 120), self.modes_tb))

        self.freq_threshold_tb = _tb("0.1", 60)
        root.Children.Add(_row(
            _lbl("RBM Threshold:", 120), self.freq_threshold_tb, _lbl("Hz")
        ))

        run_btn = Button()
        run_btn.Content = "① Create & Solve Modal"
        run_btn.Height = 32
        run_btn.FontSize = 11
        run_btn.Margin = Thickness(0, 8, 0, 4)
        run_btn.Click += self.on_run_modal
        root.Children.Add(run_btn)

        self.modal_status = _status("")
        root.Children.Add(self.modal_status)

        root.Children.Add(_sep())

        # ── Log (moved up for better UX) ──
        log_lbl = Label()
        log_lbl.Content = "Analysis Log:"
        log_lbl.FontWeight = System.Windows.FontWeights.SemiBold
        log_lbl.Margin = Thickness(0, 0, 0, 4)
        root.Children.Add(log_lbl)

        self.log_tb = TextBox()
        self.log_tb.Height = 200  # Increased from 100
        self.log_tb.IsReadOnly = True
        self.log_tb.VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        self.log_tb.TextWrapping = System.Windows.TextWrapping.NoWrap
        self.log_tb.FontFamily = System.Windows.Media.FontFamily("Consolas")
        self.log_tb.FontSize = 9
        root.Children.Add(self.log_tb)

        root.Children.Add(_sep())

        # ── 2. Disconnected Bodies ──
        root.Children.Add(_hdr("2. Disconnected Bodies"))

        iter_lbl = Label()
        iter_lbl.Content = "Iteration: 0  |  Total Suppressed: 0"
        iter_lbl.FontSize = 10
        iter_lbl.Foreground = Brushes.DarkBlue
        self.iter_lbl = iter_lbl
        root.Children.Add(iter_lbl)

        # ListBox with checkboxes
        self.rb_listbox = ListBox()
        self.rb_listbox.Height = 180
        self.rb_listbox.Margin = Thickness(0, 6, 0, 6)
        root.Children.Add(self.rb_listbox)

        # Action buttons
        btn_panel = StackPanel()
        btn_panel.Orientation = Orientation.Horizontal
        btn_panel.Margin = Thickness(0, 4, 0, 4)

        contact_btn = Button()
        contact_btn.Content = "② Create Contacts (Face-to-Face)"
        contact_btn.Width = 220
        contact_btn.Height = 28
        contact_btn.Margin = Thickness(0, 0, 4, 0)
        contact_btn.Click += self.on_create_contacts
        btn_panel.Children.Add(contact_btn)

        suppress_btn = Button()
        suppress_btn.Content = "③ Suppress Checked"
        suppress_btn.Width = 140
        suppress_btn.Height = 28
        suppress_btn.Margin = Thickness(4, 0, 4, 0)
        suppress_btn.Click += self.on_suppress
        btn_panel.Children.Add(suppress_btn)

        root.Children.Add(btn_panel)

        # Tolerance
        tol_panel = StackPanel()
        tol_panel.Orientation = Orientation.Horizontal
        tol_panel.Margin = Thickness(0, 4, 0, 0)
        tol_panel.Children.Add(_lbl("Tolerance:", 80))
        self.tolerance_tb = _tb("0.01", 50)
        tol_panel.Children.Add(self.tolerance_tb)
        tol_panel.Children.Add(_lbl("mm", 30))
        root.Children.Add(tol_panel)

        root.Children.Add(_sep())

        # ── 3. Iteration Control ──
        root.Children.Add(_hdr("3. Iteration"))

        iter_panel = StackPanel()
        iter_panel.Orientation = Orientation.Horizontal

        rerun_btn = Button()
        rerun_btn.Content = "④ Re-Run Modal"
        rerun_btn.Width = 130
        rerun_btn.Height = 28
        rerun_btn.Margin = Thickness(0, 0, 8, 0)
        rerun_btn.Click += self.on_rerun_modal
        iter_panel.Children.Add(rerun_btn)

        restore_btn = Button()
        restore_btn.Content = "Restore All"
        restore_btn.Width = 100
        restore_btn.Height = 28
        restore_btn.Click += self.on_restore_all
        iter_panel.Children.Add(restore_btn)

        root.Children.Add(iter_panel)

        # ── Close ──
        close_btn = Button()
        close_btn.Content = "Close"
        close_btn.Width = 80
        close_btn.Height = 26
        close_btn.HorizontalAlignment = HorizontalAlignment.Right
        close_btn.Margin = Thickness(0, 10, 0, 0)
        close_btn.Click += self.on_close
        root.Children.Add(close_btn)

        # Wrap in ScrollViewer for better navigation
        sv = ScrollViewer()
        sv.Content = root
        sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        self.Content = sv

        # Test log immediately
        try:
            self.log_tb.Text = "=== Tied Contact Check Dialog ===\n"
            self.log_tb.Text += "Ready to analyze. Click '① Create & Solve Modal' to start.\n\n"
        except Exception as init_ex:
            MessageBox.Show("Init log test failed: {}".format(str(init_ex)), "Init Error")

    def log(self, msg):
        try:
            if self.log_tb is None:
                raise Exception("log_tb is None!")
            self.log_tb.Text += msg + "\n"
            self.log_tb.ScrollToEnd()
        except Exception as ex:
            # Fallback: show MessageBox if logging fails
            MessageBox.Show("Logging failed: {}\nMessage was: {}".format(str(ex), msg), "Log Error")

    # ── ① Create & Solve Modal ───────────────────────────────────────────

    def on_run_modal(self, sender, e):
        """Create Modal analysis and solve"""
        try:
            self.log("=" * 50)
            self.log("① Creating Modal Analysis...")
        except Exception as log_ex:
            MessageBox.Show("Log failed: {}\n\nlog_tb type: {}".format(
                str(log_ex), type(self.log_tb)), "Log Error")
            return

        self.modal_status.Content = "Working..."
        self.modal_status.Foreground = Brushes.DarkOrange

        try:
            max_modes = int(self.modes_tb.Text.strip() or "20")
        except ValueError:
            MessageBox.Show("Invalid Max Modes value.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning)
            return

        try:
            model = ExtAPI.DataModel.Project.Model

            # Delete old MX_TiedCheck analysis if exists
            for analysis in model.Analyses:
                if "MX_TiedCheck" in analysis.Name:
                    self.log("  Deleting old: {}".format(analysis.Name))
                    analysis.Delete()

            # Create new Modal
            modal = model.AddModalAnalysis()
            modal.Name = "MX_TiedCheck_Iter{}".format(self._iteration + 1)
            self._modal_analysis = modal

            # Settings
            settings = modal.AnalysisSettings
            settings.MaximumModesToFind = max_modes

            self.log("  Created: {}".format(modal.Name))
            self.log("  Max Modes: {}".format(max_modes))

            # Solve
            self.log("  Solving... (this may take a while)")
            sol = modal.Solution
            sol.Solve(True)  # Wait for completion

            self.log("  ✓ Solve complete.")

            # Detect rigid bodies
            self._iteration += 1
            self.detect_rigid_bodies()

            self.modal_status.Content = "Modal solved. Check results below."
            self.modal_status.Foreground = Brushes.DarkGreen

        except Exception as ex:
            self.log("  ✗ ERROR: {}".format(str(ex)))
            import traceback
            self.log(traceback.format_exc())
            self.modal_status.Content = "Failed - see log."
            self.modal_status.Foreground = Brushes.Red

    def detect_rigid_bodies(self):
        """Analyze modal results to find rigid body motion"""
        self.log("\n=== Detecting Rigid Body Motion ===")

        if not self._modal_analysis:
            self.log("  ERROR: No modal analysis.")
            return

        try:
            freq_threshold = float(self.freq_threshold_tb.Text.strip() or "0.1")
        except ValueError:
            freq_threshold = 0.1

        try:
            model = ExtAPI.DataModel.Project.Model
            sol = self._modal_analysis.Solution

            # Add Total Deformation result to read mode frequencies
            deform = sol.AddTotalDeformation()
            deform.Name = "MX_TempDeform"

            # Get all bodies (active only)
            all_bodies = [b for b in model.Geometry.GetChildren(
                DataModelObjectCategory.Body, True) if not b.Suppressed]

            self.log("  Active bodies: {}".format(len(all_bodies)))
            self.log("  Frequency threshold: {} Hz".format(freq_threshold))

            # Detect RBM modes (freq < threshold)
            rbm_modes = []
            max_modes = int(self.modes_tb.Text.strip() or "20")

            self.log("\n  Reading mode frequencies...")

            # Evaluate all results first
            sol.EvaluateAllResults()

            for mode_num in range(1, max_modes + 1):
                try:
                    deform.Mode = mode_num

                    # Read frequency (no need to call Evaluate again)
                    freq = float(deform.Frequency.Value)

                    # Log all modes for debugging
                    if freq < freq_threshold:
                        rbm_modes.append((mode_num, freq))
                        self.log("  Mode {}: {:.6f} Hz  ← RBM!".format(mode_num, freq))
                    else:
                        self.log("  Mode {}: {:.6f} Hz".format(mode_num, freq))

                except Exception as mode_ex:
                    self.log("  Mode {}: ERROR - {}".format(mode_num, str(mode_ex)))
                    break  # No more modes

            self.log("\n  Total modes found: {}".format(mode_num - 1))
            self.log("  RBM modes (< {} Hz): {}".format(freq_threshold, len(rbm_modes)))

            # Delete temp result
            deform.Delete()

            if not rbm_modes:
                self.log("\n  ✓ No rigid body modes detected!")
                self.log("  → All bodies are properly connected.")
                self._rigid_bodies = []
                self.update_rb_listbox()
                return

            self.log("\n  Found {} RBM modes.".format(len(rbm_modes)))

            # Step 1: Contact connectivity analysis (fast pre-filter)
            self.log("\n  Analyzing Contact connectivity...")
            suspect_bodies = self._analyze_contact_connectivity(model, all_bodies)

            if suspect_bodies:
                self.log("  Found {} suspect bodies (isolated or weakly connected)".format(
                    len(suspect_bodies)))
                for body in suspect_bodies:
                    body_name = body.Name if hasattr(body, 'Name') else "Body"
                    self.log("    • {}".format(body_name))
            else:
                self.log("  All bodies are connected via Contacts.")

            # Step 2: Strain check only for suspect bodies (if RBM > 6)
            if len(rbm_modes) <= 6:
                self.log("\n  ≤ 6 RBM modes → Normal (global system RBM only)")
                rbm_bodies_map = {}
            elif not suspect_bodies:
                self.log("\n  All bodies connected but {} RBM modes → Check all".format(
                    len(rbm_modes)))
                rbm_bodies_map = self._detect_rbm_bodies_by_energy(
                    sol, all_bodies, rbm_modes)
            else:
                self.log("\n  Checking strain for suspect bodies only...")
                rbm_bodies_map = self._detect_rbm_bodies_by_energy(
                    sol, suspect_bodies, rbm_modes)

            if not rbm_bodies_map:
                self.log("\n  ✓ All RBM modes are global (full system).")
                self.log("  → No disconnected bodies detected.")
                self._rigid_bodies = []
                self.update_rb_listbox()
                return

            # Build list of disconnected bodies
            self._rigid_bodies = []
            for body, mode_list in rbm_bodies_map.items():
                body_name = body.Name if hasattr(body, 'Name') else "Body_{}".format(body.Id)
                modes_str = ", ".join([str(m) for m in mode_list[:5]])
                if len(mode_list) > 5:
                    modes_str += ", ..."
                min_freq = min([f for m, f in rbm_modes if m in mode_list])

                self._rigid_bodies.append((body, modes_str, min_freq))
                self.log("  • {} — RBM in {} modes ({})".format(
                    body_name, len(mode_list), modes_str))

            self.log("\n  Total disconnected bodies: {}".format(len(self._rigid_bodies)))
            self.update_rb_listbox()

            suppressed_count = len([b for b in model.Geometry.GetChildren(
                DataModelObjectCategory.Body, True) if b.Suppressed])

            self.iter_lbl.Content = "Iteration: {}  |  Total Suppressed: {}".format(
                self._iteration, suppressed_count)

        except Exception as ex:
            self.log("  ERROR during detection: {}".format(str(ex)))
            import traceback
            self.log(traceback.format_exc())

    def _analyze_contact_connectivity(self, model, all_bodies):
        """
        Analyze Contact Region connectivity to find isolated/weakly connected bodies.
        Returns list of suspect bodies (not part of main connected component).
        """
        try:
            connections = model.Connections
            contacts = connections.GetChildren(DataModelObjectCategory.ContactRegion, True)

            if not contacts:
                self.log("    No Contact Regions defined → All bodies suspect")
                return list(all_bodies)

            # Build adjacency map: body -> set of connected bodies
            adjacent = {}
            for body in all_bodies:
                adjacent[body] = set()

            for contact in contacts:
                try:
                    # Extract bodies from Contact and Target geometry
                    contact_bodies = set()

                    # Get Contact side bodies
                    if hasattr(contact, 'SourceLocation'):
                        contact_bodies.update(self._get_bodies_from_selection(
                            contact.SourceLocation, all_bodies))
                    elif hasattr(contact, 'ContactGeometry'):
                        contact_bodies.update(self._get_bodies_from_geometry(
                            contact.ContactGeometry, all_bodies))

                    # Get Target side bodies
                    if hasattr(contact, 'TargetLocation'):
                        contact_bodies.update(self._get_bodies_from_selection(
                            contact.TargetLocation, all_bodies))
                    elif hasattr(contact, 'TargetGeometry'):
                        contact_bodies.update(self._get_bodies_from_geometry(
                            contact.TargetGeometry, all_bodies))

                    # Add edges between all pairs
                    contact_bodies_list = list(contact_bodies)
                    for i, b1 in enumerate(contact_bodies_list):
                        for b2 in contact_bodies_list[i+1:]:
                            adjacent[b1].add(b2)
                            adjacent[b2].add(b1)

                except Exception:
                    continue

            # Find connected components using BFS
            visited = set()
            components = []

            for body in all_bodies:
                if body in visited:
                    continue

                # BFS to find connected component
                component = set()
                queue = [body]
                while queue:
                    current = queue.pop(0)
                    if current in visited:
                        continue
                    visited.add(current)
                    component.add(current)
                    for neighbor in adjacent[current]:
                        if neighbor not in visited:
                            queue.append(neighbor)

                components.append(component)

            # Find largest component (main assembly)
            if not components:
                return list(all_bodies)

            main_component = max(components, key=len)
            self.log("    Main component: {} bodies".format(len(main_component)))
            if len(components) > 1:
                self.log("    Isolated components: {}".format(len(components) - 1))

            # Return bodies NOT in main component
            suspect = [body for body in all_bodies if body not in main_component]
            return suspect

        except Exception as ex:
            self.log("    Connectivity analysis failed: {}".format(str(ex)))
            return []

    def _get_bodies_from_selection(self, selection_info, all_bodies):
        """Extract bodies from SelectionInfo (face selection)"""
        bodies = set()
        try:
            for entity in selection_info.Entities:
                # Entity is a face (GeoFace), find parent body
                for body in all_bodies:
                    try:
                        geo_body = body.GetGeoBody()
                        for face in geo_body.Faces:
                            if face == entity:
                                bodies.add(body)
                                break
                    except:
                        continue
        except:
            pass
        return bodies

    def _get_bodies_from_geometry(self, geometry, all_bodies):
        """Extract bodies from geometry scoping (legacy API)"""
        bodies = set()
        try:
            geo_ids = list(geometry.Ids)
            for body in all_bodies:
                try:
                    geo_body = body.GetGeoBody()
                    for face in geo_body.Faces:
                        if hasattr(face, 'Id') and face.Id in geo_ids:
                            bodies.add(body)
                            break
                except:
                    continue
        except:
            pass
        return bodies

    def _detect_rbm_bodies_by_energy(self, sol, all_bodies, rbm_modes):
        """
        Detect which bodies exhibit rigid body motion using strain energy.
        Returns {body: [mode_nums]} for bodies with RBM.

        Logic:
          - For each RBM mode, check each body's equivalent elastic strain
          - Low strain (<threshold) → rigid body motion (no deformation)
          - High strain → participating in deformation (connected)
        """
        from Ansys.ACT.Interfaces.Common import SelectionTypeEnum

        rbm_bodies = {}  # {body: [mode_nums]}
        strain_threshold = 1e-5  # Very low strain = RBM (increased from 1e-8 for robustness)

        self.log("  Checking strain energy per body per mode...")
        self.log("  Strain threshold: {:.2e}".format(strain_threshold))

        for mode_num, freq in rbm_modes:
            try:
                # Create equivalent elastic strain result
                strain_result = sol.AddEquivalentElasticStrain()
                strain_result.Name = "MX_TempStrain_M{}".format(mode_num)
                strain_result.Mode = mode_num

                # Evaluate once per mode (not per body!)
                sol.EvaluateAllResults()

                for body in all_bodies:
                    body_name = body.Name if hasattr(body, 'Name') else "Body"

                    try:
                        # Scope to this body
                        sel = ExtAPI.SelectionManager.CreateSelectionInfo(
                            SelectionTypeEnum.GeometryEntities)
                        sel.Entities = [body.GetGeoBody()]
                        strain_result.Location = sel

                        # Get maximum strain in this body for this mode (no re-evaluation needed)
                        # Maximum returns a Quantity object, extract the numeric value
                        max_strain_qty = strain_result.Maximum
                        max_strain = float(max_strain_qty.Value) if hasattr(max_strain_qty, 'Value') else float(max_strain_qty)

                        # RBM detection: very low strain = rigid motion
                        if max_strain < strain_threshold:
                            if body not in rbm_bodies:
                                rbm_bodies[body] = []
                            rbm_bodies[body].append(mode_num)
                            self.log("    Mode {}: {} — RBM (strain={:.2e})".format(
                                mode_num, body_name, max_strain))
                        else:
                            self.log("    Mode {}: {} — Deforming (strain={:.2e})".format(
                                mode_num, body_name, max_strain))

                    except Exception as body_ex:
                        # Failed to check this body - skip
                        self.log("    Mode {}: {} — Check failed: {}".format(
                            mode_num, body_name, str(body_ex)))
                        continue

                # Delete temp result
                strain_result.Delete()

            except Exception as mode_ex:
                self.log("  ERROR checking mode {}: {}".format(mode_num, str(mode_ex)))
                continue

        # Filter: only return bodies that are RBM in SOME (not all) modes
        # If a body is RBM in all RBM modes, it's part of the global system RBM
        total_rbm_modes = len(rbm_modes)
        filtered = {}

        for body, mode_list in rbm_bodies.items():
            # If RBM in less than all modes → disconnected part
            # If RBM in all modes → might be global system RBM (need more analysis)
            # For now, include all that are RBM in at least one mode
            if len(mode_list) > 0:
                filtered[body] = mode_list

        return filtered

    def update_rb_listbox(self):
        """Update ListBox with rigid bodies"""
        self.rb_listbox.Items.Clear()

        for body, modes, freq in self._rigid_bodies:
            body_name = body.Name if hasattr(body, 'Name') else "Body_{}".format(body.Id)

            cb = CheckBox()
            cb.Content = "{}  (modes: {}, freq: {:.4f} Hz)".format(
                body_name, modes, freq)
            cb.Tag = body
            cb.IsChecked = True

            self.rb_listbox.Items.Add(cb)

    # ── ② Create Contacts ─────────────────────────────────────────────────

    def on_create_contacts(self, sender, e):
        """Create face-to-face contacts for checked bodies"""
        import time

        self.log("\n" + "=" * 50)
        self.log("② Creating Face-to-Face Contacts...")

        checked = [item for item in self.rb_listbox.Items if item.IsChecked]

        if not checked:
            self.log("  No bodies checked.")
            return

        try:
            tolerance_mm = float(self.tolerance_tb.Text.strip() or "0.01")
        except ValueError:
            tolerance_mm = 0.01

        tolerance_m = tolerance_mm / 1000.0

        self.log("  Bodies to process: {}".format(len(checked)))
        self.log("  Tolerance: {:.2f} mm".format(tolerance_mm))

        success_count = 0
        failed_bodies = []
        start_time = time.time()

        for idx, cb in enumerate(checked):
            body = cb.Tag
            body_name = body.Name if hasattr(body, 'Name') else "Body_{}".format(body.Id)

            self.log("\n[{}/{}] {}".format(idx + 1, len(checked), body_name))

            try:
                result = self.create_contact_for_body(body, tolerance_m)

                if result:
                    success_count += 1
                else:
                    failed_bodies.append(body)

            except Exception as ex:
                self.log("  ✗ EXCEPTION: {}".format(str(ex)))
                failed_bodies.append(body)

        elapsed = time.time() - start_time

        self.log("\n=== Summary ===")
        self.log("  Created: {} contacts".format(success_count))
        self.log("  Failed: {}".format(len(failed_bodies)))
        self.log("  Time: {:.1f} seconds".format(elapsed))

        if failed_bodies:
            names = ", ".join([b.Name for b in failed_bodies])
            self.log("\n  Failed bodies: {}".format(names))
            self.log("  → Recommendation: Suppress these bodies instead.")

            result = MessageBox.Show(
                "Failed to create contacts for:\n\n" + names +
                "\n\nSuppress these bodies?",
                "Auto-Contact Failed",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question)

            if result == MessageBoxResult.Yes:
                for body in failed_bodies:
                    body.Suppressed = True
                    self.log("  Suppressed: {}".format(body.Name))

    def create_contact_for_body(self, body, tolerance_m):
        """
        Create contact between body and nearest adjacent body
        Returns True on success, False on failure
        """
        model = ExtAPI.DataModel.Project.Model

        # Find adjacent bodies
        all_bodies = [b for b in model.Geometry.GetChildren(
            DataModelObjectCategory.Body, True) if not b.Suppressed and b != body]

        if not all_bodies:
            self.log("  ✗ No other active bodies.")
            return False

        # Find facing faces
        target_body, facing_pairs = self.find_best_contact_target(
            body, all_bodies, tolerance_m)

        if not target_body or not facing_pairs:
            self.log("  ✗ No facing faces found (tolerance: {:.1f} mm).".format(
                tolerance_m * 1000))
            return False

        target_name = target_body.Name if hasattr(target_body, 'Name') else "Body"
        self.log("  Found: {} facing face pairs with {}".format(
            len(facing_pairs), target_name))

        # Create Contact Region
        try:
            connections = model.Connections
            contact = connections.AddContactRegion()
            contact.Name = "Auto_{}_{}".format(
                body.Name if hasattr(body, 'Name') else "Body",
                target_body.Name if hasattr(target_body, 'Name') else "Target")

            from Ansys.ACT.Interfaces.Common import SelectionTypeEnum

            # Source faces (from body)
            sel_src = ExtAPI.SelectionManager.CreateSelectionInfo(
                SelectionTypeEnum.GeometryEntities)
            sel_src.Entities = [pair[0] for pair in facing_pairs]
            contact.SourceLocation = sel_src

            # Target faces (from target_body)
            sel_tgt = ExtAPI.SelectionManager.CreateSelectionInfo(
                SelectionTypeEnum.GeometryEntities)
            sel_tgt.Entities = [pair[1] for pair in facing_pairs]
            contact.TargetLocation = sel_tgt

            # Set contact type
            try:
                contact.ContactType = Ansys.Mechanical.DataModel.Enums.ContactType.Bonded
            except:
                pass

            self.log("  ✓ Created contact: {} <-> {}".format(
                body.Name if hasattr(body, 'Name') else "Body",
                target_name))

            return True

        except Exception as ex:
            self.log("  ✗ Failed to create contact: {}".format(str(ex)))
            return False

    def find_best_contact_target(self, body, all_bodies, tolerance_m):
        """
        Find best target body with facing faces (OPTIMIZED)
        Returns (target_body, facing_face_pairs)

        Optimizations:
          1. Bounding box pre-filter
          2. Face sampling (max 100 faces per body)
          3. Early exit (stop at 5 face pairs)
          4. Progress reporting
          5. Timeout (10 seconds)
        """
        import math
        import time
        import random

        start_time = time.time()
        timeout_sec = 10.0

        body_geo = body.GetGeoBody()
        body_faces = list(body_geo.Faces)
        body_name = body.Name if hasattr(body, 'Name') else "Body"

        # ── Optimization 1: Face Sampling ──
        MAX_FACES = 100
        if len(body_faces) > MAX_FACES:
            self.log("  [OPTIMIZE] {} has {} faces - sampling {}".format(
                body_name, len(body_faces), MAX_FACES))
            body_faces = random.sample(body_faces, MAX_FACES)

        # Bounding box
        try:
            body_bb = body_geo.BoundingBox
        except:
            body_bb = None

        best_target = None
        best_pairs = []
        max_pairs = 0

        total_bodies = len(all_bodies)
        checked_bodies = 0

        for target_body in all_bodies:
            # ── Optimization 2: Timeout Check ──
            elapsed = time.time() - start_time
            if elapsed > timeout_sec:
                self.log("  [TIMEOUT] Stopped after {:.1f}s ({}/{} bodies checked)".format(
                    elapsed, checked_bodies, total_bodies))
                break

            target_geo = target_body.GetGeoBody()
            target_name = target_body.Name if hasattr(target_body, 'Name') else "Target"

            # ── Optimization 3: Bounding Box Filter ──
            if body_bb:
                try:
                    target_bb = target_geo.BoundingBox
                    bb_dist = self.bounding_box_distance(body_bb, target_bb)

                    if bb_dist > tolerance_m * 2.0:  # 충분히 멀면 스킵
                        continue
                except:
                    pass  # BB 계산 실패 시 계속 진행

            target_faces = list(target_geo.Faces)

            # ── Optimization 4: Face Sampling ──
            if len(target_faces) > MAX_FACES:
                target_faces = random.sample(target_faces, MAX_FACES)

            facing_pairs = []
            face_checks = 0

            for face1 in body_faces:
                for face2 in target_faces:
                    face_checks += 1

                    if self.are_faces_facing(face1, face2, tolerance_m):
                        facing_pairs.append((face1, face2))

                        # ── Optimization 5: Early Exit ──
                        if len(facing_pairs) >= 5:
                            self.log("  [EARLY EXIT] Found 5 face pairs with {}".format(
                                target_name))
                            best_target = target_body
                            best_pairs = facing_pairs
                            return best_target, best_pairs

            if len(facing_pairs) > max_pairs:
                max_pairs = len(facing_pairs)
                best_target = target_body
                best_pairs = facing_pairs

            checked_bodies += 1

            # ── Optimization 6: Progress Reporting ──
            if checked_bodies % 5 == 0 or facing_pairs:
                self.log("  Progress: {}/{} bodies | Best: {} pairs{}".format(
                    checked_bodies, total_bodies, max_pairs,
                    " with {}".format(target_name) if facing_pairs else ""))

        return best_target, best_pairs

    def bounding_box_distance(self, bb1, bb2):
        """
        Compute minimum distance between two bounding boxes
        Returns 0 if they overlap
        """
        import math

        # BB min/max corners
        try:
            min1 = bb1.MinCorner
            max1 = bb1.MaxCorner
            min2 = bb2.MinCorner
            max2 = bb2.MaxCorner
        except:
            return 0.0  # Can't compute - assume close

        # Distance per axis (0 if overlapping)
        dx = max(0.0, max(min1.X - max2.X, min2.X - max1.X))
        dy = max(0.0, max(min1.Y - max2.Y, min2.Y - max1.Y))
        dz = max(0.0, max(min1.Z - max2.Z, min2.Z - max1.Z))

        return math.sqrt(dx*dx + dy*dy + dz*dz)

    def are_faces_facing(self, face1, face2, tolerance_m):
        """
        Check if two faces are facing each other
        Criteria:
          1. Normals are opposite (dot product ≈ -1)
          2. Distance between centroids < tolerance
          3. Not from same body (already filtered)
        """
        import math

        try:
            # Get face properties
            n1 = face1.NormalAtParam(0.5, 0.5)
            n2 = face2.NormalAtParam(0.5, 0.5)
            c1 = face1.Centroid
            c2 = face2.Centroid

            # 1. Check normal direction (should be opposite)
            dot = n1.X*n2.X + n1.Y*n2.Y + n1.Z*n2.Z
            if dot > -0.8:  # Not facing (should be close to -1)
                return False

            # 2. Check distance
            dx = c1.X - c2.X
            dy = c1.Y - c2.Y
            dz = c1.Z - c2.Z
            dist = math.sqrt(dx*dx + dy*dy + dz*dz)

            if dist > tolerance_m:
                return False

            return True

        except Exception:
            return False

    # ── ③ Suppress ────────────────────────────────────────────────────────

    def on_suppress(self, sender, e):
        """Suppress checked bodies"""
        self.log("\n" + "=" * 50)
        self.log("③ Suppressing checked bodies...")

        checked = [item for item in self.rb_listbox.Items if item.IsChecked]

        if not checked:
            self.log("  No bodies checked.")
            return

        try:
            model = ExtAPI.DataModel.Project.Model

            for cb in checked:
                body = cb.Tag
                body.Suppressed = True
                body_name = body.Name if hasattr(body, 'Name') else "Body"
                self.log("  Suppressed: {}".format(body_name))

            # Update geometry
            model.Geometry.UpdateGeometry()

            self.log("\n  ✓ {} bodies suppressed.".format(len(checked)))
            self.log("  → Run '④ Re-Run Modal' to verify.")

            # Update iteration counter
            suppressed_count = len([b for b in model.Geometry.GetChildren(
                DataModelObjectCategory.Body, True) if b.Suppressed])

            self.iter_lbl.Content = "Iteration: {}  |  Total Suppressed: {}".format(
                self._iteration, suppressed_count)

        except Exception as ex:
            self.log("  ✗ ERROR: {}".format(str(ex)))

    # ── ④ Re-Run Modal ────────────────────────────────────────────────────

    def on_rerun_modal(self, sender, e):
        """Re-run modal with current (possibly suppressed) geometry"""
        self.on_run_modal(sender, e)

    # ── Restore All ───────────────────────────────────────────────────────

    def on_restore_all(self, sender, e):
        """Restore all suppressed bodies"""
        self.log("\n" + "=" * 50)
        self.log("Restoring all suppressed bodies...")

        try:
            model = ExtAPI.DataModel.Project.Model
            count = 0

            for body in model.Geometry.GetChildren(DataModelObjectCategory.Body, True):
                if body.Suppressed:
                    body.Suppressed = False
                    count += 1
                    body_name = body.Name if hasattr(body, 'Name') else "Body"
                    self.log("  Restored: {}".format(body_name))

            if count > 0:
                model.Geometry.UpdateGeometry()
                self.log("\n  ✓ {} bodies restored.".format(count))

                self._iteration = 0
                self.iter_lbl.Content = "Iteration: 0  |  Total Suppressed: 0"
            else:
                self.log("  No suppressed bodies found.")

        except Exception as ex:
            self.log("  ✗ ERROR: {}".format(str(ex)))

    def on_close(self, sender, e):
        self.Close()


def show_tied_check_dialog(analysis):
    """Launch Tied Contact Check dialog."""
    dlg = TiedContactCheckDialog()
    dlg.Show()


def Initialize():
    pass


def Finalize():
    pass
