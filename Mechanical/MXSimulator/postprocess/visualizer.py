# encoding: utf-8
"""
MX Post-Process Viewer — PyQt5 + matplotlib GUI
"""
import os
import sys
import numpy as np

from PyQt5.QtWidgets import (
    QMainWindow, QWidget, QVBoxLayout, QHBoxLayout, QTabWidget,
    QTableWidget, QTableWidgetItem, QComboBox, QPushButton, QLabel,
    QCheckBox, QFileDialog, QHeaderView, QAbstractItemView, QSplitter,
    QSizePolicy, QGroupBox, QLineEdit,
)
from PyQt5.QtCore import Qt
from PyQt5.QtGui import QColor, QFont

from matplotlib.backends.backend_qt5agg import FigureCanvasQTAgg as FigureCanvas
from matplotlib.backends.backend_qt5agg import NavigationToolbar2QT as NavToolbar
import matplotlib.pyplot as plt
import matplotlib.ticker as ticker

from analyzer import (
    parse_ansys_export, compute_fft, compute_frf, coherence,
    extract_modal_params, rms, peak_to_peak,
    rainflow_count, damage_miner,
)

COLORS = plt.rcParams['axes.prop_cycle'].by_key()['color']


# ── Shared matplotlib canvas widget ──────────────────────────────────
class PlotWidget(QWidget):
    def __init__(self, nrows=1, ncols=1, height_ratios=None, parent=None):
        super().__init__(parent)
        kw = {}
        if height_ratios:
            kw['gridspec_kw'] = {'height_ratios': height_ratios}
        self.fig, axes = plt.subplots(nrows, ncols, figsize=(8, 5), **kw)
        self.fig.set_tight_layout(True)
        if nrows == 1 and ncols == 1:
            self.axes = [axes]
        elif nrows == 1 or ncols == 1:
            self.axes = list(axes)
        else:
            self.axes = [ax for row in axes for ax in row]
        self.canvas = FigureCanvas(self.fig)
        self.nav = NavToolbar(self.canvas, self)
        lay = QVBoxLayout(self)
        lay.setContentsMargins(0, 0, 0, 0)
        lay.addWidget(self.nav)
        lay.addWidget(self.canvas)

    def draw(self):
        self.fig.tight_layout()
        self.canvas.draw_idle()

    def export_png(self, path, dpi=150):
        self.fig.savefig(path, dpi=dpi, bbox_inches='tight')


# ── Helper: body combo + overlay checkbox ────────────────────────────
def make_body_ctrl(bodies, on_change, on_overlay):
    row = QHBoxLayout()
    row.addWidget(QLabel("Body:"))
    cb = QComboBox()
    for b in bodies:
        cb.addItem(b['name'])
    cb.currentIndexChanged.connect(on_change)
    row.addWidget(cb)
    ovl = QCheckBox("Overlay all")
    ovl.stateChanged.connect(on_overlay)
    row.addWidget(ovl)
    row.addStretch()
    return row, cb, ovl


# ── Tab 1: Summary ───────────────────────────────────────────────────
class SummaryTab(QWidget):
    def __init__(self, meta, base_dir, parent=None):
        super().__init__(parent)
        self.meta = meta
        self.base_dir = base_dir
        lay = QVBoxLayout(self)

        # Header
        hdr = QLabel(
            "Analysis: <b>{}</b>  |  Operating freq: <b>{} Hz</b>".format(
                meta.get('analysis', '?'),
                meta.get('operating_freq_hz', '?')
            )
        )
        hdr.setTextFormat(Qt.RichText)
        hdr.setStyleSheet("font-size:12px; padding:6px;")
        lay.addWidget(hdr)

        # Thresholds
        thr_red    = float(meta.get('thresh_red_mm',    0.3))
        thr_yellow = float(meta.get('thresh_yellow_mm', 0.1))

        # Table
        bodies = meta.get('bodies', [])
        # StrainE = body 총 변형에너지 (Result.Total, schema 2.1+). basis 가 Total 이 아닌 값
        # (구버전 metadata 의 최대-요소값 / 폴백) 은 '*' 를 붙여 총합으로 읽히지 않게 한다.
        cols = ['Rank', 'Body / NS', 'MaxDef [mm]', 'MaxVM [MPa]',
                'P2P [mm]', 'RMS [mm]', 'StrainE Total [mJ]', 'Severity']
        self.table = QTableWidget(len(bodies), len(cols))
        self.table.setHorizontalHeaderLabels(cols)
        self.table.horizontalHeader().setSectionResizeMode(1, QHeaderView.Stretch)
        self.table.setSelectionBehavior(QAbstractItemView.SelectRows)
        self.table.setEditTriggers(QAbstractItemView.NoEditTriggers)
        self.table.setAlternatingRowColors(True)

        for i, b in enumerate(bodies):
            max_def = float(b.get('max_def', 0))
            max_vm  = float(b.get('max_vm',  0))

            # Try to compute P2P / RMS from CSV
            p2p_val = rms_val = float('nan')
            csv_path = os.path.join(base_dir, b.get('csv', ''))
            if os.path.exists(csv_path):
                try:
                    _, v = parse_ansys_export(csv_path)
                    if len(v):
                        p2p_val = peak_to_peak(v)
                        rms_val = rms(v)
                except Exception:
                    pass

            if max_def >= thr_red:
                sev = '🔴 Critical'
                bg  = QColor(255, 180, 180)
            elif max_def >= thr_yellow:
                sev = '🟡 Warning'
                bg  = QColor(255, 250, 180)
            else:
                sev = '🟢 OK'
                bg  = QColor(200, 240, 200)

            se = b.get('strain_energy')
            se_basis = b.get('strain_energy_basis')
            if se is None:
                se_str = '—'
            elif se_basis == 'Total':
                se_str = '{:.3g}'.format(float(se))
            else:
                se_str = '{:.3g} *'.format(float(se))   # 총합이 아님 (basis != Total)
            vals = [
                str(b.get('rank', i + 1)),
                b.get('name', '?'),
                '{:.4f}'.format(max_def),
                '{:.2f}'.format(max_vm),
                '{:.4f}'.format(p2p_val) if not np.isnan(p2p_val) else '—',
                '{:.4f}'.format(rms_val) if not np.isnan(rms_val) else '—',
                se_str,
                sev,
            ]
            for j, val in enumerate(vals):
                item = QTableWidgetItem(val)
                item.setBackground(bg)
                item.setTextAlignment(Qt.AlignCenter)
                self.table.setItem(i, j, item)
            if se is not None and se_basis != 'Total':
                self.table.item(i, 6).setToolTip(
                    "basis = {0}: not a body total (max single-element value / legacy export). "
                    "Re-export with a build that records Result.Total.".format(se_basis))

        lay.addWidget(self.table)

        # Export button
        btn_row = QHBoxLayout()
        btn_png = QPushButton("Export table PNG")
        btn_png.clicked.connect(self._export_png)
        btn_row.addWidget(btn_png)
        btn_row.addStretch()
        lay.addLayout(btn_row)

    def _export_png(self):
        path, _ = QFileDialog.getSaveFileName(self, "Save PNG", "summary.png", "PNG (*.png)")
        if path:
            # Grab widget as pixmap
            pm = self.table.grab()
            pm.save(path)


