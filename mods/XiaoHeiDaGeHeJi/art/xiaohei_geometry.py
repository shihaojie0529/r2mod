"""Stylized gunner sculpt in metres. Y is up, +Z is forward. Joint origins double as pose anchors."""
from math import cos, exp, pi, sin, sqrt, tau
from random import Random

from build_body import cross, plus, scale, unit


PIVOTS = {"Hips": (0,.93,0), "Chest": (0,1.22,0), "Head": (0,1.52,.012),
          "RightUpperArm": (.199,1.425,0), "RightLowerArm": (.278,1.265,.177),
          "RightHand": (.245,1.340,.379), "LeftUpperArm": (-.198,1.425,0),
          "LeftLowerArm": (-.272,1.167,-.005), "LeftHand": (-.240,1.022,.158)}
for side, s in (("Right",1), ("Left",-1)):
    PIVOTS.update({side+"UpperLeg": (.094*s,.93,0), side+"LowerLeg": (.102*s,.51,.012),
                   side+"Foot": (.110*s,.116,0)})

INK = "151d29"
POLO = "161a20"
SEAM = "2b3238"
IVORY = "d8e1dc"
NAVY = "2b3e55"
STEEL = "677d8b"
TEAL = "1faab1"
LIGHT = "85f5de"
SKIN = "bd8766"

from sculpt_tools import sculpt_tools


