# encoding: utf-8
"""
MX Digital Twin Simulator - ANSYS Mechanical ACT Extension
Cap Vibration Time Force Setup - Phase 1: STEP Import + Face Analysis
"""

import sys
import os
import clr

# Shared DLL 로드 (공용 로직) - 선택적
try:
    dll_path = os.path.join(os.path.dirname(__file__), "bin", "MXDigitalTwinModeller.Core.dll")
    if os.path.exists(dll_path):
        clr.AddReferenceToFileAndPath(dll_path)
        from MXDigitalTwinModeller.Core.Geometry import GeometryUtils
        from MXDigitalTwinModeller.Core.Spatial import SpatialIndex, BodyBounds
except Exception as dll_ex:
    # Shared DLL 없어도 메인 기능은 작동
    pass

# WPF UI 사용을 위한 참조
clr.AddReference("PresentationFramework")
clr.AddReference("PresentationCore")
clr.AddReference("WindowsBase")
clr.AddReference("System.Xaml")

# File Dialog
clr.AddReference("System.Windows.Forms")
from System.Windows.Forms import OpenFileDialog, DialogResult

from System.Windows import Window, Application
from System.Windows.Controls import (
    StackPanel, Button, Label, TextBox, TextBlock,
    GroupBox, Orientation, ScrollViewer, ScrollBarVisibility
)
from System.Windows import (
    HorizontalAlignment, VerticalAlignment,
    Thickness, MessageBox, MessageBoxButton, MessageBoxImage
)
from System.Windows.Media import Brushes

# ANSYS Mechanical API
try:
    import Ansys
    from Ansys.ACT.Interfaces.Mechanical import IMechanicalExtAPI
    from Ansys.ACT.Automation.Mechanical import Model
except ImportError as ansys_ex:
    # Ansys 모듈 로드 실패 - ExtAPI 직접 사용
    pass

try:
    from Ansys.Mechanical.DataModel.Enums import (
        DataModelObjectCategory, GeometryDefineByType
    )
except ImportError:
    # 런타임에 로드될 수 있음
    pass

# Note: ExtAPI는 ANSYS Mechanical 런타임에서 글로벌 변수로 자동 제공됨
# ExtAPI.DataModel, ExtAPI.SelectionManager 등 사용 가능


# ================================================================
# ACT Extension Callbacks
# ================================================================

def on_init(_=None):
    """
    ACT Extension 초기화 콜백
    Extension이 로드될 때 ANSYS Mechanical에 의해 자동 호출됨
    """
    pass  # 현재는 초기화 작업 불필요


# ================================================================
# Helper Functions - Face Normal Analysis
# ================================================================

def classify_normal_direction(normal):
    """
    법선 벡터를 주요 축 방향으로 분류

    Parameters
    ----------
    normal : Vector or tuple (X, Y, Z)
        법선 벡터

    Returns
    -------
    str : '+X', '-X', '+Y', '-Y', '+Z', '-Z'
    """
    try:
        # Vector 객체인 경우
        nx, ny, nz = abs(normal.X), abs(normal.Y), abs(normal.Z)
        orig_x, orig_y, orig_z = normal.X, normal.Y, normal.Z
    except:
        # Tuple인 경우
        nx, ny, nz = abs(normal[0]), abs(normal[1]), abs(normal[2])
        orig_x, orig_y, orig_z = normal[0], normal[1], normal[2]

    # 가장 큰 성분 찾기
    if nz > nx and nz > ny:
        return '+Z' if orig_z > 0 else '-Z'
    elif ny > nx and ny > nz:
        return '+Y' if orig_y > 0 else '-Y'
    else:
        return '+X' if orig_x > 0 else '-X'


# ================================================================
# Cap Vibration Time Force Dialog - Phase 1
# ================================================================

