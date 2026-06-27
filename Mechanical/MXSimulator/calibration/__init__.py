"""
Material Twin - Calibration Module

Inverse identification and parameter optimization for ANSYS/LS-DYNA material models.

Submodules:
- elastic_calibrator: Young's modulus, Poisson's ratio from tensile test
- plastic_calibrator: Yield stress, tangent modulus (BISO/BKIN)
- visco_calibrator: Prony series from DMA data
- material_models: Hyperelastic, viscoelastic model implementations
- utils: CSV parsing, data validation, result formatting
"""

__version__ = "0.1.0"
__author__ = "MX Digital Twin Team"

# Phase 1A: Elastic calibration
from .elastic_calibrator import calibrate_elastic, estimate_yield_stress, suggest_material_name

__all__ = [
    'calibrate_elastic',
    'estimate_yield_stress',
    'suggest_material_name'
]