def build(add):
    mesh, shell, line, body, limb, plaque, box = sculpt_tools(add)
    torso=[(.924,0,0,.164,.107),(.937,0,0,.165,.109),(1.026,0,-.002,.155,.106),
           (1.119,0,0,.153,.106),(1.238,0,.001,.167,.119),(1.336,0,.001,.181,.127),
           (1.406,0,-.001,.196,.110),(1.434,0,-.006,.202,.095),
           (1.462,0,-.008,.159,.082),(1.492,0,-.008,.066,.065)]
    def polo_point(a,y,allowance=0):
        x,z,rx,rz=torso[-1][1:]
        for lo,hi in zip(torso,torso[1:]):
            if y<=hi[0]:
                f=max(0,(y-lo[0])/(hi[0]-lo[0]))
                x,z,rx,rz=[u+(v-u)*f for u,v in zip(lo[1:],hi[1:])]
                break
        return x+sin(a)*(rx+allowance),y,z+cos(a)*(rz+allowance)
    def opening(a):
        return 1.492-.061*max(0,cos(a))**5
    angles=[tau*j/64 for j in range(64)]
    shell("Polo / short sleeve knit",[[polo_point(a,.924+(opening(a)-.924)*j/46) for a in angles]
          for j in range(47)],POLO,"Chest",False)
    body("Polo / gathered hem",[(.921,0,0,.165,.110),(.936,0,0,.168,.111)],SEAM,"Chest",36)
    body("Neck",[(1.418,0,-.006,.144,.093),(1.440,0,-.006,.121,.087),
                 (1.468,0,-.009,.084,.072),(1.486,0,-.013,.063,.063),
                 (1.560,0,-.018,.056,.054)],"b78263","Head",40)
    # Fold the collar along the actual three-dimensional neckline. Each front tip
    # lies against the polo, with the center open to bare skin.
    for s in (-1,1):
        verts=[]
        for a,drop in ((.025,0),(.14,.010),(.26,.034),(.36,.046),(.52,.028),
                       (.74,.019),(1.05,.015),(1.40,.014),(1.85,.014),(2.45,.012),(3.142,.012)):
            top=opening(a)
            edge=polo_point(s*a,top,.002)
            fold=polo_point(s*a,top+.001,.005)
            leaf=polo_point(s*a,top-drop,.004)
            verts.extend((edge,(fold[0],fold[1]+.002,fold[2]),leaf))
        mesh("Polo / folded collar",verts,[(i,i+1,i+4,i+3) for i in range(0,len(verts)-3) if i%3!=2],
             "20272b","Chest")
    placket=[]
    for y in (1.313,1.34,1.38,1.41,1.432):
        placket.extend((polo_point(-.064,y,.0015),polo_point(.064,y,.0015)))
    mesh("Polo / placket",placket,[(i,i+1,i+3,i+2) for i in (0,2,4,6)],"20262a","Chest")
    for y in (1.334,1.372,1.410):
        x,y,z=polo_point(0,y,.002)
        limb("Polo / button",(x,y,z),(x,y,z+.002),[(0,.0027,.0027),(1,.0027,.0027)],"455050","Chest",10)
    # A small three-frame recorder motif, embroidered on the left chest.
    for i in range(3):
        line("Polo / frame stitch", [(-.110+i*.010,1.353,.111),(-.105+i*.010,1.364,.114)],
             .0026,IVORY,"Chest",5)
    for side,s in (("Right",1),("Left",-1)):
        shoulder=PIVOTS[side+"UpperArm"]; elbow=PIVOTS[side+"LowerArm"]; wrist=PIVOTS[side+"Hand"]
        upper,lower,hand = side+"UpperArm",side+"LowerArm",side+"Hand"
        limb(side+" / upper arm",shoulder,elbow,[(.26,.060,.067),(.54,.053,.058),
             (.83,.041,.044),(1.02,.036,.040)],SKIN,upper)
        limb(side+" / forearm",elbow,wrist,[(-.07,.035,.039),(.03,.039,.043),(.22,.045,.047),
             (.63,.035,.036),(.94,.026,.031),(1.08,.026,.030)],SKIN,lower)
        # A curved sleeve grows out of the shoulder yoke. Use the very same
        # surface parameterization for the knit, hem and three woven stripes.
        span=tuple(b-a for a,b in zip(shoulder,elbow))
        sections=[((s*.132,1.420,-.006),.043,.063),
                  ((s*.166,1.423,-.005),.049,.071),
                  ((s*.194,1.416,-.001),.060,.080),
                  (plus(shoulder,scale(span,.155)),.068,.081),
                  (plus(shoulder,scale(span,.34)),.067,.077),
                  (plus(shoulder,scale(span,.52)),.061,.070),
                  (plus(shoulder,scale(span,.58)),.060,.068)]
        def sleeve(j,a,margin=0):
            center,ry,rz=sections[j]
            axis=unit(tuple(q-p for p,q in zip(sections[max(0,j-1)][0],sections[min(len(sections)-1,j+1)][0])))
            outer=unit(plus((0,1,0),scale(axis,-axis[1])))
            across=unit(cross(axis,outer))
            return plus(center,plus(scale(outer,(ry+margin)*cos(a)),scale(across,(rz+margin)*sin(a))))
        shell(side+" / curved polo sleeve",[[sleeve(j,tau*i/32) for i in range(32)]
              for j in range(len(sections))],POLO,upper)
        shell(side+" / ribbed sleeve edge",[[sleeve(j,tau*i/32,.0006) for i in range(32)]
              for j in (5,6)],SEAM,upper,False)
        for z in (-.020,0,.020):
            verts=[]
            for j,(_,ry,rz) in enumerate(sections):
                for q in (z-.004,z+.004):
                    verts.append(sleeve(j,s*q/rz,.0011))
            mesh(side+" / sleeve tape",verts,[(i,i+1,i+3,i+2) for i in range(0,len(verts)-2,2)],IVORY,upper)
            yoke=[]
            for x in (.082,.098,.114,.131,.149,.168):
                for zz in (z-.004,z+.004):
                    lo,hi=1.434,1.492
                    for _ in range(14):
                        y=(lo+hi)*.5
                        rx,_,cz=polo_point(pi/2,y)
                        _,_,zf=polo_point(0,y)
                        rim=rx*sqrt(max(0,1-((zz-cz)/(zf-cz))**2))
                        if rim>x: lo=y
                        else: hi=y
                    yoke.append((s*x,(lo+hi)*.5+.0012,zz))
            mesh(side+" / shoulder tape",yoke,[(i,i+1,i+3,i+2) for i in range(0,len(yoke)-2,2)],
                 IVORY,"Chest")
        limb(side+" / cuff shell",elbow,wrist,[(.76,.034,.038),(.81,.035,.039),(.99,.031,.035)],
             INK,lower)
        limb(side+" / cuff seam",elbow,wrist,[(.80,.036,.040),(.84,.036,.039)],
             TEAL if s==1 else STEEL,lower)
        # Short fingerless gloves. The right index finger runs along the trigger guard.
        x,y,z=wrist
        palm=(x,y-.012,z+.045)
        limb(side+" / glove",wrist,palm,[(-.16,.027,.030),(.2,.036,.030),(.75,.036,.032),
             (1.05,.028,.030)],"37444d",hand)
        for i in range(4):
            if s==1:
                height=y+.008-.014*i; tipz=z+(.092 if i==0 else .067)
                line(side+" / grip finger",[(x+.024,height,z+.031),(x+.029,height,tipz-.010),
                     (x+.013,height-.003,tipz),(x-.004,height-.006,tipz-.002)],
                     [.009,.009,.008,.0065],SKIN,hand)
            else:
                lateral=-.023+i*.013
                line(side+" / bent finger",[(x+lateral,y-.007,z+.044),(x+lateral,y-.031,z+.061),
                     (x+lateral,y-.052,z+.057),(x+lateral,y-.056,z+.047)],
                     [.009,.009,.008,.006],SKIN,hand)
        line(side+" / thumb",[(x-s*.022,y+.004,z+.009),(x-s*.031,y+.002,z+.041),
                (x-s*.018,y-.005,z+.059)],[.013,.012,.008],SKIN,hand)
    # The pants and soles have a slightly exaggerated graphic silhouette against dark terrain.
    body("Trousers / seat",[(.795,0,-.006,.134,.092),(.842,0,-.008,.160,.110),
         (.925,0,-.004,.159,.109),(.958,0,0,.146,.100)],NAVY,"Hips",28)
    body("Belt / webbing",[(.929,0,-.003,.163,.113),(.964,0,-.002,.154,.106)],INK,"Hips")
    box("Belt / clasp",(0,.942,.113),(.027,.015,.008),.003,STEEL,"Hips")
    for s,side in ((1,"Right"),(-1,"Left")):
        thigh,calf,foot=side+"UpperLeg",side+"LowerLeg",side+"Foot"
        def legrows(rows):
            return [(y,s*x,z,a,b) for y,x,z,a,b in rows]
        body(side+" / tapered thigh",legrows([(.474,.102,.012,.060,.068),(.533,.103,.011,.065,.076),
             (.637,.103,0,.073,.086),(.75,.100,-.003,.087,.103),(.86,.091,-.005,.093,.103),
             (.93,.088,-.004,.083,.089)]),NAVY,thigh)
        body(side+" / shin",legrows([(.120,.110,0,.043,.052),(.221,.112,-.005,.050,.057),
             (.313,.111,-.013,.062,.070),(.387,.110,-.015,.066,.076),(.477,.104,.006,.059,.065),
             (.524,.103,.013,.061,.068)]),NAVY,calf)
        # A stitched knee dart follows the curved fabric (clear even at profile angles).
        path=[(s*(.103+.064*sin(a)),.53-.014*cos(a),.012+.076*cos(a))
              for a in (-.88,-.5,0,.5,.88)]
        line(side+" / knee dart",path,.002,"465566",thigh,5)
        line(side+" / outer leg seam",[(s*.169,.83,-.068),(s*.183,.735,-.034),
             (s*.176,.635,-.019),(s*.167,.554,-.010)],.0025,"475a69",thigh,6)
        plaque(side+" / trouser slash",[(s*.164,.827,.047),(s*.160,.827,.057),
               (s*.149,.746,.066),(s*.149,.732,.055)],(s*.003,0,0),"597184",thigh)
        body(side+" / boot collar",legrows([(.094,.110,-.003,.052,.058),(.160,.110,-.004,.054,.059),
             (.191,.110,-.006,.050,.056)]),INK,foot)
        def shoe(label, rows, tint):
            rings=[]
            for z,r,b,t in rows:
                rings.append([(s*.109+r*(1 if sin(a)>0 else -1)*abs(sin(a))**.72,
                              (b+t)/2+(t-b)/2*(1 if cos(a)>0 else -1)*abs(cos(a))**.6,z)
                             for a in [tau*j/16 for j in range(16)]])
            shell(side+" / "+label,rings,tint,foot)
        shoe("outsole",[(-.083,.041,.014,.049),(-.060,.054,.007,.051),(.052,.060,.008,.047),
             (.148,.064,.011,.049),(.190,.040,.021,.050)],"81949b")
        shoe("sneaker",[(-.080,.039,.045,.112),(-.046,.053,.041,.152),(.024,.057,.038,.143),
             (.082,.059,.040,.101),(.145,.058,.044,.081),(.185,.038,.045,.063)],"d5ddd8")
        for i in range(3):
            y=.130-i*.010; z=.041+i*.017
            line(side+" / black lace",[(s*.109-.029,y-.005,z),(s*.109,y+.002,z-.003),
                 (s*.109+.028,y-.004,z)],.0032,"34414a",foot)
        plaque(side+" / heel accent",[(s*.159,.070,-.065),(s*.169,.075,.009),
               (s*.170,.086,-.001),(s*.159,.090,-.062)],(s*.002,0,0),TEAL,foot)
    # A miniature field recorder is worn on the back of the belt.
    box("Replay pack / shell",(0,.975,-.114),(.101,.061,.034),.014,INK,"Hips")
    box("Replay pack / inset",(0,.977,-.148),(.090,.047,.006),.004,STEEL,"Hips")
    box("Replay pack / cassette glass",(0,.980,-.155),(.071,.026,.004),.003,"263b47","Hips")
    for x in (-.044,.044):
        limb("Replay pack / reel",(x,.980,-.158),(x,.980,-.165),
             [(0,.016,.016),(1,.016,.016)],IVORY,"Hips",12)
        limb("Replay pack / reel pin",(x,.980,-.165),(x,.980,-.167),
             [(0,.006,.006),(1,.006,.006)],INK,"Hips",10)
    line("Replay pack / tape",[(-.046,.968,-.165),(.046,.968,-.165)],.0025,LIGHT,"Hips",5)
    # Angular cast gun body. Profiles are cut from both sides, rear-to-muzzle.
    hand="RightHand"; x=.246
    def gun(name, profile, thick, tint):
        plaque("Light pistol / "+name,[(x-thick,y,z) for z,y in profile],(2*thick,0,0),tint,hand)
    gun("grip",[(.414,1.365),(.455,1.366),(.440,1.277),(.407,1.273),(.402,1.286)],.019,"243240")
    gun("slide",[(.376,1.378),(.396,1.401),(.543,1.405),(.599,1.390),(.595,1.362),
                 (.566,1.350),(.404,1.352),(.379,1.362)],.027,"d4dbd1")
    gun("frame",[(.391,1.360),(.565,1.359),(.554,1.343),(.471,1.338),(.444,1.350),(.401,1.343)],
        .025,INK)
    gun("power cell",[(.456,1.371),(.481,1.390),(.548,1.389),(.562,1.375),(.544,1.365),
                     (.466,1.365)],.0285,"283e4a")
    gun("energized channel",[(.474,1.372),(.485,1.384),(.547,1.383),(.537,1.374)],.030,LIGHT)
    gun("rear sight",[(.386,1.398),(.392,1.412),(.405,1.412),(.410,1.398)],.020,INK)
    gun("barrel crown",[(.570,1.399),(.595,1.394),(.605,1.387),(.605,1.366),
                        (.593,1.358),(.573,1.357)],.026,INK)
    limb("Light pistol / bore",(x,1.381,.599),(x,1.381,.607),
         [(0,.014,.014),(1,.014,.014)],TEAL,hand,12)
    limb("Light pistol / core",(x,1.381,.606),(x,1.381,.609),
         [(0,.007,.007),(1,.007,.007)],LIGHT,hand,10)
    line("Light pistol / trigger guard",[(x,1.349,.484),(x,1.316,.479),(x,1.311,.446)],
         [.006,.005,.005],INK,hand,6)
    from pistol_details import build as build_pistol_details
    build_pistol_details(add)
    build_face(mesh,shell,line,body,limb)


