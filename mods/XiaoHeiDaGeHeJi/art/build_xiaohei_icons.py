"""Render the five Xiaohei HUD glyphs. Run from any directory with Pillow."""
from pathlib import Path

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent / "generated" / "icons"
INK = "#15212c"
STEEL = "#293a49"
ICE = "#edf5ef"
BLUE = "#71cfff"
DEEP = "#3682b0"


def pistol(draw):
    # Stepped rear sight, short slide and an open trigger guard.
    draw.polygon([(46, 105), (59, 85), (69, 85), (72, 75), (85, 75),
                  (85, 85), (185, 85), (185, 112), (118, 112), (104, 125),
                  (88, 125), (70, 170), (47, 164), (63, 117), (46, 117)], fill=ICE)
    draw.line([(111, 117), (103, 138), (88, 138)], fill=ICE, width=5)
    draw.rectangle((125, 94, 181, 102), fill=DEEP)
    draw.polygon([(187, 97), (217, 80), (207, 96), (224, 99), (192, 109)], fill=BLUE)


def highlight(draw):
    for x, y, color in [(46, 170, DEEP), (91, 145, BLUE), (136, 120, ICE)]:
        draw.polygon([(x, y), (x+9, y-40), (x+35, y-54), (x+26, y-14)], fill=color)
    draw.line([(50, 185), (124, 185), (188, 147)], fill=BLUE, width=5)
    draw.polygon([(191, 80), (195, 57), (200, 75), (217, 77),
                  (201, 84), (195, 101), (191, 86), (177, 85)], fill=ICE)


def seeker(draw):
    for dx, dy, sx, sy in [(0, 0, 1, 1), (69, 0, -1, 1),
                           (0, 65, 1, -1), (69, 65, -1, -1)]:
        x, y = 139 + dx, 59 + dy
        draw.line([(x, y+16*sy), (x, y), (x+16*sx, y)], fill=ICE, width=6)
    draw.ellipse((170, 87, 180, 97), fill=BLUE)
    draw.line([(39, 176), (78, 176), (100, 161), (115, 130), (138, 108)], fill=DEEP, width=12, joint="curve")
    draw.line([(39, 172), (75, 172), (95, 157), (110, 127), (138, 103)], fill=BLUE, width=6, joint="curve")
    draw.polygon([(128, 105), (156, 92), (143, 116), (132, 117)], fill=ICE)


def blink(draw):
    for x, y, color in [(44, 93, DEEP), (75, 86, BLUE), (139, 73, ICE)]:
        draw.ellipse((x+17, y, x+37, y+20), fill=color)
        draw.polygon([(x+12, y+24), (x+29, y+24), (x+37, y+43),
                      (x+55, y+43), (x+52, y+51), (x+30, y+51),
                      (x+22, y+41), (x+14, y+54), (x+29, y+64),
                      (x+11, y+90), (x, y+90), (x+14, y+67),
                      (x, y+61), (x-14, y+71), (x-24, y+69), (x-4, y+50)], fill=color)
    draw.line([(172, 174), (158, 189), (86, 189)], fill=BLUE, width=5)
    draw.polygon([(79, 189), (95, 179), (92, 197)], fill=BLUE)


def replay(draw):
    for x, y, color in [(45, 73, DEEP), (60, 80, BLUE), (78, 88, ICE)]:
        draw.polygon([(x, y), (x+40, y), (x+46, y+6),
                      (x+22, y+6), (x, y+28)], fill=color)
    draw.polygon([(79, 103), (129, 80), (134, 83), (131, 99),
                  (119, 105), (119, 115), (107, 122), (94, 118), (83, 123)], fill=ICE)
    draw.polygon([(93, 125), (107, 128), (126, 120), (135, 128),
                  (150, 119), (151, 113), (181, 103), (185, 111),
                  (163, 121), (161, 130), (136, 147), (125, 141),
                  (120, 164), (82, 179), (86, 149), (78, 151)], fill=ICE)
    draw.polygon([(174, 87), (174, 62), (197, 74)], fill=BLUE)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    for name, render in [("highlight", highlight), ("pistol", pistol),
                         ("seeker", seeker), ("blink", blink), ("replay", replay)]:
        image = Image.new("RGB", (256, 256), INK)
        draw = ImageDraw.Draw(image)
        draw.polygon([(0, 174), (198, 0), (232, 0), (0, 208)], fill="#1e2f3f")
        draw.rectangle((7, 7, 248, 248), outline=STEEL, width=4)
        render(draw)
        image.resize((128, 128), Image.Resampling.LANCZOS).save(OUT / f"xh-{name}.png")
