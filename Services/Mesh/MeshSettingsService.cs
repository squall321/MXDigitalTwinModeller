using System;
using System.Collections.Generic;
using System.Linq;
using SpaceClaim.Api.V252.Scripting.Commands;
using SpaceClaim.Api.V252.Scripting.Commands.CommandOptions;
using SpaceClaim.Api.V252.Scripting.Selection;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Mesh
{
    public static class MeshSettingsService
    {
        private const int TargetDivisions = 10;
        private const double MinSizeM = 0.0001;  // 0.1mm
        private const double MaxSizeM = 0.01;    // 10mm

        /// <summary>
        /// 바디의 BoundingBox에서 X/Y/Z 기본 메쉬 크기 계산 (mm 반환)
        /// </summary>
        public static void ComputeDefaultSizes(DesignBody body,
            out double xMm, out double yMm, out double zMm)
        {
            xMm = 2.0;
            yMm = 2.0;
            zMm = 2.0;

            try
            {
                Box bbox = body.Shape.GetBoundingBox(Matrix.Identity);
                double xLen = bbox.MaxCorner.X - bbox.MinCorner.X;
                double yLen = bbox.MaxCorner.Y - bbox.MinCorner.Y;
                double zLen = bbox.MaxCorner.Z - bbox.MinCorner.Z;

                double xSizeM = Clamp(xLen / TargetDivisions, MinSizeM, MaxSizeM);
                double ySizeM = Clamp(yLen / TargetDivisions, MinSizeM, MaxSizeM);
                double zSizeM = Clamp(zLen / TargetDivisions, MinSizeM, MaxSizeM);

                xMm = GeometryUtils.MetersToMm(xSizeM);
                yMm = GeometryUtils.MetersToMm(ySizeM);
                zMm = GeometryUtils.MetersToMm(zSizeM);
            }
            catch
            {
                // BoundingBox 실패 시 기본값 유지
            }
        }

        /// <summary>
        /// Scripting API를 사용하여 메쉬 생성
        /// </summary>
        public static void GenerateMesh(ICollection<DesignBody> bodies,
            double elementSizeMm,
            ElementShapeType shape,
            MidsideNodesType midsideNodes,
            double growthRate,
            SizeFunctionType sizeFunction)
        {
            // InitMeshSettings
            InitMeshSettings.Execute(PhysicsType.Structural, null);

            // Selection
            var docObjects = bodies.Cast<IDocObject>().ToArray();
            var bodySelection = Selection.Create(docObjects);
            var emptySelection = Selection.Empty();

            // Options
            var options = new CreateMeshOptions();
            options.ElementSize = elementSizeMm / 1000.0;
            options.SolidElementShape = shape;
            options.MidsideNodes = midsideNodes;
            options.GrowthRate = growthRate;
            options.SizeFunctionType = sizeFunction;

            // Execute
            CreateMesh.Execute(bodySelection, emptySelection, options, null);
        }

        /// <summary>
        /// 현재 Part 아래의 모든 DesignBody를 재귀적으로 수집
        /// (컴포넌트 내부 바디 포함)
        /// </summary>
        public static List<DesignBody> GetAllDesignBodies(Part rootPart)
        {
            var result = new List<DesignBody>();
            CollectBodies(rootPart, result);
            return result;
        }

        /// <summary>
        /// IPart 인터페이스 기반 재귀 수집 (컴포넌트 occurrence 바디 포함).
        /// ANSYS 공식 샘플(FindMatchingFaces)과 동일한 패턴 사용.
        /// </summary>
        private static void CollectBodies(IPart part, List<DesignBody> result)
        {
            if (part == null) return;

            foreach (var body in part.Bodies)
            {
                var db = body as DesignBody;
                if (db != null)
                    result.Add(db);
            }

            foreach (var comp in part.Components)
            {
                try
                {
                    if (comp.Content != null)
                        CollectBodies(comp.Content, result);
                }
                catch { }
            }
        }

        // ─── 방향별 에지 사이징 ───

        /// <summary>
        /// 바디의 에지 방향에 따라 X/Y/Z축별 메쉬 사이징 적용.
        /// 에지의 chord 방향과 각 축의 내적(절대값)이 가장 큰 축의 사이즈를 적용.
        /// 이미 사이징이 정의된 에지는 SpaceClaim이 내부적으로 우선 처리.
        /// </summary>
        public static int ApplyDirectionalSizing(DesignBody body,
            double xSizeMm, double ySizeMm, double zSizeMm,
            List<string> log)
        {
            // X, Y, Z 사이즈가 모두 동일하면 방향별 사이징 불필요
            if (Math.Abs(xSizeMm - ySizeMm) < 0.001 &&
                Math.Abs(ySizeMm - zSizeMm) < 0.001)
            {
                if (log != null)
                    log.Add(string.Format("  {0}: X=Y=Z ({1:F2}mm) - 방향별 사이징 스킵",
                        body.Name ?? "Unnamed", xSizeMm));
                return 0;
            }

            // 바디의 에지 수집
            var edgeSet = new HashSet<DesignEdge>();
            foreach (DesignFace face in body.Faces)
            {
                foreach (DesignEdge de in face.Edges)
                    edgeSet.Add(de);
            }

            if (edgeSet.Count == 0)
            {
                if (log != null)
                    log.Add(string.Format("  {0}: 에지 없음", body.Name ?? "Unnamed"));
                return 0;
            }

            // 에지를 지배적 축 방향으로 분류
            var xEdges = new List<DesignEdge>();
            var yEdges = new List<DesignEdge>();
            var zEdges = new List<DesignEdge>();

            foreach (DesignEdge de in edgeSet)
            {
                double dx, dy, dz;
                if (!TryGetEdgeDirection(de, out dx, out dy, out dz))
                    continue;

                // 가장 큰 성분 축 = 에지의 지배적 방향
                if (dx >= dy && dx >= dz)
                    xEdges.Add(de);
                else if (dy >= dx && dy >= dz)
                    yEdges.Add(de);
                else
                    zEdges.Add(de);
            }

            int applied = 0;
            applied += ApplySizingToEdges(xEdges, xSizeMm, log, "X");
            applied += ApplySizingToEdges(yEdges, ySizeMm, log, "Y");
            applied += ApplySizingToEdges(zEdges, zSizeMm, log, "Z");

            if (log != null)
                log.Add(string.Format("  {0}: {1}/{2} 에지 사이징 완료 (X:{3}, Y:{4}, Z:{5})",
                    body.Name ?? "Unnamed", applied, edgeSet.Count,
                    xEdges.Count, yEdges.Count, zEdges.Count));

            return applied;
        }

        /// <summary>
        /// 에지의 chord 방향 성분 (시작점→끝점의 X/Y/Z 절대값)
        /// </summary>
        private static bool TryGetEdgeDirection(DesignEdge de,
            out double absDx, out double absDy, out double absDz)
        {
            absDx = absDy = absDz = 0;
            try
            {
                Edge edgeShape = de.Shape;
                Interval bounds = edgeShape.Bounds;
                Point startPt = edgeShape.Geometry.Evaluate(bounds.Start).Point;
                Point endPt = edgeShape.Geometry.Evaluate(bounds.End).Point;

                absDx = Math.Abs(endPt.X - startPt.X);
                absDy = Math.Abs(endPt.Y - startPt.Y);
                absDz = Math.Abs(endPt.Z - startPt.Z);
                return (absDx + absDy + absDz) > 1e-12;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 에지 그룹에 메쉬 사이징 적용 (CreateEdgeSizeControl Scripting API)
        /// </summary>
        private static int ApplySizingToEdges(List<DesignEdge> edges, double sizeMm,
            List<string> log, string axisName)
        {
            if (edges.Count == 0) return 0;

            try
            {
                var docObjects = edges.Cast<IDocObject>().ToArray();
                var selection = Selection.Create(docObjects);

                var options = new CreateEdgeSizeControlOptions();
                options.ElementSize = sizeMm / 1000.0; // mm → m

                CreateEdgeSizeControl.Execute(selection, options, null);

                if (log != null)
                    log.Add(string.Format("    {0}축: {1}개 에지 → {2:F2}mm",
                        axisName, edges.Count, sizeMm));
                return edges.Count;
            }
            catch (Exception ex)
            {
                if (log != null)
                    log.Add(string.Format("    {0}축 사이징 실패: {1}", axisName, ex.Message));
                return 0;
            }
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
