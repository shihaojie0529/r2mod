"""Wei Ge's watch, delivery, cut, door and selection icons for the in-run HUD."""
from pathlib import Path
from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent / "generated" / "icons"
CREAM, BLUE, GOLD, OLIVE = "#f1e9cf", "#74d9fc", "#efbb72", "#91a394"
INK = "#192529"


def line(d, pts, fill=CREAM, width=7):
    d.line(pts, fill=fill, width=width, joint="curve")


def watch(d):
    d.polygon([(99, 31), (150, 31), (157, 67), (94, 67)], fill=OLIVE)
    d.polygon([(92, 181), (155, 181), (148, 220), (100, 220)], fill=OLIVE)
    d.rounded_rectangle((66, 60, 185, 190), radius=26, fill=CREAM)
    d.rounded_rectangle((77, 71, 174, 179), radius=17, fill=INK)
    line(d, [(86, 117), (102, 117), (113, 96), (126, 135), (138, 111), (160, 111)], BLUE, 7)
    for x in (91, 115, 139):
        d.rectangle((x, 149, x+16, 162), fill=GOLD)


def chair(d):
    d.polygon([(87, 80), (150, 47), (165, 91), (101, 123)], fill=CREAM)
    d.polygon([(100, 127), (165, 96), (202, 114), (138, 149)], fill=GOLD)
    for x, y in [(99, 128), (139, 149), (194, 117)]:
        line(d, [(x, y), (x+5, y+34)], CREAM, 9)
    line(d, [(36, 161), (77, 139)], BLUE, 8)
    line(d, [(43, 187), (93, 159)], BLUE, 5)
    line(d, [(71, 206), (117, 180)], BLUE, 5)


def cut(d):
    for x, y, sx, sy in [(44,62,1,1),(136,62,-1,1),(44,152,1,-1),(136,152,-1,-1)]:
        line(d, [(x+sx*19,y),(x,y),(x,y+sy*19)], BLUE, 6)
    d.ellipse((73, 78, 100, 104), fill=CREAM)
    d.polygon([(61,131),(68,110),(91,106),(106,115),(117,121),(111,130),
               (96,124),(91,135),(104,145),(98,153),(81,141),(69,145)], fill=CREAM)
    line(d, [(109,169),(128,178),(151,178),(174,159),(196,137)], GOLD, 10)
    d.polygon([(170,134),(206,127),(200,163),(189,149)], fill=GOLD)


def door(d):
    for x, y, size, color in [(133,53,66,GOLD),(41,73,90,CREAM)]:
        line(d, [(x,y+size),(x,y),(x+size*.70,y-size*.12),(x+size*.70,y+size*.88)], color, 8)
        d.polygon([(x+6,y+5), (x+size*.38,y+size*.06),
                   (x+size*.38,y+size*1.10),(x+6,y+size*.96)], fill=OLIVE)
    line(d, [(61,191),(105,178),(139,143),(170,139)], BLUE, 9)
    d.polygon([(157,125),(192,134),(171,160)],fill=BLUE)


def select_all(d):
    for x,y,sx,sy in [(41,52,1,1),(190,52,-1,1),(41,184,1,-1),(190,184,-1,-1)]:
        line(d, [(x+sx*25,y),(x,y),(x,y+sy*25)], BLUE, 7)
    for x,y in [(69,79),(113,79),(69,119)]:
        d.rectangle((x,y,x+31,y+29), fill=CREAM if y<100 else OLIVE)
    d.polygon([(130,116),(202,166),(174,170),(193,196),(177,207),(159,176),(142,197)], fill=GOLD)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    for name, render in [("steps",watch),("delivery",chair),("cut",cut),("portal",door),("select",select_all)]:
        image = Image.new("RGB", (256, 256), INK)
        d = ImageDraw.Draw(image)
        d.polygon([(0,180),(207,0),(245,0),(0,216)], fill="#25383c")
        d.rectangle((7,7,248,248), outline="#48595a", width=4)
        render(d)
        image.resize((128,128), Image.Resampling.LANCZOS).save(OUT / f"wei-{name}.png")
