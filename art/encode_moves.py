"""Encode rendered move reel in two forms: a small inline GIF and a full MP4."""
import json
import sys
import subprocess
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import imageio_ffmpeg

source=Path(sys.argv[1]); dist=Path(__file__).resolve().parents[1]/"dist"
meta=json.loads((source/"sequence.json").read_text()); fps=meta["fps"]
names={"idle":"拳击架势","jab":"刺拳","cross":"后手短摆拳","uppercut":"上勾拳","ready":"满层 · 双拳相碰",
       "slam":"杠铃片重砸","slamHeavy":"过顶重砸","charge":"借过一下","drink":"喝一口",
       "aim":"瞄准队友","throw":"抛出摇摇杯","run":"奔跑","air":"腾空","land":"落地缓冲","lobby":"整备","spawn":"起身备战","hit":"受击","death":"倒地"}
font=ImageFont.truetype("/System/Library/Fonts/STHeiti Medium.ttc",23)
eng=ImageFont.truetype("/System/Library/Fonts/Avenir Next.ttc",13)
images=[]; cmd=None
for shot in meta["shots"]:
    for f in range(shot["first"],shot["last"]+1):
        im=Image.open(source/f'frame-{f:04d}.png').convert("RGB")
        d=ImageDraw.Draw(im,"RGBA");w,h=im.size
        d.rounded_rectangle((11,10,22+d.textlength(names[shot["id"]],font=font),42),radius=4,fill=(15,23,26,205))
        d.text((17,12),names[shot["id"]],font=font,fill=(241,243,237,255))
        d.text((14,h-26),"CUI GE   /   TRAINING ROOM",font=eng,fill=(195,208,208,235))
        if cmd is None:
            cmd=subprocess.Popen([imageio_ffmpeg.get_ffmpeg_exe(),"-loglevel","error","-y","-f","rawvideo","-pix_fmt","rgb24",
                "-s",f"{w}x{h}","-r",str(fps),"-i","-","-c:v","libx264","-pix_fmt","yuv420p","-crf","21",
                "-movflags","+faststart",str(dist/"CuiGe-Moves.mp4")],stdin=subprocess.PIPE)
        cmd.stdin.write(im.tobytes())
        images.append(im.resize((340,320),Image.Resampling.LANCZOS))
cmd.stdin.close()
if cmd.wait():raise RuntimeError("MP4 encode failed")
# Share one palette across the loop to avoid a flashing background.
sample=Image.new("RGB",(340,320*min(12,len(images))))
for i in range(min(12,len(images))):sample.paste(images[i*(len(images)-1)//11],(0,i*320))
pal=sample.quantize(colors=128,method=Image.Quantize.MEDIANCUT)
out=[im.quantize(palette=pal,dither=Image.Dither.NONE) for im in images]
out[0].save(dist/"CuiGe-Moves.gif",save_all=True,append_images=out[1:],duration=[round((i+1)*1000/fps)-round(i*1000/fps) for i in range(len(out))],loop=0,optimize=True,disposal=1)
print("Encoded",len(images),"frames,",fps,"fps")
