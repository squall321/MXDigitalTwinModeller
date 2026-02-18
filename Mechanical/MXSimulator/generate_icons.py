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

print(f"\nDone. Icons saved to: {OUT_DIR}")
print("Deploy with: bash deploy.sh mechanical --xml")
