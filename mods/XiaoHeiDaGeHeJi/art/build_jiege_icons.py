"""Create Jie Ge's HUD icons and the ten stages of the jade-sword counter."""
from math import cos, hypot, radians, sin
from pathlib import Path

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent / "generated" / "icons"
INK, EDGE, JADE, IVORY, GOLD = "#152925", "#3a514a", "#83e3c4", "#eff2d8", "#f4c875"


def blade(d, grip, tip, width, bright=IVORY, shade=JADE):
    x, y = grip
    dx, dy = tip[0] - x, tip[1] - y
    length = hypot(dx, dy)
    dx, dy = dx / length, dy / length

    def at(along, side):
        return x + dx * along - dy * side, y + dy * along + dx * side

    start = width * 1.6
    tang = at(start, 0)
    d.line([at(0, 0), tang], fill=GOLD, width=max(3, round(width * .55)))
    d.polygon([at(start, -width), at(start + width, -width * .8), tip, tang], fill=shade)
    d.polygon([tang, tip, at(start + width, width * .8), at(start, width)], fill=bright)
    d.line([at(start + width * .6, -width * 1.7), tang, at(start + width * .6, width * 1.7)],
           fill=GOLD, width=max(2, round(width * .47)), joint="curve")


def flight(d):
    blade(d, (39, 181), (219, 151), 11)
    d.ellipse((121, 58, 145, 82), fill=GOLD)
    d.polygon([(120, 86), (138, 88), (150, 103), (176, 92), (181, 99),
               (150, 115), (134, 106), (132, 129), (152, 145), (149, 160),
               (138, 162), (139, 151), (118, 138), (99, 160), (78, 168),
               (76, 160), (93, 149), (106, 128), (106, 105), (87, 119),
               (80, 115)], fill=IVORY)
    d.line([(49, 196), (81, 196), (143, 183)], fill=JADE, width=5)


def draw_sword(d):
    blade(d, (47, 188), (202, 57), 14)
    for x, y in [(51, 148), (79, 182)]:
        d.line([(x, y), (x+36, y-25), (x+51, y-40)], fill=JADE, width=5)
    d.polygon([(181, 49), (186, 29), (191, 47), (208, 49),
               (191, 55), (185, 68)], fill=GOLD)


def dash(d):
    for x, y, dist in [(34, 96, 86), (28, 120, 60), (35, 144, 34)]:
        d.polygon([(x, y), (x+dist, y-10), (x+dist-9, y+1)], fill=JADE)
    blade(d, (50, 177), (219, 69), 13)
    d.line([(96, 186), (151, 158), (184, 125)], fill=IVORY, width=5)


def storm(d):
    for angle in [-65, -43, -22, 22, 43, 65, 0]:
        a = radians(angle)
        grip = (126 + 31*sin(a), 173 - 24*cos(a))
        tip = (126 + 110*sin(a), 165 - 128*cos(a))
        blade(d, grip, tip, 6 if angle else 9, GOLD, IVORY)
    d.polygon([(117, 181), (126, 169), (135, 181), (126, 194)], fill=JADE)


def recall(d, count):
    blade(d, (194, 59), (72, 154), 10)
    blade(d, (161, 38), (62, 127), 7, JADE)
    blade(d, (207, 102), (95, 163), 7, JADE)
    d.line([(67, 93), (43, 117), (43, 170), (105, 170)], fill=GOLD, width=7, joint="curve")
    for index in range(9):
        group, within = divmod(index, 3)
        x = 23 + group * 74 + within * 19
        d.polygon([(x, 221), (x+7, 191), (x+19, 187), (x+12, 217)],
                  fill=JADE if index < count else "#47625b")


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    glyphs = [("flight", flight), ("draw", draw_sword), ("dash", dash), ("storm", storm)]
    for name, draw in glyphs + [(f"recall-{n}", lambda d, count=n: recall(d, count)) for n in range(10)]:
        image = Image.new("RGB", (256, 256), INK)
        d = ImageDraw.Draw(image)
        d.polygon([(0, 184), (230, 0), (256, 0), (0, 219)], fill="#213930")
        d.rectangle((7, 7, 248, 248), outline=EDGE, width=4)
        draw(d)
        image.resize((128, 128), Image.Resampling.LANCZOS).save(OUT / f"jie-{name}.png")
