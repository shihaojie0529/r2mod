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
output=Path(__file__).resolve().parent/'generated'/'props.json'
output.write_text(json.dumps(dict(parts=parts),separators=(',',':')),encoding='utf-8')
