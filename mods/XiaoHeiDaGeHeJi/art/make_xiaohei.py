"""blender -b --factory-startup --python-exit-code 1 --python art/make_xiaohei.py"""
import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from bake_friend import bake
from xiaohei_geometry import PIVOTS, build

# A planted asymmetric stance reads both in the lobby and in the neutral GLB.
# Deform the sculpt and joint pivots identically so runtime articulation stays aligned.
def stance(point,joint):
    if not joint.endswith(("Leg","Foot")): return point
    x,y,z=point
    s=1 if joint.startswith("Right") else -1
    t=max(0,min(1,(.93-y)/.814))
    from math import sin,pi
    return (x+s*.047*t,y,z+s*.084*t+.024*sin(t*pi))

bake("Xiaohei",PIVOTS,build,stance,[
    ((.50,-3.6,1.88),(0,0,1.625),.495),
    ((1.18,-3.6,1.91),(-.074,-.04,1.528),.96,650),
    ((3.22,-3.72,2.02),(0,-.06,.96),2.05,580)])