# ── Tab 2: Time History ───────────────────────────────────────────────
class TimeHistTab(QWidget):
    def __init__(self, meta, base_dir, parent=None):
        super().__init__(parent)
        self.meta = meta
        self.base_dir = base_dir
        self._load_data()

        lay = QVBoxLayout(self)
        ctrl, self.body_cb, self.overlay_cb = make_body_ctrl(
            meta.get('bodies', []), self._update, self._update)
        lay.addLayout(ctrl)

        self.pw = PlotWidget(nrows=2, ncols=1, height_ratios=[3, 1])
        lay.addWidget(self.pw)

        btn_row = QHBoxLayout()
        b = QPushButton("Export PNG")
        b.clicked.connect(lambda: self._save_png())
        btn_row.addWidget(b); btn_row.addStretch()
        lay.addLayout(btn_row)

        self._update()

    def _load_data(self):
        self.body_data = {}
        for b in self.meta.get('bodies', []):
            p = os.path.join(self.base_dir, b.get('csv', ''))
            if os.path.exists(p):
                try:
                    t, v = parse_ansys_export(p)
                    self.body_data[b['name']] = (t, v)
                except Exception:
                    pass
        fc = self.meta.get('force_csv', '')
        self.force_data = None
        if fc and os.path.exists(fc):
            try:
                self.force_data = parse_ansys_export(fc)
            except Exception:
                pass

    def _update(self, *_):
        ax_d, ax_f = self.pw.axes[0], self.pw.axes[1]
        ax_d.cla(); ax_f.cla()

        if self.overlay_cb.isChecked():
            for ci, (name, (t, v)) in enumerate(self.body_data.items()):
                ax_d.plot(t, v, lw=1, color=COLORS[ci % len(COLORS)], label=name)
            ax_d.legend(fontsize=7, loc='upper right')
            ax_d.set_title("Time History — all bodies")
        else:
            name = self.body_cb.currentText()
            if name in self.body_data:
                t, v = self.body_data[name]
                r = rms(v); p2p = peak_to_peak(v)
                ax_d.plot(t, v, 'b-', lw=1)
                ax_d.axhline(r,  color='orange', ls='--', lw=1,
                             label='RMS = {:.4f} mm'.format(r))
                ax_d.axhline(-r, color='orange', ls='--', lw=1)
                ax_d.legend(fontsize=8)
                ax_d.set_title(
                    '{} | P2P = {:.4f} mm, RMS = {:.4f} mm'.format(name, p2p, r),
                    fontsize=9)

        ax_d.set_ylabel('Total Deformation [mm]')
        ax_d.grid(True, alpha=0.3)

        if self.force_data is not None:
            tf, ff = self.force_data
            ax_f.plot(tf, ff, 'r-', lw=1)
        ax_f.set_xlabel('Time [s]')
        ax_f.set_ylabel('Force [N]')
        ax_f.grid(True, alpha=0.3)
        self.pw.draw()

    def _save_png(self):
        p, _ = QFileDialog.getSaveFileName(self, "Save PNG", "time_history.png", "PNG (*.png)")
        if p:
            self.pw.export_png(p)


