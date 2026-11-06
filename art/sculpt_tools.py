"""Primitive builders for rigged game characters, in metres and Unity coordinates."""
from math import cos, sin, tau
from build_body import cross, loft, plus, scale, tube, unit

def color(hexcode):
    return tuple(int(hexcode[k:k+2],16)/255 for k in (0,2,4))

def sculpt_tools(add):
    def mesh(name, points, faces, tint, bone):
        add(name,points,faces,color(tint),bone)

    def shell(name, rows, tint, bone, caps=True):
        loft(add,name,rows,color(tint),bone,caps)

    def line(name, path, radius, tint, bone, n=8):
        tube(add,name,path,radius,color(tint),bone,n)

    def body(name, profile, tint, bone, n=24):
        rows = [[(x+rx*sin(tau*j/n), y, z+rz*cos(tau*j/n)) for j in range(n)]
                for y,x,z,rx,rz in profile]
        shell(name,rows,tint,bone)

    def limb(name, start, end, profile, tint, bone, n=16):
        toward = tuple(q-p for p,q in zip(start,end))
        axis = unit(toward)
        major = unit(cross((0,0,1) if abs(axis[2])<.85 else (0,1,0),axis))
        other = cross(axis,major)
        rows=[]
        for f,a,b in profile:
            center=plus(start,scale(toward,f))
            rows.append([plus(center,plus(scale(major,a*cos(tau*j/n)),scale(other,b*sin(tau*j/n))))
                         for j in range(n)])
        shell(name,rows,tint,bone)

    def plaque(name, polygon, dz, tint, bone):
        # Thin axial extrusion used for fabric appliques and cast trim.
        n=len(polygon)
        faces=[tuple(range(n))[::-1],tuple(range(n,2*n))]
        faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
        mesh(name,polygon+[plus(p,dz) for p in polygon],faces,tint,bone)

    def box(name,center,half,cut,tint,bone):
        x,y,z=center
        a,b,c=half
        ring=[(-a+cut,-c),(a-cut,-c),(a,-c+cut),(a,c-cut),(a-cut,c),
              (-a+cut,c),(-a,c-cut),(-a,-c+cut)]
        shell(name,[[(x+dx,y+h,z+dz) for dx,dz in ring] for h in (-b,b)],tint,bone)

    return mesh, shell, line, body, limb, plaque, box
