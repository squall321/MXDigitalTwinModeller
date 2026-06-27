"""
MXSimulator Icon Generator
Run once with: python generate_icons.py
Requires: pip install Pillow
Outputs 32x32 PNG icons to the same directory.
"""

from PIL import Image, ImageDraw, ImageFont
import os

OUT_DIR = os.path.dirname(os.path.abspath(__file__)) + "/images"
os.makedirs(OUT_DIR, exist_ok=True)

SIZE = 32
RADIUS = 6  # rounded corner radius


def rounded_rect(draw, xy, radius, fill):
    x0, y0, x1, y1 = xy
    draw.rectangle([x0 + radius, y0, x1 - radius, y1], fill=fill)
    draw.rectangle([x0, y0 + radius, x1, y1 - radius], fill=fill)
    draw.ellipse([x0, y0, x0 + radius*2, y0 + radius*2], fill=fill)
    draw.ellipse([x1 - radius*2, y0, x1, y0 + radius*2], fill=fill)
    draw.ellipse([x0, y1 - radius*2, x0 + radius*2, y1], fill=fill)
    draw.ellipse([x1 - radius*2, y1 - radius*2, x1, y1], fill=fill)


def make_icon(filename, bg_color, symbol, symbol_color=(255, 255, 255)):
    img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Rounded background
    rounded_rect(draw, [1, 1, SIZE - 2, SIZE - 2], RADIUS, bg_color)

    # Draw symbol using basic shapes
    if symbol == "cursor":
        # Selection cursor (arrow shape)
        pts = [(8, 6), (8, 22), (12, 18), (15, 25), (17, 24), (14, 17), (19, 17)]
        draw.polygon(pts, fill=symbol_color)
        draw.polygon(pts, outline=symbol_color)
        # Small rectangle to indicate selection
        draw.rectangle([14, 8, 23, 14], outline=symbol_color, width=2)
        draw.rectangle([14, 16, 23, 22], outline=symbol_color, width=1)

    elif symbol == "wave":
        # Sine-wave-like shape for modal analysis
        cx, cy = SIZE // 2, SIZE // 2
        # Draw 3 arcs simulating a wave
        draw.arc([5, cy - 7, 13, cy + 7], start=180, end=0, fill=symbol_color, width=3)
        draw.arc([13, cy - 7, 21, cy + 7], start=0, end=180, fill=symbol_color, width=3)
        draw.arc([21, cy - 7, 29, cy + 7], start=180, end=0, fill=symbol_color, width=3)
        # Frequency label dots
        draw.ellipse([8, 8, 11, 11], fill=symbol_color)
        draw.ellipse([21, 8, 24, 11], fill=symbol_color)

    elif symbol == "plus":
        # Plus sign for Add Scenario
        cx, cy = SIZE // 2, SIZE // 2
        w = 4
        draw.rectangle([cx - w, cy - 10, cx + w, cy + 10], fill=symbol_color)
        draw.rectangle([cx - 10, cy - w, cx + 10, cy + w], fill=symbol_color)

    img.save(os.path.join(OUT_DIR, filename))
    print(f"  Created: {filename}")


print("Generating MXSimulator icons...")

# Named Selections: blue bg + cursor/selection symbol
make_icon("cap_vibration.png",
          bg_color=(30, 100, 200),
          symbol="cursor")

# Modal Analysis: green bg + wave symbol
make_icon("modal_analysis.png",
          bg_color=(30, 150, 60),
          symbol="wave")

# Add Scenario: orange bg + plus symbol
make_icon("add_scenario.png",
          bg_color=(210, 100, 20),
          symbol="plus")

# Face Pair NS: purple bg + two overlapping rectangles (A|B)
make_icon("face_pair.png",
          bg_color=(120, 40, 160),
          symbol="plus")  # reuse plus shape as placeholder

# Override face_pair with a custom AB symbol
img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
draw = ImageDraw.Draw(img)
rounded_rect(draw, [1, 1, SIZE - 2, SIZE - 2], RADIUS, (120, 40, 160))
# Left rect (A)
draw.rectangle([4, 9, 14, 23], outline=(255, 255, 255), width=2)
draw.line([9, 13, 9, 19], fill=(255, 255, 255), width=2)
draw.line([9, 13, 13, 13], fill=(255, 255, 255), width=1)
draw.line([9, 16, 12, 16], fill=(255, 255, 255), width=1)
# Right rect (B)
draw.rectangle([18, 9, 28, 23], outline=(255, 255, 180), width=2)
draw.line([22, 13, 22, 19], fill=(255, 255, 180), width=2)
draw.line([22, 13, 26, 13], fill=(255, 255, 180), width=1)
draw.arc([22, 13, 27, 16], start=270, end=90, fill=(255, 255, 180), width=1)
draw.line([22, 16, 26, 16], fill=(255, 255, 180), width=1)
draw.arc([22, 16, 27, 19], start=270, end=90, fill=(255, 255, 180), width=1)
# Link arrow between A and B
draw.line([15, 16, 17, 16], fill=(255, 255, 255), width=2)
img.save(os.path.join(OUT_DIR, "face_pair.png"))
print("  Created: face_pair.png (custom)")