# ── Tab: Fatigue (rainflow + Miner) ──────────────────────────────────
class FatigueTab(QWidget):
    """Rainflow cycle histogram + Miner's-rule cumulative damage for a body's response
    time-history. The signal is the same deformation CSV the other tabs use; treat its
    amplitude as the fatigue-driving quantity and apply a power-law S-N curve N = A*S^-m.
    Defaults are placeholders — the user enters the real S-N constants + a stress-scale."""

    def __init__(self, meta, base_dir, parent=None):
        super().__init__(parent)
        self.meta = meta
        self.base_dir = base_dir
        self._load_data()

        lay = QVBoxLayout(self)

        # body selector
        row = QHBoxLayout()
        row.addWidget(QLabel("Body:"))
        self.body_cb = QComboBox()
        for b in meta.get('bodies', []):
            self.body_cb.addItem(b['name'])
        self.body_cb.currentIndexChanged.connect(self._update)
        row.addWidget(self.body_cb)
        row.addStretch()
        lay.addLayout(row)

        # S-N / scaling controls
        p = QGroupBox("S-N curve  N = A · S^(−m)   and signal→stress scale")
        pr = QHBoxLayout(p)
        def _field(label, default, w=80):
            pr.addWidget(QLabel(label))
            e = QLineEdit(default); e.setMaximumWidth(w)
            e.editingFinished.connect(self._update)
            pr.addWidget(e); return e
        self.scale_tb = _field("scale (stress/unit):", "1.0")
        self.A_tb     = _field("A:", "1e12")
        self.m_tb     = _field("m:", "3.0")
        self.endur_tb = _field("endurance:", "0.0")
        self.bins_tb  = _field("bins:", "16", 50)
        pr.addStretch()
        lay.addWidget(p)

        self.pw = PlotWidget(nrows=1, ncols=1)
        lay.addWidget(self.pw)

        self.summary_lbl = QLabel("")
        f = QFont(); f.setBold(True)
        self.summary_lbl.setFont(f)
        lay.addWidget(self.summary_lbl)

        btn_row = QHBoxLayout()
        b = QPushButton("Export PNG")
        b.clicked.connect(self._save_png)
        btn_row.addWidget(b); btn_row.addStretch()
        lay.addLayout(btn_row)

        self._update()

    def _load_data(self):
        self.body_data = {}
        for b in self.meta.get('bodies', []):
            p = os.path.join(self.base_dir, b.get('csv', ''))
            if os.path.exists(p):
                try:
                    t, v = parse_ansys_export(p)
                    self.body_data[b['name']] = (t, v)
                except Exception:
                    pass

    def _f(self, tb, default):
        try:
            return float(tb.text().strip())
        except (ValueError, AttributeError):
            return default

    def _update(self, *_):
        ax = self.pw.axes[0]
        ax.cla()
        name = self.body_cb.currentText()
        if name not in self.body_data:
            ax.set_title("no data"); self.pw.draw(); return

        _, v = self.body_data[name]
        scale = self._f(self.scale_tb, 1.0)
        A     = self._f(self.A_tb, 1e12)
        m     = self._f(self.m_tb, 3.0)
        endur = self._f(self.endur_tb, 0.0)
        nbins = int(self._f(self.bins_tb, 16))

        stress = np.asarray(v, dtype=float) * scale
        rc = rainflow_count(stress, nbins=nbins)
        if rc['ranges'].size == 0:
            ax.set_title("no closed cycles"); self.pw.draw(); return

        dmg = damage_miner(rc['ranges'], rc['counts'], A, m,
                           endurance=(endur if endur > 0 else None))

        edges = rc.get('bin_edges')
        bc = rc.get('bin_counts')
        if edges is not None and bc is not None:
            centers = 0.5 * (edges[:-1] + edges[1:])
            width = (edges[1] - edges[0]) * 0.9
            ax.bar(centers, bc, width=width, color=COLORS[0], alpha=0.8, edgecolor='k', lw=0.5)
        ax.set_xlabel("Stress range S  [scaled units]")
        ax.set_ylabel("Cycle count  n")
        ax.set_title("Rainflow histogram — {}  ({})".format(name, rc['method']))
        ax.grid(True, alpha=0.3)

        D = dmg['D']
        life = dmg['repeats_to_failure']
        life_s = ("{:.3g} block repeats".format(life) if life != float('inf') else "∞ (all below endurance)")
        self.summary_lbl.setText(
            "Miner damage D = {:.4g}   →   life ≈ {}   |   total cycles = {:.1f}, method = {}".format(
                D, life_s, float(rc['counts'].sum()), rc['method']))
        self.pw.draw()

    def _save_png(self):
        p, _ = QFileDialog.getSaveFileName(self, "Save PNG", "fatigue.png", "PNG (*.png)")
        if p:
            self.pw.export_png(p)


