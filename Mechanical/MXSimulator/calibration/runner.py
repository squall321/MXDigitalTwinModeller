#!/usr/bin/env python3
# encoding: utf-8
"""
MX Material Twin - Unified Calibration Runner

Usage: python runner.py <input.json>
       (or) MaterialCalibrator.exe <input.json>

통합 실행기 - 모든 calibration type을 하나의 exe로 처리
"""
# @lat: [[material-calibrator#외부 Calibrator EXE#빌드]]

import sys
import os
import json


def main():
    """
    통합 calibration runner.

    Input JSON format:
    {
        "calibration_type": "elastic",  // "elastic", "plastic", "visco", "hyper", etc.
        "displacement": [...],
        "force": [...],
        "gauge_length": 50.0,
        "cross_section_area": 37.5,
        ...
    }

    Output: <input>_result.json
    """
    if len(sys.argv) < 2:
        print("Usage: runner.py <input.json>")
        print("  or")
        print("Usage: MaterialCalibrator.exe <input.json>")
        sys.exit(1)

    input_path = os.path.abspath(sys.argv[1])
    if not os.path.exists(input_path):
        print("ERROR: input file not found: {}".format(input_path))
        sys.exit(1)

    # Output path: same dir, _result suffix
    output_path = input_path.replace('.json', '_result.json')

    try:
        # Read input JSON
        with open(input_path, 'r', encoding='utf-8') as f:
            input_data = json.load(f)

        # Get calibration type
        calib_type = input_data.get('calibration_type', 'elastic')

        # Add calibration dir to path
        here = os.path.dirname(os.path.abspath(__file__))
        if here not in sys.path:
            sys.path.insert(0, here)

        # Route to appropriate calibrator
        if calib_type == 'elastic':
            result = run_elastic_calibration(input_data)
        elif calib_type == 'plastic':
            result = run_plastic_calibration(input_data)
        elif calib_type == 'visco':
            result = run_visco_calibration(input_data)
        elif calib_type == 'hyper':
            result = run_hyper_calibration(input_data)
        else:
            raise ValueError("Unknown calibration_type: {}".format(calib_type))

        # Write result JSON
        output = {
            'success': True,
            'calibration_type': calib_type,
            'result': result
        }

        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(output, f, indent=2)

        print("SUCCESS: {} calibration completed".format(calib_type))
        print("Result written to: {}".format(output_path))

    except Exception as ex:
        # Write error JSON
        import traceback
        output = {
            'success': False,
            'error': str(ex),
            'traceback': traceback.format_exc()
        }

        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(output, f, indent=2)

        print("ERROR: {}".format(str(ex)))
        sys.exit(1)


def run_elastic_calibration(input_data):
    """Phase 1A: Elastic calibration"""
    from elastic_calibrator import calibrate_elastic, suggest_material_name

    displacement = input_data['displacement']
    force = input_data['force']
    gauge_length = input_data['gauge_length']
    cross_section_area = input_data['cross_section_area']
    poisson_ratio = input_data.get('poisson_ratio', 0.3)
    density = input_data.get('density', 7850.0)
    max_elastic_strain = input_data.get('max_elastic_strain', 0.002)

    result = calibrate_elastic(
        displacement=displacement,
        force=force,
        gauge_length=gauge_length,
        cross_section_area=cross_section_area,
        max_elastic_strain=max_elastic_strain,
        poisson_ratio=poisson_ratio,
        density=density
    )

    # Add material suggestion
    result['suggested_material'] = suggest_material_name(result['E_modulus'], density)

    # Convert numpy arrays to lists for JSON serialization
    if 'strain' in result:
        result['strain'] = result['strain'].tolist()
    if 'stress' in result:
        result['stress'] = result['stress'].tolist()

    return result


def run_plastic_calibration(input_data):
    """Phase 1B: Plastic calibration (향후 구현)"""
    raise NotImplementedError("Plastic calibration: Phase 1B (coming soon)")


def run_visco_calibration(input_data):
    """Phase 3A: Viscoelastic calibration (향후 구현)"""
    raise NotImplementedError("Viscoelastic calibration: Phase 3A (coming soon)")


def run_hyper_calibration(input_data):
    """Phase 3B: Hyperelastic calibration (향후 구현)"""
    raise NotImplementedError("Hyperelastic calibration: Phase 3B (coming soon)")


if __name__ == '__main__':
    main()
