# encoding: utf-8
"""
MX Digital Twin Simulator - ANSYS Mechanical ACT Extension
Cap Vibration 하중 정의 및 시뮬레이션 설정
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

from System.Windows import Window, Application
from System.Windows.Controls import (
    StackPanel, Button, Label, TextBox,
    GroupBox, Grid, RowDefinition, ColumnDefinition,
    Orientation
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
from Ansys.ACT.Mechanical.Fields import LoadDefineBy
from Ansys import Quantity


# ================================================================
# Cap Vibration Dialog
# ================================================================

class CapVibrationDialog(Window):
    """
    Cap Vibration 하중 대화상자 (WPF)
    진동 조건 입력 및 Named Selection 연결
    """

    def __init__(self):
        # Window 속성
        self.Title = "Cap Vibration Load Definition"
        self.Width = 500
        self.Height = 400
        self.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen

        # 메인 레이아웃
        main_panel = StackPanel()
        main_panel.Margin = Thickness(10)

        # 헤더
        header = Label()
        header.Content = "Cap Vibration Load Configuration"
        header.FontSize = 16
        header.FontWeight = System.Windows.FontWeights.Bold
        header.Margin = Thickness(0, 0, 0, 10)
        main_panel.Children.Add(header)

        # Vibration Settings Group
        vib_group = self._create_vibration_group()
        main_panel.Children.Add(vib_group)

        # Named Selection Group
        ns_group = self._create_named_selection_group()
        main_panel.Children.Add(ns_group)

        # 버튼 패널
        btn_panel = self._create_button_panel()
        main_panel.Children.Add(btn_panel)

        # Status Label
        self.status_label = Label()
        self.status_label.Content = "Ready"
        self.status_label.Foreground = Brushes.Gray
        self.status_label.Margin = Thickness(0, 10, 0, 0)
        main_panel.Children.Add(self.status_label)

        self.Content = main_panel

    def _create_vibration_group(self):
        """진동 설정 그룹"""
        group = GroupBox()
        group.Header = "Vibration Parameters"
        group.Margin = Thickness(0, 0, 0, 10)

        panel = StackPanel()
        panel.Margin = Thickness(5)

        # Frequency
        freq_panel = StackPanel()
        freq_panel.Orientation = Orientation.Horizontal
        freq_panel.Margin = Thickness(0, 5, 0, 5)

        freq_label = Label()
        freq_label.Content = "Frequency (Hz):"
        freq_label.Width = 120
        freq_panel.Children.Add(freq_label)

        self.freq_textbox = TextBox()
        self.freq_textbox.Width = 150
        self.freq_textbox.Text = "100.0"
        freq_panel.Children.Add(self.freq_textbox)

        panel.Children.Add(freq_panel)

        # Amplitude
        amp_panel = StackPanel()
        amp_panel.Orientation = Orientation.Horizontal
        amp_panel.Margin = Thickness(0, 5, 0, 5)

        amp_label = Label()
        amp_label.Content = "Amplitude (mm):"
        amp_label.Width = 120
        amp_panel.Children.Add(amp_label)

        self.amp_textbox = TextBox()
        self.amp_textbox.Width = 150
        self.amp_textbox.Text = "0.5"
        amp_panel.Children.Add(self.amp_textbox)

        panel.Children.Add(amp_panel)

        # Duration
        dur_panel = StackPanel()
        dur_panel.Orientation = Orientation.Horizontal
        dur_panel.Margin = Thickness(0, 5, 0, 5)

        dur_label = Label()
        dur_label.Content = "Duration (s):"
        dur_label.Width = 120
        dur_panel.Children.Add(dur_label)

        self.dur_textbox = TextBox()
        self.dur_textbox.Width = 150
        self.dur_textbox.Text = "1.0"
        dur_panel.Children.Add(self.dur_textbox)

        panel.Children.Add(dur_panel)

        group.Content = panel
        return group

    def _create_named_selection_group(self):
        """Named Selection 설정 그룹"""
        group = GroupBox()
        group.Header = "Named Selection"
        group.Margin = Thickness(0, 0, 0, 10)

        panel = StackPanel()
        panel.Margin = Thickness(5)

        ns_panel = StackPanel()
        ns_panel.Orientation = Orientation.Horizontal
        ns_panel.Margin = Thickness(0, 5, 0, 5)

        ns_label = Label()
        ns_label.Content = "Target NS:"
        ns_label.Width = 120
        ns_panel.Children.Add(ns_label)

        self.ns_textbox = TextBox()
        self.ns_textbox.Width = 200
        self.ns_textbox.Text = "Cap_Surface"
        ns_panel.Children.Add(self.ns_textbox)

        panel.Children.Add(ns_panel)

        info_label = Label()
        info_label.Content = "Specify the Named Selection to apply vibration load"
        info_label.FontSize = 10
        info_label.Foreground = Brushes.Gray
        panel.Children.Add(info_label)

        group.Content = panel
        return group

    def _create_button_panel(self):
        """버튼 패널"""
        panel = StackPanel()
        panel.Orientation = Orientation.Horizontal
        panel.HorizontalAlignment = HorizontalAlignment.Right
        panel.Margin = Thickness(0, 10, 0, 0)

        # Apply 버튼
        apply_btn = Button()
        apply_btn.Content = "Apply"
        apply_btn.Width = 80
        apply_btn.Height = 28
        apply_btn.Margin = Thickness(0, 0, 10, 0)
        apply_btn.Click += self.on_apply_click
        panel.Children.Add(apply_btn)

        # Close 버튼
        close_btn = Button()
        close_btn.Content = "Close"
        close_btn.Width = 80
        close_btn.Height = 28
        close_btn.Click += self.on_close_click
        panel.Children.Add(close_btn)

        return panel

    def on_apply_click(self, sender, e):
        """Apply 버튼 클릭"""
        try:
            # 입력값 가져오기
            freq = float(self.freq_textbox.Text)
            amp = float(self.amp_textbox.Text)
            dur = float(self.dur_textbox.Text)
            ns_name = self.ns_textbox.Text

            # 유효성 검증
            if freq <= 0 or amp <= 0 or dur <= 0:
                raise ValueError("All values must be positive")

            if not ns_name:
                raise ValueError("Named Selection name is required")

            # Mechanical API를 통해 실제 하중 적용
            try:
                model = ExtAPI.DataModel.Project.Model
                analysis = model.Analyses[0]  # 첫 번째 분석

                # Displacement 하중 추가 (Harmonic)
                displacement = analysis.AddDisplacement()
                displacement.DefineBy = LoadDefineBy.Components

                # Y 방향 진동 (mm 단위)
                displacement.YComponent.Output.DiscreteValues = [
                    Quantity(0, "mm"),
                    Quantity(amp, "mm")
                ]

                # Named Selection 연결
                ns = model.NamedSelections.Children.GetByName(ns_name)
                if ns:
                    displacement.Location = ns
                else:
                    raise ValueError("Named Selection '{0}' not found".format(ns_name))

                # 주파수 설정 (분석 설정에 따라 다름)
                # analysis.FrequencyRange = freq

                self.status_label.Content = "Load applied: {0} @ {1} Hz".format(ns_name, freq)
                self.status_label.Foreground = Brushes.Green

            except Exception as api_ex:
                raise Exception("Mechanical API error: {0}".format(str(api_ex)))

            MessageBox.Show(
                "Cap Vibration load applied successfully!\n\n" +
                "Frequency: {0} Hz\n".format(freq) +
                "Amplitude: {0} mm\n".format(amp) +
                "Duration: {0} s\n".format(dur) +
                "Target: {0}".format(ns_name),
                "Cap Vibration",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            )

        except ValueError as ex:
            self.status_label.Content = "Input error: {0}".format(str(ex))
            self.status_label.Foreground = Brushes.Red

            MessageBox.Show(
                str(ex),
                "Input Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            )
        except Exception as ex:
            self.status_label.Content = "Error: {0}".format(str(ex))
            self.status_label.Foreground = Brushes.Red

            MessageBox.Show(
                "An error occurred:\n\n{0}".format(str(ex)),
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            )

    def on_close_click(self, sender, e):
        """Close 버튼 클릭"""
        self.Close()


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
