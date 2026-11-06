"""blender -b --factory-startup --python-exit-code 1 --python art/make_jiege.py"""
import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from bake_friend import bake
from jiege_geometry import PIVOTS, build

bake("JieGe",PIVOTS,build,lambda point,joint: point,[
    ((-.65,-3.9,1.91),(0,0,1.624),.50),
    ((-.97,-3.7,1.93),(-.044,-.012,1.54),.87,490),
    ((2.95,-3.55,2.32),(0,-.015,.84),2.36,762),
    ((-.99,3.5,1.94),(.012,.06,1.295),1.20,390)],floor_height=-.23,matte_shadows=True)
