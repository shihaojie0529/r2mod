"""blender -b --factory-startup --python-exit-code 1 --python art/make_weige.py"""
import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from bake_friend import bake
from weige_geometry import PIVOTS, build

bake("WeiGe",PIVOTS,build,lambda point,joint:point,[
    ((-.49,-3.8,1.89),(0,-.02,1.618),.515),
    ((-.66,-3.7,1.91),(-.050,-.05,1.429),1.00,570),
    ((1.45,-4.1,2.07),(0,-.04,.96),2.12,566),
    ((2.52,-3.40,2.78),(.301,-.215,1.161),.29,342)],matte_shadows=True)
