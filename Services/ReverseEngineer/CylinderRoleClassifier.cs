using System;
using System.Collections.Generic;
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
    /// Cylinder face 의 역할을 adjacency graph 와 IsReversed 플래그로 분류.
    ///
    /// 규칙 (front metal scope 에 최적화):
    ///   * 인접 평면 2개 + 둘 다 cylinder 와 tangent → EdgeFillet
    ///       - IsReversed=false → 볼록 fillet (외부 round)
    ///       - IsReversed=true  → 오목 fillet (내부 채움)
    ///   * 인접 평면 1~2개 + 모두 perpendicular(축이 평면에 수직)
    ///       - IsReversed=false → Boss (외부 cylinder, 평면에서 솟음)
    ///       - IsReversed=true  → Hole 의 내부 cylinder
    ///   * 그 외 → FunctionalCylinder
    ///
    /// 추가로 axis 방향 vs shared edge 방향 비교:
    ///   * axis ∥ edge → fillet (cylinder 축이 모서리 따라감)
    ///   * axis ⊥ edge → boss/hole entry (cylinder 축이 평면에 수직)
    /// </summary>
    public class CylinderRoleClassifier
    {
        /// <summary>
        /// W4-2c kernel-truth material-side probe. The hole↔boss decision keys on
        /// face.IsReversed, but dirty STEP imports can flip a cylinder's orientation —
        /// gate-proven on 11752, whose solid PIN imported with IsReversed=true and was
        /// mis-classified as a ThroughHole (every downstream hole-op then "General
        /// Failure"-poisoned the body). ContainsPoint on the cylinder axis-centre is the
        /// ground truth: SOLID core = boss/pin, VOID core = hole. Returns true if the
        /// core is solid, null if it can't be measured (→ fall back to IsReversed).
        /// </summary>
        private static bool? ProbeSolidCore(DesignBody body, FaceFeature face)
        {
            try
            {
                Face live = null; int i = 0;
                foreach (var df in body.Faces) { if (i == face.FaceIndex) { live = df.Shape; break; } i++; }
                if (live == null) return null;
                var cyl = live.Geometry as Cylinder;
                if (cyl == null) return null;
                var A = cyl.Axis.Origin; var D = cyl.Axis.Direction;
                double dux = D.X, duy = D.Y, duz = D.Z;
                var bb = live.GetBoundingBox(Matrix.Identity);
                double lo = double.MaxValue, hi = double.MinValue;
                for (int c = 0; c < 8; c++)
                {
                    double X = (c & 1) == 0 ? bb.MinCorner.X : bb.MaxCorner.X;
                    double Y = (c & 2) == 0 ? bb.MinCorner.Y : bb.MaxCorner.Y;
                    double Z = (c & 4) == 0 ? bb.MinCorner.Z : bb.MaxCorner.Z;
                    double t = X * dux + Y * duy + Z * duz;
                    if (t < lo) lo = t; if (t > hi) hi = t;
                }
                double tMid = (lo + hi) / 2.0;
                double s = tMid - (A.X * dux + A.Y * duy + A.Z * duz);
                var pt = Point.Create(A.X + dux * s, A.Y + duy * s, A.Z + duz * s);
                return body.Shape.ContainsPoint(pt);
            }
            catch { return null; }
        }

        /// <summary>
        /// 한 face 의 CylinderRole + Concavity 추론.
        /// </summary>
        public static void Classify(
            FaceFeature face,
            DesignBody body,
            Dictionary<int, FaceAdjacency> adjGraph,
            List<FaceFeature> allFaces)
        {
            // Cone 은 cylinder 와 동일한 plane-adjacency 로직을 거치게 한다.
            //   - IsReversed=true (오목 cone) + 인접 평면 perpendicular → ConicalHole
            //   - IsReversed=false (볼록 cone) + 인접 평면 perpendicular → ConicalBoss
            // 그 외 type 은 기존 로직 그대로 (Plane, Torus, Sphere 등).
            if (face.Type != SurfaceType.Cylinder && face.Type != SurfaceType.Cone)
            {
                // Plane = Flat, Torus = corner blend (CornerBlend), 나머지 Unknown
                if (face.Type == SurfaceType.Plane)
                {
                    face.Concavity = Concavity.Flat;
                }
                else if (face.Type == SurfaceType.Torus)
                {
                    face.CylinderRole = CylinderRole.CornerBlend;
                    face.Concavity = face.IsReversed ? Concavity.Concave : Concavity.Convex;
                }
                else if (face.Type == SurfaceType.Sphere)
                {
                    // sphere 도 corner blend 일 수 있음 (3-way fillet 교차점)
                    face.CylinderRole = CylinderRole.CornerBlend;
                    face.Concavity = face.IsReversed ? Concavity.Concave : Concavity.Convex;
                }
                return;
            }

            bool isCone = face.Type == SurfaceType.Cone;

            // W4-2c: effective orientation = kernel-truth material side when measurable,
            // else the (sometimes-flipped) IsReversed flag. solidCore=true ⇒ boss/pin
            // (NOT reversed); void core ⇒ hole (reversed). On clean imports this equals
            // IsReversed (no change); on flipped imports like 11752 it corrects the role.
            bool? solidCore = ProbeSolidCore(body, face);
            bool effReversed = solidCore.HasValue ? !solidCore.Value : face.IsReversed;

            // Concavity 우선 결정 (kernel-truth corrected)
            face.Concavity = effReversed ? Concavity.Concave : Concavity.Convex;

            FaceAdjacency adj;
            if (!adjGraph.TryGetValue(face.FaceIndex, out adj))
            {
                face.CylinderRole = CylinderRole.Unknown;
                return;
            }

            // 인접 평면 face 카운트 + tangent/perpendicular 카운트
            int planeTangentCount = 0;
            int planePerpendicularCount = 0;
            foreach (var neighborIdx in adj.Neighbors)
            {
                if (neighborIdx >= allFaces.Count) continue;
                var n = allFaces[neighborIdx];
                if (n.Type != SurfaceType.Plane) continue;

                EdgeRelation rel;
                if (!adj.EdgeRelations.TryGetValue(neighborIdx, out rel)) continue;

                if (rel.IsTangent) planeTangentCount++;
                else if (rel.IsPerpendicular) planePerpendicularCount++;
            }

            // Audit fix: 분류 로직 재정렬.
            //   기존 cascade 는 "tangent ≥ 2 면 EdgeFillet" 가 가장 먼저 와서
            //   chamfered base 를 가진 boss (1 tangent + 1 perpendicular) 가
            //   EdgeFillet 으로 오분류되었다. perpendicular 증거가 있으면
            //   tangent 가 0~1 인 한 boss/hole 로 우선 분류한다.
            if (planePerpendicularCount >= 1 && planeTangentCount <= 1)
            {
                if (isCone)
                {
                    face.CylinderRole = effReversed
                        ? CylinderRole.ConicalHole
                        : CylinderRole.ConicalBoss;
                }
                else
                {
                    face.CylinderRole = effReversed
                        ? CylinderRole.ThroughHole
                        : CylinderRole.Boss;
                }
                return;
            }
            if (planeTangentCount >= 2)
            {
                // Cone 이 두 평면에 tangent 인 경우는 드물지만 (예: chamfer 가 양쪽으로
                //   확장), 발생 시 fillet 보다는 conical taper 로 보는 게 더 정확하다.
                if (isCone)
                {
                    face.CylinderRole = effReversed
                        ? CylinderRole.ConicalHole
                        : CylinderRole.ConicalBoss;
                }
                else
                {
                    face.CylinderRole = CylinderRole.EdgeFillet;
                }
                return;
            }
            if (planeTangentCount == 1)
            {
                if (isCone)
                {
                    // tangent 1개만 있는 cone — countersink 의 입구만 평면에 닿고
                    //   반대쪽은 cylinder 로 이어지는 흔한 케이스.
                    face.CylinderRole = effReversed
                        ? CylinderRole.ConicalHole
                        : CylinderRole.ConicalBoss;
                }
                else
                {
                    face.CylinderRole = CylinderRole.EdgeFillet;
                }
                return;
            }

            // 평면 인접이 0 인 경우: tangent fillet/torus 로 둘러싸인 cylinder.
            // Audit fix: 기존엔 IsReversed=false 면 무조건 FunctionalCylinder 였으나,
            //   fillet wrap 한 단계 너머에 평면이 있으면 사실은 Boss 다.
            //   1-hop 더 들어가 parent plane 을 inspect 한다.
            bool wrappedByFilletsOverPlane = false;
            foreach (var nIdx in adj.Neighbors)
            {
                if (nIdx >= allFaces.Count) continue;
                var n = allFaces[nIdx];
                if (n.Type != SurfaceType.Torus && n.Type != SurfaceType.Cylinder) continue;
                if (!adjGraph.TryGetValue(nIdx, out var nAdj)) continue;
                foreach (var nnIdx in nAdj.Neighbors)
                {
                    if (nnIdx >= allFaces.Count) continue;
                    if (allFaces[nnIdx].Type == SurfaceType.Plane)
                    {
                        wrappedByFilletsOverPlane = true;
                        break;
                    }
                }
                if (wrappedByFilletsOverPlane) break;
            }

            if (isCone)
            {
                // Cone 은 어떤 평면 인접도 없을 때 — 매우 드물지만 손상된 모델 가능.
                //   ConicalHole/Boss 로 기록해서 downstream detector 가 픽업하도록.
                face.CylinderRole = effReversed
                    ? CylinderRole.ConicalHole
                    : CylinderRole.ConicalBoss;
                return;
            }

            face.CylinderRole = effReversed
                ? CylinderRole.ThroughHole
                : (wrappedByFilletsOverPlane ? CylinderRole.Boss : CylinderRole.FunctionalCylinder);
        }

        /// <summary>
        /// 전체 face 들 일괄 분류.
        /// </summary>
        public static void ClassifyAll(
            List<FaceFeature> faces,
            DesignBody body,
            Dictionary<int, FaceAdjacency> adjGraph)
        {
            foreach (var f in faces)
            {
                Classify(f, body, adjGraph, faces);
            }

            // Boss / Hole 의 추가 판정: 두 평면 사이에서 cylinder 가 sandwiched 되어 있고
            //   둘 다 perpendicular 라면 through hole 또는 functional cylinder
            // (1차 분류 결과만 봐도 충분; 더 정교한 disambiguation 은 Stage 2 IdentifyHoles 로 보완)
        }
    }
}
