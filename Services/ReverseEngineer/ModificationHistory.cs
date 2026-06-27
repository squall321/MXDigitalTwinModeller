using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer
{
    /// <summary>
    /// Modification history log (Cycle 33). Pure in-memory display-only log.
    /// NOT a true undo system — does not snapshot geometry. Just records
    /// which ModificationService primitive was called with which params so the
    /// user can see what edits have been applied in the current session.
    ///
    /// A true geometry-undo would require saving a .scdocx backup before each
    /// mutation, which is intentionally NOT done here (avoid I/O cost on every
    /// edit). For real undo: the user saves backups manually OR re-opens the
    /// original file.
    /// </summary>
    public static class ModificationHistory
    {
        public class Entry
        {
            public DateTime Timestamp;
            public string Operation;       // e.g. "ChangeHoleDiameter"
            public string FeatureId;       // H1, B2, FC1, W1
            public string ParamLabel;      // D, H, R, T
            public double BeforeMm;
            public double AfterMm;
            public bool Success;
            public string ErrorMessage;    // null if success
            public int ModifiedFaceCount;
        }

        // session-life list (cleared by Clear())
        private static readonly List<Entry> s_entries = new List<Entry>();

        public static IReadOnlyList<Entry> Entries
        {
            get { return s_entries.AsReadOnly(); }
        }

        public static int Count
        {
            get { return s_entries.Count; }
        }

        public static void Record(
            string operation,
            string featureId,
            string paramLabel,
            double beforeMm,
            double afterMm,
            ModificationResult result)
        {
            try
            {
                s_entries.Add(new Entry
                {
                    Timestamp = DateTime.Now,
                    Operation = operation ?? "(unknown)",
                    FeatureId = featureId ?? "",
                    ParamLabel = paramLabel ?? "",
                    BeforeMm = beforeMm,
                    AfterMm = afterMm,
                    Success = result != null && result.Success,
                    ErrorMessage = result != null ? result.ErrorMessage : "(null result)",
                    ModifiedFaceCount = (result != null && result.ModifiedFaceIndices != null)
                        ? result.ModifiedFaceIndices.Count : 0,
                });
            }
            catch { /* logging must never throw */ }
        }

        public static void Clear()
        {
            s_entries.Clear();
        }

        /// <summary>
        /// Build a human-readable multi-line summary of all recorded entries.
        /// Used by ShowModificationHistoryCommand dialog.
        /// </summary>
        public static string FormatSummary()
        {
            if (s_entries.Count == 0)
                return "(no modifications recorded in this session)";

            var sb = new StringBuilder();
            sb.AppendFormat("Modification history — {0} entries\n", s_entries.Count);
            sb.AppendLine("---------------------------------------------");
            for (int i = 0; i < s_entries.Count; i++)
            {
                var e = s_entries[i];
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "[{0:HH:mm:ss}] #{1,2}  {2}\n",
                    e.Timestamp, i + 1, e.Operation);
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "          {0} {1}: {2:F3} → {3:F3} mm   ",
                    e.FeatureId, e.ParamLabel, e.BeforeMm, e.AfterMm);
                if (e.Success)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "OK  (touched {0} face(s))\n", e.ModifiedFaceCount);
                }
                else
                {
                    sb.AppendFormat("FAIL: {0}\n", e.ErrorMessage ?? "(no msg)");
                }
            }
            return sb.ToString();
        }
    }
}
