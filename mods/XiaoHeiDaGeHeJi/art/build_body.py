"""Original gymnast's build, stringer tank and training gear. Distances are in metres."""
from math import asin, cos, exp, pi, sin, sqrt

SKIN = (192/255, 141/255, 112/255)
TANK = (.077, .083, .094)
BINDING = (.132, .141, .148)
PANTS = (.132, .146, .158)
POCKET = (.162, .176, .184)
SOLE = (.245, .262, .273)
UPPER = (.092, .105, .119)
CYAN = (.34, .77, .78)
GOLD = (.83, .65, .30)


def plus(a, b):
    return tuple(x + y for x, y in zip(a, b))


def scale(a, k):
    return tuple(x * k for x in a)


def cross(a, b):
    return (a[1]*b[2] - a[2]*b[1], a[2]*b[0] - a[0]*b[2], a[0]*b[1] - a[1]*b[0])


def unit(a):
    return scale(a, 1 / sqrt(sum(x*x for x in a)))


def loft(add, name, rings, color, bone, caps=True):
    n = len(rings[0])
    vertices = [p for row in rings for p in row]
    faces = [(i*n + j, i*n + (j+1) % n, (i+1)*n + (j+1) % n, (i+1)*n + j)
             for i in range(len(rings)-1) for j in range(n)]
    if caps:
        # Fan from the center so both convex and slightly concave end rings triangulate cleanly.
        for end, rev in ((0, True), (len(rings)-1, False)):
            # Separate the cap's vertices so its flat normal never darkens the curved limb.
            rim = len(vertices)
            vertices.extend(rings[end])
            center = len(vertices)
            vertices.append(tuple(sum(p[k] for p in rings[end])/n for k in range(3)))
            for j in range(n):
                a, b = rim+j, rim+(j+1) % n
                faces.append((center,b,a) if rev else (center,a,b))
    add(name, vertices, faces, color, bone)


def tube(add, name, points, radius, color, bone, n=7):
    rings = []
    for i, center in enumerate(points):
        tangent = unit(plus(points[min(i+1,len(points)-1)], scale(points[max(0,i-1)], -1)))
        ref = (0,0,1) if abs(tangent[2]) < .85 else (0,1,0)
        u = unit(cross(tangent, ref))
        v = cross(u,tangent)
        r = radius if isinstance(radius,(float,int)) else radius[i]
        rings.append([plus(center,plus(scale(u,r*sin(2*pi*j/n)),scale(v,r*cos(2*pi*j/n)))) for j in range(n)])
    loft(add,name,rings,color,bone)


# y, half breadth, front depth, rear depth, z center. Chest is fuller than the waist.
TORSO = [(.915,.153,.101,.092,.005),(.980,.164,.102,.096,.003),
         (1.060,.151,.097,.094,.004),(1.135,.163,.099,.096,.003),
         (1.210,.188,.109,.109,.001),(1.272,.204,.132,.112,0),
         (1.330,.211,.143,.112,0),(1.380,.218,.129,.108,0),
         (1.414,.232,.110,.101,0),(1.439,.222,.094,.093,-.001),
         (1.454,.199,.085,.088,-.003),(1.466,.151,.078,.080,-.004),
         (1.480,.085,.067,.068,-.007),
         (1.500,.059,.046,.051,-.013)]


def torso_dims(y):
    for a,b in zip(TORSO,TORSO[1:]):
        if y <= b[0]:
            t = max(0,(y-a[0])/(b[0]-a[0]))
            return tuple(p+(q-p)*t for p,q in zip(a[1:],b[1:]))
    return TORSO[-1][1:]


def torso_point(y, theta, loosen=0):
    xw, front, back, center = torso_dims(y)
    s, c = sin(theta), cos(theta)
    x = (xw+loosen)*s
    z = (front if c>0 else back)*c + center + loosen*c
    if c>0:
        # Two rounded pectorals separated by the sternum.
        z += .012 * exp(-((y-1.323)/.068)**2) * c*c * (1-exp(-(x/.041)**2))
    return x,y,z


def garment_top(t):
    c,s = cos(t),abs(sin(t))
    # Neckline in front, higher racer back, narrow straps, deep curved armholes at the sides.
    if s < .72:
        f=s/.72
        low=1.315 if c>=0 else 1.415
        return low + (1.455-low)*f**2
    f=(s-.72)/.28
    return 1.455 - .246*f**1.65


