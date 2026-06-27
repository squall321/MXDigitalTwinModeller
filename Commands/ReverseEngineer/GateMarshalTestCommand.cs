using System;
using System.Drawing;
using System.Threading;
using System.Text;
using System.IO;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Commands;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.UI;

#if V251
using SpaceClaim.Api.V251;
using SpaceClaim.Api.V251.Extensibility;
using SpaceClaim.Api.V251.Modeler;
using SpaceClaim.Api.V251.Geometry;
#elif V252
using SpaceClaim.Api.V252;
using SpaceClaim.Api.V252.Extensibility;
using SpaceClaim.Api.V252.Modeler;
using SpaceClaim.Api.V252.Geometry;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Commands.ReverseEngineer
{
    /// <summary>
    /// GATE-0 for the MCP-SpaceClaim architecture (MCP_SERVER_PLAN.md). Throwaway probe.
    /// Proves the thread-marshalling spine of Approach C BEFORE any HTTP code is written:
    /// a BACKGROUND thread schedules a WriteBlock mutation onto the SC API thread via the
    /// documented primitive Application.ExecuteOnMainThread(Task) (xml:5501), and we assert
    /// the body actually ran on the API thread inside a valid WriteBlock, committed a body,
    /// and propagated an exception across the thread boundary.
    /// Results are written to a JSON-ish text file so the gate can be判정 headless.
    /// If assertion (b) fails (ran on bg thread) the entire Approach-C spine is invalid.
    /// </summary>
    public class GateMarshalTestCommand : BaseCommandCapsule
    {
        public const string CommandName = "MXDigitalTwinModeller.GateMarshalTest";
        private static readonly string ResultPath =
            @"D:\MXDigitalTwinModeller\Test\RE_SelfTest\gate0_marshal_result.txt";

        public GateMarshalTestCommand()
            : base(CommandName, "GATE-0 Marshal Test", IconHelper.MeshIcon,
                   "MCP 스레드 마샬링 GATE-0: bg thread -> ExecuteOnMainThread -> WriteBlock")
        {
        }

        protected override void OnUpdate(Command command)
        {
            command.IsEnabled = IsWindowActive();
        }

        protected override void OnExecute(Command command, ExecutionContext context, Rectangle buttonRect)
        {
            // Recorded on the API thread (this OnExecute runs on it).
            int apiThreadId = Thread.CurrentThread.ManagedThreadId;
            var sb = new StringBuilder();
            sb.AppendLine("# GATE-0 marshal test");
            sb.AppendLine("apiThreadId=" + apiThreadId);

            // ---- Part 1: normal mutation marshalled from a background thread ----
            RunOne(apiThreadId, sb, throwInside: false, label: "create");
            // ---- Part 2: exception propagation across the thread boundary ----
            RunOne(apiThreadId, sb, throwInside: true, label: "throw");

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
                File.WriteAllText(ResultPath, sb.ToString(), new UTF8Encoding(false));
            }
            catch { /* best effort */ }

            try { ValidationHelper.ShowError("GATE-0 done. See gate0_marshal_result.txt", "GATE-0"); }
            catch { }
        }

        private void RunOne(int apiThreadId, StringBuilder sb, bool throwInside, string label)
        {
            var done = new ManualResetEventSlim(false);
            int ranOnThreadId = -1;
            bool insideWriteBlock = false;
            bool bodyCreated = false;
            Exception err = null;

            // Explicitly NOT the API thread — mimics the future HttpListener callback thread.
            var bg = new Thread(() =>
            {
                Application.ExecuteOnMainThread(new Task(delegate
                {
                    try
                    {
                        WriteBlock.ExecuteTask("GATE marshal " + label, new Task(delegate
                        {
                            ranOnThreadId = Thread.CurrentThread.ManagedThreadId;
                            insideWriteBlock = WriteBlock.IsActive;
                            if (throwInside)
                                throw new InvalidOperationException("GATE intentional throw");
                            // Real mutation: a small cylinder via the project-verified path.
                            var win = Window.ActiveWindow;
                            if (win != null && win.Document != null)
                            {
                                Part main = win.Document.MainPart;
                                var plane = Plane.PlaneXY;
                                Profile profile = new CircleProfile(plane, 0.005); // 5mm r
                                Body b = Body.ExtrudeProfile(profile, 0.005);
                                DesignBody.Create(main, "GateBox_" + label, b);
                                bodyCreated = true;
                            }
                        }));
                    }
                    catch (Exception e) { err = e; }
                    finally { done.Set(); }
                }));
            });
            bg.IsBackground = true;
            bg.Start();

            bool completed = done.Wait(TimeSpan.FromSeconds(30));

            sb.AppendLine(string.Format(
                "[{0}] completed={1} ranOnThreadId={2} onApiThread={3} insideWriteBlock={4} bodyCreated={5} err={6}",
                label, completed, ranOnThreadId,
                (ranOnThreadId == apiThreadId), insideWriteBlock, bodyCreated,
                err == null ? "none" : err.GetType().Name + ":" + err.Message));
        }
    }
}
