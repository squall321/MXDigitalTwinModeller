#!/usr/bin/env python3
# encoding: utf-8
"""
Elastic Calibration Runner (venv Python)

Usage: python runner_elastic.py <input.json>

IronPython에서 System.Diagnostics.Process.Start()로 호출됩니다.
(postprocess/runner.py와 동일한 패턴)
"""

import sys
import os
import json


def main():
    """
    Command-line interface for elastic calibration.

    Input: JSON file path (displacement, force, params)
    Output: JSON file (same dir, _result.json)
    """
    if len(sys.argv) < 2:
        print("Usage: runner_elastic.py <input.json>")
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

        displacement = input_data['displacement']
        force = input_data['force']
        gauge_length = input_data['gauge_length']
        cross_section_area = input_data['cross_section_area']
        poisson_ratio = input_data.get('poisson_ratio', 0.3)
        density = input_data.get('density', 7850.0)
        max_elastic_strain = input_data.get('max_elastic_strain', 0.002)

        # Add calibration dir to path
        here = os.path.dirname(os.path.abspath(__file__))
        if here not in sys.path:
            sys.path.insert(0, here)

        # Import calibrator (only works in CPython with scipy)
        from elastic_calibrator import calibrate_elastic, suggest_material_name

        # Run calibration
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

        # Write result JSON
        output = {
            'success': True,
            'result': result
        }

        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(output, f, indent=2)

        print("SUCCESS: Result written to {}".format(output_path))

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


if __name__ == '__main__':
    main()
