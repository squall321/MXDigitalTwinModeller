"""
Elastic calibration module

Phase 1A: Young's modulus (E) determination from tensile test data
"""
# @lat: [[material-calibrator#외부 Calibrator EXE#핵심 캘리브레이션 로직]]

import numpy as np
from scipy.stats import linregress


def calibrate_elastic(displacement, force, gauge_length, cross_section_area,
                       max_elastic_strain=0.002, poisson_ratio=0.3, density=None):
    """
    Calibrate elastic material properties from tensile test data

    Phase 1A: Linear regression on elastic region (0 ~ 0.2% strain)

    Parameters:
    -----------
    displacement : list or ndarray
        Displacement [mm]
    force : list or ndarray
        Force [N]
    gauge_length : float
        Gauge length [mm]
    cross_section_area : float
        Cross-sectional area [mm²]
    max_elastic_strain : float, optional
        Maximum strain to consider for elastic regime (default: 0.002 = 0.2%)
    poisson_ratio : float, optional
        Poisson's ratio (default: 0.3, typical for steel)
        User can provide known value or use default
    density : float, optional
        Density [kg/m³] (e.g., 7850 for steel, 2700 for aluminum)
        If None, user must provide separately

    Returns:
    --------
    result : dict
        {
            'E_modulus': float [MPa],
            'poisson_ratio': float [-],
            'density': float [kg/m³] or None,
            'elastic_limit_stress': float [MPa],
            'r_squared': float,
            'num_points_used': int,
            'strain': ndarray,
            'stress': ndarray
        }

    Raises:
    -------
    ValueError
        If insufficient data points in elastic region
    """
    # Convert to numpy arrays
    displacement = np.array(displacement, dtype=float)
    force = np.array(force, dtype=float)

    # Calculate engineering strain and stress
    strain = displacement / gauge_length  # [-]
    stress = force / cross_section_area  # [MPa]

    # Filter elastic region (0 ~ max_elastic_strain)
    elastic_mask = (strain >= 0) & (strain <= max_elastic_strain)
    strain_elastic = strain[elastic_mask]
    stress_elastic = stress[elastic_mask]

    if len(strain_elastic) < 10:
        raise ValueError(
            f"Insufficient data points in elastic region (0~{max_elastic_strain*100:.2f}%): "
            f"{len(strain_elastic)} points (min 10 required)"
        )

    # Linear regression: stress = E * strain
    slope, intercept, r_value, p_value, std_err = linregress(strain_elastic, stress_elastic)

    E_modulus = slope  # [MPa]
    r_squared = r_value ** 2

    # Elastic limit (maximum stress in fitted region)
    elastic_limit_stress = stress_elastic[-1]

    # Validation
    if E_modulus < 1000:
        raise ValueError(
            f"Unrealistic Young's modulus: {E_modulus:.0f} MPa (too low). "
            f"Check units: displacement [mm], force [N], area [mm²]"
        )

    if E_modulus > 1e6:
        raise ValueError(
            f"Unrealistic Young's modulus: {E_modulus:.0f} MPa (too high). "
            f"Check units: displacement [mm], force [N], area [mm²]"
        )

    if r_squared < 0.95:
        print(f"[Warning] Low R² = {r_squared:.4f} in elastic region. "
              f"Data may be noisy or contain non-linear behavior.")

    return {
        'E_modulus': E_modulus,
        'poisson_ratio': poisson_ratio,
        'density': density,
        'elastic_limit_stress': elastic_limit_stress,
        'r_squared': r_squared,
        'num_points_used': len(strain_elastic),
        'strain': strain,
        'stress': stress
    }


def estimate_yield_stress(displacement, force, gauge_length, cross_section_area,
                           E_modulus, offset_strain=0.002):
    """
    Estimate yield stress using 0.2% offset method

    Phase 1B 준비: 항복 응력 추정

    Parameters:
    -----------
    offset_strain : float
        Offset strain for yield point (default: 0.002 = 0.2%)

    Returns:
    --------
    sigma_y : float
        Yield stress [MPa]
    """
    strain = np.array(displacement) / gauge_length
    stress = np.array(force) / cross_section_area

    # Offset line: stress = E * (strain - offset)
    offset_line = E_modulus * (strain - offset_strain)

    # Find intersection (first point where stress exceeds offset line)
    for i in range(len(strain)):
        if stress[i] >= offset_line[i]:
            # Linear interpolation
            if i > 0:
                # Between points i-1 and i
                s1, s2 = stress[i-1], stress[i]
                o1, o2 = offset_line[i-1], offset_line[i]
                # Interpolate
                sigma_y = s1 + (s2 - s1) * (o1 - o1) / (o2 - o1)
            else:
                sigma_y = stress[i]
            return sigma_y

    # No intersection found (data too short or no yielding)
    return stress[-1]


