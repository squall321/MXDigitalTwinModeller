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
    QSizePolicy, QGroupBox,
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
        cols = ['Rank', 'Body / NS', 'MaxDef [mm]', 'MaxVM [MPa]',
                'P2P [mm]', 'RMS [mm]', 'Severity']
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

            vals = [
                str(b.get('rank', i + 1)),
                b.get('name', '?'),
                '{:.4f}'.format(max_def),
                '{:.2f}'.format(max_vm),
                '{:.4f}'.format(p2p_val) if not np.isnan(p2p_val) else '—',
                '{:.4f}'.format(rms_val) if not np.isnan(rms_val) else '—',
                sev,
            ]
            for j, val in enumerate(vals):
                item = QTableWidgetItem(val)
                item.setBackground(bg)
                item.setTextAlignment(Qt.AlignCenter)
                self.table.setItem(i, j, item)

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