# Post-Process Viewer: dark teal bg + Bode/chart symbol (axes + two lines)
img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
draw = ImageDraw.Draw(img)
rounded_rect(draw, [1, 1, SIZE - 2, SIZE - 2], RADIUS, (20, 120, 110))
# Axes
draw.line([5, 26, 27, 26], fill=(255, 255, 255), width=2)   # x-axis
draw.line([5, 5,  5,  26], fill=(255, 255, 255), width=2)   # y-axis
# Magnitude curve (peak shape)
pts_mag = [(6, 22), (9, 20), (12, 12), (14, 8), (16, 12), (19, 20), (22, 22), (26, 21)]
draw.line(pts_mag, fill=(100, 220, 200), width=2)
# Phase curve (S-shape, offset below)
pts_ph  = [(6, 24), (10, 24), (14, 21), (18, 27), (22, 27), (26, 27)]
draw.line(pts_ph,  fill=(255, 200, 60),  width=2)
img.save(os.path.join(OUT_DIR, "post_process.png"))
print("  Created: post_process.png (custom)")

# K-File Export: dark orange bg + white "K" + export arrow
img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
draw = ImageDraw.Draw(img)
rounded_rect(draw, [1, 1, SIZE - 2, SIZE - 2], RADIUS, (170, 70, 10))
# White "K" letter (two diagonals + vertical)
draw.line([7, 7, 7, 25], fill=(255, 255, 255), width=3)       # vertical stem
draw.line([7, 16, 18, 7], fill=(255, 255, 255), width=3)       # upper diagonal
draw.line([7, 16, 18, 25], fill=(255, 255, 255), width=3)      # lower diagonal
# Export arrow (right side, bottom): →
draw.line([21, 20, 28, 20], fill=(255, 220, 100), width=2)     # horizontal
draw.line([25, 16, 29, 20], fill=(255, 220, 100), width=2)     # upper arrow
draw.line([25, 24, 29, 20], fill=(255, 220, 100), width=2)     # lower arrow
img.save(os.path.join(OUT_DIR, "kfile_export.png"))
print("  Created: kfile_export.png (custom)")

# Material Twin: deep blue bg + calibration curve symbol (experimental data + fit)
img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
draw = ImageDraw.Draw(img)
rounded_rect(draw, [1, 1, SIZE - 2, SIZE - 2], RADIUS, (25, 70, 140))

# Axes for stress-strain plot
draw.line([5, 26, 28, 26], fill=(255, 255, 255), width=2)   # x-axis (strain)
draw.line([5, 5,  5,  26], fill=(255, 255, 255), width=2)   # y-axis (stress)

# Experimental data points (scattered dots - gray/white)
exp_points = [(7, 23), (9, 20), (11, 17), (13, 14), (15, 12), (17, 11), (19, 10), (21, 9), (23, 9)]
for x, y in exp_points:
    draw.ellipse([x-1, y-1, x+1, y+1], fill=(200, 200, 200))

# Fitted curve (smooth red/orange line through data)
fitted_curve = [(6, 24), (8, 21), (10, 18), (12, 15), (14, 12), (16, 10.5), (18, 9.5), (20, 9), (22, 8.7), (24, 8.5)]
draw.line(fitted_curve, fill=(255, 120, 60), width=2)

# Small target/optimization symbol (top right corner)
draw.ellipse([23, 5, 28, 10], outline=(100, 220, 100), width=2)  # outer circle
draw.ellipse([24.5, 6.5, 26.5, 8.5], fill=(100, 220, 100))        # center dot

img.save(os.path.join(OUT_DIR, "material_twin.png"))
print("  Created: material_twin.png (custom)")

# Tied Check icon (if not already exists)
if not os.path.exists(os.path.join(OUT_DIR, "tied_check.png")):
    img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    rounded_rect(draw, [1, 1, SIZE - 2, SIZE - 2], RADIUS, (140, 60, 120))

    # Two surfaces with contact symbols
    draw.rectangle([6, 10, 12, 18], outline=(255, 255, 255), width=2)
    draw.rectangle([20, 14, 26, 22], outline=(255, 255, 255), width=2)

    # Connection/tie lines
    draw.line([12, 13, 20, 17], fill=(100, 255, 100), width=2)
    draw.line([12, 15, 20, 19], fill=(100, 255, 100), width=2)

    # Check mark
    draw.line([22, 6, 24, 8], fill=(100, 255, 100), width=2)
    draw.line([24, 8, 28, 4], fill=(100, 255, 100), width=2)

    img.save(os.path.join(OUT_DIR, "tied_check.png"))
    print("  Created: tied_check.png (custom)")

print(f"\nDone. Icons saved to: {OUT_DIR}")
print("Deploy with: bash Mechanical/deploy_mxsimulator.sh --xml")
