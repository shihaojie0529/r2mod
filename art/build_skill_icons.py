"""Generate Cui Ge's five HUD glyphs at a size legible in a run."""
from pathlib import Path
from PIL import Image, ImageDraw

out = Path(__file__).parent/'generated'/'icons'
out.mkdir(parents=True,exist_ok=True)
ink='#16272b'; white='#eef2df'; gold='#e8bb74'; aqua='#6bd1c4'
for name in ('pump','punch','slam','charge','shaker'):
    im=Image.new('RGB',(256,256),ink); d=ImageDraw.Draw(im)
    d.polygon([(0,168),(163,0),(256,0),(0,256)],fill='#243e41')
    d.rectangle((6,6,249,249),outline='#517273',width=4)
    def line(points,col,w): d.line(points,fill=col,width=w,joint='curve')
    def poly(points,c): d.polygon(points,fill=c)
    if name=='punch':
        poly([(45,153),(60,115),(91,89),(151,62),(185,65),(207,92),(207,131),(163,151),(131,187),(97,199)],white)
        for off in (0,25,50): line([(106+off,88-off//3),(122+off,112-off//3)],ink,6)
        line([(87,139),(131,117),(158,121),(153,133),(125,149)],ink,7)
        poly([(42,160),(66,139),(103,184),(82,207)],aqua)
    elif name=='slam':
        d.ellipse((34,66,193,184),fill=gold);d.ellipse((35,50,194,166),fill=white,outline=ink,width=6)
        d.ellipse((53,62,175,151),outline='#74958e',width=8);d.ellipse((91,87,137,120),fill=ink)
        for x,y in ((44,194),(93,210),(148,207),(193,183)): line([(125,164),(x,y)],gold,6)
        line([(186,38),(208,75)],aqua,8);line([(204,33),(220,59)],aqua,4)
    elif name=='charge':
        d.ellipse((135,46,180,91), fill=gold)
        poly([(105,83),(144,84),(175,111),(209,115),(198,134),(151,134),(131,117),(108,144),(135,161),(122,197),(100,213),(93,196),(104,172),(76,157),(53,176),(32,176),(72,126)],white)
        for x,y in ((30,81),(16,104),(27,125)):line([(x,y),(x+54,y)],aqua,6)
    elif name=='shaker':
        poly([(72,84),(181,84),(171,191),(158,204),(97,204),(84,193)],white)
        poly([(77,133),(176,133),(171,183),(87,183)],aqua)
        poly([(64,68),(89,55),(109,55),(115,42),(151,42),(154,55),(171,56),(185,68),(185,80),(64,80)],gold)
        line([(126,140),(126,177)],ink,8);line([(108,158),(144,158)],ink,8)
    else:
        line([(50,184),(201,82)],white,13)
        for x,y in ((68,170),(85,159),(169,102),(186,91)):
            line([(x-15,y-22),(x+15,y+22)],aqua if x in (68,186) else white,17)
        line([(59,72),(95,72),(108,44),(119,87),(132,66),(160,66)],gold,5)
    im.resize((128,128),Image.Resampling.LANCZOS).save(out/(name+'.png'))