# ── Tab: Energy (elemental strain energy per body) ───────────────────
class EnergyTab(QWidget):
    """파트별 진동에너지.

    두 개의 소스를 받는다:
      1) energy.json (EnergyDialog 산출, schema energy-1.0) — set(모드/주파수)별
         랭킹 + localized 플래그까지 있는 풍부한 데이터. 있으면 이쪽을 쓴다.
      2) metadata.json 의 body[].strain_energy — 폴백.

    중요: strain_energy 의 basis 가 'Total' 이 아니면 그 값들의 합은 총 에너지가
    아니므로 점유율(%)을 계산하면 안 된다. 그 경우 절대값만 그리고 경고를 띄운다.
    """

    _GOOD_BASIS = 'Total'

    def __init__(self, meta, base_dir, parent=None):
        super().__init__(parent)
        self.meta = meta
        self.base_dir = base_dir
        self._load_data()

        lay = QVBoxLayout(self)

        # 경고 배너 (basis 가 폴백일 때만 보인다)
        self.warn_lbl = QLabel("")
        self.warn_lbl.setWordWrap(True)
        self.warn_lbl.setStyleSheet(
            "QLabel { background:#4a2a2a; color:#ffb4b4; padding:6px; border-radius:4px; }")
        self.warn_lbl.setVisible(False)
        self._warn_active = False
        lay.addWidget(self.warn_lbl)

        # set(모드) 선택 — energy.json 이 있을 때만 의미가 있다
        row = QHBoxLayout()
        row.addWidget(QLabel("View:"))
        self.set_cb = QComboBox()
        for label in self._set_labels():
            self.set_cb.addItem(label)
        self.set_cb.currentIndexChanged.connect(self._update)
        row.addWidget(self.set_cb)
        self.set_cb.setEnabled(bool(self.ej))
        row.addStretch()
        lay.addLayout(row)

        self.pw = PlotWidget(nrows=1, ncols=1)
        lay.addWidget(self.pw)

        self.summary_lbl = QLabel("")
        self.summary_lbl.setWordWrap(True)
        lay.addWidget(self.summary_lbl)

        brow = QHBoxLayout()
        b = QPushButton("Export PNG")
        b.clicked.connect(self._save_png)
        brow.addWidget(b)
        brow.addStretch()
        lay.addLayout(brow)

        self._update()

    # ------------------------------------------------------------------
    def _load_data(self):
        # --- energy.json (풍부한 소스) ---
        # 형태를 검증/정규화하고, 이상하면 조용히 폴백한다 (탭 생성에서 예외가 나면 뷰어 전체가 안 뜬다).
        self.ej = None
        self.ej_error = None
        try:
            import json
            p = os.path.join(self.base_dir, "energy.json")
            if os.path.exists(p):
                with open(p, 'r', encoding='utf-8') as f:
                    ej = json.load(f)
                if isinstance(ej, dict) and str(ej.get('schema_version', '')).startswith('energy-'):
                    self.ej = self._normalize_ej(ej)
        except Exception as ex:
            self.ej = None
            self.ej_error = str(ex)

        # --- metadata 폴백 ---
        # basis 는 body 마다 기록된다 (Total 과 폴백이 섞일 수 있다). 전부 Total 일 때만 점유율 유효.
        self.energy = []
        bases = set()
        for b in self.meta.get('bodies', []):
            se = b.get('strain_energy')
            if se is None:
                continue
            try:
                self.energy.append((b.get('name', '?'), float(se)))
            except (ValueError, TypeError):
                continue
            bases.add(b.get('strain_energy_basis'))
        self.energy.sort(key=lambda x: x[1], reverse=True)
        if not bases or bases == {None}:
            self.basis = None
        elif bases == {'Total'}:
            self.basis = 'Total'
        else:
            self.basis = 'mixed(' + ','.join(sorted(str(x) for x in bases)) + ')'

    def _normalize_ej(self, ej):
        """energy.json 의 형태를 검증하고 숫자 필드를 float 로 강제한다. 이상하면 ValueError."""
        def num(v, default=0.0):
            try:
                return float(v)
            except (TypeError, ValueError):
                return default
        sets = ej.get('sets')
        bodies = ej.get('bodies')
        if not isinstance(sets, list) or not isinstance(bodies, list):
            raise ValueError("energy.json: 'sets' and 'bodies' must be lists")
        nsets = []
        for s in sets:
            if not isinstance(s, dict):
                continue
            kept = []
            for r in (s.get('kept') or []):
                if isinstance(r, dict) and 'body' in r:
                    kept.append({'body': str(r['body']), 'energy': num(r.get('energy')),
                                 'share': num(r.get('share')), 'cum_share': num(r.get('cum_share'))})
            ns = dict(s)
            ns['kept'] = kept
            ns['total_energy'] = num(s.get('total_energy'))
            ns['localized'] = bool(s.get('localized'))
            for k in ('frequency_hz', 'time_s'):
                if s.get(k) is not None:
                    ns[k] = num(s.get(k), None)
            nsets.append(ns)
        nbodies = []
        for b in bodies:
            if isinstance(b, dict) and 'body' in b:
                nb = dict(b)
                nb['body'] = str(b['body'])
                nb['max_share'] = num(b.get('max_share'))
                nb['sum_energy'] = num(b.get('sum_energy'))
                nbodies.append(nb)
        out = dict(ej)
        out['sets'] = nsets
        out['bodies'] = nbodies
        return out

    def _basis(self):
        """실제로 쓰인 집계 기준. 모르면 None."""
        if self.ej:
            return self.ej.get('energy_basis')
        return self.basis

    def _shares_valid(self):
        """점유율(%)을 표시해도 되는가 — 총합 기준일 때만 참."""
        return self._basis() == self._GOOD_BASIS

    def _set_labels(self):
        if not self.ej:
            return ["(metadata strain energy)"]
        labels = ["종합 — body 별 최대 점유율"]
        for s in self.ej.get('sets', []):
            sn = s.get('set')
            kind = s.get('kind', 'mode')
            f = s.get('frequency_hz')
            t = s.get('time_s')
            if sn is None:
                lab = "result"
            elif kind == 'time':
                lab = ("set {0}  (t={1:.4g} s)".format(sn, t) if t is not None else "set {0}".format(sn))
            elif kind == 'frequency':
                lab = ("set {0}  (f={1:.1f} Hz)".format(sn, f) if f else "set {0}".format(sn))
            elif f:
                lab = "mode {0}  ({1:.1f} Hz)".format(sn, f)
            else:
                lab = "mode {0}".format(sn)
            if s.get('localized'):
                lab += "  ◆ localized"
            labels.append(lab)
        return labels

    # ------------------------------------------------------------------
    def _update(self, *_):
        ax = self.pw.axes[0]
        ax.cla()

        basis = self._basis()
        valid = self._shares_valid()
        if basis and not valid:
            self.warn_lbl.setText(
                "집계 기준이 '{0}' 다 — 바디 안에서 가장 큰 요소 하나의 값이라 "
                "합해도 총 에너지가 아니다. 점유율(%)은 신뢰할 수 없어 표시하지 "
                "않는다. Mechanical 쪽에서 Result.Total 이 읽히는지 GATE "
                "(verify_energy_api.py) 로 확인할 것.".format(basis))
            self._warn_active = True
            self.warn_lbl.setVisible(True)
        elif basis is None:
            self.warn_lbl.setText(
                "집계 기준 미기록 (구버전 metadata). 점유율은 참고용으로만 볼 것.")
            self._warn_active = True
            self.warn_lbl.setVisible(True)
        else:
            self._warn_active = False
            self.warn_lbl.setVisible(False)

        if getattr(self, 'ej_error', None):
            # energy.json 이 있었지만 형식이 깨져 무시됨 - 폴백으로 그리되 사용자에게 알린다
            self.warn_lbl.setText("energy.json ignored (malformed: {0})  {1}".format(
                self.ej_error[:100], self.warn_lbl.text()))
            self._warn_active = True
            self.warn_lbl.setVisible(True)

        if self.ej:
            try:
                self._draw_from_energy_json(ax, valid)
            except Exception as ex:
                ax.cla()
                ax.text(0.5, 0.5, "energy.json could not be drawn:\n" + str(ex)[:120],
                        ha='center', va='center', transform=ax.transAxes)
                self.summary_lbl.setText("")
        elif self.energy:
            self._draw_legacy(ax, valid)
        else:
            ax.text(0.5, 0.5,
                    "No energy data.\n\nRun [Vibration Energy] in Mechanical,\n"
                    "then export energy.json into this folder.",
                    ha='center', va='center', transform=ax.transAxes)
            self.summary_lbl.setText("")
        self.pw.draw()

    def _draw_from_energy_json(self, ax, valid):
        idx = max(0, self.set_cb.currentIndex())
        unit = (self.ej.get('units') or {}).get('energy', '')

        if idx == 0:
            rows = [(b['body'], b.get('max_share', 0.0), b.get('sum_energy', 0.0),
                     b.get('worst_set')) for b in self.ej.get('bodies', [])]
            title = "Peak share per body (across all sets)"
            xlabel = "max share of a set [%]" if valid else "cumulative energy [{0}]".format(unit)
            vals = [r[1] * 100 for r in rows] if valid else [r[2] for r in rows]
            annot = [("set {0}".format(r[3]) if r[3] is not None else "") for r in rows]
        else:
            s = self.ej.get('sets', [])[idx - 1]
            kept = s.get('kept', [])
            rows = [(r['body'], r.get('share', 0.0), r.get('energy', 0.0), None)
                    for r in kept]
            f = s.get('frequency_hz')
            t = s.get('time_s')
            kind = s.get('kind', 'mode')
            sn = s.get('set')
            head = ("mode {0}".format(sn) if kind == 'mode' and sn is not None
                    else ("set {0}".format(sn) if sn is not None else "result"))
            tag = ("  t={0:.4g} s".format(t) if (kind == 'time' and t is not None)
                   else ("  f={0:.1f} Hz".format(f) if f else ""))
            title = "{0}{1} - total {2:.4g} {3}".format(head, tag, s.get('total_energy', 0.0), unit)
            xlabel = "share [%]" if valid else "energy [{0}]".format(unit)
            vals = [r[1] * 100 for r in rows] if valid else [r[2] for r in rows]
            annot = [""] * len(rows)

        if not rows:
            ax.text(0.5, 0.5, "No bodies passed the filter for this set",
                    ha='center', va='center', transform=ax.transAxes)
            self.summary_lbl.setText("")
            return

        names = [r[0] for r in rows]
        y = list(range(len(names)))
        # 1위는 눈에 띄게
        colors = [COLORS[3] if i == 0 else COLORS[0] for i in y]
        ax.barh(y, vals, color=colors, alpha=0.9, edgecolor='k', lw=0.5)
        ax.set_yticks(y)
        ax.set_yticklabels(names, fontsize=8)
        for i, v in enumerate(vals):
            txt = ("  {0:.1f}%".format(v) if valid else "  {0:.3g}".format(v))
            if annot[i]:
                txt += "  ({0})".format(annot[i])
            ax.text(v, i, txt, va='center', fontsize=7)
        ax.set_xlabel(xlabel)
        ax.set_title(title, fontsize=10)
        ax.invert_yaxis()
        ax.grid(True, axis='x', alpha=0.3)

        sets = self.ej.get('sets', [])
        nloc = len([s for s in sets if s.get('localized')])
        self.summary_lbl.setText(
            "analysis: {0} [{1}]   basis: {2}   set {3}개 (localized {4}개)   worst: {5}".format(
                self.ej.get('analysis', '?'), self.ej.get('analysis_type', '?'),
                self.ej.get('energy_basis', '?'), len(sets), nloc,
                self.ej.get('worst_body', '?')))

    def _draw_legacy(self, ax, valid):
        names = [n for n, _ in self.energy]
        se = [s for _, s in self.energy]
        unit = (self.meta.get('units') or {}).get('energy', 'mJ')
        total = sum(se) or 1.0
        y = list(range(len(names)))
        colors = [COLORS[3] if i == 0 else COLORS[0] for i in y]
        ax.barh(y, se, color=colors, alpha=0.9, edgecolor='k', lw=0.5)
        ax.set_yticks(y)
        ax.set_yticklabels(names, fontsize=8)
        for i, s in enumerate(se):
            if valid:
                ax.text(s, i, "  {0:.3g} ({1:.0%})".format(s, s / total), va='center', fontsize=7)
            else:
                ax.text(s, i, "  {0:.3g}".format(s), va='center', fontsize=7)
        ax.set_xlabel("Strain Energy [{0}]".format(unit))
        ax.set_title("Strain energy by body", fontsize=10)
        ax.invert_yaxis()
        ax.grid(True, axis='x', alpha=0.3)
        if valid:
            self.summary_lbl.setText(
                "Total = {0:.4g} {1}   over {2} bodies".format(total, unit, len(names)))
        else:
            self.summary_lbl.setText(
                "{0} bodies — 합계는 총 에너지가 아니므로 표시하지 않는다.".format(len(names)))

    def _save_png(self):
        p, _ = QFileDialog.getSaveFileName(self, "Save PNG", "energy.png", "PNG (*.png)")
        if p:
            self.pw.export_png(p)