def build_torso(add):
    angles=[2*pi*j/64 for j in range(64)]
    # Sculpt only the visible skin so the cloth never intersects the pectorals at oblique angles.
    def skin(t,f):
        bottom=garment_top(t)-.006
        return torso_point(bottom+(TORSO[-1][0]-bottom)*f,t)
    rings=[[skin(t,i/22) for t in angles] for i in range(23)]
    loft(add,"Upper body and trapezius",rings,SKIN,"Chest",False)
    def garment(t, f):
        top=garment_top(t)
        bottom=.938 + .007*cos(2*t+.3)
        return torso_point(bottom+(top-bottom)*f,t,.005+.005*(1-f)**2)
    fractions=[i/22 for i in range(23)]
    loft(add,"Black training tank",[[garment(t,f) for t in angles] for f in fractions],TANK,"Chest",False)
    def trim(y_offset, bottom=False):
        row=[]
        for t in angles:
            _,y,_=garment(t,0 if bottom else 1)
            row.append(torso_point(y+y_offset,t,.012 if bottom else .007))
        return row
    loft(add,"Bound neckline and armholes",[trim(-.010),trim(.0005)],BINDING,"Chest",False)
    loft(add,"Double stitched shirt hem",[trim(0,True),trim(.007,True)],BINDING,"Chest",False)
    # A tank has two closed shoulder straps. Bridge the front and rear of each armhole
    # over the trapezius, finding the upper shoulder ridge at each strip's width.
    for s in (1,-1):
        vs=[]; faces=[]
        for i in range(9):
            x=.125+i*.005
            lo,hi=1.439,1.500
            for _ in range(16):
                m=(lo+hi)/2
                if torso_dims(m)[0] > x: lo=m
                else: hi=m
            ridge=(lo+hi)/2
            for j in range(25):
                a=pi*j/24
                base=1.366 if j<=12 else 1.399
                y=base+(ridge-base)*sin(a)
                t=asin(min(1,x/torso_dims(y)[0]))
                if j>12: t=pi-t
                xx,_,z=torso_point(y,s*t,.0077)
                vs.append((xx,y+.003*sin(a)**4,z))
        for i in range(8):
            for j in range(24):
                k=25*i+j
                face=(k,k+25,k+26,k+1)
                faces.append(face if s>0 else face[::-1])
        add(("Right" if s>0 else "Left")+" tank shoulder strap",vs,faces,TANK,"Chest")
    # Chain follows the surface, over each trapezius and down onto the upper chest.
    chain=[]
    for j in range(65):
        t=2*pi*j/64
        c,s=cos(t),sin(t)
        y=1.488-.081*max(0,c)**2-.026*max(0,-c)**2
        w,front,back,z0=torso_dims(y)
        x=(.075+.003*max(0,c))*s
        ratio=min(1,abs(x/w))
        z=z0+(1 if c>=0 else -1)*(front if c>=0 else back)*sqrt(1-ratio*ratio)
        chain.append((x+.002*s,y,z+.003*c))
    tube(add,"Fine gold neck chain",chain,.00245,GOLD,"Chest",8)
    # A small original dumbbell badge, slanted across the left pectoral.
    def badge(x,y):
        w,_,_,_=torso_dims(y)
        t=asin(x/(w+.006))
        px,_,z=torso_point(y,t,.0075)
        return px,y,z+.0025
    def stroke(name,a,b,r):
        tube(add,name,[badge(*a),badge(*b)],r,CYAN,"Chest",8)
    stroke("Chest emblem bar",(-.107,1.300),(-.059,1.320),.0027)
    for i,(x,y,h) in enumerate([(-.106,1.301,.0065),(-.099,1.304,.010),
                                (-.068,1.317,.010),(-.061,1.320,.0065)]):
        stroke("Chest emblem plate "+str(i),(x+h*.39,y-h),(x-h*.39,y+h),.0028)


def arm_section(add, name, start, end, profile, side, bone, color=SKIN):
    # First radius is width in the plane of the arm, second radius faces front/back.
    delta=plus(end,scale(start,-1))
    d=unit(delta)
    u=unit((d[1],-d[0],0))
    v=cross(u,d)
    n=22
    rows=[]
    for t,r,zr,forward,bias in profile:
        center=plus(plus(start,scale(delta,t)),(side*bias,0,forward))
        row=[]
        for j in range(n):
            a=2*pi*j/n
            row.append(plus(center,plus(scale(u,r*sin(a)),scale(v,zr*cos(a)))))
        rows.append(row)
    loft(add,name,rows,color,bone)


