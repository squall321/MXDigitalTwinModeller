using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer;

#if V251
using SpaceClaim.Api.V251;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer
{
    /// <summary>
    /// Color-codes DesignFace items in SpaceClaim based on the FeatureGraph
    /// classification roles (Hole/Boss/Fillet/Wall/Slit/Pattern).
    /// Mutations are wrapped in WriteBlock.ExecuteTask.
    /// Uses DesignFace.SetColor(IAppearanceContext, Color?). Passing null for
    /// the context targets the design window appearance.
    /// </summary>
    // @lat: [[reverse-engineer#FeatureVisualizer]]
    public static class FeatureVisualizer
    {
        // ---- COLOR PALETTE ----
        public static readonly Color HoleColor          = Color.FromArgb(255,   0,   0); // RED
        public static readonly Color BossColor          = Color.FromArgb(  0, 200,   0); // GREEN
        public static readonly Color FilletColor        = Color.FromArgb(  0,   0, 255); // BLUE
        public static readonly Color WallColor          = Color.FromArgb(200, 200, 200); // LIGHT GRAY
        public static readonly Color SlitColor          = Color.FromArgb(255, 165,   0); // ORANGE
        public static readonly Color HolePatternColor   = Color.FromArgb(180,   0,   0); // DARKER RED
        public static readonly Color BossPatternColor   = Color.FromArgb(  0, 140,   0); // DARKER GREEN

        /// <summary>
        /// Walk the FeatureGraph and apply per-face colors to designBody.
        /// Safe to call multiple times; later assignments overwrite earlier ones,
        /// so priority order is: Wall (background) → Slit → Fillet → Hole/Boss → Pattern highlight.
        /// </summary>
        public static void ApplyColors(DesignBody designBody, FeatureGraph graph)
        {
            if (designBody == null) throw new ArgumentNullException(nameof(designBody));
            if (graph == null) throw new ArgumentNullException(nameof(graph));

            var faces = designBody.Faces.ToList();
            if (faces.Count == 0) return;

            // Index hole patterns / boss patterns by member id for quick lookup.
            var patternedHoleIds = new HashSet<string>();
            if (graph.HolePatterns != null)
            {
                foreach (var hp in graph.HolePatterns)
                    if (hp.MemberHoleIds != null)
                        foreach (var id in hp.MemberHoleIds)
                            patternedHoleIds.Add(id);
            }
            var patternedBossIds = new HashSet<string>();
            if (graph.BossPatterns != null)
            {
                foreach (var bp in graph.BossPatterns)
                    if (bp.MemberBossIds != null)
                        foreach (var id in bp.MemberBossIds)
                            patternedBossIds.Add(id);
            }

            WriteBlock.ExecuteTask("Visualize FeatureGraph", () =>
            {
                // 1. Wall pair faces — light gray background tint.
                if (graph.Walls != null)
                {
                    foreach (var w in graph.Walls)
                    {
                        SafeSetColor(faces, w.FaceA, WallColor);
                        SafeSetColor(faces, w.FaceB, WallColor);
                    }
                }

                // 2. Slit faces — orange (overrides wall coloring for the same faces).
                if (graph.Slits != null)
                {
                    foreach (var s in graph.Slits)
                    {
                        SafeSetColor(faces, s.FaceA, SlitColor);
                        SafeSetColor(faces, s.FaceB, SlitColor);
                    }
                }

                // 3. Fillets — blue (edge fillets + corner blends).
                if (graph.Fillets != null)
                {
                    foreach (var f in graph.Fillets)
                        SafeSetColor(faces, f.FaceIndex, FilletColor);
                }

                // 4. Holes — red (or darker red if part of a pattern).
                if (graph.Holes != null)
                {
                    foreach (var h in graph.Holes)
                    {
                        var c = (h.Id != null && patternedHoleIds.Contains(h.Id))
                            ? HolePatternColor
                            : HoleColor;
                        SafeSetColor(faces, h.CylinderFaceIndex, c);
                    }
                }

                // 5. Bosses — green (or darker green if part of a pattern).
                if (graph.Bosses != null)
                {
                    foreach (var b in graph.Bosses)
                    {
                        var c = (b.Id != null && patternedBossIds.Contains(b.Id))
                            ? BossPatternColor
                            : BossColor;
                        SafeSetColor(faces, b.CylinderFaceIndex, c);
                    }
                }
            });
        }

        /// <summary>
        /// Revert all face colors on the body to default (null → inherit from body).
        /// </summary>
        public static void ClearColors(DesignBody designBody)
        {
            if (designBody == null) throw new ArgumentNullException(nameof(designBody));
            var faces = designBody.Faces.ToList();
            if (faces.Count == 0) return;

            WriteBlock.ExecuteTask("Clear FeatureGraph colors", () =>
            {
                foreach (var df in faces)
                {
                    try { df.SetColor(null, null); }
                    catch { /* swallow per-face errors */ }
                }
            });
        }

        // ---------------------------------------------------------------
        // Internal helpers
        // ---------------------------------------------------------------
        private static void SafeSetColor(List<DesignFace> faces, int faceIndex, Color color)
        {
            if (faceIndex < 0 || faceIndex >= faces.Count) return;
            try
            {
                faces[faceIndex].SetColor(null, color);
            }
            catch
            {
                // SC may throw if face was invalidated; swallow.
            }
        }
    }
}
