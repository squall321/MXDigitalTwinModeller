# encoding: utf-8
"""
최소 버전 - 에러 확인용
"""

import sys
import clr

# WPF 참조
try:
    clr.AddReference("PresentationFramework")
    clr.AddReference("PresentationCore")
    clr.AddReference("WindowsBase")
    print("WPF references loaded OK")
except Exception as e:
    print("ERROR loading WPF: {}".format(e))

try:
    from System.Windows import Window, MessageBox, MessageBoxButton, MessageBoxImage
    from System.Windows.Controls import Label, StackPanel
    from System.Windows import Thickness
    print("WPF imports OK")
except Exception as e:
    print("ERROR importing WPF: {}".format(e))

# ANSYS API 확인
try:
    # ExtAPI는 글로벌 변수로 제공됨
    print("ExtAPI available: {}".format(ExtAPI is not None))
except NameError:
    print("ERROR: ExtAPI not available (not running in Mechanical?)")

class MinimalDialog(Window):
    """최소 대화창"""
    def __init__(self):
        self.Title = "Minimal Test Dialog"
        self.Width = 400
        self.Height = 200

        panel = StackPanel()
        panel.Margin = Thickness(10)

        label = Label()
        label.Content = "If you see this, WPF works!\n\nExtAPI available: {}".format(
            ExtAPI is not None if 'ExtAPI' in dir() else False)
        label.FontSize = 14
        panel.Children.Add(label)

        self.Content = panel

def show_minimal_dialog(analysis):
    """최소 대화창 표시"""
    try:
        dialog = MinimalDialog()
        dialog.ShowDialog()
    except Exception as ex:
        # WPF MessageBox도 안 되면 print
        try:
            MessageBox.Show(
                "Dialog error: {}".format(str(ex)),
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            )
        except:
            print("CRITICAL ERROR: {}".format(str(ex)))
            import traceback
            traceback.print_exc()

# Direct test
if __name__ == "__main__":
    show_minimal_dialog(None)