class CapVibrationDialog(Window):
    """
    Cap Vibration Time Force Setup (WPF)
    Phase 1: STEP Import + Face Normal Analysis + Named Selection Creation
    """

    def __init__(self):
        # Window 속성
        self.Title = "Cap Vibration - Complete Workflow"
        self.Width = 700
        self.Height = 950
        self.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen

        # 데이터 저장
        self.step_file_path = ""
        self.csv_file_path = ""
        self.imported_bodies = []
        self.face_data_list = []
        self.ns_dict = {}
        self.current_analysis = None

        # Mesh 설정 (기본값)
        self.element_size_mm = 2.0
        self.growth_rate = 1.8

        # Analysis 설정 (Phase 5)
        self.num_modes = 20
        self.max_frequency_hz = 1000.0
        self.end_time_s = 0.02
        self.time_step_s = 0.0001

        # 메인 레이아웃
        main_panel = StackPanel()
        main_panel.Margin = Thickness(10)

        # 헤더
        header = Label()
        header.Content = "Cap Vibration Time Force Setup"
        header.FontSize = 16
        header.FontWeight = System.Windows.FontWeights.Bold
        header.Margin = Thickness(0, 0, 0, 10)
        main_panel.Children.Add(header)

        # Phase Info
        info_label = Label()
        info_label.Content = "Complete Workflow: STEP → Mesh → Forces → Modal Superposition Analysis"
        info_label.FontSize = 11
        info_label.Foreground = Brushes.DarkBlue
        info_label.Margin = Thickness(0, 0, 0, 10)
        main_panel.Children.Add(info_label)

        # Import Notice
        notice = TextBlock()
        notice.Text = "⚠ Please import STEP file manually first: File → Import → External Geometry File"
        notice.FontSize = 10
        notice.Foreground = Brushes.OrangeRed
        notice.Margin = Thickness(0, 0, 0, 5)
        notice.TextWrapping = System.Windows.TextWrapping.Wrap
        main_panel.Children.Add(notice)

        # STEP Files Group
        step_group = self._create_step_group()
        main_panel.Children.Add(step_group)

        # Named Selection Group
        ns_group = self._create_ns_group()
        main_panel.Children.Add(ns_group)

        # Mesh Settings Group
        mesh_group = self._create_mesh_group()
        main_panel.Children.Add(mesh_group)

        # Force Application Group (Phase 4)
        force_group = self._create_force_group()
        main_panel.Children.Add(force_group)

        # Analysis Setup Group (Phase 5)
        analysis_group = self._create_analysis_group()
        main_panel.Children.Add(analysis_group)

        # Log Group
        log_group = self._create_log_group()
        main_panel.Children.Add(log_group)

        # 버튼 패널
        btn_panel = self._create_button_panel()
        main_panel.Children.Add(btn_panel)

        self.Content = main_panel

    def _create_step_group(self):
        """Face Analysis 그룹"""
        group = GroupBox()
        group.Header = "Face Analysis (Manual Import Required)"
        group.Margin = Thickness(0, 0, 0, 10)

        panel = StackPanel()
        panel.Margin = Thickness(5)

        # Instruction label
        instruction = TextBlock()
        instruction.Text = "1. Import STEP manually: File → Import → External Geometry File\n2. Click 'Analyze Faces' below"
        instruction.FontSize = 9
        instruction.Foreground = Brushes.Gray
        instruction.Margin = Thickness(0, 0, 0, 10)
        instruction.TextWrapping = System.Windows.TextWrapping.Wrap
        panel.Children.Add(instruction)

        # Analyze button
        import_btn = Button()
        import_btn.Content = "Analyze Faces (Current Geometry)"
        import_btn.Width = 220
        import_btn.Height = 28
        import_btn.Margin = Thickness(0, 0, 0, 0)
        import_btn.HorizontalAlignment = HorizontalAlignment.Left
        import_btn.Click += self.on_import_click
        panel.Children.Add(import_btn)

        group.Content = panel
        return group

    def _create_ns_group(self):
        """Named Selection 결과 그룹"""
        group = GroupBox()
        group.Header = "Named Selections (Auto-Created)"
        group.Margin = Thickness(0, 0, 0, 10)

        panel = StackPanel()
        panel.Margin = Thickness(5)

        self.ns_status_label = Label()
        self.ns_status_label.Content = "No Named Selections created yet"
        self.ns_status_label.FontSize = 10
        self.ns_status_label.Foreground = Brushes.Gray
        panel.Children.Add(self.ns_status_label)

        group.Content = panel
        return group

    def _create_mesh_group(self):
        """Mesh Settings 그룹"""
        group = GroupBox()
        group.Header = "Mesh Settings"
        group.Margin = Thickness(0, 0, 0, 10)

        panel = StackPanel()
        panel.Margin = Thickness(5)

        # Element Size
        size_panel = StackPanel()
        size_panel.Orientation = Orientation.Horizontal
        size_panel.Margin = Thickness(0, 0, 0, 5)

        size_label = Label()
        size_label.Content = "Element Size:"
        size_label.Width = 100
        size_panel.Children.Add(size_label)

        self.element_size_textbox = TextBox()
        self.element_size_textbox.Width = 80
        self.element_size_textbox.Text = str(self.element_size_mm)
        size_panel.Children.Add(self.element_size_textbox)

        size_unit = Label()
        size_unit.Content = "mm"
        size_unit.Margin = Thickness(5, 0, 0, 0)
        size_panel.Children.Add(size_unit)

        panel.Children.Add(size_panel)

        # Growth Rate
        growth_panel = StackPanel()
        growth_panel.Orientation = Orientation.Horizontal
        growth_panel.Margin = Thickness(0, 0, 0, 10)

        growth_label = Label()
        growth_label.Content = "Growth Rate:"
        growth_label.Width = 100
        growth_panel.Children.Add(growth_label)

        self.growth_rate_textbox = TextBox()
        self.growth_rate_textbox.Width = 80
        self.growth_rate_textbox.Text = str(self.growth_rate)
        growth_panel.Children.Add(self.growth_rate_textbox)

        panel.Children.Add(growth_panel)

        # Generate Mesh 버튼
        generate_btn = Button()
        generate_btn.Content = "Generate Mesh"
        generate_btn.Width = 150
        generate_btn.Height = 28
        generate_btn.Margin = Thickness(100, 5, 0, 0)
        generate_btn.HorizontalAlignment = HorizontalAlignment.Left
        generate_btn.Click += self.on_generate_mesh_click
        panel.Children.Add(generate_btn)

        group.Content = panel
        return group

    def _create_force_group(self):
        """Force Application 그룹 (Phase 4)"""
        group = GroupBox()
        group.Header = "Time Force Application (CSV)"
        group.Margin = Thickness(0, 0, 0, 10)

        panel = StackPanel()
        panel.Margin = Thickness(5)

        # CSV File 선택
        csv_panel = StackPanel()
        csv_panel.Orientation = Orientation.Horizontal
        csv_panel.Margin = Thickness(0, 0, 0, 5)

        csv_label = Label()
        csv_label.Content = "CSV File:"
        csv_label.Width = 80
        csv_panel.Children.Add(csv_label)

        self.csv_textbox = TextBox()
        self.csv_textbox.Width = 400
        self.csv_textbox.IsReadOnly = True
        csv_panel.Children.Add(self.csv_textbox)

        csv_browse_btn = Button()
        csv_browse_btn.Content = "Browse..."
        csv_browse_btn.Width = 80
        csv_browse_btn.Margin = Thickness(5, 0, 0, 0)
        csv_browse_btn.Click += self.on_csv_browse_click
        csv_panel.Children.Add(csv_browse_btn)

        panel.Children.Add(csv_panel)

        # CSV 형식 설명
        format_label = Label()
        format_label.Content = "Format: Time(s), Force(N)"
        format_label.FontSize = 9
        format_label.Foreground = Brushes.Gray
        format_label.Margin = Thickness(80, 0, 0, 5)
        panel.Children.Add(format_label)

        # Apply Forces 버튼
        apply_btn = Button()
        apply_btn.Content = "Apply Forces to Named Selections"
        apply_btn.Width = 220
        apply_btn.Height = 28
        apply_btn.Margin = Thickness(80, 5, 0, 0)
        apply_btn.HorizontalAlignment = HorizontalAlignment.Left
        apply_btn.Click += self.on_apply_forces_click
        panel.Children.Add(apply_btn)

        group.Content = panel
        return group

    def _create_analysis_group(self):
        """Analysis Setup 그룹 (Phase 5)"""
        group = GroupBox()
        group.Header = "Modal Superposition Analysis Setup"
        group.Margin = Thickness(0, 0, 0, 10)

        panel = StackPanel()
        panel.Margin = Thickness(5)

        # Number of Modes
        modes_panel = StackPanel()
        modes_panel.Orientation = Orientation.Horizontal
        modes_panel.Margin = Thickness(0, 0, 0, 5)

        modes_label = Label()
        modes_label.Content = "Number of Modes:"
        modes_label.Width = 120
        modes_panel.Children.Add(modes_label)

        self.num_modes_textbox = TextBox()
        self.num_modes_textbox.Width = 80
        self.num_modes_textbox.Text = str(self.num_modes)
        modes_panel.Children.Add(self.num_modes_textbox)

        panel.Children.Add(modes_panel)

        # Max Frequency
        freq_panel = StackPanel()
        freq_panel.Orientation = Orientation.Horizontal
        freq_panel.Margin = Thickness(0, 0, 0, 5)

        freq_label = Label()
        freq_label.Content = "Max Frequency:"
        freq_label.Width = 120
        freq_panel.Children.Add(freq_label)

        self.max_freq_textbox = TextBox()
        self.max_freq_textbox.Width = 80
        self.max_freq_textbox.Text = str(self.max_frequency_hz)
        freq_panel.Children.Add(self.max_freq_textbox)

        freq_unit = Label()
        freq_unit.Content = "Hz"
        freq_unit.Margin = Thickness(5, 0, 0, 0)
        freq_panel.Children.Add(freq_unit)

        panel.Children.Add(freq_panel)

        # End Time
        time_panel = StackPanel()
        time_panel.Orientation = Orientation.Horizontal
        time_panel.Margin = Thickness(0, 0, 0, 5)

        time_label = Label()
        time_label.Content = "End Time:"
        time_label.Width = 120
        time_panel.Children.Add(time_label)

        self.end_time_textbox = TextBox()
        self.end_time_textbox.Width = 80
        self.end_time_textbox.Text = str(self.end_time_s)
        time_panel.Children.Add(self.end_time_textbox)

        time_unit = Label()
        time_unit.Content = "s"
        time_unit.Margin = Thickness(5, 0, 0, 0)
        time_panel.Children.Add(time_unit)

        panel.Children.Add(time_panel)

        # Time Step
        step_panel = StackPanel()
        step_panel.Orientation = Orientation.Horizontal
        step_panel.Margin = Thickness(0, 0, 0, 10)

        step_label = Label()
        step_label.Content = "Time Step:"
        step_label.Width = 120
        step_panel.Children.Add(step_label)

        self.time_step_textbox = TextBox()
        self.time_step_textbox.Width = 80
        self.time_step_textbox.Text = str(self.time_step_s)
        step_panel.Children.Add(self.time_step_textbox)

        step_unit = Label()
        step_unit.Content = "s"
        step_unit.Margin = Thickness(5, 0, 0, 0)
        step_panel.Children.Add(step_unit)

        panel.Children.Add(step_panel)

        # Create Analysis 버튼
        create_btn = Button()
        create_btn.Content = "Create Modal Superposition Analysis"
        create_btn.Width = 250
        create_btn.Height = 28
        create_btn.Margin = Thickness(120, 5, 0, 0)
        create_btn.HorizontalAlignment = HorizontalAlignment.Left
        create_btn.Click += self.on_create_analysis_click
        panel.Children.Add(create_btn)

        group.Content = panel
        return group

    def _create_log_group(self):
        """로그 그룹"""
        group = GroupBox()
        group.Header = "Log"
        group.Margin = Thickness(0, 0, 0, 10)

        # ScrollViewer로 감싸기
        scroll = ScrollViewer()
        scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        scroll.Height = 200

        self.log_textbox = TextBox()
        self.log_textbox.IsReadOnly = True
        self.log_textbox.TextWrapping = System.Windows.TextWrapping.Wrap
        self.log_textbox.AcceptsReturn = True
        self.log_textbox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto

        scroll.Content = self.log_textbox
        group.Content = scroll
        return group

    def _create_button_panel(self):
        """버튼 패널"""
        panel = StackPanel()
        panel.Orientation = Orientation.Horizontal
        panel.HorizontalAlignment = HorizontalAlignment.Right
        panel.Margin = Thickness(0, 10, 0, 0)

        # Create NS 버튼
        create_ns_btn = Button()
        create_ns_btn.Content = "Create Named Selections"
        create_ns_btn.Width = 180
        create_ns_btn.Height = 28
        create_ns_btn.Margin = Thickness(0, 0, 10, 0)
        create_ns_btn.Click += self.on_create_ns_click
        panel.Children.Add(create_ns_btn)

        # Close 버튼
        close_btn = Button()
        close_btn.Content = "Close"
        close_btn.Width = 80
        close_btn.Height = 28
        close_btn.Click += self.on_close_click
        panel.Children.Add(close_btn)

        return panel

    # ================================================================
    # Event Handlers
    # ================================================================

    def on_browse_click(self, sender, e):
        """Browse 버튼 클릭"""
        dialog = OpenFileDialog()
        dialog.Filter = "STEP Files (*.stp;*.step)|*.stp;*.step|All Files (*.*)|*.*"
        dialog.Title = "Select STEP File"

        if dialog.ShowDialog() == DialogResult.OK:
            self.step_file_path = dialog.FileName
            self.file_textbox.Text = self.step_file_path
            self.log("Selected STEP file: {0}".format(self.step_file_path))

    def on_import_click(self, sender, e):
        """Analyze 버튼 클릭 - 기존 geometry 면 분석"""
        try:
            self.log("\n=== Starting Face Analysis ===")

            # 기존 모델의 모든 바디 가져오기
            model = ExtAPI.DataModel.Project.Model
            all_bodies = model.Geometry.GetChildren(DataModelObjectCategory.Body, True)

            self.imported_bodies = list(all_bodies)
            self.log("Found {0} bodies in current model".format(len(self.imported_bodies)))

            if len(self.imported_bodies) == 0:
                MessageBox.Show(
                    "No geometry found in model!\n\n" +
                    "Please import STEP file first:\n" +
                    "File → Import → External Geometry File",
                    "No Geometry",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                )
                return

            # 면 법선 분석
            self.analyze_face_normals()

            self.log("\n=== Import and Analysis Complete ===")
            self.log("Total faces analyzed: {0}".format(len(self.face_data_list)))

            # 방향별 통계
            direction_counts = {}
            for face_data in self.face_data_list:
                direction = face_data['direction']
                direction_counts[direction] = direction_counts.get(direction, 0) + 1

            self.log("\nFaces by direction:")
            for direction in sorted(direction_counts.keys()):
                self.log("  {0}: {1} faces".format(direction, direction_counts[direction]))

            MessageBox.Show(
                "Import complete!\n\n" +
                "Bodies: {0}\n".format(len(self.imported_bodies)) +
                "Faces analyzed: {0}\n\n".format(len(self.face_data_list)) +
                "Click 'Create Named Selections' to create directional NS",
                "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            )

        except Exception as ex:
            self.log("\nERROR: {0}".format(str(ex)))
            MessageBox.Show(
                "Import failed:\n\n{0}".format(str(ex)),
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            )

    def on_create_ns_click(self, sender, e):
        """Named Selection 생성 버튼"""
        if not self.face_data_list:
            MessageBox.Show(
                "Please import and analyze STEP file first",
                "No Data",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            )
            return

        try:
            self.log("\n=== Creating Named Selections ===")
            self.create_directional_named_selections()
            self.log("\n=== Named Selections Created ===")

            # NS 상태 업데이트
            ns_list = ", ".join(sorted(self.ns_dict.keys()))
            self.ns_status_label.Content = "Created: {0}".format(ns_list)
            self.ns_status_label.Foreground = Brushes.Green

            MessageBox.Show(
                "Named Selections created:\n\n{0}".format(
                    "\n".join(["Contact_" + d for d in sorted(self.ns_dict.keys())])
                ),
                "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            )

        except Exception as ex:
            self.log("\nERROR: {0}".format(str(ex)))
            MessageBox.Show(
                "Failed to create Named Selections:\n\n{0}".format(str(ex)),
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            )

    def on_csv_browse_click(self, sender, e):
        """CSV 파일 선택"""
        dialog = OpenFileDialog()
        dialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*"
        dialog.Title = "Select Time-Force CSV File"

        if dialog.ShowDialog() == DialogResult.OK:
            self.csv_file_path = dialog.FileName
            self.csv_textbox.Text = self.csv_file_path
            self.log("CSV file selected: {0}".format(self.csv_file_path))

    def on_apply_forces_click(self, sender, e):
        """Apply Forces 버튼 클릭"""
        if not self.csv_file_path:
            MessageBox.Show(
                "Please select a CSV file first",
                "No CSV File",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            )
            return

        if not self.ns_dict:
            MessageBox.Show(
                "Please create Named Selections first",
                "No Named Selections",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            )
            return

        try:
            # Get current analysis
            self.current_analysis = ExtAPI.DataModel.Project.Model.Analyses[0]

            # Parse CSV and apply forces
            self.apply_directional_forces()

            MessageBox.Show(
                "Forces applied successfully!\n\n" +
                "Check the Model Tree for Force objects.",
                "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            )
        except Exception as ex:
            self.log("\nERROR applying forces: {0}".format(str(ex)))
            MessageBox.Show(
                "Failed to apply forces:\n\n{0}".format(str(ex)),
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            )

    def on_generate_mesh_click(self, sender, e):
        """Generate Mesh 버튼 클릭"""
        if not self.imported_bodies:
            MessageBox.Show(
                "Please import STEP file and analyze faces first",
                "No Bodies",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            )
            return

        try:
            # 텍스트박스에서 값 읽기
            self.element_size_mm = float(self.element_size_textbox.Text)
            self.growth_rate = float(self.growth_rate_textbox.Text)

            # Mesh 생성
            self.generate_mesh()

            MessageBox.Show(
                "Mesh generation completed!\n\n" +
                "Element Size: {0} mm\n".format(self.element_size_mm) +
                "Growth Rate: {0}\n\n".format(self.growth_rate) +
                "Check the Mesh object in Model Tree.",
                "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            )
        except ValueError:
            MessageBox.Show(
                "Invalid mesh settings.\n\nPlease enter valid numbers.",
                "Invalid Input",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            )
        except Exception as ex:
            self.log("\nERROR generating mesh: {0}".format(str(ex)))
            MessageBox.Show(
                "Failed to generate mesh:\n\n{0}".format(str(ex)),
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            )

    def on_create_analysis_click(self, sender, e):
        """Create Analysis 버튼 클릭"""
        try:
            # 텍스트박스에서 값 읽기
            self.num_modes = int(self.num_modes_textbox.Text)
            self.max_frequency_hz = float(self.max_freq_textbox.Text)
            self.end_time_s = float(self.end_time_textbox.Text)
            self.time_step_s = float(self.time_step_textbox.Text)

            # Analysis 생성
            self.create_modal_superposition_analysis()

            MessageBox.Show(
                "Modal Superposition Analysis created successfully!\n\n" +
                "Modal Analysis: {0} modes, max {1} Hz\n".format(self.num_modes, self.max_frequency_hz) +
                "Transient: {0} s duration, {1} s step\n\n".format(self.end_time_s, self.time_step_s) +
                "Check Model Tree → Analysis.\n\n" +
                "You can now Solve the analyses.",
                "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            )
        except ValueError:
            MessageBox.Show(
                "Invalid analysis settings.\n\nPlease enter valid numbers.",
                "Invalid Input",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            )
        except Exception as ex:
            self.log("\nERROR creating analysis: {0}".format(str(ex)))
            MessageBox.Show(
                "Failed to create analysis:\n\n{0}".format(str(ex)),
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            )

    def on_close_click(self, sender, e):
        """Close 버튼 클릭"""
        self.Close()

    # ================================================================
    # Core Logic - Phase 1
    # ================================================================

    def import_step_file(self):
        """
        STEP 파일 임포트
        GeometryImport API 사용
        """
        self.log("Importing STEP file...")

        model = ExtAPI.DataModel.Project.Model

        # 방법 1: GeometryImportGroup 사용 (ANSYS 2025 R2)
        try:
            self.log("Trying GeometryImportGroup.AddGeometryImport()...")

            # GeometryImportGroup에서 Import 추가
            geom_import_group = model.GeometryImportGroup
            geom_import = geom_import_group.AddGeometryImport()

            self.log("GeometryImport object created")

            # Import preferences 설정
            import_pref = Ansys.ACT.Mechanical.Utilities.GeometryImportPreference()
            import_pref.ProcessNamedSelections = True

            # 임포트 실행
            geom_import.Import(
                self.step_file_path,
                Ansys.ACT.Mechanical.Utilities.GeometryImportPreference.FormatType.Automatic,
                import_pref
            )

            self.log("Import executed via GeometryImportGroup")

        except AttributeError as e1:
            # 방법 2: 직접 Geometry Import 시도
            self.log("GeometryImportGroup not available, trying alternative method...")

            try:
                # Model에 직접 Import 메서드가 있는지 확인
                geometry = model.Geometry

                # 파일 경로만 전달하는 간단한 방식
                from Ansys.ACT.Mechanical.Utilities import GeometryImport

                GeometryImport.Import(self.step_file_path)

                self.log("Import executed via GeometryImport utility")

            except Exception as e2:
                self.log("ERROR: Both import methods failed")
                self.log("  Method 1 error: {0}".format(str(e1)))
                self.log("  Method 2 error: {0}".format(str(e2)))

                # 사용자에게 수동 임포트 안내
                raise Exception(
                    "Automatic STEP import failed.\n\n" +
                    "Please import the STEP file manually:\n" +
                    "1. File → Import → External Geometry File\n" +
                    "2. Select: {0}\n\n".format(self.step_file_path) +
                    "Then run this dialog again for face analysis."
                )

        # 임포트된 바디 가져오기
        all_bodies = model.Geometry.GetChildren(DataModelObjectCategory.Body, True)

        self.imported_bodies = list(all_bodies)
        self.log("Found {0} bodies in model".format(len(self.imported_bodies)))

    def analyze_face_normals(self):
        """
        각 바디의 면을 순회하며 법선 방향 분석
        """
        self.log("\nAnalyzing face normals...")
        self.face_data_list = []

        for body_idx, body in enumerate(self.imported_bodies):
            try:
                # GeoBody 가져오기
                geo_body = body.GetGeoBody()

                if geo_body is None:
                    self.log("  Body {0}: No geometry".format(body_idx))
                    continue

                # Faces 컬렉션 순회
                face_count = geo_body.Faces.Count
                self.log("  Body {0}: {1} faces".format(body_idx, face_count))

                for face_idx in range(face_count):
                    try:
                        face = geo_body.Faces[face_idx]

                        # 면 중심에서 법선 계산 (u=0.5, v=0.5는 파라미터 공간 중심)
                        normal_vec = face.GetFaceNormal(0.5, 0.5)

                        # 법선을 주요 축으로 분류
                        direction = classify_normal_direction(normal_vec)

                        # 데이터 저장
                        self.face_data_list.append({
                            'body': body,
                            'body_idx': body_idx,
                            'face': face,
                            'face_idx': face_idx,
                            'normal': (normal_vec.X, normal_vec.Y, normal_vec.Z),
                            'direction': direction
                        })

                    except Exception as face_ex:
                        self.log("    Face {0} error: {1}".format(face_idx, str(face_ex)))
                        continue

            except Exception as body_ex:
                self.log("  Body {0} error: {1}".format(body_idx, str(body_ex)))
                continue

        self.log("Face analysis complete: {0} faces".format(len(self.face_data_list)))

    def create_directional_named_selections(self):
        """
        방향별로 Named Selection 생성
        Contact_+Z, Contact_-Z, etc.
        """
        model = ExtAPI.DataModel.Project.Model

        # 방향별 그룹화
        by_direction = {}
        for face_data in self.face_data_list:
            direction = face_data['direction']
            if direction not in by_direction:
                by_direction[direction] = []
            by_direction[direction].append(face_data)

        self.log("Creating Named Selections for {0} directions...".format(len(by_direction)))

        # 각 방향별 NS 생성
        self.ns_dict = {}

        for direction, face_data_group in by_direction.items():
            ns_name = "Contact_" + direction
            self.log("  Creating NS: {0} ({1} faces)".format(ns_name, len(face_data_group)))

            try:
                # Named Selection 생성
                ns = model.AddNamedSelection()
                ns.Name = ns_name
                ns.ScopingMethod = GeometryDefineByType.Worksheet

                # 면 선택 - SelectionManager 사용
                selection = ExtAPI.SelectionManager.CreateSelectionInfo(
                    Ansys.ACT.Interfaces.Common.SelectionTypeEnum.GeometryEntities
                )

                for face_data in face_data_group:
                    # IGeoFace를 선택에 추가
                    # Note: face_data['face']는 IGeoFace 객체
                    selection.Entities.Add(face_data['face'])

                ns.Location = selection

                self.ns_dict[direction] = ns
                self.log("    Created: {0}".format(ns_name))

            except Exception as ns_ex:
                self.log("    ERROR creating {0}: {1}".format(ns_name, str(ns_ex)))
                raise

        self.log("Named Selections created: {0}".format(len(self.ns_dict)))

    def parse_csv_force_data(self):
        """
        CSV 파일에서 시간-하중 데이터 읽기
        형식: Time(s), Force(N)

        Returns
        -------
        (time_vals, force_vals) : (list of float, list of float)
        """
        import csv

        time_vals = []
        force_vals = []

        self.log("\nParsing CSV file: {0}".format(self.csv_file_path))

        with open(self.csv_file_path, 'r') as f:
            reader = csv.reader(f)

            # 헤더 스킵
            header = next(reader)
            self.log("CSV Header: {0}".format(header))

            # 데이터 읽기
            for row_idx, row in enumerate(reader):
                try:
                    time_vals.append(float(row[0]))
                    force_vals.append(float(row[1]))
                except (ValueError, IndexError) as ex:
                    self.log("  Warning: Skipping row {0}: {1}".format(row_idx + 2, row))
                    continue

        self.log("CSV data parsed: {0} time points".format(len(time_vals)))
        self.log("Time range: {0} to {1} s".format(min(time_vals), max(time_vals)))
        self.log("Force range: {0} to {1} N".format(min(force_vals), max(force_vals)))

        return time_vals, force_vals

    def apply_directional_forces(self):
        """
        방향별 Named Selection에 Time Force 적용
        +Z는 양수, -Z는 음수 (반대 방향)
        X, Y도 동일
        """
        # CSV 파싱
        time_vals, force_vals = self.parse_csv_force_data()

        if not time_vals:
            raise Exception("No valid data in CSV file")

        # Quantity 타입 필요 (ANSYS API)
        from Ansys.Core.Units import Quantity

        self.log("\nApplying forces to Named Selections...")

        # 방향별 Force 적용
        directions_map = {
            '+X': ('X', 1.0),
            '-X': ('X', -1.0),
            '+Y': ('Y', 1.0),
            '-Y': ('Y', -1.0),
            '+Z': ('Z', 1.0),
            '-Z': ('Z', -1.0)
        }

        force_count = 0

        for direction, ns in self.ns_dict.items():
            if direction not in directions_map:
                self.log("  Warning: Unknown direction {0}, skipping".format(direction))
                continue

            axis, sign = directions_map[direction]

            self.log("  Creating Force for Contact_{0} (axis={1}, sign={2})".format(
                direction, axis, sign))

            try:
                # Force 객체 생성
                force = self.current_analysis.AddForce()
                force.Name = "Force_" + direction
                force.Location = ns

                # Components 모드로 설정
                force.DefineBy = Ansys.ACT.Automation.Mechanical.LoadDefineBy.Components

                # 시간 변화 하중 적용 (Tabular Data)
                # 부호 적용 (반대 방향은 음수)
                discrete_vals = [Quantity(f * sign, "N") for f in force_vals]

                # 축별 적용
                if axis == 'X':
                    force.XComponent.Output.DiscreteValues = discrete_vals
                elif axis == 'Y':
                    force.YComponent.Output.DiscreteValues = discrete_vals
                elif axis == 'Z':
                    force.ZComponent.Output.DiscreteValues = discrete_vals

                self.log("    Applied {0} time points to {1} axis".format(
                    len(discrete_vals), axis))

                force_count += 1

            except Exception as force_ex:
                self.log("    ERROR: {0}".format(str(force_ex)))
                raise

        self.log("\nForce application complete: {0} forces created".format(force_count))

    def generate_mesh(self):
        """
        격자(Mesh) 생성
        임포트된 바디에 대해 mesh 설정 및 생성
        """
        model = ExtAPI.DataModel.Project.Model
        mesh = model.Mesh

        from Ansys.Core.Units import Quantity

        self.log("\nGenerating mesh...")
        self.log("Element Size: {0} mm".format(self.element_size_mm))
        self.log("Growth Rate: {0}".format(self.growth_rate))

        # Global Mesh Settings
        mesh.ElementSize = Quantity(self.element_size_mm, "mm")

        # Advanced settings
        try:
            # Growth Rate 설정 (MeshControlsOptions에 있을 수 있음)
            if hasattr(mesh, 'GrowthRate'):
                mesh.GrowthRate = self.growth_rate
                self.log("Growth rate applied: {0}".format(self.growth_rate))

            # Curvature & Proximity 기반 세분화
            if hasattr(mesh, 'UseAdaptiveSizing'):
                mesh.UseAdaptiveSizing = True
                self.log("Adaptive sizing enabled")

        except Exception as setting_ex:
            self.log("  Note: Some advanced settings not available: {0}".format(str(setting_ex)))

        # 임포트된 바디에 대해 Body Sizing 추가 (선택적)
        body_sizing_count = 0
        try:
            for body in self.imported_bodies:
                # Body Sizing 추가
                body_sizing = mesh.AddSizing()
                body_sizing.Location = body
                body_sizing.ElementSize = Quantity(self.element_size_mm, "mm")
                body_sizing_count += 1

            self.log("Body sizing controls added: {0}".format(body_sizing_count))

        except Exception as sizing_ex:
            self.log("  Note: Body sizing not applied: {0}".format(str(sizing_ex)))

        # Mesh 생성 실행
        self.log("\nGenerating mesh (this may take a while)...")
        mesh.GenerateMesh()

        # 결과 확인
        try:
            node_count = mesh.Nodes if hasattr(mesh, 'Nodes') else "N/A"
            element_count = mesh.Elements if hasattr(mesh, 'Elements') else "N/A"

            self.log("Mesh generation complete!")
            self.log("  Nodes: {0}".format(node_count))
            self.log("  Elements: {0}".format(element_count))

        except Exception as stat_ex:
            self.log("Mesh generated (statistics not available)")

    def create_modal_superposition_analysis(self):
        """
        Modal Analysis + Transient Structural (Mode Superposition) 생성
        Phase 5 구현
        """
        model = ExtAPI.DataModel.Project.Model
        from Ansys.Core.Units import Quantity

        self.log("\n" + "=" * 60)
        self.log("Creating Modal Superposition Analysis System")
        self.log("=" * 60)

        # 1. Modal Analysis 생성
        self.log("\n[Step 1] Creating Modal Analysis...")

        try:
            # Analysis 추가
            modal = model.Analyses.AddModalAnalysis()
            modal.Name = "Modal"

            self.log("  Modal Analysis created")

            # Modal settings
            modal_settings = modal.AnalysisSettings
            modal_settings.MaximumModesToFind = self.num_modes
            modal_settings.RangeMaximum = Quantity(self.max_frequency_hz, "Hz")

            self.log("  Number of modes: {0}".format(self.num_modes))
            self.log("  Max frequency: {0} Hz".format(self.max_frequency_hz))

        except Exception as modal_ex:
            self.log("  ERROR creating Modal Analysis: {0}".format(str(modal_ex)))
            raise

        # 2. Transient Structural Analysis 생성
        self.log("\n[Step 2] Creating Transient Structural Analysis...")

        try:
            transient = model.Analyses.AddTransientStructuralAnalysis()
            transient.Name = "Transient_ModalSuperposition"

            self.log("  Transient Analysis created")

            # Transient settings
            transient_settings = transient.AnalysisSettings

            # Time settings
            transient_settings.StepEndTime = Quantity(self.end_time_s, "sec")
            transient_settings.InitialTimeStep = Quantity(self.time_step_s, "sec")
            transient_settings.MinimumTimeStep = Quantity(self.time_step_s, "sec")
            transient_settings.MaximumTimeStep = Quantity(self.time_step_s, "sec")

            self.log("  End time: {0} s".format(self.end_time_s))
            self.log("  Time step: {0} s".format(self.time_step_s))

            # Mode Superposition Method 활성화
            try:
                if hasattr(transient_settings, 'SolutionMethod'):
                    # Mode Superposition 설정
                    transient_settings.SolutionMethod = Ansys.ACT.Automation.Mechanical.TransientSolutionMethod.ModeSuperposition
                    self.log("  Solution Method: Mode Superposition")
                else:
                    self.log("  Warning: SolutionMethod property not available")
                    self.log("  Please manually set Analysis Settings → Solution Method → Mode Superposition")

            except Exception as method_ex:
                self.log("  Warning: Could not set Mode Superposition: {0}".format(str(method_ex)))
                self.log("  Please manually set: Analysis Settings → Solution Method → Mode Superposition")

        except Exception as transient_ex:
            self.log("  ERROR creating Transient Analysis: {0}".format(str(transient_ex)))
            raise

        # 3. Fixed Support 자동 추가 (최소 1개 필요)
        self.log("\n[Step 3] Checking boundary conditions...")

        try:
            # Modal에 Fixed Support가 있는지 확인
            if modal.Children.Count == 0:
                self.log("  Note: No boundary conditions found")
                self.log("  You need to add Fixed Support manually for the analysis to run")
            else:
                self.log("  Boundary conditions found: {0}".format(modal.Children.Count))

        except Exception as bc_ex:
            self.log("  Warning: {0}".format(str(bc_ex)))

        # 4. 결과 요약
        self.log("\n" + "=" * 60)
        self.log("Modal Superposition Analysis Setup Complete!")
        self.log("=" * 60)
        self.log("\nSummary:")
        self.log("  Modal Analysis: '{0}'".format(modal.Name))
        self.log("    - Modes: {0}".format(self.num_modes))
        self.log("    - Max Freq: {0} Hz".format(self.max_frequency_hz))
        self.log("\n  Transient Analysis: '{0}'".format(transient.Name))
        self.log("    - Duration: {0} s".format(self.end_time_s))
        self.log("    - Time Step: {0} s".format(self.time_step_s))
        self.log("    - Method: Mode Superposition")
        self.log("\nNext Steps:")
        self.log("  1. Add Fixed Support to Modal Analysis (if not present)")
        self.log("  2. Verify Solution Method = Mode Superposition in Transient Settings")
        self.log("  3. Solve Modal Analysis first")
        self.log("  4. Solve Transient Analysis")

        # Current analysis 업데이트 (Force 적용 시 사용)
        self.current_analysis = transient

    # ================================================================
    # Utility
    # ================================================================

    def log(self, message):
        """로그 출력"""
        current = self.log_textbox.Text
        self.log_textbox.Text = current + message + "\n"
        self.log_textbox.ScrollToEnd()


# ================================================================
# ACT Callbacks
# ================================================================

def show_cap_vibration_dialog(analysis):
    """
    Cap Vibration 대화상자 표시 (ACT 콜백)

    Parameters
    ----------
    analysis : Analysis
        현재 활성 분석 객체 (Mechanical이 전달)
    """
    try:
        dialog = CapVibrationDialog()
        dialog.ShowDialog()
    except Exception as ex:
        MessageBox.Show(
            "Failed to open Cap Vibration dialog:\n\n{0}".format(str(ex)),
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        )


# ================================================================
# Extension Initialization
# ================================================================

def Initialize():
    """
    ACT Extension 초기화 함수 (선택적)
    Mechanical이 확장을 로드할 때 자동 호출
    """
    pass


def Finalize():
    """
    ACT Extension 종료 함수 (선택적)
    Mechanical이 확장을 언로드할 때 자동 호출
    """
    pass
