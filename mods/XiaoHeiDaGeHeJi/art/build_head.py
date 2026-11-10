"""Faceted portrait sculpt in metres, +Y up and +Z toward the face."""

from math import cos, pi, sin
from random import Random


def build_head(add):
    def mesh(name, vertices, faces, pigment):
        color = tuple(int(pigment[i:i + 2], 16) / 255 for i in (0, 2, 4))
        add(name, vertices, faces, color, "Head")

    def shell(name, rings, color, close=True):
        width = len(rings[0])
        verts = [p for row in rings for p in row]
        faces = []
        for r in range(len(rings) - 1):
            for i in range(width):
                a, b = r * width + i, r * width + (i + 1) % width
                faces.append((a, b, b + width, a + width))
        if close:
            faces.extend((tuple(reversed(range(width))),
                          tuple(range(len(verts) - width, len(verts)))))
        mesh(name, verts, faces, color)

    def oval(cx, y, cz, rx, rz, count=24):
        return [(cx + rx * sin(i * 2 * pi / count), y,
                 cz + rz * cos(i * 2 * pi / count)) for i in range(count)]

    skin = 'c08d70'
    # Horizontal sections: height, half width, depth at middle forehead, back.
    sections = [
        (1.505, .030, .052, -.021), (1.512, .053, .078, -.044),
        (1.525, .072, .089, -.063), (1.546, .085, .091, -.082),
        (1.567, .092, .094, -.092), (1.592, .099, .093, -.101),
        (1.617, .102, .094, -.105), (1.640, .104, .094, -.107),
        (1.657, .103, .089, -.110), (1.675, .101, .096, -.112),
        (1.697, .100, .098, -.114), (1.721, .099, .095, -.112),
        (1.745, .094, .086, -.108), (1.765, .080, .072, -.094),
        (1.785, .052, .049, -.066), (1.795, .024, .023, -.034),
    ]

    def dims(y):
        for a, b in zip(sections, sections[1:]):
            if y <= b[0]:
                f = max(0.0, (y - a[0]) / (b[0] - a[0]))
                return tuple(lo + f * (hi - lo) for lo, hi in zip(a[1:], b[1:]))
        return sections[-1][1:]

    def tent(n, center, span):
        return max(0, 1 - abs(n - center) / span)

    def front(x, y):
        width, depth, _ = dims(y)
        u = min(.99999, abs(x) / width)
        # Supraorbital/zygomatic planes surrounding the recessed orbit.
        z = depth * max(0, 1 - u ** 2.8) ** .52
        z += .006 * tent(u, .68, .34) * tent(y, 1.630, .032)
        z -= .006 * tent(u, .42, .31) * tent(y, 1.656, .020)
        z -= .004 * tent(u, .77, .24) * tent(y, 1.584, .042)
        z += .003 * tent(u, .30, .35) * tent(y, 1.563, .023)
        return z

    rings = []
    for y, width, _, back in sections:
        ring = []
        for i in range(32):
            a = 2 * pi * i / 32
            x = width * sin(a)
            if cos(a) >= 0:
                z = front(x, y)
            else:
                z = -abs(back) * abs(cos(a)) ** .89
            ring.append((x, y, z))
        rings.append(ring)
    shell("Portrait | cheekbones and jaw", rings, skin)
    shell("Portrait | neck", [oval(0, y, z, rx, rz) for y, z, rx, rz in
          [(1.413, -.004, .074, .064), (1.443, -.007, .069, .063),
           (1.471, -.013, .065, .061), (1.505, -.016, .063, .055),
           (1.546, -.022, .060, .046)]], 'b68165')

    def patch(name, coords, tone, side=1):
        # XY coordinates follow the face, offsets are relative to the local skin.
        verts = [(side * x, y, front(side * x, y) + d) for x, y, d in coords]
        area = sum(x * v[1] - y * v[0] for (x, y, _), v in zip(verts, verts[1:] + verts[:1]))
        ids = list(range(len(verts)))
        mesh(name, verts, [ids if area >= 0 else ids[::-1]], tone)

    def strip(name, rows, tone, side=1):
        verts = [(side*x, y, front(side*x,y)+d) for row in rows for x,y,d in row]
        faces = []
        w = len(rows[0])
        for j in range(len(rows)-1):
            for i in range(w-1):
                a = j*w+i
                face = [a, a+1, a+1+w, a+w]
                if side < 0:
                    face.reverse()
                faces.append(face)
        mesh(name, verts, faces, tone)

    for side, label in [(1, 'L'), (-1, 'R')]:
        def fp(name, coords, tone):
            patch(label + " | " + name, coords, tone, side)

        # A narrow almond shape and a slightly heavier upper lid under straight brows.
        eye = [( .0212,1.6532,.0036), (.028,1.6563,.0045), (.039,1.6583,.0050),
               (.0495,1.6580,.0047), (.057,1.6564,.0041), (.063,1.6542,.0035),
               (.055,1.6515,.0042), (.044,1.6504,.0051), (.033,1.6508,.0045)]
        fp("eye white", eye, 'c4b4a4')
        fp("dark hazel iris", [(.0356,1.6577,.0059),(.0473,1.6578,.0059),
               (.0478,1.6544,.0061),(.0460,1.6518,.0062),(.0433,1.6507,.0062),
               (.0397,1.6509,.0062),(.0365,1.6523,.0061),(.0352,1.6550,.0061)], '392d29')
        fp("pupil", [(.0387,1.6571,.0068),(.0448,1.6572,.0068),
               (.0449,1.6531,.0068),(.0422,1.6519,.0068),(.0390,1.6531,.0068)], '211f20')
        fp("corneal glint",[(.0392,1.6568,.0071),(.0417,1.6569,.0071),
               (.0413,1.6554,.0071),(.0393,1.6552,.0071)],'d7c8b6')
        upper = [(x,y,d+.0026) for x,y,d in eye[:6]]
        strip(label+" | upper lashes", [upper,[(x,y+h,d-.0003) for (x,y,d),h in
               zip(upper,[.0007,.0013,.0018,.0017,.0011,.0003])]], '514039',side)
        fp("upper lid fold",[(.025,1.6583,.0024),(.034,1.6623,.0025),
                (.047,1.6632,.0026),(.057,1.6613,.0022),(.061,1.6599,.0019),
                (.049,1.6617,.0026),(.036,1.6614,.0026)],'a57560')
        brow_low = [(.018,1.673,.006),(.027,1.674,.008),(.042,1.676,.009),
                    (.057,1.6765,.007),(.073,1.674,.004)]
        brow_high = [(.017,1.677,.005),(.026,1.681,.007),(.043,1.683,.008),
                     (.057,1.682,.006),(.074,1.6749,.004)]
        strip(label+" | eyebrow", [brow_low, brow_high], '443735',side)
        strip(label+" | brow crest", [brow_high[1:4],[(x,y+.0015,d-.0005)
              for x,y,d in brow_high[1:4]]], '76604e',side)

    # Nose sits continuously between the eyes and broadens toward the alae.
    nose_rows = []
    for y, half, peak, wing in [(1.608,.019,.003,.001),(1.611,.026,.015,.010),
                (1.616,.028,.024,.016),(1.623,.025,.030,.014),
                (1.629,.020,.026,.007),(1.644,.017,.018,.003),
                (1.657,.014,.011,.002),(1.672,.011,.004,.001)]:
        offsets = [0,.48,.69,1,.69,.48,0]
        widths = [-1,-.78,-.46,0,.46,.78,1]
        nose_rows.append([(half*u,y,.001 if abs(u)==1 else
                           wing if abs(u)==.78 else peak*v) for u,v in zip(widths, offsets)])
    strip("Portrait | bridge and alae", nose_rows, skin)
    for side, label in [(1,'L'),(-1,'R')]:
        patch(label+" | nostril",[(.0112,1.6122,.0157),(.016,1.6140,.0187),
              (.0205,1.6135,.0147),(.0215,1.6116,.0113),(.016,1.6119,.0138)],'795547',side)

    lip_x = [-.034,-.026,-.014,-.007,0,.007,.014,.026,.034]
    def liprow(heights, offsets):
        return list(zip(lip_x, heights, offsets))
    seam = liprow([1.5755,1.5751,1.5755,1.5757,1.5754,1.5757,1.5757,1.5755,1.5760],
                  [.0015,.003,.005,.0058,.006,.0058,.005,.003,.0015])
    upper = liprow([1.5756,1.5774,1.5795,1.5814,1.5798,1.5814,1.5795,1.5775,1.5761],
                   [.0017,.0029,.0039,.0043,.0048,.0043,.0039,.0029,.0017])
    lower = liprow([1.5750,1.5731,1.5704,1.5695,1.5691,1.5695,1.5704,1.5731,1.5755],
                   [.0014,.0028,.004,.0042,.0043,.0042,.004,.0028,.0014])
    strip("Portrait | vermilion upper", [seam,upper], 'a97762')
    strip("Portrait | vermilion lower", [lower,seam], 'bd8a76')
    strip("Portrait | mouth", [[(x,y-.0011,d+.0008) for x,y,d in seam],
                                [(x,y+.00055,d+.0011) for x,y,d in seam]], '76534a')

    # Ear rims have an outward face and a shallow concha. A single white earbud
    # points down and forward as it does in the profile photograph.
    for side, label in [(1,'L'),(-1,'R')]:
        ear = [(1.616,-.001),(1.617,-.015),(1.632,-.025),(1.653,-.027),
               (1.664,-.019),(1.667,-.006),(1.659,.004),(1.648,.008),
               (1.634,.006),(1.626,.004)]
        rim = [(side*(.106+.003*tent(y,1.650,.024)),y,z) for y,z in ear]
        base = [(side*.092,y,z) for y,z in ear]
        shell(label+" | helix", [base,rim], 'bf886c')
        inner = [(side*.110,1.642+(y-1.642)*.66,-.008+(z+.008)*.63) for y,z in ear]
        mesh(label+" | ear bowl", inner, [list(range(len(inner)))], 'aa7660')
    shell("Portrait | ceramic earpiece", [oval(.106,y,zz,rx,rz,10) for y,zz,rx,rz in
             [(1.634,.003,.005,.004),(1.639,.001,.008,.008),(1.647,-.002,.007,.008),
              (1.649,-.002,.004,.004)]], 'e7e5da')
    shell("Portrait | earpiece stem",[oval(x,y,z,rx,rz,8) for x,y,z,rx,rz in
             [(.107,1.613,.013,.0028,.003),(.108,1.615,.013,.0036,.0035),
              (.110,1.637,.004,.0036,.0038),(.109,1.640,.003,.003,.003)]],'eeeade')

    # The clippered undercut sits on the skin. The longer top is a separate
    # volume with a square, forward overhang and a lifted, broken silhouette.
    rnd = Random(309)
    tau = 2 * pi

    def clamp(x):
        return min(1, max(0, x))

    def smooth(x):
        x = clamp(x)
        return x*x*(3-2*x)

    def lerp(a,b,t):
        return tuple(x*(1-t)+y*t for x,y in zip(a,b))

    def plus(a,b):
        return tuple(x+y for x,y in zip(a,b))

    def minus(a,b):
        return tuple(x-y for x,y in zip(a,b))

    def scaled(a,k):
        return tuple(x*k for x in a)

    def cross(a,b):
        return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])

    def unit(v):
        m=sum(e*e for e in v)**.5
        return scaled(v,1/max(m,1e-10))

    palette=['171819','1e1e1f','262425','2f2b2b','3a3330',
             '463b34','57473b','6d5545','866654','a57a60','b9886b','c08d70']
    pigments=[tuple(int(c[i:i+2],16) for i in (0,2,4)) for c in palette]
    groups=[([],[]) for _ in palette]

    def append(k,v,f):
        verts,faces=groups[k]
        b=len(verts)
        verts.extend(v)
        faces.extend(tuple(b+i for i in face) for face in f)

    def scalp(a,y,extra=0):
        c,s=cos(a),sin(a)
        width,_,back=dims(y)
        x=s*width
        z=front(x,y) if c>=0 else back*abs(c)**.89
        return (x+extra*s,y,z+extra*c)

    def edge(a):
        c=cos(a)
        # The frontal ~2/3 of the haircut shares one height. Temples turn
        # softly up before the cut disappears into the warmer side fade.
        front_edge=1.719 + .0022*sin(a*3+.5) + .0028*sin(13*a-.2) + .0013*sin(25*a) - .005*smooth((abs(sin(a))-.60)/.40)
        side=1.695+.021*smooth(c/.62) - .027*smooth((-c+.04)/.70)
        return side+smooth((c-.24)/.48)*(front_edge-side)+.0009*sin(13*a)

    def clipper_bottom(a):
        c=cos(a)
        # Expose skin around the ears and in front of the temples. A narrow
        # occipital tail grows low behind the ears, as in the profile reference.
        if c>.38:
            return edge(a)-.005*smooth((.81-c)/.43)
        return 1.653+.048*smooth((c+.08)/.47)-.050*smooth((-c+.02)/.48)-.022*smooth((-c-.66)/.34)

    # Shared vertices carry density from skin to near-black stubble. This field
    # changes continuously both across the temple and down behind the ear.
    n, layers=64,15
    fade_vertices, fade_faces, density=[],[],[]
    for j in range(layers):
        t=j/(layers-1)
        for i in range(n):
            a=tau*(i+.28*(j%2)+rnd.uniform(-.11,.11))/n
            y0=clipper_bottom(a)
            y=y0+(edge(a)+.011-y0)*(t+rnd.uniform(-.012,.012)*sin(pi*t))
            fade_vertices.append(scalp(a,y,.00075))
            c=cos(a)
            nape=smooth((-c+.035)/.46)
            frontal=smooth((c-.18)/.42)
            # Preserve an ample patch of warm skin at the forward corner, then
            # build darkness up to the top and across the ear into the nape.
            ink=(.12-.09*frontal)+(.87+.075*frontal)*(1-(1-t)**1.65)
            ink+=nape*.79*(1-t)**1.52
            ink+=(rnd.uniform(-.020,.020)+.010*sin(8*a))*(.09+sin(pi*t))
            # Materials interpolate in linear light. Shape this density so the
            # midpoint reads as dark, close-cut hair rather than a pale grey.
            density.append(1-(1-clamp(ink))**2.03)
            if j:
                k=(i+1)%n; lo=(j-1)*n; hi=j*n
                fade_faces.append((lo+i,lo+k,hi+k,hi+i))
    light=tuple(v/255 for v in pigments[-1])
    dark=tuple(v/255 for v in pigments[0])
    add("Hair | clipper fade",fade_vertices,fade_faces,light,"Head",shade=(dark,density))

    def envelope(a,t):
        # A hand-built barber profile: close temples, a wide upper shelf,
        # irregular flattop and a small quiff sitting further forward.
        c,s=cos(a),sin(a); t=clamp(t)
        bottom=edge(a)
        base=scalp(a,bottom)
        quiff=smooth((c-.45)/.55)
        # Smooth slopes at all guide sections prevent long specular ledges.
        outer=.1115+.0017*quiff
        depth=.107 if c>0 else .119
        bw,bf,bb=dims(bottom)
        sx,sr=s,(base[2] / (bf if c>=0 else abs(bb)))
        knots=[0,.13,.31,.54,.71,.84,.93,.978,1]
        xs=[bw,.103,outer-.0007,outer+.001,outer-.002,.098,.076,.041,.0003]
        zs=[bf if c>=0 else abs(bb),.103 if c>=0 else .113,depth,depth+.004,
            depth+.001,.094,.074,.043,.0003]
        def profile(vals):
            def slope(j):
                if j==0: return (vals[1]-vals[0])/(knots[1]-knots[0])
                if j==len(knots)-1: return (vals[j]-vals[j-1])/(knots[j]-knots[j-1])
                a0=(vals[j]-vals[j-1])/(knots[j]-knots[j-1])
                a1=(vals[j+1]-vals[j])/(knots[j+1]-knots[j])
                return 0 if a0*a1<=0 else 2*a0*a1/(a0+a1)
            for j in range(len(knots)-1):
                if t<=knots[j+1]:
                    span=knots[j+1]-knots[j]
                    f=(t-knots[j])/span; f2=f*f;f3=f2*f
                    return ((2*f3-3*f2+1)*vals[j]+(-2*f3+3*f2)*vals[j+1]
                            +(f3-2*f2+f)*slope(j)*span+(f3-f2)*slope(j+1)*span)
        xx,zz=sx*profile(xs),sr*profile(zs)
        # Widely spaced clump groups bend toward the front. They shape the
        # silhouette itself rather than appearing as a patterned layer on it.
        ripple=(.0018*sin(7*a+3*t)+.0011*sin(13*a-3*t)+.0007*sin(21*a+8*t))*sin(pi*t)
        xx+=s*ripple-.003*t
        zz+=c*ripple+quiff*(.007*sin(pi*t)**1.2) - .002*t
        top=1.816 + .006*quiff +.0028*sin(3*a-.7)*sin(pi*t)
        y=bottom+(top-bottom)*t+.0019*sin(8*a+3*t)*sin(pi*t)
        return (xx,y,zz)

    def outside(a,t):
        across=minus(envelope(a+.004,t),envelope(a-.004,t))
        rise=minus(envelope(a,min(.999,t+.001)),envelope(a,max(.001,t-.001)))
        return unit(cross(across,rise))

    # Dark continuous volume underneath overlapping locks. This avoids showing
    # the underlying head through the part when viewed from above or behind.
    ts=[0,.055,.13,.21,.31,.42,.54,.63,.71,.78,.84,.89,.93,.957,.978,.991]
    cap=[]
    for j,t in enumerate(ts):
        row=[]
        for i in range(n):
            a=tau*(i+.28*(j%2)+rnd.uniform(-.12,.12))/n
            row.append(envelope(a,t+rnd.uniform(-.010,.010)*sin(pi*t)))
        cap.append(row)
    # One welded dark underlayer keeps specular and vertex normals continuous
    # across the crown; visible texture is carried by real fibers above it.
    cap_vertices=[v for row in cap for v in row]
    cap_vertex_count=len(cap_vertices)
    cap_vertices.append((-.003,1.818,-.002))
    cap_faces=[]
    for j in range(len(ts)-1):
        for i in range(n):
            k=(i+1)%n
            lo=j*n; hi=(j+1)*n
            cap_faces.append((lo+i,lo+k,hi+k,hi+i))
    last=(len(ts)-1)*n
    cap_faces.extend((last+i,last+(i+1)%n,cap_vertex_count) for i in range(n))
    mesh("Hair | continuous undercoat",cap_vertices,cap_faces,'151413')

    def blade(path,wide,thickness,pigment,hint,thin=False):
        """A tapered curved fiber or a lock. The root sits inside the volume."""
        verts=[]; faces=[]
        # A triangular cross-section gives a narrow highlight rather than a
        # broad sheet. The last section tapers almost to zero before the tip.
        radii=(.44,1,.76,.34) if not thin else (.52,1,.58)
        for j,(point,scale) in enumerate(zip(path,radii)):
            tangent=unit(minus(path[min(j+1,len(path)-1)],path[max(0,j-1)]))
            across=unit(cross(tangent,hint))
            if sum(q*q for q in across)<.5:
                across=unit(cross(tangent,(1,0,0)))
            up=unit(cross(across,tangent))
            verts.extend([plus(point,scaled(across,-wide*scale)),
                          plus(point,scaled(up,thickness*scale)),
                          plus(point,scaled(across,wide*scale))])
            if j:
                o=(j-1)*3
                for k in range(3):
                    b=(k+1)%3
                    faces.append((o+k,o+b,o+b+3,o+k+3))
        tip=len(verts)
        verts.append(path[-1])
        last=tip-3
        faces.extend([(last+k,last+(k+1)%3,tip) for k in range(3)])
        faces.append((2,1,0))
        append(pigment,verts,faces)

    def curve(p0,p1,p2,u):
        a=lerp(p0,p1,u)
        b=lerp(p1,p2,u)
        return lerp(a,b,u)

    # Fiber guides travel up from the raised front hairline and lean forward at
    # their tips. On the back and sides they incline into the crown. Each guide
    # carries multiple fine fibers whose differing lengths break up the profile.
    for i in range(640):
        a=rnd.uniform(-pi,pi)
        t=rnd.uniform(.84,.995) if i%5==0 else rnd.uniform(.005,.98)
        c,s=cos(a),sin(a); quiff=smooth((c-.49)/.51)
        start=envelope(a,t); norm=outside(a,t)
        low=1-smooth((t-.70)/.30)
        crest=smooth((t-.51)/.32)
        # Blend the upward local growth direction into a forward drift across
        # the upper surface. Tip positions remain local to the root: no fiber
        # terminates at the parameterization's pole.
        climb=unit(minus(envelope(a,min(.997,t+.003)),envelope(a,t-.003)))
        lateral=unit(minus(envelope(a+.003,t),envelope(a-.003,t)))
        climb=unit(plus(climb,scaled(lateral,rnd.uniform(-.35,.24))))
        curl_direction=(-.20+.56*sin(3*a+2*t)+rnd.uniform(-.27,.19),
                        .36, .92+rnd.uniform(-.22,.12))
        dot=sum(x*y for x,y in zip(curl_direction,norm))
        across_top=unit(minus(curl_direction,scaled(norm,dot)))
        flow=unit(lerp(climb,across_top,crest))
        length=rnd.uniform(.009,.023)*(1+.12*quiff)
        tip=plus(plus(start,scaled(flow,length)),
                 scaled(norm,rnd.uniform(.0012,.0045)+quiff*crest*.0021))
        tip=plus(tip,(0,rnd.uniform(.0005,.0039)*crest,0))
        start=plus(start,scaled(norm,-.0024))
        mid=plus(lerp(start,tip,.57),scaled(norm,rnd.uniform(.001,.0038)))
        mid=plus(mid,(0,.0008+.0032*quiff*crest,0))
        width=rnd.uniform(.0007,.00205)*(1+.13*low)
        col=rnd.choices((0,1,2,3),weights=(4,10,7,1))[0]
        path=[curve(start,mid,tip,u) for u in (0,.26,.53,.78,1)]
        blade(path,width,width*.30,col,norm)
        # A finer strand splits from the end of a few grouped locks.
        if i%2==0:
            shift=rnd.uniform(-.0025,.0025)
            fan=(cos(a)*shift,.001,sin(a)*-shift)
            fine_tip=plus(tip,(fan[0]+rnd.uniform(-.002,.002),rnd.uniform(.0008,.004),fan[2]))
            root=plus(path[1],scaled(fan,.48))
            bend=plus(path[3],fan)
            blade([curve(root,bend,fine_tip,u) for u in (0,.40,.74,1)],
                  rnd.uniform(.00020,.00056),.00020,min(4,col+1),norm,True)

    # The lower front is a soft, uneven underlayer with a few larger groups
    # crossing it. Most fibers end just before the forehead. The few long locks
    # visibly turn forward, then down, while the upper hair sweeps upward.
    for i in range(170):
        a=rnd.uniform(-1.44,1.44)
        distinct=max(tent(a,-.36,.13),.83*tent(a,.14,.13),.53*tent(a,.77,.12))
        root_t=rnd.uniform(.10,.34)
        start=plus(envelope(a,root_t),scaled(outside(a,root_t),-.0012))
        aa=a-.08+rnd.uniform(-.048,.048)
        y=edge(aa)-rnd.uniform(-.001,.004)-distinct*rnd.uniform(.005,.016)
        tip=scalp(aa,y,.0018+distinct*rnd.uniform(.002,.007))
        mid=plus(lerp(start,tip,.50),(rnd.uniform(-.0008,.0011),.0002,
                 .005+distinct*.006))
        blade([curve(start,mid,tip,u) for u in (0,.32,.69,1)],
              rnd.uniform(.00030,.00114)*(1+.3*distinct),.00034,rnd.choice((0,0,1,2)),
              (sin(a),.045,cos(a)),True)

    # Fine clipper grain across the fade breaks up the transition from bare skin
    # to dark hair, especially directly above and to the rear of the ear.
    for k in range(1700):
        a=rnd.uniform(.73,2*pi-.73)
        y0=clipper_bottom(a); high=edge(a)+.004; t=rnd.random()
        if high-y0<.010 or t<.05:
            continue
        y=y0+t*(high-y0)
        c=cos(a); side=sin(a)
        back=smooth((-c+.035)/.46)
        aa=a-.014*(1 if side>0 else -1)+rnd.uniform(-.010,.010)
        tip=scalp(aa,y+rnd.uniform(.0017,.0046)+.0019*back,.00145)
        mid=scalp(a,y,.00140)
        width=rnd.uniform(.00015,.00040)
        spread=(cos(a)*width,0,-sin(a)*width)
        tint=max(1,min(9,int(8.9-(4.5*t+3.8*back)-rnd.random()*1.2)))
        append(tint,[minus(mid,spread),plus(mid,spread),tip],[(0,1,2)])

    # Individual crowns grow out of local, irregularly spaced groups. A group
    # lifts as a tight fan before its very fine tips diverge. Position and flow
    # follow the mirror selfie: stronger at front-middle, sparse near the back.
    fans = [
        # angle around skull, root height on top, height above root, lateral lean
        (-1.67,.80,.016,-.009),(-1.21,.88,.020,-.013),(-.91,.75,.014,-.007),
        (-.66,.94,.025,-.007),(-.31,.86,.017,-.008),(-.04,.96,.022,.004),
        (.19,.83,.016,.008),(.42,.93,.024,.004),(.72,.89,.019,.011),
        (.99,.76,.014,.007),(1.24,.91,.021,.009),(1.67,.82,.017,.010),
        (2.08,.89,.014,.002),(2.54,.94,.020,-.005),(-2.66,.86,.018,-.007),
        (-2.06,.94,.014,-.007),
    ]
    for a,t,h,lean in fans:
        forward=.005+.008*max(0,cos(a))
        center=envelope(a,t)
        norm=outside(a,t)
        sideways=unit(minus(envelope(a+.014,t),envelope(a-.014,t)))
        root=plus(center,scaled(norm,-.0020))
        main_tip=plus(center,(lean,h,forward))
        # Two short curved cores make the grouping read against a bright sky.
        # The higher arc is intentionally carried by a few distinct fibers.
        for j in range(2):
            r=plus(root,scaled(sideways,(j-.5)*.0025))
            tip=plus(lerp(center,main_tip,.66+.13*j),scaled(sideways,(j-.5)*.0027))
            mid=plus(lerp(r,tip,.47),(-.001,.0027,-.001))
            blade([curve(r,mid,tip,u) for u in (0,.25,.53,.76,1)],
                  rnd.uniform(.0009,.0015),.00043,rnd.choice((0,1,2)),norm)
        for j in range(rnd.randint(6,11)):
            offset=rnd.uniform(-.004,.004)
            r=plus(root,scaled(sideways,offset))
            twist=rnd.uniform(-.006,.006)
            tip=plus(lerp(center,main_tip,rnd.uniform(.69,1.08)),
                     plus(scaled(sideways,offset+twist),(rnd.uniform(-.001,.001),
                                                         rnd.uniform(-.0009,.0018),
                                                         rnd.uniform(-.003,.003))))
            mid=plus(lerp(r,tip,rnd.uniform(.41,.59)),
                     plus(scaled(sideways,-.33*twist),(0,.0035,-forward*.34)))
            blade([curve(r,mid,tip,u) for u in (0,.32,.69,1)],
                  rnd.uniform(.00035,.00085),.00032,rnd.choice((0,1,1,2,3)),norm,True)

    # A few longer fine guard hairs give the cut a tousled top in silhouette.
    for _ in range(45):
        a=rnd.uniform(-pi,pi);t=rnd.uniform(.64,.965)
        p=envelope(a,t)
        normal=outside(a,t)
        p0=plus(p,scaled(normal,-.0013))
        rise=rnd.uniform(.008,.0165)*(1+.31*max(0,cos(a)))
        drift=(rnd.uniform(-.012,.007),rise,
               rnd.uniform(-.002,.013)+max(0,cos(a))*.004)
        tip=plus(plus(p0,drift),scaled(normal,rnd.uniform(.002,.005)))
        mid=plus(lerp(p0,tip,.56),(rnd.uniform(-.001,.002),.003,rnd.uniform(-.002,.001)))
        blade([curve(p0,mid,tip,u) for u in (0,.42,.72,1)],
              rnd.uniform(.00025,.00065),.00030,rnd.choice((1,1,2,3)),normal,True)

    # Short occipital tail: narrow behind the ear, longer in rear profile.
    for _ in range(144):
        a=pi+rnd.uniform(-1.24,1.24)
        y=clipper_bottom(a)+rnd.uniform(.003,.023)
        start=scalp(a,y+.011,.001)
        tip=plus(scalp(a,y-rnd.uniform(.006,.024),.0025),(0,-.002,-.002))
        mid=plus(lerp(start,tip,.58),(0,.0005,-.004))
        blade([curve(start,mid,tip,u) for u in (0,.39,.72,1)],
              rnd.uniform(.00035,.0012),.00042,rnd.choice((1,2,3)),(sin(a),0,cos(a)),True)

    for k,(vs,fs) in enumerate(groups):
        if fs:
            mesh("Hair | textured crop %02d"%k,vs,fs,palette[k])
