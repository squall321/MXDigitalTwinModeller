"""
Synthetic data generator for calibration validation

Phase 1A: Elastic tensile test data generation
"""

import numpy as np


def generate_elastic_tensile(E=200000, nu=0.3, rho=7850,
                              gauge_length=50.0, cross_section_area=37.5,
                              max_strain=0.005, num_points=100,
                              noise_std=0.0):
    """
    Generate synthetic tensile test data (elastic regime only)

    Phase 1A 검증용: 선형 탄성 응력-변형률 관계

    Parameters:
    -----------
    E : float
        Young's modulus [MPa]
    nu : float
        Poisson's ratio [-]
    rho : float
        Density [kg/m³]
    gauge_length : float
        Gauge length [mm]
    cross_section_area : float
        Cross-sectional area [mm²]
    max_strain : float
        Maximum engineering strain [-]
    num_points : int
        Number of data points
    noise_std : float
        Standard deviation of Gaussian noise on force [N]

    Returns:
    --------
    displacement : ndarray
        Displacement [mm], shape (num_points,)
    force : ndarray
        Force [N], shape (num_points,)
    """
    # Engineering strain (0 to max_strain)
    strain = np.linspace(0, max_strain, num_points)

    # Hooke's law: σ = E * ε
    stress = E * strain  # [MPa]

    # Force = σ * A
    force = stress * cross_section_area  # [N]

    # Displacement = ε * L0
    displacement = strain * gauge_length  # [mm]

    # Add noise if requested
    if noise_std > 0:
        noise = np.random.normal(0, noise_std, num_points)
        force = force + noise

    return displacement, force


def generate_bilinear_plastic_tensile(E=200000, sigma_y=250, Et=5000,
                                       gauge_length=50.0, cross_section_area=37.5,
                                       max_strain=0.05, num_points=200,
                                       noise_std=0.0):
    """
    Generate synthetic tensile test data with bilinear plasticity

    Phase 1B 검증용: 탄성 + 소성 경화

    Parameters:
    -----------
    E : float
        Young's modulus [MPa]
    sigma_y : float
        Yield stress [MPa]
    Et : float
        Tangent modulus (plastic hardening slope) [MPa]
    max_strain : float
        Maximum engineering strain [-]

    Returns:
    --------
    displacement : ndarray
        Displacement [mm]
    force : ndarray
        Force [N]
    """
    strain = np.linspace(0, max_strain, num_points)
    stress = np.zeros_like(strain)

    # Yield strain
    epsilon_y = sigma_y / E

    for i, eps in enumerate(strain):
        if eps <= epsilon_y:
            # Elastic regime
            stress[i] = E * eps
        else:
            # Plastic regime (bilinear kinematic hardening)
            stress[i] = sigma_y + Et * (eps - epsilon_y)

    force = stress * cross_section_area
    displacement = strain * gauge_length

    if noise_std > 0:
        noise = np.random.normal(0, noise_std, num_points)
        force = force + noise

    return displacement, force


def save_csv(displacement, force, filename):
    """
    Save tensile test data to CSV file

    Format:
    displacement_mm,force_N
    0.0,0.0
    0.01,75.0
    ...

    Parameters:
    -----------
    displacement : ndarray
        Displacement [mm]
    force : ndarray
        Force [N]
    filename : str
        Output CSV file path
    """
    import csv

    with open(filename, 'w', newline='', encoding='utf-8') as f:
        writer = csv.writer(f)
        writer.writerow(['displacement_mm', 'force_N'])
        for d, f_val in zip(displacement, force):
            writer.writerow([f'{d:.6f}', f'{f_val:.6f}'])


# Example usage / Unit test
if __name__ == '__main__':
    print("=== Phase 1A: Elastic Calibration Test ===")

    # Test case: Steel (E=200 GPa)
    disp, force = generate_elastic_tensile(
        E=200000,  # MPa
        gauge_length=50.0,  # mm (ASTM E8 standard)
        cross_section_area=12.5 * 3.0,  # 12.5mm width × 3mm thickness
        max_strain=0.002,  # 0.2% strain (elastic limit)
        num_points=50
    )

    # Save to CSV
    save_csv(disp, force, 'test_elastic_steel.csv')
    print(f"Generated elastic tensile data:")
    print(f"  Displacement range: {disp[0]:.4f} ~ {disp[-1]:.4f} mm")
    print(f"  Force range: {force[0]:.2f} ~ {force[-1]:.2f} N")
    print(f"  Saved to: test_elastic_steel.csv")

    # Verify linear regression will recover E
    # Stress = Force / Area
    A0 = 12.5 * 3.0  # mm²
    stress = force / A0  # MPa
    strain = disp / 50.0  # -

    # Linear fit (should recover E = 200000 MPa)
    from scipy.stats import linregress
    slope, intercept, r_value, p_value, std_err = linregress(strain, stress)
    print(f"\n  Linear regression:")
    print(f"    E (recovered) = {slope:.0f} MPa")
    print(f"    Expected E = 200000 MPa")
    print(f"    Error = {abs(slope - 200000) / 200000 * 100:.2f}%")
    print(f"    R² = {r_value**2:.6f}")

    assert abs(slope - 200000) / 200000 < 0.01, "E recovery error > 1%"
    print("\n✅ Phase 1A validation: PASS (E within ±1%)")
