# encoding: utf-8
"""
MX Digital Twin Simulator - ANSYS Mechanical ACT Extension
Cap Vibration Time Force Setup - Phase 1: STEP Import + Face Analysis
"""

import sys
import os
import clr

# Shared DLL 로드 (공용 로직)
dll_path = os.path.join(os.path.dirname(__file__), "bin", "MXDigitalTwinModeller.Core.dll")
if os.path.exists(dll_path):
    clr.AddReferenceToFileAndPath(dll_path)
    from MXDigitalTwinModeller.Core.Geometry import GeometryUtils
    from MXDigitalTwinModeller.Core.Spatial import SpatialIndex, BodyBounds

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
    StackPanel, Button, Label, TextBox,
    GroupBox, Orientation, ScrollViewer, ScrollBarVisibility
)
from System.Windows import (
    HorizontalAlignment, VerticalAlignment,
    Thickness, MessageBox, MessageBoxButton, MessageBoxImage
)
from System.Windows.Media import Brushes

# ANSYS Mechanical API
import Ansys
from Ansys.ACT.Interfaces.Mechanical import IMechanicalExtAPI
from Ansys.ACT.Automation.Mechanical import Model

try:
    from Ansys.Mechanical.DataModel.Enums import (
        DataModelObjectCategory, GeometryDefineByType
    )
except ImportError:
    # IronPython 런타임에서 경로가 다를 수 있음
    pass

# Note: ExtAPI는 ANSYS Mechanical 런타임에서 글로벌 변수로 자동 제공됨
# ExtAPI.DataModel, ExtAPI.SelectionManager 등 사용 가능


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
        self.Title = "Cap Vibration Time Force Setup - Phase 1"
        self.Width = 700
        self.Height = 600
        self.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen

        # 데이터 저장
        self.step_file_path = ""
        self.imported_bodies = []
        self.face_data_list = []
        self.ns_dict = {}

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

        # Phase 1 Info
        info_label = Label()
        info_label.Content = "Phase 1: STEP Import + Face Analysis + Named Selection"
        info_label.FontSize = 11
        info_label.Foreground = Brushes.DarkBlue
        info_label.Margin = Thickness(0, 0, 0, 10)
        main_panel.Children.Add(info_label)

        # STEP Files Group
        step_group = self._create_step_group()
        main_panel.Children.Add(step_group)

        # Named Selection Group
        ns_group = self._create_ns_group()
        main_panel.Children.Add(ns_group)

        # Log Group
        log_group = self._create_log_group()
        main_panel.Children.Add(log_group)

        # 버튼 패널
        btn_panel = self._create_button_panel()
        main_panel.Children.Add(btn_panel)

        self.Content = main_panel

    def _create_step_group(self):
        """STEP 파일 그룹"""
        group = GroupBox()
        group.Header = "STEP File Import"
        group.Margin = Thickness(0, 0, 0, 10)

        panel = StackPanel()
        panel.Margin = Thickness(5)

        # File path
        file_panel = StackPanel()
        file_panel.Orientation = Orientation.Horizontal
        file_panel.Margin = Thickness(0, 5, 0, 5)

        file_label = Label()
        file_label.Content = "STEP File:"
        file_label.Width = 80
        file_panel.Children.Add(file_label)

        self.file_textbox = TextBox()
        self.file_textbox.Width = 400
        self.file_textbox.IsReadOnly = True
        file_panel.Children.Add(self.file_textbox)

        browse_btn = Button()
        browse_btn.Content = "Browse..."
        browse_btn.Width = 80
        browse_btn.Margin = Thickness(5, 0, 0, 0)
        browse_btn.Click += self.on_browse_click
        file_panel.Children.Add(browse_btn)

        panel.Children.Add(file_panel)

        # Import button
        import_btn = Button()
        import_btn.Content = "Import and Analyze Faces"
        import_btn.Width = 180
        import_btn.Height = 28
        import_btn.Margin = Thickness(0, 10, 0, 0)
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
        """Import 버튼 클릭 - STEP 임포트 + 면 분석"""
        if not self.step_file_path:
            MessageBox.Show(
                "Please select a STEP file first",
                "No File Selected",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            )
            return

        try:
            self.log("\n=== Starting STEP Import ===")

            # Phase 1-1: STEP 임포트
            self.import_step_file()

            # Phase 1-2: 면 법선 분석
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
        geometry = model.Geometry

        # GeometryImport 추가
        geom_import = geometry.AddGeometryImport()

        # Import preferences 설정
        import_pref = Ansys.ACT.Mechanical.Utilities.GeometryImportPreference()
        import_pref.ProcessNamedSelections = True

        # 임포트 실행
        geom_import.Import(
            self.step_file_path,
            Ansys.ACT.Mechanical.Utilities.GeometryImportPreference.FormatType.Automatic,
            import_pref
        )

        self.log("Import command executed")

        # 임포트된 바디 가져오기 (새로 추가된 바디들)
        # Note: IsImported 속성이 없을 수 있으므로, 모든 바디를 가져옴
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
