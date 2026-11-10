"""Combine two renders of the same mesh into a contact sheet. Requires Pillow."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parent.parent
gen = root / "art" / "generated"
canvas = Image.new("RGB",(1132,835),"#162025")
d = ImageDraw.Draw(canvas)
def font(size, bold=False):
    for target in ("/System/Library/Fonts/Supplemental/Arial Bold.ttf" if bold else "/System/Library/Fonts/Supplemental/Arial.ttf",
                   "DejaVuSans-Bold.ttf" if bold else "DejaVuSans.ttf"):
        try: return ImageFont.truetype(target,size)
        except OSError: pass
    return ImageFont.load_default()
def character_title(size):
    # Common Chinese fonts on macOS, Windows, and Linux. Use the romanized
    # name on minimal machines that have no Chinese glyphs installed.
    for family in ("/System/Library/Fonts/STHeiti Medium.ttc", "msyhbd.ttc",
                   "NotoSansCJK-Bold.ttc", "NotoSansSC-Bold.otf", "wqy-zenhei.ttc"):
        try: return "崔哥", ImageFont.truetype(family, size)
        except OSError: pass
    return "CUI GE", font(size, True)

title, title_font = character_title(37)
d.text((22,33),title,font=title_font,anchor="lm",fill="#eff3eb")
d.text((1110,33),"SURVIVOR STUDY    /    001",anchor="rm",font=font(14,True),fill="#aed5ce")
d.line((22,65,1110,65),fill="#55716f")
for name,width,x,label in (("full",510,22,"01  /  SILHOUETTE"),("bust",558,552,"02  /  PORTRAIT")):
    with Image.open(gen/(name+".png")) as im:
        im=im.convert("RGB").resize((width,round(im.height*width/im.width)),Image.Resampling.LANCZOS)
        canvas.paste(im,(x,81))
    d.text((x,805),label,font=font(14,True),fill="#ccdfda")
canvas.save(root/"dist"/"CuiGe-preview.jpg",quality=93,subsampling=0)
