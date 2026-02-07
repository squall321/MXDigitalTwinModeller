using System;
using System.Collections.Generic;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Material
{
    /// <summary>
    /// 재료 물성 적용 서비스 (mm-tonne-s 단위계)
    /// </summary>
    public static class MaterialService
    {
        // ─── 재료 물성 캐시 (재료명 → mm-tonne-s 값 배열) ───
        // 다이얼로그 재오픈 시에도 값을 유지하기 위한 static 캐시
        private static readonly Dictionary<string, double[]> _materialCache
            = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 캐시에 재료 물성 저장
        /// </summary>
        public static void CacheMaterial(string materialName, double[] values)
        {
            _materialCache[materialName] = (double[])values.Clone();
        }

        /// <summary>
        /// 캐시에서 재료 물성 조회 (없으면 null)
        /// </summary>
        public static double[] GetCachedValues(string materialName)
        {
            double[] vals;
            if (_materialCache.TryGetValue(materialName, out vals))
                return (double[])vals.Clone();
            return null;
        }

        /// <summary>
        /// 바디의 현재 재료 이름과 밀도(mm-tonne-s) 읽기.
        /// 재료가 없으면 null 반환.
        /// </summary>
        public static string ReadBodyMaterialName(DesignBody body, out double densityMTS)
        {
            densityMTS = 0;
            try
            {
                var hasMat = body as IHasMaterial;
                if (hasMat == null || hasMat.Material == null)
                    return null;

                DocumentMaterial mat = hasMat.Material;
                // Density: SI(kg/m³) → mm-tonne-s(tonne/mm³) = ×1e-12
                densityMTS = mat.Density * 1e-12;
                return mat.Name;
            }
            catch
            {
                return null;
            }
        }

        // ─── mm-tonne-s 단위계 기본값 ───
        // Density: tonne/mm³ (steel = 7.85e-9)
        // E, G, σ: MPa
        // ν: dimensionless
        // CTE: 1/K
        // k (ThermalConductivity): mW/(mm·K) = W/(m·K) numerically
        // Cp (SpecificHeat): mJ/(tonne·K) = J/(kg·K) × 1e6

        public static readonly string[] PropertyNames = new[]
        {
            "Density",
            "ElasticModulus",
            "PoissonsRatio",
            "ShearModulus",
            "TensileStrength",
            "CTE",
            "ThermalConductivity",
            "SpecificHeat"
        };

        public static readonly string[] PropertyDisplayNames = new[]
        {
            "밀도 (Density)",
            "영률 (E)",
            "푸아송비 (ν)",
            "전단계수 (G)",
            "인장강도 (σ_u)",
            "열팽창계수 (CTE)",
            "열전도율 (k)",
            "비열 (Cp)"
        };

        public static readonly string[] PropertyUnits = new[]
        {
            "tonne/mm³",
            "MPa",
            "",
            "MPa",
            "MPa",
            "1/K",
            "mW/(mm·K)",
            "mJ/(tonne·K)"
        };

        /// <summary>
        /// 재료를 생성하고 바디에 적용
        /// </summary>
        public static int ApplyMaterial(Part part, string materialName,
            double density, double elasticModulus, double poissonsRatio,
            double shearModulus, double tensileStrength, double cte,
            double thermalConductivity, double specificHeat,
            ICollection<DesignBody> bodies, List<string> log)
        {
            if (bodies == null || bodies.Count == 0)
            {
                if (log != null) log.Add("적용할 바디가 없습니다.");
                return 0;
            }

            Document doc = part.Document;

            // 기존 DocumentMaterial이 있으면 재사용, 없으면 새로 생성
            DocumentMaterial docMat;
            if (!doc.Materials.TryGetValue(materialName, out docMat))
            {
                // 라이브러리에서 Steel 가져와서 복사 후 수정
                LibraryMaterial libMat = null;
                try
                {
                    libMat = LibraryMaterial.Library["Steel"];
                }
                catch { }

                if (libMat != null)
                {
                    docMat = DocumentMaterial.Copy(doc, libMat);
                    docMat.Name = materialName;
                }
                else
                {
                    // Steel이 없으면 아무거나 하나 가져와서 복사
                    foreach (var kvp in LibraryMaterial.Library)
                    {
                        docMat = DocumentMaterial.Copy(doc, kvp.Value);
                        docMat.Name = materialName;
                        break;
                    }
                }

                if (docMat == null)
                {
                    if (log != null) log.Add("재료 라이브러리에서 기본 재료를 찾을 수 없습니다.");
                    return 0;
                }

                if (log != null) log.Add(string.Format("재료 '{0}' 생성", materialName));
            }
            else
            {
                if (log != null) log.Add(string.Format("기존 재료 '{0}' 업데이트", materialName));
            }

            // mm-tonne-s 값을 캐시에 저장
            CacheMaterial(materialName, new double[]
            {
                density, elasticModulus, poissonsRatio, shearModulus,
                tensileStrength, cte, thermalConductivity, specificHeat
            });

            // mm-tonne-s → SI 변환 후 물성 설정
            // Density: tonne/mm³ → kg/m³ (×1e12)
            double densitySI = density * 1e12;
            docMat.SetProperty(new MaterialProperty(
                MaterialPropertyId.Density, "Density", densitySI, "kg/m^3"));

            // ElasticModulus: MPa → Pa (×1e6)
            double eSI = elasticModulus * 1e6;
            docMat.SetProperty(new MaterialProperty(
                MaterialPropertyId.ElasticModulus, "Young's Modulus", eSI, "Pa"));

            // PoissonsRatio: dimensionless
            docMat.SetProperty(new MaterialProperty(
                MaterialPropertyId.PoissonsRatio, "Poisson's Ratio", poissonsRatio, ""));

            // ShearModulus: MPa → Pa (×1e6)
            if (shearModulus > 0)
            {
                double gSI = shearModulus * 1e6;
                docMat.SetProperty(new MaterialProperty(
                    MaterialPropertyId.ShearModulus, "Shear Modulus", gSI, "Pa"));
            }

            // TensileStrength: MPa → Pa (×1e6)
            if (tensileStrength > 0)
            {
                double sigmaSI = tensileStrength * 1e6;
                docMat.SetProperty(new MaterialProperty(
                    MaterialPropertyId.TensileStrength, "Tensile Strength", sigmaSI, "Pa"));
            }

            // CTE: 1/K (no conversion)
            if (cte > 0)
            {
                docMat.SetProperty(new MaterialProperty(
                    "General.ThermalExpansion.Isotropic.CTE",
                    "Coefficient of Thermal Expansion", cte, "1/C"));
            }

            // ThermalConductivity: mW/(mm·K) → W/(m·K) (numerically same)
            if (thermalConductivity > 0)
            {
                docMat.SetProperty(new MaterialProperty(
                    MaterialPropertyId.ThermalConductivity,
                    "Thermal Conductivity", thermalConductivity, "W/m-deg C"));
            }

            // SpecificHeat: mJ/(tonne·K) → J/(kg·K) (×1e-6)
            if (specificHeat > 0)
            {
                double cpSI = specificHeat * 1e-6;
                docMat.SetProperty(new MaterialProperty(
                    MaterialPropertyId.SpecificHeat,
                    "Specific Heat", cpSI, "J/kg-deg C"));
            }

            // 바디에 재료 적용
            int applied = 0;
            foreach (DesignBody body in bodies)
            {
                try
                {
                    var hasMat = body as IHasMaterial;
                    if (hasMat != null)
                    {
                        hasMat.Material = docMat;
                        applied++;
                        if (log != null)
                            log.Add(string.Format("  {0}: 재료 적용 완료", body.Name ?? "Unnamed"));
                    }
                }
                catch (Exception ex)
                {
                    if (log != null)
                        log.Add(string.Format("  {0}: 적용 실패 - {1}",
                            body.Name ?? "Unnamed", ex.Message));
                }
            }

            if (log != null)
                log.Add(string.Format("\n총 {0}/{1} 바디에 재료 적용 완료", applied, bodies.Count));

            return applied;
        }

        /// <summary>
        /// Part의 모든 DesignBody 수집 (컴포넌트 포함)
        /// </summary>
        public static List<DesignBody> GetAllDesignBodies(Part rootPart)
        {
            var result = new List<DesignBody>();
            CollectBodies(rootPart, result);
            return result;
        }

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

        /// <summary>
        /// Steel (mm-tonne-s) 기본값
        /// </summary>
        public static double[] GetSteelDefaults()
        {
            return new double[]
            {
                7.85e-9,    // Density (tonne/mm³)
                210000,     // E (MPa)
                0.3,        // ν
                80769,      // G (MPa) ≈ E/(2(1+ν))
                400,        // σ_u (MPa)
                1.2e-5,     // CTE (1/K)
                50,         // k (mW/(mm·K))
                4.34e8      // Cp (mJ/(tonne·K)) = 434 J/(kg·K) × 1e6
            };
        }

        /// <summary>
        /// Aluminum (mm-tonne-s) 기본값
        /// </summary>
        public static double[] GetAluminumDefaults()
        {
            return new double[]
            {
                2.7e-9,     // Density
                69000,      // E
                0.33,       // ν
                25940,      // G
                310,        // σ_u
                2.3e-5,     // CTE
                237,        // k
                9.0e8       // Cp = 900 × 1e6
            };
        }

        /// <summary>
        /// CFRP (mm-tonne-s) 기본값
        /// </summary>
        public static double[] GetCFRPDefaults()
        {
            return new double[]
            {
                1.6e-9,     // Density
                135000,     // E
                0.3,        // ν
                5000,       // G
                1500,       // σ_u
                0.5e-6,     // CTE
                7,          // k
                8.0e8       // Cp = 800 × 1e6
            };
        }
    }
}