class ReactionsTab(QWidget):
    """Grouped bar of Rx/Ry/Rz/|R| per reaction scope. Reads top-level `reactions` (a list of
    {scope,x,y,z,mag}) — added by a future ForceReaction extraction. Placeholder when absent."""

    def __init__(self, meta, base_dir, parent=None):
        super().__init__(parent)
        self.meta = meta
        self.base_dir = base_dir
        self.reactions = meta.get('reactions', []) or []

        lay = QVBoxLayout(self)
        self.pw = PlotWidget(nrows=1, ncols=1)
        lay.addWidget(self.pw)
        row = QHBoxLayout()
        b = QPushButton("Export PNG"); b.clicked.connect(self._save_png)
        row.addWidget(b); row.addStretch()
        lay.addLayout(row)
        self._update()

    def _update(self, *_):
        ax = self.pw.axes[0]; ax.cla()
        if not self.reactions:
            ax.text(0.5, 0.5, "no reaction data in metadata\n(reaction extraction is opt-in)",
                    ha='center', va='center', transform=ax.transAxes)
        else:
            scopes = [r.get('scope', '?') for r in self.reactions]
            comps = ['x', 'y', 'z', 'mag']
            x = np.arange(len(scopes))
            w = 0.2
            for ci, c in enumerate(comps):
                vals = [float(r.get(c, 0.0) or 0.0) for r in self.reactions]
                ax.bar(x + (ci - 1.5) * w, vals, w, label=c.upper(),
                       color=COLORS[ci % len(COLORS)])
            ax.set_xticks(x); ax.set_xticklabels(scopes, fontsize=8)
            unit = self.meta.get('units', {}).get('force', 'N')
            ax.set_ylabel("Reaction force [{}]".format(unit))
            ax.set_title("Reaction forces by scope")
            ax.legend(fontsize=8); ax.grid(True, axis='y', alpha=0.3)
        self.pw.draw()

    def _save_png(self):
        p, _ = QFileDialog.getSaveFileName(self, "Save PNG", "reactions.png", "PNG (*.png)")
        if p:
            self.pw.export_png(p)


