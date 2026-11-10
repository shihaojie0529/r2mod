"""Model the iron plate and the drink shaker in metres, relative to the grip."""
import json
from math import sin, cos, tau, pi
from pathlib import Path

parts = []

def lathe(kind, name, profile, color, segments=48):
    # Profile is a closed cross-section, (radius, axial position). The plate's axle
    # points forward (+Z), while the shaker stands up (+Y) with its lid at the top.
    vertices, indices = [], []
    for radius, depth in profile:
        for i in range(segments):
            angle = i * tau / segments
            x, y = radius*cos(angle), radius*sin(angle)
            p = (x,y,depth) if kind == 'plate' else (x,depth,-y)
            vertices.extend(round(v,6) for v in p)
    for row in range(len(profile)):
        next_row = (row + 1) % len(profile)
        for i in range(segments):
            j = (i + 1) % segments
            a,b,c,d = row*segments+i,row*segments+j,next_row*segments+j,next_row*segments+i
            indices.extend((a,b,c, a,c,d))
    parts.append(dict(kind=kind,name=name,color=color,vertices=vertices,triangles=indices))

lathe('plate','Cast iron',[(.026,-.017),(.165,-.017),(.183,-.012),(.183,.012),(.165,.017),(.026,.017)],(.19,.215,.23))
lathe('plate','Rubber rim',[(.163,-.018),(.178,-.023),(.185,-.016),(.185,.016),(.178,.023),(.163,.018)],(.078,.092,.10))
for side in (-1,1):
    # Both faces carry a raised teal ring and a steel insert at the bore.
    d = side*.0185
    lathe('plate','Raised stripe',[(.147,d-.001),(.153,d-.001),(.153,d+.001),(.147,d+.001)],(.27,.70,.64))
lathe('plate','Machined bore',[(.023,-.023),(.038,-.023),(.045,-.017),(.045,.017),(.038,.023),(.023,.023)],(.65,.70,.70))
lathe('shaker','Flask',[(.001,-.071),(.028,-.071),(.034,-.065),(.040,.046),(.039,.060),(.001,.060)],(.17,.53,.48),24)
lathe('shaker','Grip',[(.036,-.025),(.039,-.024),(.041,.009),(.038,.010)],(.11,.27,.26),24)
lathe('shaker','Lid',[(.001,.052),(.041,.052),(.043,.057),(.042,.072),(.030,.081),(.013,.083),(.001,.083)],(.11,.14,.15),32)
lathe('shaker','Spout',[(.001,.080),(.014,.080),(.015,.101),(.012,.104),(.001,.104)],(.88,.69,.37),20)
lathe('shaker','Cap stripe',[(.041,.057),(.044,.057),(.044,.061),(.041,.061)],(.91,.74,.42),32)
# Raised weight lettering and moulded graduation marks are part of the mesh,
# so they follow the existing held/thrown poses without extra renderers.
def block(kind, name, center, size, color):
    x,y,z=center; a,b,c=(v/2 for v in size)
    vs=[(x+dx*a,y+dy*b,z+dz*c) for dx,dy,dz in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
    faces=[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
    indices=[j for face in faces for tri in [(face[0],face[1],face[2]),(face[0],face[2],face[3])] for j in tri]
    parts.append(dict(kind=kind,name=name,color=color,vertices=[round(v,6) for p in vs for v in p],triangles=indices))
segments={
    'a':(0,.023,.026,.004),'g':(0,0,.026,.004),'d':(0,-.023,.026,.004),
    'f':(-.013,.012,.004,.020),'b':(.013,.012,.004,.020),
    'e':(-.013,-.012,.004,.020),'c':(.013,-.012,.004,.020)}
for side in (-1,1):
    for center,digit in [(-.022,'2'),(.022,'0')]:
        for key in ('abged' if digit=='2' else 'abcdef'):
            x,y,w,h=segments[key]
            block('plate','Weight 20', (center+x,.090+y,side*.0178),(w,h,.0014),(.67,.74,.74))
    for x in (-.072,.072):
        block('plate','Lower maker bar',(x,-.092,side*.0178),(.026,.006,.0014),(.30,.43,.43))
for i in range(5):
    y=.016+i*.007
    radius=.034+(.040-.034)*(y+.065)/.111
    block('shaker','Volume graduation',(0,y,radius+.0004),(.018 if i%2==0 else .011,.0015,.0008),(.72,.86,.79))
for i in range(6):
    lathe('shaker','Grip rib',[(.040+i*.0002,-.023+i*.0055),(.041+i*.0002,-.023+i*.0055),(.041+i*.0002,-.022+i*.0055),(.040+i*.0002,-.022+i*.0055)],(.14,.33,.31),32)

output=Path(__file__).resolve().parent/'generated'/'props.json'
output.write_text(json.dumps(dict(parts=parts),separators=(',',':')),encoding='utf-8')