def build_face(mesh,shell,line,body,limb):
    # Warm oval face with a high forehead, softly squared chin and broad straight brows.
    sections=[(1.516,.027,.064,-.032),(1.524,.052,.089,-.045),(1.541,.072,.098,-.060),
              (1.566,.085,.099,-.077),(1.598,.097,.101,-.093),(1.633,.106,.101,-.104),
              (1.661,.108,.099,-.111),(1.691,.106,.108,-.115),(1.720,.105,.108,-.115),
              (1.750,.101,.103,-.113),(1.780,.088,.089,-.104),(1.802,.068,.069,-.083),
              (1.818,.038,.037,-.055),(1.825,.009,.007,-.023)]
    def dims(y):
        for row,nextrow in zip(sections,sections[1:]):
            if y<=nextrow[0]:
                f=max(0,(y-row[0])/(nextrow[0]-row[0]))
                return [p+(q-p)*f for p,q in zip(row[1:],nextrow[1:])]
        return sections[-1][1:]
    def front(x,y):
        width,depth,_=dims(y); u=min(1,abs(x)/width)
        z=depth*max(0,1-u**2.55)**.55
        z+=.006*exp(-((y-1.637)/.022)**2-((u-.65)/.24)**2)
        z-=.006*exp(-((y-1.665)/.018)**2-((u-.46)/.29)**2)
        z+=.002*exp(-((y-1.568)/.016)**2-((u-.23)/.25)**2)
        return z
    def point(a,y):
        w,d,b=dims(y); x=w*sin(a)
        return x,y,front(x,y) if cos(a)>=0 else b*abs(cos(a))**.88
    n=48
    shell("Portrait / face planes",[[point(tau*j/n,y) for j in range(n)] for y,*_ in sections if y<=1.75],
          SKIN,"Head")
    def patch(name, rows, tone, side=1):
        pts=[(side*x,y,front(side*x,y)+z) for row in rows for x,y,z in row]
        w=len(rows[0])
        faces=[(j*w+i,j*w+i+1,(j+1)*w+i+1,(j+1)*w+i) for j in range(len(rows)-1) for i in range(w-1)]
        mesh(name,pts,faces,tone,"Head")
    def stripe(name, lower, upper, tone,side=1):
        patch(name,[lower,upper],tone,side)
    for s in (-1,1):
        # Eye length and lid height are taken from the straight-on reference.
        xx=[.017,.025,.037,.048,.059,.071]
        lo=[1.661,1.657,1.6549,1.6550,1.6575,1.6625]
        hi=[1.6613,1.6659,1.6700,1.6705,1.6686,1.663]
        strip=lambda ys,z:[(x,y,z) for x,y in zip(xx,ys)]
        stripe("Eye / sclera",strip(lo,.0024),strip(hi,.0024),"ddd3c2",s)
        iris=[(.029,1.659,.0031),(.034,1.6559,.0036),(.043,1.6554,.004),
              (.050,1.6567,.0039),(.055,1.6602,.0034)]
        stripe("Eye / iris",iris,[(.029,1.6666,.0034),(.034,1.6692,.0039),(.043,1.6702,.0044),
               (.050,1.6696,.0039),(.055,1.6665,.0034)],"362c28",s)
        stripe("Eye / pupil",[(.035,1.660,.0044),(.039,1.658,.0048),(.045,1.659,.0046),(.048,1.661,.0044)],
               [(.035,1.665,.0044),(.039,1.667,.0048),(.045,1.667,.0046),(.048,1.665,.0044)],"181c20",s)
        stripe("Eye / glint",[(.036,1.6645,.0052),(.039,1.6644,.0052)],
               [(.036,1.6667,.0052),(.039,1.6664,.0052)],"e8e4d8",s)
        stripe("Eye / upper lid",strip(hi,.0048),strip([v+h for v,h in zip(hi,(.0005,.0018,.0023,.0026,.0018,.0004))],.0045),"473932",s)
        stripe("Eye / lid fold",[(x,y+h,.0017) for x,y,h in zip(xx,hi,(.002,.004,.0046,.0043,.0033,.0017))],
               [(x,y+h,.0017) for x,y,h in zip(xx,hi,(.0023,.0047,.0056,.0052,.0040,.0019))],"a06f57",s)
        brow=[(.016,1.683,.004),(.028,1.686,.006),(.043,1.689,.006),(.058,1.690,.005),
              (.072,1.686,.003),(.079,1.682,.002)]
        stripe("Brow / tapered arch",brow,[(.018,1.690,.004),(.029,1.697,.006),(.044,1.699,.006),
               (.059,1.698,.005),(.073,1.690,.003),(.079,1.683,.002)],"302b29",s)
        ear="b47f66"
        body("Ear / helix",[(1.607,s*.101,-.008,.005,.007),(1.618,s*.107,-.007,.011,.014),
             (1.649,s*.109,-.008,.011,.019),(1.665,s*.105,-.011,.008,.015),
             (1.669,s*.103,-.010,.004,.008)],SKIN,"Head",16)
        line("Ear / concha",[(s*.117,1.65,-.008),(s*.118,1.647,-.017),(s*.118,1.63,-.016),
             (s*.116,1.626,-.010)],.0023,ear,"Head")
    # A single silver stud from the side view, on the anatomical right.
    x=.071; y=1.69; z=front(x,y)+.003
    limb("Brow / silver stud",(x,y,z),(x,y,z+.004),[(0,.0028,.0028),(1,.002,.002)],IVORY,"Head",10)
    nose=[]
    for y,w,h,wing in [(1.609,.018,.002,.003),(1.612,.025,.014,.010),(1.619,.026,.026,.015),
                (1.626,.022,.032,.010),(1.641,.017,.024,.005),(1.661,.014,.014,.002),
                (1.678,.011,.006,.001),(1.686,.007,.001,0)]:
        nose.append([(w*u,y,off) for u,off in zip((-1,-.72,-.43,0,.43,.72,1),
                     (.0004,wing,h*.71,h,h*.71,wing,.0004))])
    patch("Nose / bridge and alae",nose,SKIN)
    for s in (-1,1):
        stripe("Nose / nostril",[(.009,1.613,.015),(.016,1.613,.014),(.022,1.614,.011)],
               [(.010,1.6145,.017),(.016,1.616,.018),(.022,1.615,.013)],"7b5549",s)
    xs=[-.039,-.030,-.016,-.008,0,.008,.016,.030,.039]
    def lip(ys,ds):
        return list(zip(xs,ys,ds))
    seam=lip([1.572,1.571,1.5695,1.569,1.5688,1.5691,1.5698,1.571,1.5728],
             [.001,.003,.005,.006,.006,.006,.005,.003,.001])
    stripe("Mouth / upper lip",seam,lip([1.572,1.574,1.576,1.578,1.576,1.578,1.576,1.574,1.5728],
           [.001,.003,.004,.005,.006,.005,.004,.003,.001]),"9b6b5c")
    stripe("Mouth / lower lip",lip([1.5718,1.5688,1.563,1.562,1.5618,1.562,1.563,1.569,1.5727],
           [.001,.002,.005,.006,.007,.006,.005,.002,.001]),seam,"b98471")
    stripe("Mouth / seam",[(x,y-.0007,z+.0008) for x,y,z in seam],
           [(x,y+.0007,z+.0008) for x,y,z in seam],"725044")
    # The cap has close sides and broad, irregular overlapping sweeps across the forehead.
    rng=Random(74)
    def surface(a,t,offset=0):
        frontness=max(0,cos(a))
        y0=1.603+.145*frontness**.80+.056*abs(sin(a))**1.8
        y0-=.011*exp(-((a+.42)/.33)**2)
        # A broad rounded crop grows from the skull rather than converging linearly
        # into a point. The raised front and short temples follow the two close-ups.
        from math import asin
        angle0=asin(max(-.98,min(.98,(y0-1.689)/.148)))
        angle=angle0+(pi/2-angle0)*t
        lift=sin(pi*t)
        rad=max(.007,cos(angle))
        xx=(.118*rad+offset)*sin(a)-.006*t+.0022*sin(3*a+.8)*lift
        zz=(.133*rad+offset)*cos(a)-.005+.0045*sin(3*a+.8)*cos(a)*lift+.005*frontness*(1-t)
        yy=1.689+.148*sin(angle)+.0035*sin(3*a+.8)*lift
        return (xx,yy,zz)
    ns=80; rings=[[surface(tau*j/ns,t) for j in range(ns)] for t in [0,.06,.16,.28,.42,.56,.7,.81,.90,.96,.99,1]]
    for inward,drop in ((.985,.003),(.945,.006)):
        rings.insert(0,[(x*inward,y-drop,z*inward) for x,y,z in [surface(tau*j/ns,0) for j in range(ns)]])
    shell("Hair / close-cut base",rings,"1b1e21","Head")
    palettes=["1a1c20","1e2024","23252a","292b2e","303235"]
    for band,(count,lower,upper) in enumerate(((95,.01,.30),(115,.20,.55),(125,.43,.79),(88,.70,.992))):
        grouped=[([],[]) for _ in palettes]
        for i in range(count):
            a=tau*(i+rng.uniform(-.43,.43))/count-pi
            t=rng.uniform(lower,upper)
            da=(.23+.20*max(0,cos(a)))*(-1 if sin(a+.55)<0 else 1)
            dt=rng.uniform(.16,.33)
            span=rng.uniform(.050,.131)
            verts=[]
            for u,r in ((0,.40),(.28,1),(.60,.91),(.83,.53),(1,.018)):
                aa=a+u*da+.05*sin(pi*u); tt=max(0,t-dt*u)
                yy=min(0,t-dt*u)*.032
                for offset,mid in ((-span*r,0),(0,1),(span*r,0)):
                    px,py,pz=surface(aa+offset,tt,.0008+.0024*sin(pi*u)+mid*.0021)
                    verts.append((px,py+yy+.0027*sin(pi*u),pz))
            color_idx=rng.choices(range(5),weights=(.18,.38,.29,.13,.02))[0]
            points,faces=grouped[color_idx]
            base=len(points); points.extend(verts)
            faces.extend(tuple(base+j for j in f) for v in (0,3,6,9)
                         for f in ((v,v+1,v+4,v+3),(v+1,v+2,v+5,v+4)))
        for i,(vs,fs) in enumerate(grouped):
            if fs: mesh("Hair / swept locks "+str(band)+"."+str(i),vs,fs,palettes[i],"Head")

    wisps=[]; faces=[]
    for i in range(88):
        a=rng.uniform(-pi,pi)
        t=rng.uniform(.11,.94)
        k=len(wisps)
        for u,r in ((0,.35),(.36,1),(.70,.65),(1,.015)):
            aa=a+.12*u*(-1 if sin(a+.6)<0 else 1)
            tt=min(.997,t+.048*u)
            for w in (-1,0,1):
                x,y,z=surface(aa+w*.018*r,tt,.0008+.004*u+.001*(1-abs(w))*r)
                wisps.append((x+.0035*u*sin(a),y+.009*u*u,z+.0015*u*cos(a)))
        for j in (0,3,6):
            faces.extend(((k+j,k+j+1,k+j+4,k+j+3),(k+j+1,k+j+2,k+j+5,k+j+4)))
    mesh("Hair / loose crown tips",wisps,faces,"222429","Head")
