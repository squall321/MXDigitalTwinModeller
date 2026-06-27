using System;
using System.Collections.Generic;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer
{
    /// <summary>
    /// 얇고 긴 wall pair = slit (안테나 라인, 슬릿).
    /// 기본 기준:
    ///   (1) thickness ≤ MaxThicknessMm (절대 두께)
    ///   (2) aspect = sqrt(area) / T ≥ MinAspectRatio
    ///   (3) thickness &lt; minBboxDim * BboxThicknessRatio
    ///       → 본체 최단변 대비 충분히 얇아야 한다.
    ///         얇은 판 (e.g. 0.05mm × 200 × 100) 의 양면 wall 이
    ///         자기 자신을 slit 으로 오인식하는 것을 막는다.
    ///   (4) area &lt; (bboxX × bboxY × BboxCrossSectionRatio)
    ///       (bbox 의 정렬과 무관하게 가장 큰 두 차원의 곱 사용)
    ///       → slit 의 면적은 body 의 dominant cross-section 의 일부여야 한다.
    /// </summary>
    public class SlitDetector
    {
        /// <summary>
        /// Scale-invariant refactor with legacy floor:
        ///   effectiveMax = max(MaxThicknessFloorMm, MaxThicknessRatio × MinBboxDim).
        /// The floor preserves legacy behavior at phone/desktop scale (where
        /// 0.02 × 10mm = 0.2mm would otherwise wrongly filter out 0.5mm slits
        /// in a 100×50×10 box — spec 08_box_slit / RT16_add_slit regression).
        /// 기본 0.02 × MinBboxDim, floor 2.0mm:
        ///   - desktop phone-frame (minBboxDim ≈ 10mm)  → max(2.0, 0.2) = 2.0mm (legacy)
        ///   - 일반 desktop 부품 (minBboxDim ≈ 100mm)   → max(2.0, 2.0) = 2.0mm (legacy)
        ///   - 1m assembly (minBboxDim ≈ 1000mm)        → max(2.0, 20)  = 20mm (scales up)
        ///   - MEMS (minBboxDim ≈ 1mm)                  → max(2.0, 0.02)= 2.0mm (floor wins;
        ///     still gated by the BboxThicknessRatio gate below).
        /// </summary>
        public double MaxThicknessFloorMm { get; set; } = 2.0;
        public double MaxThicknessRatio { get; set; } = 0.02;
        public double MinAspectRatio { get; set; } = 5.0;

        /// <summary>thickness 가 minBboxDim 의 이 비율보다 작아야 slit. (이미 ratio)</summary>
        public double BboxThicknessRatio { get; set; } = 0.5;

        /// <summary>slit 면적이 dominant cross-section 의 이 비율보다 작아야 한다. (이미 ratio)</summary>
        public double BboxCrossSectionRatio { get; set; } = 0.25;

        public List<SlitFeature> Detect(
            List<WallFeature> walls,
            List<FaceFeature> faces,
            double[] bboxSizeMm,
            ModelScale.Scale scale = null)
        {
            if (scale == null) scale = ModelScale.ComputeFromBboxSize(bboxSizeMm);
            double effectiveMaxThicknessMm = Math.Max(
                MaxThicknessFloorMm,
                MaxThicknessRatio * scale.MinBboxDimMm);
            var slits = new List<SlitFeature>();
            int slitId = 1;

            // bbox 기반 게이트 값을 미리 계산 (bbox 가 없으면 무한대 → 게이트 무시)
            double minBboxDim = double.PositiveInfinity;
            double dominantCrossSectionMm2 = double.PositiveInfinity;
            if (bboxSizeMm != null && bboxSizeMm.Length >= 3)
            {
                double bx = bboxSizeMm[0];
                double by = bboxSizeMm[1];
                double bz = bboxSizeMm[2];
                minBboxDim = Math.Min(bx, Math.Min(by, bz));

                // 가장 큰 두 차원의 곱 = body 의 dominant cross-section.
                // (어느 축이 두께 축인지 미리 알 수 없으므로 축 정렬과 무관한 metric.)
                double maxDim = Math.Max(bx, Math.Max(by, bz));
                double midDim;
                if ((bx >= by && bx <= bz) || (bx <= by && bx >= bz))
                    midDim = bx;
                else if ((by >= bx && by <= bz) || (by <= bx && by >= bz))
                    midDim = by;
                else
                    midDim = bz;
                dominantCrossSectionMm2 = maxDim * midDim;
            }

            foreach (var w in walls)
            {
                // Gate 1: 절대 두께 (scale-invariant; effectiveMaxThicknessMm = ratio × MinBboxDim)
                if (w.ThicknessMm > effectiveMaxThicknessMm) continue;

                double areaMm2 = w.AreaMm2;
                if (areaMm2 <= 0) continue;

                // Gate 2: aspect ratio
                // 가정: 직사각형 face (긴 변 L, 짧은 변 W). area = L*W.
                // 단순화: aspect = sqrt(area) / thickness — slit 이 정사각 라인 모양일 때만 정확
                double aspect = Math.Sqrt(areaMm2) / w.ThicknessMm;
                if (aspect < MinAspectRatio) continue;

                // Gate 3: thin-plate-as-slit 차단.
                // body 의 최단변보다 충분히 얇아야 (BboxThicknessRatio = 0.5 → 절반 미만)
                // slit 자격이 있다. 0.05mm × 200 × 100 같은 thin-plate 의 top/bottom
                // wall (t=0.05, minBboxDim=0.05) 은 0.05 < 0.025 = false 로 막힌다.
                if (w.ThicknessMm >= minBboxDim * BboxThicknessRatio) continue;

                // Gate 4: slit 면적이 body dominant cross-section 의 일부여야 한다.
                // 예: 200×100 plate (cross=20000mm²) 에서 W1 area ≈ 20000 → 20000 < 5000 false 차단.
                //     반면 박스 100×50×10 에 5mm 슬릿 area=300 은 cross=5000, 300 < 1250 통과.
                if (areaMm2 >= dominantCrossSectionMm2 * BboxCrossSectionRatio) continue;

                var slit = new SlitFeature
                {
                    Id = "SL" + slitId,
                    FaceA = w.FaceA,
                    FaceB = w.FaceB,
                    WidthMm = w.ThicknessMm,
                    LengthMm = Math.Sqrt(areaMm2),  // 근사
                    AspectRatio = aspect,
                    LongAxis = new[] { 0.0, 0.0, 0.0 },  // Stage 1 에서는 미산출
                };

                slits.Add(slit);
                slitId++;
            }

            return slits;
        }
    }
}
