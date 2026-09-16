"""가짜 ansys.dpf.core — 딥 헬스 프로브 테스트용."""
import os
__version__ = "0.0-fake"


class _Server:
    version = "10.0-fake"
    ansys_path = "/fake/ansys/v252"


class server:
    @staticmethod
    def get_or_create_server(_):
        if os.environ.get("FAKE_DPF_NO_LICENSE"):
            raise RuntimeError("DPF premium license not available")
        return _Server()


class _Field:
    data = [[0.0, 0.0, 0.0]] * 81


class _Op:
    def eval(self):
        return [_Field()]


class _Results:
    def displacement(self):
        return _Op()


class Model:
    def __init__(self, path):
        self.results = _Results()
