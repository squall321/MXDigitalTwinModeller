using System;
using System.Collections.Generic;
using System.Linq;
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
    /// DesignBody → FaceAdjacency 그래프.
    /// `designFace.AdjacentFaces` API + 각 인접 쌍 사이의 edge 분석 (tangent/perpendicular).
    /// Cylinder discrimination 의 기반.
    /// </summary>
    public class AdjacencyBuilder
    {
        // tangent 판정: 노멀의 dot product 가 (1 - ε) 이상이면 tangent.
        // Note: 0.90 로 완화 (이전 0.95). RoundEdges 결과의 cylinder-torus 공유 edge 에서
        //   미세한 수치 오차로 dot 이 0.93~0.97 사이로 나오는 경우가 있어, 0.95 컷이 너무
        //   엄격해서 G1-연속 fillet 체인을 끊는 사례가 있었음 (spec 51, 61, 66).
        //   0.90 은 ≈ 25.8° 까지 허용 — fillet G1 으로는 충분히 관대하고, perpendicular(90°)
        //   와는 여전히 안전 마진이 있다.
        private const double TangentDotThreshold = 0.90;
        // perpendicular 판정: dot product 의 절대값이 ε 이하면 perpendicular
        private const double PerpendicularDotThreshold = 0.15;

        /// <summary>
        /// DesignBody 의 모든 face 에 대해 adjacency graph 빌드.
        /// </summary>
        /// <returns>key = face index, value = FaceAdjacency</returns>
        public Dictionary<int, FaceAdjacency> Build(DesignBody designBody)
        {
            if (designBody == null)
                throw new ArgumentNullException(nameof(designBody));

            // face → index map
            var faces = designBody.Faces.ToList();
            var faceToIndex = new Dictionary<DesignFace, int>();
            for (int i = 0; i < faces.Count; i++)
                faceToIndex[faces[i]] = i;

            var graph = new Dictionary<int, FaceAdjacency>();
            for (int i = 0; i < faces.Count; i++)
                graph[i] = new FaceAdjacency { FaceIndex = i };

            // 각 face 의 AdjacentFaces 순회
            for (int i = 0; i < faces.Count; i++)
            {
                var df = faces[i];
                var adj = graph[i];

                foreach (var neighborDf in df.AdjacentFaces)
                {
                    if (!faceToIndex.TryGetValue(neighborDf, out int neighborIdx)) continue;
                    if (neighborIdx == i) continue;
                    if (adj.Neighbors.Contains(neighborIdx)) continue;

                    adj.Neighbors.Add(neighborIdx);

                    // 두 face 사이의 edge 관계 분석
                    var relation = AnalyzeEdgeRelation(df, neighborDf);
                    adj.EdgeRelations[neighborIdx] = relation;
                }
            }

            return graph;
        }

        /// <summary>
        /// 두 인접 face 사이의 edge 관계 (tangent vs perpendicular) 추론.
        ///
        /// 알고리즘:
        ///   1. 두 face 가 공유하는 edge 찾기
        ///   2. edge 중점에서 양쪽 face 의 normal 계산
        ///   3. dot product 로 tangent / perpendicular 판정
        /// </summary>
        private EdgeRelation AnalyzeEdgeRelation(DesignFace a, DesignFace b)
        {
            var rel = new EdgeRelation
            {
                IsTangent = false,
                IsPerpendicular = false,
                EdgeLengthMm = 0.0,
                NormalDot = 0.0,
            };

            // Audit fix: 두 face 가 한 개 이상의 edge 를 공유할 수 있다 (예: 분할된
            //   fillet 가장자리). 첫 번째 edge 만 보면 짧은 seam 이 주된 관계인 양
            //   잘못 판정될 수 있으므로, 모든 공유 edge 를 모은 뒤 가장 긴 edge 를
            //   대표로 채택한다 (가장 긴 edge 가 보통 진짜 face-to-face 경계).
            var sharedEdges = new List<DesignEdge>();
            var aEdgeSet = new HashSet<DesignEdge>(a.Edges);
            foreach (var be in b.Edges)
            {
                if (aEdgeSet.Contains(be)) sharedEdges.Add(be);
            }
            if (sharedEdges.Count == 0) return rel;

            DesignEdge sharedEdge = null;
            double bestLen = -1;
            foreach (var e in sharedEdges)
            {
                double L;
                try { L = e.Shape.Length; }
                catch { continue; }
                // 1e-9: WARNING — kernel epsilon (nm-scale degenerate-seam filter in meters,
                // mathematical-zero guard). Not a feature threshold; do not refactor to ratio.
                if (L < 1e-9) continue; // degenerate seam 은 건너뜀
                if (L > bestLen) { bestLen = L; sharedEdge = e; }
            }
            if (sharedEdge == null) return rel;

            var edgeShape = sharedEdge.Shape;
            rel.EdgeLengthMm = edgeShape.Length * 1000.0;

            // edge 중점 계산
            Point midPoint;
            try
            {
                var bounds = edgeShape.Bounds;
                double midT = (bounds.Start + bounds.End) / 2.0;
                midPoint = edgeShape.Geometry.Evaluate(midT).Point;
            }
            catch
            {
                // fallback: start + end / 2
                var sp = edgeShape.StartPoint;
                var ep = edgeShape.EndPoint;
                midPoint = Point.Create((sp.X + ep.X) / 2.0, (sp.Y + ep.Y) / 2.0, (sp.Z + ep.Z) / 2.0);
            }

            // 두 face 의 normal at midPoint
            var na = NormalAtPoint(a, midPoint);
            var nb = NormalAtPoint(b, midPoint);

            if (na == null || nb == null) return rel;

            double dot = na[0] * nb[0] + na[1] * nb[1] + na[2] * nb[2];
            rel.NormalDot = dot;

            // Audit fix: 기존엔 Math.Abs(dot) 로 anti-parallel (dot ≈ -1) 까지
            //   tangent 로 잡았는데, 이는 솔리드 양면이 마주보는 ("sheet metal 두 면")
            //   상황이지 G1 tangent 가 아니다. positively-aligned normal 만
            //   tangent (fillet/smooth blend) 로 인정한다.
            if (dot >= TangentDotThreshold) rel.IsTangent = true;

            // perpendicular: 90도 — boss/hole entry
            if (Math.Abs(dot) <= PerpendicularDotThreshold) rel.IsPerpendicular = true;

            return rel;
        }

        /// <summary>
        /// face 의 한 점에서의 outward normal (단위 벡터).
        /// IsReversed 적용해서 진정한 outward 방향 반환.
        ///
        /// 알고리즘:
        ///   1. Surface 가 Plane/Cylinder/Sphere/Cone 이면 closed-form 으로 계산 (빠르고
        ///      안정적).
        ///   2. 그 외 (Torus, NurbsSurface, Procedural) 는 Surface.ProjectPoint(point)
        ///      .Normal 을 사용. 이것이 SpaceClaim API 가 제공하는 일반 surface 평가의
        ///      정식 경로다. Torus 의 corner-blend fillet 면이 fillet chain 의 분기점
        ///      이므로 여기서 null 을 반환하면 chain 이 산산조각 난다 (spec 51, 14, 66
        ///      재현 케이스).
        /// </summary>
        private double[] NormalAtPoint(DesignFace df, Point point)
        {
            try
            {
                var face = df.Shape;
                Vector n;
                bool computed = false;

                if (face.Geometry is Plane plane)
                {
                    var dz = plane.Frame.DirZ.UnitVector;
                    n = dz;
                    computed = true;
                }
                else if (face.Geometry is Cylinder cyl)
                {
                    // 원기둥: 축에서 point 방향으로 향하는 반경 방향
                    var origin = cyl.Frame.Origin;
                    var axisZ = cyl.Frame.DirZ.UnitVector;
                    // point - origin 을 축에 수직 성분만 추출
                    var diff = Vector.Create(point.X - origin.X, point.Y - origin.Y, point.Z - origin.Z);
                    double along = Vector.Dot(diff, axisZ);
                    var radial = Vector.Create(
                        diff.X - axisZ.X * along,
                        diff.Y - axisZ.Y * along,
                        diff.Z - axisZ.Z * along);
                    double mag = radial.Magnitude;
                    // 1e-9: WARNING — kernel epsilon (nm-scale numerical-stability guard
                    // against degenerate point-on-axis). Not a feature threshold.
                    if (mag >= 1e-9)
                    {
                        n = Vector.Create(radial.X / mag, radial.Y / mag, radial.Z / mag);
                        computed = true;
                    }
                    else
                    {
                        n = Vector.Create(0, 0, 0); // unused
                    }
                }
                else if (face.Geometry is Sphere sphere)
                {
                    var origin = sphere.Frame.Origin;
                    var diff = Vector.Create(point.X - origin.X, point.Y - origin.Y, point.Z - origin.Z);
                    double mag = diff.Magnitude;
                    if (mag >= 1e-9)
                    {
                        n = Vector.Create(diff.X / mag, diff.Y / mag, diff.Z / mag);
                        computed = true;
                    }
                    else
                    {
                        n = Vector.Create(0, 0, 0); // unused
                    }
                }
                else if (face.Geometry is Cone cone)
                {
                    // 원뿔: cylinder 와 유사한 처리 (간이)
                    var origin = cone.Frame.Origin;
                    var axisZ = cone.Frame.DirZ.UnitVector;
                    var diff = Vector.Create(point.X - origin.X, point.Y - origin.Y, point.Z - origin.Z);
                    double along = Vector.Dot(diff, axisZ);
                    var radial = Vector.Create(
                        diff.X - axisZ.X * along,
                        diff.Y - axisZ.Y * along,
                        diff.Z - axisZ.Z * along);
                    double mag = radial.Magnitude;
                    if (mag >= 1e-9)
                    {
                        n = Vector.Create(radial.X / mag, radial.Y / mag, radial.Z / mag);
                        computed = true;
                    }
                    else
                    {
                        n = Vector.Create(0, 0, 0); // unused
                    }
                }
                else
                {
                    n = Vector.Create(0, 0, 0); // placeholder; will be overwritten below
                }

                // Fallback for Torus / NurbsSurface / Procedural surfaces (and degenerate
                // closed-form cases). SpaceClaim Surface.ProjectPoint(p) returns a
                // SurfaceEvaluation whose .Normal is the surface's outward normal at the
                // closest point. This handles Torus corner-blend faces correctly — they
                // were previously returning null and breaking fillet-chain traversal.
                if (!computed)
                {
                    try
                    {
                        var eval = face.Geometry.ProjectPoint(point);
                        if (eval != null)
                        {
                            var dir = eval.Normal; // Direction (unit vector)
                            n = Vector.Create(dir.X, dir.Y, dir.Z);
                            computed = true;
                        }
                    }
                    catch
                    {
                        // fall through
                    }
                }

                if (!computed) return null;

                double sign = face.IsReversed ? -1.0 : 1.0;
                return new[] { n.X * sign, n.Y * sign, n.Z * sign };
            }
            catch
            {
                return null;
            }
        }
    }
}