def build_arms(add, side):
    name="Right" if side>0 else "Left"
    shoulder=(.179*side,1.400,-.002)
    elbow=(.455*side,1.235,0)
    wrist=(.64*side,1.075,0)
    arm_section(add,name+" deltoid, biceps and triceps",shoulder,elbow,[
        (-.10,.041,.051,0,-.004),(.01,.066,.080,-.004,0),(.10,.080,.090,-.004,0),
        (.20,.087,.093,-.003,0),(.30,.086,.092,-.002,0),(.38,.081,.086,-.002,0),
        (.45,.075,.081,.002,0),(.57,.074,.088,.005,0),
        (.72,.069,.082,.005,0),(.86,.056,.063,.002,0),(1,.045,.047,0,0),
        (1.055,.041,.043,0,0)],side,name+"UpperArm")
    arm_section(add,name+" forearm",elbow,wrist,[
        (-.095,.042,.045,0,0),(0,.048,.050,-.004,0),(.15,.057,.058,-.006,0),
        (.29,.060,.061,-.004,0),(.49,.052,.053,0,0),(.71,.039,.043,0,0),
        (.91,.030,.034,.001,0),(1.06,.029,.034,.001,0)],side,name+"LowerArm")
    bone=name+"Hand"
    palm_end=(.696*side,1.014,.012)
    arm_section(add,name+" palm",wrist,palm_end,[
        (-.14,.029,.030,0,0),(.12,.031,.034,0,0),(.48,.031,.044,.001,0),
        (.79,.028,.042,.004,0),(1.06,.022,.037,.004,0)],side,bone)
    for i,(offset,length) in enumerate([(.032,.079),(.011,.083),(-.010,.077),(-.029,.062)]):
        # Knuckles curl back in toward the grip, thumb on the forward side.
        x=.688+(.004 if i in (1,2) else 0)
        y=1.025+.004*abs(i-1)
        z=.013+offset
        path=[(side*x,y,z),(side*(x+.021),y-length*.40,z+.003),
              (side*(x+.013),y-length*.72,z+.009),(side*(x-.009),y-length*.80,z+.010)]
        tube(add,name+" finger "+str(i+1),path,[.0105,.011,.009,.0068],SKIN,bone,9)
    tube(add,name+" thumb",[(side*.661,1.055,.031),(side*.672,1.041,.055),
             (side*.691,1.016,.060),(side*.692,1.000,.054)],
         [.017,.015,.012,.008],SKIN,bone,9)


def horizontal(add,name,profile,color,bone,n=28):
    rows=[[(x+w*sin(2*pi*j/n),y,z+b*cos(2*pi*j/n)) for j in range(n)] for y,x,z,w,b in profile]
    loft(add,name,rows,color,bone)


