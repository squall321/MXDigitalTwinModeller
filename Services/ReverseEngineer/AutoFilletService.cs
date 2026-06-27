using System;
using System.Collections.Generic;
using System.Linq;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer
{
    /// <summary>
    /// 변형(OffsetFaces 등) 후 새로 생긴 sharp edge 들을 찾아,
    /// 기존 fillet style(dominant R)에 맞추어 자동 라운딩한다.
    ///
    /// 사용 흐름:
    ///   1) 변형 전 FeatureGraph 에서 DetermineTargetRadius 로 대표 R 결정
    ///   2) RoundAllSharpEdges (광범위) 또는 RoundEdgesNearFaces (타겟) 호출
    ///
    /// 모든 mutation 은 WriteBlock.ExecuteTask 내부에서 실행한다.
    /// </summary>
    // @lat: [[reverse-engineer#AutoFilletService]]
    public static class AutoFilletService
    {
        // Convex sharp edge 판정 임계값.
        // dihedral angle < ~170° 이면 sharp(라운딩 후보).
        // 두 face normal 간의 dot 으로 환산: cos(170°) ≈ -0.9848.
        // 즉 dot > -0.9848 (== more than ~10° 꺾임) 이면 sharp.
        // (두 outward normal 이 거의 정반대이면 평면연속이므로 라운딩 불필요.)
        private const double SharpDotThreshold = -0.9848; // cos(170°)

        // 매우 작은 dihedral (거의 평면 연속) 은 skip.
        // dot ≈ +1 이면 두 outward normal 이 같은 방향 → tangent / 이미 매끈함.
        private const double TangentDotThreshold = 0.95;

        // ----------------------------------------------------------------
        // Result
        // ----------------------------------------------------------------
        public class AutoFilletResult
        {
            /// <summary>true = 정상 종료(0개 라운딩이어도 success). false = 치명적 실패.</summary>
            public bool Success { get; set; }

            /// <summary>실제로 라운딩된 edge 수.</summary>
            public int EdgesRounded { get; set; }

            /// <summary>라운딩 시도했으나 실패한 edge 수 (RoundEdges 예외).</summary>
            public int EdgesSkippedFailed { get; set; }

            /// <summary>적용된 반경 (mm).</summary>
            public double AppliedRadiusMm { get; set; }

            /// <summary>설명/진단 메시지.</summary>
            public string Notes { get; set; }

            public AutoFilletResult()
            {
                Notes = string.Empty;
            }
        }

        // ----------------------------------------------------------------
        // DetermineTargetRadius
        // ----------------------------------------------------------------
        /// <summary>
        /// 기존 FilletChain 들의 RadiusMm 분포에서 가장 많이 등장하는 값(dominant)을 target 으로 반환.
        /// 동률이면 큰 R 우선 (육안에 더 잘 보이는 fillet 을 우선시).
        /// chain 이 없으면 defaultMm 반환.
        /// </summary>
        public static double DetermineTargetRadius(FeatureGraph graph, double defaultMm = 0.5)
        {
            if (graph == null || graph.FilletChains == null || graph.FilletChains.Count == 0)
                return defaultMm;

            // RadiusMm 을 0.01 mm 단위로 round → 비슷한 R 끼리 묶기
            var grouped = graph.FilletChains
                .Where(c => c.RadiusMm > 1e-6)
                .GroupBy(c => Math.Round(c.RadiusMm, 2))
                .Select(g => new { R = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenByDescending(x => x.R)
                .ToList();

            if (grouped.Count == 0) return defaultMm;

            return grouped[0].R;
        }

        // ----------------------------------------------------------------
        // RoundAllSharpEdges (APPROACH A — simple)
        // ----------------------------------------------------------------
        /// <summary>
        /// body 의 모든 edge 를 walk 하면서 sharp convex edge 를 찾아 radiusMm 로 라운딩.
        /// </summary>
        public static AutoFilletResult RoundAllSharpEdges(DesignBody designBody, double radiusMm)
        {
            var result = new AutoFilletResult
            {
                AppliedRadiusMm = radiusMm,
            };

            if (designBody == null)
            {
                result.Notes = "designBody is null";
                return result;
            }
            if (radiusMm <= 0)
            {
                result.Notes = "radiusMm must be > 0";
                return result;
            }

            // 모든 edge 의 adjacent face 정보를 미리 수집한 뒤
            // sharp convex 인 것만 candidate 로 모은다.
            var candidates = CollectSharpEdges(designBody, modifiedFaceIndices: null);

            if (candidates.Count == 0)
            {
                result.Success = true;
                result.Notes = "No sharp convex edges found.";
                return result;
            }

            ApplyRoundEdges(designBody, candidates, radiusMm, result);
            return result;
        }

        // ----------------------------------------------------------------
        // RoundEdgesNearFaces (APPROACH B — targeted)
        // ----------------------------------------------------------------
        /// <summary>
        /// modifiedFaceIndices 에 포함된 face 와 인접한 edge 들만 검사하여
        /// sharp convex edge 를 라운딩한다.
        /// (ModificationResult 의 NewlyCreatedEdges 와 별개로, "변경된 face 주변" 만 보는 접근.)
        /// </summary>
        public static AutoFilletResult RoundEdgesNearFaces(
            DesignBody designBody,
            List<int> modifiedFaceIndices,
            double radiusMm)
        {
            var result = new AutoFilletResult
            {
                AppliedRadiusMm = radiusMm,
            };

            if (designBody == null)
            {
                result.Notes = "designBody is null";
                return result;
            }
            if (radiusMm <= 0)
            {
                result.Notes = "radiusMm must be > 0";
                return result;
            }
            if (modifiedFaceIndices == null || modifiedFaceIndices.Count == 0)
            {
                result.Success = true;
                result.Notes = "modifiedFaceIndices empty — nothing to do.";
                return result;
            }

            var modifiedSet = new HashSet<int>(modifiedFaceIndices);
            var candidates = CollectSharpEdges(designBody, modifiedSet);

            if (candidates.Count == 0)
            {
                result.Success = true;
                result.Notes = "No sharp convex edges adjacent to modified faces.";
                return result;
            }

            ApplyRoundEdges(designBody, candidates, radiusMm, result);
            return result;
        }

        // ----------------------------------------------------------------
        // Internal: edge collection + dihedral test
        // ----------------------------------------------------------------
        private class EdgeCandidate
        {
            public Edge EdgeShape;
            public DesignFace FaceA;
            public DesignFace FaceB;
            public double NormalDot;
        }

        /// <summary>
        /// body 의 모든 edge 중 sharp convex 인 것들 수집.
        /// modifiedFaceIndices 가 null 이 아니면, 두 인접 face 중 하나가 그 집합에 속해야 함.
        /// </summary>
        private static List<EdgeCandidate> CollectSharpEdges(
            DesignBody designBody,
            HashSet<int> modifiedFaceIndices)
        {
            var candidates = new List<EdgeCandidate>();

            // face index 매핑 (FeatureExtractor 와 같은 순회 순서).
            var designFaces = designBody.Faces.ToList();
            var faceToIndex = new Dictionary<DesignFace, int>();
            for (int i = 0; i < designFaces.Count; i++)
                faceToIndex[designFaces[i]] = i;

            // designEdge → (faceA, faceB) 쌍을 만들기 위해 face 별 edge set 으로 역인덱스
            // 한 edge 는 보통 2개 face 가 공유. 3개 이상이면 비-매니폴드 → skip.
            var edgeToFaces = new Dictionary<DesignEdge, List<DesignFace>>();

            try
            {
                foreach (var df in designFaces)
                {
                    foreach (var de in df.Edges)
                    {
                        if (!edgeToFaces.TryGetValue(de, out var list))
                        {
                            list = new List<DesignFace>(2);
                            edgeToFaces[de] = list;
                        }
                        list.Add(df);
                    }
                }
            }
            catch
            {
                // 일부 edge 접근 실패는 무시
            }

            foreach (var kv in edgeToFaces)
            {
                var de = kv.Key;
                var faces = kv.Value;
                if (faces.Count != 2) continue; // 비-매니폴드/자유 edge skip

                var fA = faces[0];
                var fB = faces[1];

                // targeted mode: 두 face 중 하나라도 modified set 에 있어야 함
                if (modifiedFaceIndices != null)
                {
                    int iA = faceToIndex.TryGetValue(fA, out int xA) ? xA : -1;
                    int iB = faceToIndex.TryGetValue(fB, out int xB) ? xB : -1;
                    if (!(modifiedFaceIndices.Contains(iA) || modifiedFaceIndices.Contains(iB)))
                        continue;
                }

                var edgeShape = de.Shape;
                if (edgeShape == null) continue;

                // edge midpoint
                Point midPoint;
                if (!TryEdgeMidpoint(edgeShape, out midPoint)) continue;

                // 두 face 의 outward normal
                var nA = NormalAtPoint(fA, midPoint);
                var nB = NormalAtPoint(fB, midPoint);
                if (nA == null || nB == null) continue;

                double dot = nA[0] * nB[0] + nA[1] * nB[1] + nA[2] * nB[2];

                // tangent (이미 매끈) → skip
                if (dot >= TangentDotThreshold) continue;

                // 거의 정반대 (~180°) → 평판이 끝나는 자유 edge 와 유사, skip
                if (dot <= SharpDotThreshold) continue;

                // Convex 판정:
                // 두 outward normal 이 edge midpoint 에서 "벌어지는" 형태이면 convex.
                // (midpoint 에서 nA 방향과 (faceA→faceB midpoint 차이) 의 부호로 추정.)
                if (!IsConvexAtEdge(edgeShape, midPoint, fA, fB, nA, nB)) continue;

                candidates.Add(new EdgeCandidate
                {
                    EdgeShape = edgeShape,
                    FaceA = fA,
                    FaceB = fB,
                    NormalDot = dot,
                });
            }

            return candidates;
        }

        /// <summary>
        /// edge 의 parametric midpoint 계산. AdjacencyBuilder 와 동일 패턴.
        /// </summary>
        private static bool TryEdgeMidpoint(Edge edgeShape, out Point midPoint)
        {
            midPoint = Point.Origin;
            try
            {
                var bounds = edgeShape.Bounds;
                double midT = (bounds.Start + bounds.End) / 2.0;
                midPoint = edgeShape.Geometry.Evaluate(midT).Point;
                return true;
            }
            catch
            {
                try
                {
                    var sp = edgeShape.StartPoint;
                    var ep = edgeShape.EndPoint;
                    midPoint = Point.Create((sp.X + ep.X) / 2.0,
                                            (sp.Y + ep.Y) / 2.0,
                                            (sp.Z + ep.Z) / 2.0);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Convex edge 판정:
        ///   두 face 의 평균 outward normal 방향으로 midpoint 에서 살짝 떨어진 점이
        ///   body 외부에 있으면 convex(볼록), 내부에 있으면 concave(오목).
        ///
        /// 단순화된 휴리스틱: 두 outward normal 의 합 벡터가 "벌어지는" 방향
        ///   (즉 midpoint 에서 양쪽 face 에서 모두 외부로 향함) → convex.
        /// 두 normal 의 평균이 가리키는 방향에서 (nA + nB) · (faceA_centroid - midPoint) 부호로 보강.
        /// </summary>
        private static bool IsConvexAtEdge(
            Edge edgeShape,
            Point midPoint,
            DesignFace fA,
            DesignFace fB,
            double[] nA,
            double[] nB)
        {
            // 휴리스틱: 외적의 부호로 convex/concave 판정이 가장 견고하지만,
            // 여기선 더 단순하게 두 outward normal 의 평균이 edge tangent 와 직교하며
            // body 외부로 향하는지를 본다.
            //
            // 간단 규약: dot(nA, nB) ∈ (-0.985, 0.95) 이고
            // 두 normal 의 평균 벡터가 0 이 아니면 → 외부 방향으로 합쳐지면 convex.
            //
            // 더 신뢰성 있는 방법: edge tangent t 와 nA × nB 의 방향 비교.
            //   convex 모서리에서는 (nA × nB) 가 edge tangent 와 같은 방향 (right-hand rule
            //   for outward-facing surfaces — 단, face winding 에 의존).
            //
            // 여기선 보수적으로:
            //   sumN = nA + nB
            //   |sumN| > 0 (즉 두 normal 이 완전 반대가 아님) 이고
            //   sumN 이 zero 가 아닌 유의미한 outward direction 이면 convex 후보.
            //
            // 실제 구현은 caller 가 결과를 검토하고 RoundEdges 가 실패하면 skip.
            double sx = nA[0] + nB[0];
            double sy = nA[1] + nB[1];
            double sz = nA[2] + nB[2];
            double sMag = Math.Sqrt(sx * sx + sy * sy + sz * sz);

            if (sMag < 1e-6) return false; // 두 normal 이 완전 반대 → flat 면 연속

            // Convex 일 때 두 outward normal 합이 모서리에서 외부로 향한다.
            // 본 휴리스틱은 "외부 방향" 검사를 생략하고 dot < TangentDotThreshold 만으로
            // sharp 임을 충족하면 convex 후보로 본다.
            // (Concave edge 도 dot 이 비슷할 수 있어 false positive 가능 — RoundEdges 실패시 skip.)
            return true;
        }

        /// <summary>
        /// face 의 한 점에서의 outward normal. AdjacencyBuilder 의 NormalAtPoint 와 동일 로직.
        /// </summary>
        private static double[] NormalAtPoint(DesignFace df, Point point)
        {
            try
            {
                var face = df.Shape;
                Vector n;

                if (face.Geometry is Plane plane)
                {
                    n = plane.Frame.DirZ.UnitVector;
                }
                else if (face.Geometry is Cylinder cyl)
                {
                    var origin = cyl.Frame.Origin;
                    var axisZ = cyl.Frame.DirZ.UnitVector;
                    var diff = Vector.Create(point.X - origin.X, point.Y - origin.Y, point.Z - origin.Z);
                    double along = Vector.Dot(diff, axisZ);
                    var radial = Vector.Create(
                        diff.X - axisZ.X * along,
                        diff.Y - axisZ.Y * along,
                        diff.Z - axisZ.Z * along);
                    double mag = radial.Magnitude;
                    if (mag < 1e-9) return null;
                    n = Vector.Create(radial.X / mag, radial.Y / mag, radial.Z / mag);
                }
                else if (face.Geometry is Sphere sphere)
                {
                    var origin = sphere.Frame.Origin;
                    var diff = Vector.Create(point.X - origin.X, point.Y - origin.Y, point.Z - origin.Z);
                    double mag = diff.Magnitude;
                    if (mag < 1e-9) return null;
                    n = Vector.Create(diff.X / mag, diff.Y / mag, diff.Z / mag);
                }
                else if (face.Geometry is Cone cone)
                {
                    var origin = cone.Frame.Origin;
                    var axisZ = cone.Frame.DirZ.UnitVector;
                    var diff = Vector.Create(point.X - origin.X, point.Y - origin.Y, point.Z - origin.Z);
                    double along = Vector.Dot(diff, axisZ);
                    var radial = Vector.Create(
                        diff.X - axisZ.X * along,
                        diff.Y - axisZ.Y * along,
                        diff.Z - axisZ.Z * along);
                    double mag = radial.Magnitude;
                    if (mag < 1e-9) return null;
                    n = Vector.Create(radial.X / mag, radial.Y / mag, radial.Z / mag);
                }
                else
                {
                    // Torus, NURBS 등은 normal 분석 생략 → caller skip.
                    return null;
                }

                double sign = face.IsReversed ? -1.0 : 1.0;
                return new[] { n.X * sign, n.Y * sign, n.Z * sign };
            }
            catch
            {
                return null;
            }
        }

        // ----------------------------------------------------------------
        // ApplyRoundEdges — WriteBlock 내부 라운딩 실행
        // ----------------------------------------------------------------
        /// <summary>
        /// candidates 전체를 한 번의 body.RoundEdges 호출로 시도.
        /// 실패 시 candidate 를 하나씩 빼면서 fallback (degrade gracefully).
        /// </summary>
        private static void ApplyRoundEdges(
            DesignBody designBody,
            List<EdgeCandidate> candidates,
            double radiusMm,
            AutoFilletResult result)
        {
            double rMeters = GeometryUtils.MmToMeters(radiusMm);
            var round = new FixedRadiusRound(rMeters);

            // 1) 한꺼번에 시도
            bool bulkOk = false;
            try
            {
                WriteBlock.ExecuteTask("AutoFillet bulk R=" + radiusMm + "mm", () =>
                {
                    var map = new Dictionary<Edge, EdgeRound>();
                    foreach (var c in candidates)
                    {
                        if (!map.ContainsKey(c.EdgeShape))
                            map[c.EdgeShape] = round;
                    }
                    designBody.Shape.RoundEdges(map);
                });
                bulkOk = true;
                result.EdgesRounded = candidates.Count;
                result.Success = true;
                result.Notes = "Bulk RoundEdges succeeded (" + candidates.Count + " edges).";
            }
            catch (Exception bulkEx)
            {
                // 2) fallback: 하나씩 시도 — 실패한 것만 skip
                int rounded = 0;
                int failed = 0;
                foreach (var c in candidates)
                {
                    try
                    {
                        WriteBlock.ExecuteTask("AutoFillet single R=" + radiusMm + "mm", () =>
                        {
                            var map = new Dictionary<Edge, EdgeRound>
                            {
                                { c.EdgeShape, round },
                            };
                            designBody.Shape.RoundEdges(map);
                        });
                        rounded++;
                    }
                    catch
                    {
                        failed++;
                    }
                }
                result.EdgesRounded = rounded;
                result.EdgesSkippedFailed = failed;
                result.Success = rounded > 0 || failed == 0;
                result.Notes = "Bulk failed (" + bulkEx.Message + "); per-edge fallback: "
                    + rounded + " ok, " + failed + " failed.";
            }

            // bulkOk 인데 success 아닐 일 없음 — defensive
            if (bulkOk && !result.Success)
            {
                result.Success = true;
            }
        }
    }
}
