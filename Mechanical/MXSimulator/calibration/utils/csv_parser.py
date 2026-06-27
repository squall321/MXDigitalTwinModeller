"""
CSV parser for experimental data

Phase 1A: Tensile test data (displacement, force)
"""

import csv


def parse_tensile_csv(file_path):
    """
    Parse tensile test CSV file

    Expected format:
    displacement_mm,force_N
    0.0,0.0
    0.05,187.5
    ...

    Alternative headers supported:
    - disp_mm, load_N
    - displacement, force
    - extension_mm, load_N

    Parameters:
    -----------
    file_path : str
        Path to CSV file

    Returns:
    --------
    displacement : list of float
        Displacement [mm]
    force : list of float
        Force [N]

    Raises:
    -------
    ValueError
        If file format is invalid or insufficient data points
    """
    displacement = []
    force = []

    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            reader = csv.reader(f)

            # Read header
            header = next(reader)
            if len(header) < 2:
                raise ValueError("CSV must have at least 2 columns")

            # Detect column indices
            disp_col, force_col = detect_columns(header)

            # Read data
            for row in reader:
                if len(row) < 2:
                    continue  # Skip empty rows

                try:
                    d = float(row[disp_col])
                    f = float(row[force_col])
                    displacement.append(d)
                    force.append(f)
                except (ValueError, IndexError):
                    # Skip invalid rows
                    continue

        # Validation
        if len(displacement) < 10:
            raise ValueError(f"Insufficient data points: {len(displacement)} (min 10 required)")

        return displacement, force

    except FileNotFoundError:
        raise ValueError(f"File not found: {file_path}")
    except Exception as e:
        raise ValueError(f"Failed to parse CSV: {e}")


def detect_columns(header):
    """
    Detect displacement and force columns from header

    Parameters:
    -----------
    header : list of str
        CSV header row

    Returns:
    --------
    disp_col : int
        Displacement column index
    force_col : int
        Force column index
    """
    # Normalize header (lowercase, strip whitespace)
    norm_header = [h.lower().strip() for h in header]

    # Displacement column keywords
    disp_keywords = [
        'displacement_mm', 'disp_mm', 'displacement', 'extension_mm',
        'extension', 'disp', 'delta', 'elongation'
    ]

    # Force column keywords (avoid single-letter matches to prevent false positives)
    force_keywords = [
        'force_n', 'load_n', 'force', 'load'
    ]

    # Find displacement column
    disp_col = None
    for i, col_name in enumerate(norm_header):
        if any(keyword in col_name for keyword in disp_keywords):
            disp_col = i
            break

    # Find force column
    force_col = None
    for i, col_name in enumerate(norm_header):
        if any(keyword in col_name for keyword in force_keywords):
            force_col = i
            break

    # Fallback: use first two columns
    if disp_col is None:
        disp_col = 0
    if force_col is None:
        force_col = 1

    return disp_col, force_col


def parse_dma_csv(file_path):
    """
    Parse DMA (Dynamic Mechanical Analysis) CSV file

    Phase 3A용

    Expected format:
    frequency_hz,storage_modulus_mpa,loss_modulus_mpa
    0.1,2500,150
    1.0,2800,200
    ...

    Returns:
    --------
    frequency : list of float
        Frequency [Hz]
    E_prime : list of float
        Storage modulus E' [MPa]
    E_double_prime : list of float
        Loss modulus E'' [MPa]
    """
    frequency = []
    E_prime = []
    E_double_prime = []

    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            reader = csv.reader(f)

            # Skip header
            next(reader)

            # Read data (assume columns: freq, E', E'')
            for row in reader:
                if len(row) < 3:
                    continue

                try:
                    freq = float(row[0])
                    ep = float(row[1])
                    edp = float(row[2])
                    frequency.append(freq)
                    E_prime.append(ep)
                    E_double_prime.append(edp)
                except (ValueError, IndexError):
                    continue

        if len(frequency) < 5:
            raise ValueError(f"Insufficient DMA data points: {len(frequency)} (min 5)")

        return frequency, E_prime, E_double_prime

    except Exception as e:
        raise ValueError(f"Failed to parse DMA CSV: {e}")


# Unit test
if __name__ == '__main__':
    import os
    import sys

    # Add parent directory to path
    sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '..'))

    from calibration.tests.synthetic_data import generate_elastic_tensile, save_csv

    print("=== CSV Parser Test ===")

    # Generate test data
    disp, force = generate_elastic_tensile(E=200000, max_strain=0.002, num_points=50)
    save_csv(disp, force, 'temp_test.csv')

    # Parse it back
    disp_parsed, force_parsed = parse_tensile_csv('temp_test.csv')

    print(f"Original data points: {len(disp)}")
    print(f"Parsed data points: {len(disp_parsed)}")
    print(f"Displacement range: {disp_parsed[0]:.6f} ~ {disp_parsed[-1]:.6f} mm")
    print(f"Force range: {force_parsed[0]:.6f} ~ {force_parsed[-1]:.6f} N")

    # Verify
    assert len(disp_parsed) == len(disp), "Data count mismatch"

    # Check values (use relative error for force since it can be large)
    disp_error = abs(disp_parsed[-1] - disp[-1])
    force_error_rel = abs(force_parsed[-1] - force[-1]) / max(abs(force[-1]), 1e-6)

    print(f"Displacement[-1]: expected={disp[-1]:.6f}, parsed={disp_parsed[-1]:.6f}, error={disp_error:.9f}")
    print(f"Force[-1]: expected={force[-1]:.6f}, parsed={force_parsed[-1]:.6f}, rel_error={force_error_rel:.6f}")

    assert disp_error < 1e-5, f"Displacement mismatch: {disp_error}"
    assert force_error_rel < 0.01, f"Force mismatch: {force_error_rel*100:.2f}%"

    print("✅ CSV parser test: PASS")

    # Cleanup
    os.remove('temp_test.csv')