def build_pants(add):
    pelvis=[]
    for y,w,d,scallop in [(.781,.154,.086,.054),(.832,.173,.105,.020),
            (.878,.183,.116,.002),(.922,.174,.114,0),(.959,.161,.104,0)]:
        pelvis.append([(w*sin(2*pi*j/36),y+scallop*abs(sin(2*pi*j/36))**.75,
                        -.005+d*cos(2*pi*j/36)) for j in range(36)])
    loft(add,"Joggers yoke",pelvis,PANTS,"Hips")
    horizontal(add,"Waistband",[(.925,0,0,.172,.111),(.942,0,0,.171,.111),
         (.966,0,0,.159,.105)],UPPER,"Hips")
    for s in (1,-1):
        name="Right" if s>0 else "Left"
        thigh=name+"UpperLeg"
        calf=name+"LowerLeg"
        def rings(profile):
            return [(y,s*x,z,w,b) for y,x,z,w,b in profile]
        horizontal(add,name+" tapered cargo thigh",rings([
            (.461,.115,.010,.068,.071),(.492,.115,.009,.074,.077),
            (.527,.115,.007,.078,.081),(.570,.116,.002,.075,.088),
            (.645,.116,-.001,.088,.098),(.738,.111,-.006,.099,.109),
            (.825,.100,-.007,.108,.111),(.895,.092,-.005,.099,.102),
            (.925,.090,-.004,.087,.090)]),PANTS,thigh)
        horizontal(add,name+" tapered lower leg",rings([
            (.118,.115,0,.053,.052),(.150,.115,-.001,.054,.056),
            (.195,.115,-.004,.061,.060),(.250,.115,-.007,.063,.066),
            (.320,.115,-.013,.073,.076),(.385,.115,-.011,.078,.082),
            (.454,.115,.001,.071,.073),(.511,.115,.010,.070,.073)]),PANTS,calf)
        horizontal(add,name+" gathered ankle cuff",rings([(.108,.115,-.001,.050,.051),
             (.118,.115,-.001,.053,.054),(.154,.115,-.001,.055,.057)]),BINDING,calf)
        # Pouch patch follows the outer thigh curvature with a visible masked flap and broadside.
        def panel(label,corners,col):
            outer=[(s*x,y,z) for x,y,z in corners]
            inner=[(x-s*.006,y,z) for x,y,z in outer]
            # Orient the front face outwards.
            nn=cross(plus(outer[1],scale(outer[0],-1)),plus(outer[2],scale(outer[0],-1)))
            if nn[0]*s<0:
                outer.reverse(); inner.reverse()
            n=len(outer)
            faces=[tuple(range(n)),tuple(reversed(range(n,2*n)))]
            faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
            add(name+" "+label,outer+inner,faces,col,thigh)
        panel("side cargo pocket",[(.206,.738,.059),(.216,.739,-.019),(.207,.735,-.065),
              (.194,.615,-.050),(.200,.607,.004),(.189,.625,.066)],POCKET)
        panel("cargo flap",[(.209,.748,.057),(.220,.748,-.015),(.210,.741,-.067),
              (.208,.716,-.065),(.218,.706,-.005),(.207,.722,.059)],BINDING)
        tube(add,name+" hip welt",[(s*.123,.923,.090),(s*.154,.875,.078),(s*.175,.842,.067)],
             .0034,BINDING,thigh,7)
        tube(add,name+" waistband tie",[(s*.010,.955,.108),(s*.015,.931,.116),
             (s*.021,.906,.119),(s*.018,.889,.120)],.0026,BINDING,"Hips",7)
        build_shoe(add,s)


def build_shoe(add,s):
    side="Right" if s>0 else "Left"
    bone=side+"Foot"
    x=.115*s
    def shoe_mesh(label, rows, color):
        # Width, sole height, roof height at each fore-aft station; flattened toebox and heel.
        rings=[]
        for z,w,bottom,top,offset in rows:
            middle=(bottom+top)/2
            half=(top-bottom)/2
            row=[]
            for i in range(24):
                ang=2*pi*i/24
                sx,sy=sin(ang),-cos(ang)
                xx=(abs(sx)**.72)*(1 if sx>=0 else -1)
                yy=(abs(sy)**.56)*(1 if sy>=0 else -1)
                row.append((x+s*offset+xx*w,middle+yy*half,z))
            rings.append(row)
        loft(add,side+" "+label,rings,color,bone)
    shoe_mesh("trainer outsole",[(-.084,.043,.018,.046,0),(-.070,.058,.009,.046,0),
        (-.022,.061,.008,.045,0),(.042,.061,.010,.042,-.003),(.095,.067,.009,.039,-.006),
        (.153,.065,.011,.044,-.008),(.184,.051,.015,.048,-.012),(.197,.027,.021,.047,-.012)],SOLE)
    shoe_mesh("trainer upper",[(-.081,.041,.042,.095,0),(-.066,.054,.041,.135,0),
        (-.030,.057,.041,.153,0),(.015,.057,.039,.142,-.001),(.055,.058,.037,.114,-.005),
        (.104,.061,.036,.084,-.007),(.159,.057,.039,.072,-.009),(.182,.046,.041,.064,-.012),
        (.193,.027,.044,.057,-.012)],UPPER)
    for i in range(4):
        z=.033+i*.015
        high=.133-i*.009
        tube(add,side+" shoelace "+str(i),[(x-.035,high-.007,z-.004),
             (x-.004,high+.001,z+.002),(x+.029,high-.006,z+.008)],
             .0027,SOLE,bone,7)
    tube(add,side+" heel tab",[(x,.091,-.074),(x,.132,-.082),(x,.141,-.074)],
         .0047,POCKET,bone,8)


def build_body(add):
    build_torso(add)
    build_arms(add,1)
    build_arms(add,-1)
    build_pants(add)