# ── Tab: Sweep (parameter-sweep sensitivity / Pareto / RSM) ──────────
class SweepTab(QWidget):
    """Load a folder of result cases (each a subfolder with metadata.json + optional params.json)
    and show sensitivity (∂metric/∂param), a Pareto front for two chosen metrics, and the response-
    surface r² per metric. Pure numpy via sweep_analyzer — no live solve, no API."""

    def __init__(self, meta, base_dir, parent=None):
        super().__init__(parent)
        self.meta = meta
        self.base_dir = base_dir
        self.cases = []
        self.analysis = None

        lay = QVBoxLayout(self)
        row = QHBoxLayout()
        b = QPushButton("Open sweep folder…")
        b.clicked.connect(self._open_folder)
        row.addWidget(b)
        row.addWidget(QLabel("Pareto X:"))
        self.xm_cb = QComboBox(); self.xm_cb.currentIndexChanged.connect(self._update)
        row.addWidget(self.xm_cb)
        row.addWidget(QLabel("Y:"))
        self.ym_cb = QComboBox(); self.ym_cb.currentIndexChanged.connect(self._update)
        row.addWidget(self.ym_cb)
        row.addStretch()
        lay.addLayout(row)

        self.pw = PlotWidget(nrows=1, ncols=3)
        lay.addWidget(self.pw)
        self.summary_lbl = QLabel("Open a sweep folder (subfolders each with metadata.json + optional params.json).")
        lay.addWidget(self.summary_lbl)

        # if the current result's folder is itself a sweep root, auto-load its parent
        try:
            self._try_load(os.path.dirname(base_dir))
        except Exception:
            pass
        self._update()

    def _open_folder(self):
        d = QFileDialog.getExistingDirectory(self, "Select sweep folder", self.base_dir)
        if d:
            self._try_load(d)
            self._update()

    def _try_load(self, root):
        try:
            import sweep_analyzer as SA
            entries = SA.scan_sweep_dir(root)
            cases = SA.load_cases(entries) if entries else []
        except Exception as ex:
            self.summary_lbl.setText("load failed: {}".format(ex)); return
        if len(cases) < 2:
            return  # need at least two cases to be a sweep; keep the placeholder
        self.cases = cases
        metric_keys = sorted({k for c in cases for k in c["metrics"]})
        for cb in (self.xm_cb, self.ym_cb):
            cb.blockSignals(True); cb.clear(); cb.addItems(metric_keys); cb.blockSignals(False)
        if len(metric_keys) >= 2:
            self.xm_cb.setCurrentIndex(0); self.ym_cb.setCurrentIndex(1)

    def _update(self, *_):
        axes = self.pw.axes
        for ax in axes:
            ax.cla()
        if len(self.cases) < 2:
            axes[0].text(0.5, 0.5, "no sweep loaded\n(need ≥2 cases)", ha='center', va='center',
                         transform=axes[0].transAxes)
            for ax in axes[1:]:
                ax.set_axis_off()
            self.pw.draw(); return

        import sweep_analyzer as SA
        cases = self.cases
        param_keys = sorted({k for c in cases for k in c["params"]})
        metric_keys = sorted({k for c in cases for k in c["metrics"]})

        # (1) sensitivity heat-ish bars: elasticity of each metric wrt each param
        sens = SA.sensitivity(cases)
        ax = axes[0]
        if param_keys:
            npar = len(param_keys)
            width = 0.8 / max(len(metric_keys), 1)
            xpos = np.arange(npar)
            for mi, mk in enumerate(metric_keys):
                vals = [sens.get(mk, {}).get(pk, {}).get('elasticity', 0.0) for pk in param_keys]
                ax.bar(xpos + mi * width, vals, width, label=mk)
            ax.set_xticks(xpos + 0.4 - width / 2)
            ax.set_xticklabels(param_keys, fontsize=7, rotation=20)
            ax.set_ylabel("elasticity  (%/%)"); ax.set_title("Sensitivity")
            ax.legend(fontsize=6); ax.grid(True, axis='y', alpha=0.3)
        else:
            ax.text(0.5, 0.5, "no params.json\n(sensitivity needs inputs)", ha='center', va='center',
                    transform=ax.transAxes)

        # (2) Pareto scatter for the two chosen metrics (both minimised)
        ax = axes[1]
        xm = self.xm_cb.currentText(); ym = self.ym_cb.currentText()
        if xm and ym and xm != ym:
            xs = [c["metrics"].get(xm, float('nan')) for c in cases]
            ys = [c["metrics"].get(ym, float('nan')) for c in cases]
            front, dom = SA.pareto_front(cases, {xm: 'min', ym: 'min'})
            ax.scatter([xs[i] for i in dom], [ys[i] for i in dom], c='lightgray', s=25, label='dominated')
            ax.scatter([xs[i] for i in front], [ys[i] for i in front], c=COLORS[1], s=45,
                       edgecolor='k', label='Pareto', zorder=3)
            ax.set_xlabel(xm); ax.set_ylabel(ym); ax.set_title("Pareto (min/min)")
            ax.legend(fontsize=6); ax.grid(True, alpha=0.3)
        else:
            ax.text(0.5, 0.5, "pick two distinct metrics", ha='center', va='center',
                    transform=ax.transAxes)

        # (3) response-surface r² per metric
        ax = axes[2]
        r2s = []
        for mk in metric_keys:
            rs = SA.response_surface(cases, mk)
            r2s.append((mk, rs.get('r2', float('nan')), rs.get('degree', 0)))
        if r2s:
            names = [m for m, _, _ in r2s]
            vals = [r if r == r else 0.0 for _, r, _ in r2s]
            ax.barh(range(len(names)), vals, color=COLORS[2], alpha=0.85, edgecolor='k', lw=0.5)
            ax.set_yticks(range(len(names))); ax.set_yticklabels(names, fontsize=7)
            for i, (_, r, deg) in enumerate(r2s):
                ax.text(min(max(r, 0), 1), i, "  r²={:.3f} (d{})".format(r, deg), va='center', fontsize=6)
            ax.set_xlim(0, 1.05); ax.set_xlabel("R²"); ax.set_title("Response surface fit")
            ax.invert_yaxis(); ax.grid(True, axis='x', alpha=0.3)

        self.summary_lbl.setText("{} cases | {} params | {} metrics".format(
            len(cases), len(param_keys), len(metric_keys)))
        self.pw.fig.tight_layout()
        self.pw.draw()


