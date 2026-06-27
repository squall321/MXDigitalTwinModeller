using System;
using System.Threading;

#if V251
using SpaceClaim.Api.V251;
#elif V252
using SpaceClaim.Api.V252;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Mcp
{
    /// <summary>
    /// Marshals work from a background thread (the HttpListener callback thread) onto the
    /// SpaceClaim API/main thread, where geometry mutation is legal (GATE-0, MCP_SERVER_PLAN).
    ///
    /// Uses the documented primitive Application.ExecuteOnMainThread(Task) (xml:5501) — the
    /// host-agnostic replacement for WinForms BeginInvoke — plus a ManualResetEventSlim to make
    /// the fire-and-forget/void call synchronous. The caller (a worker thread) blocks on
    /// done.Wait(timeout); the SC main thread drains its message pump and runs the work. This
    /// is SAFE only because the caller is NOT the main thread (a worker), so blocking it does
    /// not stall the pump — the GATE-0 probe's deadlock came precisely from calling this on the
    /// /RunScript main thread, which the real HttpListener callback never is.
    ///
    /// The supplied work runs OUTSIDE any WriteBlock; read-only tools call it directly, while
    /// mutating tools wrap their body in WriteBlock.ExecuteTask inside the work delegate.
    /// </summary>
    public static class ApiThreadMarshaller
    {
        /// <summary>
        /// Run <paramref name="work"/> on the SC main thread and return its string result.
        /// Throws TimeoutException if the pump does not run the task within timeoutMs
        /// (e.g. a modal dialog is blocking the pump — GATE-3 hazard). Any exception thrown
        /// inside <paramref name="work"/> is captured and rethrown on the calling thread.
        /// </summary>
        public static string Run(Func<string> work, int timeoutMs = 120000)
        {
            if (work == null) throw new ArgumentNullException("work");

            string result = null;
            Exception captured = null;
            var done = new ManualResetEventSlim(false);

            Application.ExecuteOnMainThread(new Task(delegate
            {
                try { result = work(); }
                catch (Exception ex) { captured = ex; }
                finally { done.Set(); }
            }));

            if (!done.Wait(timeoutMs))
                throw new TimeoutException(
                    "API thread did not run the marshalled task within " + timeoutMs +
                    "ms (main thread may be blocked by a modal dialog or long operation).");

            if (captured != null)
                throw new Exception("marshalled work threw: " + captured.Message, captured);

            return result;
        }
    }
}