def suggest_material_name(E_modulus, density=None):
    """
    Suggest material name based on Young's modulus

    Helper function for UI

    Parameters:
    -----------
    E_modulus : float
        Young's modulus [MPa]
    density : float, optional
        Density [kg/m³]

    Returns:
    --------
    material_name : str
        Suggested material name
    """
    if E_modulus >= 180000 and E_modulus <= 220000:
        if density and 7700 <= density <= 8000:
            return "Structural Steel"
        return "Steel (generic)"
    elif E_modulus >= 60000 and E_modulus <= 80000:
        if density and 2600 <= density <= 2900:
            return "Aluminum Alloy"
        return "Aluminum (generic)"
    elif E_modulus >= 2000 and E_modulus <= 4000:
        if density and 1000 <= density <= 1300:
            return "ABS Plastic"
        return "Plastic (generic)"
    elif E_modulus < 1000:
        return "Rubber or Foam"
    else:
        return "Unknown Material"


# Unit test
if __name__ == '__main__':
    import sys
    import os

    # Add parent directory to path
    sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))

    from tests.synthetic_data import generate_elastic_tensile

    print("=== Elastic Calibrator Test ===\n")

    # Test case 1: Steel (E = 200 GPa)
    print("Test 1: Steel (E = 200000 MPa)")
    disp, force = generate_elastic_tensile(
        E=200000,
        gauge_length=50.0,
        cross_section_area=12.5 * 3.0,  # ASTM E8 subsize
        max_strain=0.002,
        num_points=50
    )

    result = calibrate_elastic(
        displacement=disp,
        force=force,
        gauge_length=50.0,
        cross_section_area=37.5,
        poisson_ratio=0.3,
        density=7850
    )

    print(f"  Input E: 200000 MPa")
    print(f"  Calibrated E: {result['E_modulus']:.0f} MPa")
    print(f"  Error: {abs(result['E_modulus'] - 200000) / 200000 * 100:.2f}%")
    print(f"  R²: {result['r_squared']:.6f}")
    print(f"  Points used: {result['num_points_used']}")
    print(f"  Suggested material: {suggest_material_name(result['E_modulus'], result['density'])}")

    # Validation
    error_percent = abs(result['E_modulus'] - 200000) / 200000 * 100
    assert error_percent < 1.0, f"E recovery error {error_percent:.2f}% > 1%"
    print("  ✅ PASS (error < 1%)\n")

    # Test case 2: Aluminum (E = 70 GPa)
    print("Test 2: Aluminum (E = 70000 MPa)")
    disp, force = generate_elastic_tensile(
        E=70000,
        gauge_length=50.0,
        cross_section_area=37.5,
        max_strain=0.002,
        num_points=50
    )

    result = calibrate_elastic(
        displacement=disp,
        force=force,
        gauge_length=50.0,
        cross_section_area=37.5,
        poisson_ratio=0.33,
        density=2700
    )

    print(f"  Input E: 70000 MPa")
    print(f"  Calibrated E: {result['E_modulus']:.0f} MPa")
    print(f"  Error: {abs(result['E_modulus'] - 70000) / 70000 * 100:.2f}%")
    print(f"  R²: {result['r_squared']:.6f}")
    print(f"  Suggested material: {suggest_material_name(result['E_modulus'], result['density'])}")

    error_percent = abs(result['E_modulus'] - 70000) / 70000 * 100
    assert error_percent < 1.0, f"E recovery error {error_percent:.2f}% > 1%"
    print("  ✅ PASS (error < 1%)\n")

    # Test case 3: Noisy data
    print("Test 3: Steel with noise (std = 50 N)")
    disp, force = generate_elastic_tensile(
        E=200000,
        gauge_length=50.0,
        cross_section_area=37.5,
        max_strain=0.002,
        num_points=100,
        noise_std=50.0  # ±50N noise
    )

    result = calibrate_elastic(
        displacement=disp,
        force=force,
        gauge_length=50.0,
        cross_section_area=37.5
    )

    print(f"  Input E: 200000 MPa")
    print(f"  Calibrated E: {result['E_modulus']:.0f} MPa")
    print(f"  Error: {abs(result['E_modulus'] - 200000) / 200000 * 100:.2f}%")
    print(f"  R²: {result['r_squared']:.6f}")

    error_percent = abs(result['E_modulus'] - 200000) / 200000 * 100
    assert error_percent < 5.0, f"E recovery error {error_percent:.2f}% > 5% (noisy data)"
    print("  ✅ PASS (error < 5% despite noise)\n")

    print("="*50)
    print("All tests passed! ✅")
    print("="*50)