# ── Tab 3: FFT ────────────────────────────────────────────────────────
class FFTTab(QWidget):
    def __init__(self, meta, base_dir, parent=None):
        super().__init__(parent)
        self.meta = meta
        self.base_dir = base_dir
        self._load_data()

        lay = QVBoxLayout(self)
        ctrl, self.body_cb, self.overlay_cb = make_body_ctrl(
            meta.get('bodies', []), self._update, self._update)
        lay.addLayout(ctrl)

        self.pw = PlotWidget(nrows=2, ncols=1, height_ratios=[3, 1])
        lay.addWidget(self.pw)

        btn_row = QHBoxLayout()
        b = QPushButton("Export PNG")
        b.clicked.connect(lambda: self._save_png())
        btn_row.addWidget(b); btn_row.addStretch()
        lay.addLayout(btn_row)
        self._update()

    def _load_data(self):
        self.body_data = {}
        for b in self.meta.get('bodies', []):
            p = os.path.join(self.base_dir, b.get('csv', ''))
            if os.path.exists(p):
                try:
                    t, v = parse_ansys_export(p)
                    if len(t) >= 4:
                        f, Y = compute_fft(t, v)
                        self.body_data[b['name']] = (f, Y)
                except Exception:
                    pass
        fc = self.meta.get('force_csv', '')
        self.force_fft = None
        if fc and os.path.exists(fc):
            try:
                tf, ff = parse_ansys_export(fc)
                if len(tf) >= 4:
                    self.force_fft = compute_fft(tf, ff)
            except Exception:
                pass

    def _update(self, *_):
        ax_d, ax_f = self.pw.axes[0], self.pw.axes[1]
        ax_d.cla(); ax_f.cla()
        op_freq = float(self.meta.get('operating_freq_hz', 0) or 0)

        if self.overlay_cb.isChecked():
            for ci, (name, (f, Y)) in enumerate(self.body_data.items()):
                ax_d.semilogy(f, Y + 1e-12, lw=1,
                              color=COLORS[ci % len(COLORS)], label=name)
            ax_d.legend(fontsize=7)
            ax_d.set_title("FFT — all bodies")
        else:
            name = self.body_cb.currentText()
            if name in self.body_data:
                f, Y = self.body_data[name]
                ax_d.semilogy(f, Y + 1e-12, 'b-', lw=1.2)
                # Mark peaks
                peaks, _ = __import__('scipy').signal.find_peaks(
                    Y, prominence=0.05 * np.max(Y))
                for pk in peaks[:5]:
                    ax_d.annotate(
                        '{:.1f} Hz'.format(f[pk]),
                        xy=(f[pk], Y[pk]), fontsize=7, color='red',
                        xytext=(4, 4), textcoords='offset points')
                ax_d.set_title("FFT — {}".format(name), fontsize=9)

        if op_freq > 0:
            ax_d.axvline(op_freq, color='purple', ls=':', lw=1.5,
                         label='f_op={:.0f} Hz'.format(op_freq))

        ax_d.set_ylabel('|Y(f)| [mm]')
        ax_d.grid(True, which='both', alpha=0.3)
        ax_d.legend(fontsize=7)

        if self.force_fft is not None:
            ff_f, ff_Y = self.force_fft
            ax_f.semilogy(ff_f, ff_Y + 1e-12, 'r-', lw=1)
            if op_freq > 0:
                ax_f.axvline(op_freq, color='purple', ls=':', lw=1)
        ax_f.set_xlabel('Frequency [Hz]')
        ax_f.set_ylabel('|F(f)| [N]')
        ax_f.grid(True, which='both', alpha=0.3)
        ax_f.set_title("Input force spectrum")
        self.pw.draw()

    def _save_png(self):
        p, _ = QFileDialog.getSaveFileName(self, "Save PNG", "fft.png", "PNG (*.png)")
        if p:
            self.pw.export_png(p)


