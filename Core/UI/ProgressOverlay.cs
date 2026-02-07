using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI
{
    /// <summary>
    /// 별도 STA 스레드에서 "처리 중" 프로그래스 폼을 표시.
    /// WriteBlock.ExecuteTask 등 UI 스레드를 차단하는 작업 중에도
    /// 프로그래스 바 애니메이션이 멈추지 않음.
    /// </summary>
    public static class ProgressOverlay
    {
        /// <summary>
        /// "처리 중" 폼을 표시하면서 work를 현재 스레드에서 실행.
        /// work 완료 후 폼 자동 닫힘.
        /// </summary>
        public static void RunWithProgress(string message, Action work)
        {
            Form progressForm = null;
            Thread progressThread = null;
            var readyEvent = new ManualResetEventSlim(false);

            try
            {
                // 1) 별도 STA 스레드에서 프로그래스 폼 표시
                progressThread = new Thread(() =>
                {
                    progressForm = CreateProgressForm(message);
                    progressForm.Shown += (s, e) => readyEvent.Set();
                    System.Windows.Forms.Application.Run(progressForm);
                });
                progressThread.SetApartmentState(ApartmentState.STA);
                progressThread.IsBackground = true;
                progressThread.Start();

                // 폼이 표시될 때까지 대기 (최대 3초)
                readyEvent.Wait(3000);

                // 2) 현재 스레드에서 작업 실행
                work();
            }
            finally
            {
                // 3) 프로그래스 폼 닫기
                CloseForm(progressForm);
                readyEvent.Dispose();
            }
        }

        private static void CloseForm(Form form)
        {
            try
            {
                if (form != null && !form.IsDisposed && form.IsHandleCreated)
                {
                    form.Invoke(new Action(() =>
                    {
                        form.Close();
                        System.Windows.Forms.Application.ExitThread();
                    }));
                }
            }
            catch { }
        }

        private static Form CreateProgressForm(string message)
        {
            var form = new Form
            {
                Width = 360,
                Height = 110,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                ControlBox = false,
                MaximizeBox = false,
                MinimizeBox = false,
                TopMost = true,
                ShowInTaskbar = false,
                Text = "MX Digital Twin Modeller"
            };

            var label = new Label
            {
                Text = message,
                Location = new Point(20, 16),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f)
            };

            var progressBar = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Location = new Point(20, 48),
                Width = 310,
                Height = 22
            };

            form.Controls.Add(label);
            form.Controls.Add(progressBar);

            return form;
        }
    }
}
