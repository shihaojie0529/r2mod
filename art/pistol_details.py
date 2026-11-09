"""Small machined details for the existing pistol; preserves grip and muzzle."""
from math import sqrt


def build(add):
    hand = 'RightHand'
    def block(name, center, size, color):
        x,y,z=center; a,b,c=(v/2 for v in size)
        points=[(x+dx*a,y+dy*b,z+dz*c) for dx,dy,dz in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
        faces=[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
        add('Light pistol / '+name,points,faces,color,hand)
    for side in (-1,1):
        for i in range(4):
            block('Slide serration',(.246+side*.0278,1.385,.405+i*.009),(.0018,.018,.0032),(.25,.34,.38))
        for i in range(3):
            block('Cooling port',(.246+side*.0283,1.397,.517+i*.013),(.0015,.004,.006),(.07,.12,.15))
        for z in (.443,.568):
            block('Inset fastener',(.246+side*.029,1.363,z),(.0017,.004,.005),(.53,.65,.68))
    block('Front sight base',(.246,1.405,.565),(.014,.006,.014),(.09,.15,.18))
    block('Front sight bead',(.246,1.410,.565),(.005,.004,.006),(.52,.96,.87))


def update_baked():
    """Apply just the details without rebuilding or changing the existing sculpt."""
    import json
    from pathlib import Path
    path=Path(__file__).resolve().parent/'generated'/'xiaohei-model.json'
    model=json.loads(path.read_text(encoding='utf-8'))
    def signature(vertices):
        return tuple(sorted(set(tuple(round(v,5) for v in vertices[i:i+3]) for i in range(0,len(vertices),3))))
    known={signature(part['vertices']) for part in model['parts']}
    def add(name, points, faces, color, bone):
        raw=[c for p in points for c in p]
        if signature(raw) in known: return
        vertices=[]; normals=[]; triangles=[]
        for face in faces:
            a,b,c=(points[i] for i in face[:3]); u=tuple(b[i]-a[i] for i in range(3)); v=tuple(c[i]-a[i] for i in range(3))
            normal=(u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]); length=sqrt(sum(v*v for v in normal)); normal=tuple(v/length for v in normal)
            first=len(vertices)//3
            for index in face: vertices.extend(round(v,5) for v in points[index]); normals.extend(round(v,4) for v in normal)
            for i in range(1,len(face)-1): triangles.extend((first,first+i,first+i+1))
        model['parts'].append(dict(name=name,bone=bone,tint=[round(v*255) for v in color],vertices=vertices,normals=normals,triangles=triangles))
    build(add)
    path.write_text(json.dumps(model,separators=(',',':')),encoding='utf-8')

if __name__=='__main__': update_baked()