# ── Tab 4: FRF (Bode) ────────────────────────────────────────────────
class FRFTab(QWidget):
    def __init__(self, meta, base_dir, parent=None):
        super().__init__(parent)
        self.meta = meta
        self.base_dir = base_dir
        self._load_and_compute()

        lay = QVBoxLayout(self)
        ctrl, self.body_cb, self.overlay_cb = make_body_ctrl(
            meta.get('bodies', []), self._update, self._update)
        lay.addLayout(ctrl)

        # Bode: magnitude + phase, and coherence strip
        self.pw = PlotWidget(nrows=3, ncols=1, height_ratios=[3, 2, 1])
        lay.addWidget(self.pw)

        # Modal params label
        self.modal_lbl = QLabel("")
        self.modal_lbl.setFont(QFont("Consolas", 8))
        self.modal_lbl.setWordWrap(True)
        self.modal_lbl.setStyleSheet("padding:4px; background:#f5f5f5;")
        lay.addWidget(self.modal_lbl)

        btn_row = QHBoxLayout()
        b = QPushButton("Export PNG")
        b.clicked.connect(lambda: self._save_png())
        btn_row.addWidget(b); btn_row.addStretch()
        lay.addLayout(btn_row)
        self._update()

    def _load_and_compute(self):
        self.frf_data = {}   # name → (f, H_mag, H_phase, coh, params)
        self.no_force = True

        fc = self.meta.get('force_csv', '')
        if not fc or not os.path.exists(fc):
            return
        try:
            tf, ff = parse_ansys_export(fc)
        except Exception:
            return
        if len(tf) < 4:
            return
        self.no_force = False

        for b in self.meta.get('bodies', []):
            p = os.path.join(self.base_dir, b.get('csv', ''))
            if not os.path.exists(p):
                continue
            try:
                td, dd = parse_ansys_export(p)
                if len(td) < 4:
                    continue
                f, H_mag, H_phase = compute_frf(td, dd, tf, ff)
                f_c, coh = coherence(td, dd, tf, ff)
                params = extract_modal_params(f, H_mag)
                self.frf_data[b['name']] = (f, H_mag, H_phase, coh, params)
            except Exception as ex:
                print("FRF error {}: {}".format(b['name'], ex))

    def _update(self, *_):
        ax_mag, ax_ph, ax_coh = self.pw.axes
        ax_mag.cla(); ax_ph.cla(); ax_coh.cla()

        op_freq = float(self.meta.get('operating_freq_hz', 0) or 0)
        modal_lines = []

        if self.no_force:
            ax_mag.text(0.5, 0.5, 'No force CSV — FRF unavailable',
                        ha='center', va='center', transform=ax_mag.transAxes)
            self.pw.draw()
            return

        def _vlines(ax, op_freq, params, color_pk='red'):
            if op_freq > 0:
                ax.axvline(op_freq, color='purple', ls=':', lw=1.3,
                           label='f_op={:.0f} Hz'.format(op_freq))
            for p in params:
                ax.axvline(p['fn'], color=color_pk, ls='--', lw=0.7, alpha=0.6)

        if self.overlay_cb.isChecked():
            for ci, (name, (f, H_mag, H_ph, coh, params)) in \
                    enumerate(self.frf_data.items()):
                c = COLORS[ci % len(COLORS)]
                ax_mag.semilogy(f, H_mag + 1e-15, lw=1, color=c, label=name)
                ax_ph.plot(f, H_ph, lw=1, color=c)
                ax_coh.plot(f, coh, lw=1, color=c)
            if op_freq > 0:
                ax_mag.axvline(op_freq, color='purple', ls=':', lw=1.5,
                               label='f_op={:.0f} Hz'.format(op_freq))
            ax_mag.legend(fontsize=7)
            ax_mag.set_title("FRF Magnitude — all bodies")
        else:
            name = self.body_cb.currentText()
            if name not in self.frf_data:
                self.pw.draw()
                return
            f, H_mag, H_ph, coh, params = self.frf_data[name]

            ax_mag.semilogy(f, H_mag + 1e-15, 'b-', lw=1.5)
            ax_ph.plot(f, H_ph, 'b-', lw=1.2)
            ax_coh.plot(f, coh, 'g-', lw=1)

            _vlines(ax_mag, op_freq, params)
            _vlines(ax_ph,  op_freq, params)

            # Annotate peaks
            for p in params:
                ax_mag.annotate(
                    'fn={:.1f}\nQ={:.0f}'.format(
                        p['fn'], p['Q'] if not np.isnan(p['Q']) else 0),
                    xy=(p['fn'], p['H_peak']), fontsize=7, color='darkred',
                    xytext=(6, 0), textcoords='offset points',
                    arrowprops=dict(arrowstyle='->', color='red', lw=0.7))

                # Modal param text
                zeta_s = '{:.4f}'.format(p['zeta']) if not np.isnan(p['zeta']) else '—'
                Q_s    = '{:.1f}'.format(p['Q'])    if not np.isnan(p['Q'])    else '—'
                modal_lines.append(
                    '  fn={:.1f} Hz   ζ={}   Q={}   |H_pk|={:.4f} mm/N'.format(
                        p['fn'], zeta_s, Q_s, p['H_peak']))

            ax_mag.set_title("FRF — {}".format(name), fontsize=9)

        # Reference lines
        for y in (0, -90, -180):
            ax_ph.axhline(y, color='gray', ls='--' if y == -90 else '-',
                          lw=0.6, alpha=0.5)
        ax_coh.axhline(0.8, color='gray', ls='--', lw=0.8, alpha=0.6,
                       label='γ²=0.8')

        ax_mag.set_ylabel('|H(f)| [mm/N]')
        ax_mag.grid(True, which='both', alpha=0.3)
        ax_mag.legend(fontsize=7)

        ax_ph.set_ylabel('∠H [deg]')
        ax_ph.set_ylim(-200, 20)
        ax_ph.grid(True, alpha=0.3)

        ax_coh.set_ylabel('Coherence γ²')
        ax_coh.set_ylim(0, 1.05)
        ax_coh.set_xlabel('Frequency [Hz]')
        ax_coh.grid(True, alpha=0.3)
        ax_coh.legend(fontsize=7)

        self.pw.draw()

        header = "Modal parameters — {}:\n".format(
            self.body_cb.currentText() if not self.overlay_cb.isChecked() else "all")
        self.modal_lbl.setText(
            header + ("\n".join(modal_lines) if modal_lines else "  (no significant peaks found)"))

    def _save_png(self):
        p, _ = QFileDialog.getSaveFileName(self, "Save PNG", "frf.png", "PNG (*.png)")
        if p:
            self.pw.export_png(p)


# ── Main Window ───────────────────────────────────────────────────────
class MainWindow(QMainWindow):
    def __init__(self, meta, base_dir):
        super().__init__()
        self.meta = meta
        self.base_dir = base_dir
        self.setWindowTitle(
            "MX Post-Process Viewer  —  {}".format(meta.get('analysis', '')))
        self.resize(960, 720)

        bodies = meta.get('bodies', [])

        tabs = QTabWidget()
        tabs.addTab(SummaryTab(meta, base_dir),         "📊  Summary")
        tabs.addTab(TimeHistTab(meta, base_dir),         "📈  Time History")
        tabs.addTab(FFTTab(meta, base_dir),              "〰  FFT")
        tabs.addTab(FRFTab(meta, base_dir),              "〜  FRF (Bode)")
        tabs.addTab(FatigueTab(meta, base_dir),          "🔩  Fatigue")
        tabs.addTab(EnergyTab(meta, base_dir),           "⚡  Energy")
        tabs.addTab(ReactionsTab(meta, base_dir),        "🎯  Reactions")
        tabs.addTab(SweepTab(meta, base_dir),            "📐  Sweep")

        central = QWidget()
        vbox = QVBoxLayout(central)
        vbox.setContentsMargins(4, 4, 4, 4)
        vbox.addWidget(tabs)
        self.setCentralWidget(central)

        # Status bar
        n = len(bodies)
        self.statusBar().showMessage(
            "Loaded: {} bodies  |  Analysis: {}  |  Base dir: {}".format(
                n, meta.get('analysis', '?'), base_dir))
