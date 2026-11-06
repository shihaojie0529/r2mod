"""Jie Ge's portrait, casual clothing, sword case and broad double-edged riding jian."""
from math import asin, cos, exp, pi, sin, sqrt, tau
from random import Random
from build_body import cross, plus, scale, unit
from sculpt_tools import sculpt_tools

PIVOTS={"Hips":(0,.915,0),"Chest":(0,1.225,0),"Head":(0,1.52,-.005),
        "RightUpperArm":(.202,1.41,.004),"RightLowerArm":(.269,1.183,.057),
        "RightHand":(.243,1.347,.194),"LeftUpperArm":(-.200,1.410,.001),
        "LeftLowerArm":(-.281,1.196,-.063),"LeftHand":(-.359,1.067,-.169),
        "RightUpperLeg":(.092,.918,0),"RightLowerLeg":(.113,.512,.149),"RightFoot":(.088,.112,.244),
        "LeftUpperLeg":(-.092,.918,0),"LeftLowerLeg":(-.116,.506,-.075),"LeftFoot":(-.100,.112,-.228),
        "Ride":(0,0,0)}
SKIN="c28c70"
TEE="d6cfc0"
HEM="c3bbad"
PANTS="303940"
JADE="23636a"
TEAL="4aafa4"
MINT="a7f1d5"
GOLD="c59652"
DARK="253336"


def build(add):
    mesh,shell,line,body,limb,plaque,box=sculpt_tools(add)
    chest="Chest"
    # Loose cotton hangs from the shoulder, with a dropped hem and a true crew-neck opening.
    torso=[(.856,.196,.159),(.880,.197,.154),(.977,.184,.130),(1.077,.171,.115),
           (1.184,.176,.124),(1.292,.180,.131),(1.373,.191,.117),
           (1.423,.198,.102),(1.451,.162,.083),(1.477,.080,.065)]
    def shirt(a,y,margin=0):
        w,d=torso[-1][1:]
        for lo,hi in zip(torso,torso[1:]):
            if y<=hi[0]:
                f=max(0,(y-lo[0])/(hi[0]-lo[0]))
                w,d=[u+(v-u)*f for u,v in zip(lo[1:],hi[1:])]
                break
        fold=(.0038*sin(8*a+y*4)+.0027*sin(15*a-y*5))*max(0,(1.43-y)/.58)**.67
        return (sin(a)*(w+margin+fold),y,cos(a)*(d+margin+fold)-.005)
    def neck_edge(a): return 1.477-.034*max(0,cos(a))**3
    def hem(a): return .856+.009*cos(a+.5)+.004*sin(4*a)
    angles=[tau*j/60 for j in range(60)]
    shell("Tee / loose cotton",[[shirt(a,hem(a)+(neck_edge(a)-hem(a))*j/32) for a in angles]
          for j in range(33)],TEE,chest,False)
    shell("Tee / crew neck binding",[[shirt(a,neck_edge(a)-drop,.0017) for a in angles]
          for drop in (.014,.011,.002,0)],HEM,chest,False)
    shell("Tee / turned hem",[[shirt(a,hem(a)+h,.001) for a in angles] for h in (0,.008)],HEM,chest,False)
    body("Neck / collarbone",[(1.422,0,-.009,.125,.084),(1.455,0,-.010,.084,.069),
              (1.491,0,-.015,.058,.056),(1.559,0,-.018,.054,.051)],"b78265","Head",32)
    for side,sign in (("Right",1),("Left",-1)):
        upper,lower,hand=side+"UpperArm",side+"LowerArm",side+"Hand"
        shoulder,elbow,wrist=(PIVOTS[k] for k in (upper,lower,hand))
        a=tuple(q-p for p,q in zip(shoulder,elbow))
        limb(side+" / upper arm",shoulder,elbow,[(.38,.049,.052),(.63,.046,.050),
             (.86,.036,.041),(1.04,.032,.035)],SKIN,upper)
        limb(side+" / forearm",elbow,wrist,[(-.085,.029,.033),(.02,.034,.037),(.18,.040,.043),
             (.45,.037,.039),(.74,.028,.031),(1.11,.023,.026)],SKIN,lower)
        # Cloth widens near the low shoulder seam and ends in a soft oblique cylindrical cuff.
        profiles=[((.142*sign,1.409,-.002),.044,.068),((.174*sign,1.411,0),.053,.073),
                  ((.203*sign,1.406,0),.062,.075),(plus(shoulder,scale(a,.19)),.069,.076),
                  (plus(shoulder,scale(a,.44)),.067,.073),(plus(shoulder,scale(a,.67)),.064,.070),
                  (plus(shoulder,scale(a,.735)),.066,.070)]
        def sleeve(j,t,extra=0):
            center,ry,rz=profiles[j]
            axis=unit(tuple(q-p for p,q in zip(profiles[max(0,j-1)][0],profiles[min(len(profiles)-1,j+1)][0])))
            up=unit(plus((0,1,0),scale(axis,-axis[1]))); across=cross(axis,up)
            return plus(center,plus(scale(up,(ry+extra)*cos(t)),scale(across,(rz+extra)*sin(t))))
        shell(side+" / dropped sleeve",[[sleeve(j,tau*i/32) for i in range(32)]
              for j in range(len(profiles))],TEE,upper)
        shell(side+" / sleeve edge",[[sleeve(j,tau*i/32,.0005) for i in range(32)]
              for j in (5,6)],HEM,upper,False)
    # An actual two-finger sword seal. The little and ring fingers are held down by the thumb.
    hand="RightHand"
    limb("Seal / palm",PIVOTS[hand],(.240,1.399,.209),[(-.16,.022,.022),(.12,.027,.025),
         (.60,.030,.018),(1,.026,.015)],SKIN,hand)
    for i,(x,length) in enumerate(((.227,.082),(.244,.088))):
        path=[(x,1.393,.207),(x-.002,1.420,.217),(x-.007,1.447,.224),
              (x-.011,1.393+length,.231)]
        line("Seal / extended finger",path,[.0082,.008,.007,.0058],SKIN,hand,10)
        line("Seal / fingernail",[(x-.009,1.393+length-.008,.234),(x-.011,1.393+length-.002,.235)],
             .0028,"d6a990",hand,7)
    for i,x in enumerate((.257,.269)):
        line("Seal / curled finger",[(x,1.390,.207),(x,1.402,.218),
             (x-.003,1.397,.230),(x-.009,1.381,.229)],[.008,.008,.007,.0058],SKIN,hand,9)
    line("Seal / thumb",[(.214,1.367,.207),(.213,1.379,.223),(.228,1.385,.234),(.255,1.388,.231)],
         [.012,.011,.009,.0065],SKIN,hand,10)
    hand="LeftHand"
    limb("Balance / palm",PIVOTS[hand],(-.395,1.050,-.200),[(-.12,.022,.022),(.27,.028,.018),
         (.77,.029,.016),(1,.026,.015)],SKIN,hand)
    for i in range(4):
        x=-.382-.009*i; z=-.206+.009*i
        line("Balance / finger",[(x,1.049,z),(x-.015,1.033,z-.013),
             (x-.023,1.010+.003*i,z-.018),(x-.020,1.000+.004*i,z-.015)],
             [.0078,.0073,.0064,.0048],SKIN,hand,9)
    line("Balance / thumb",[(-.369,1.059,-.189),(-.368,1.044,-.214),(-.380,1.036,-.220)],
         [.011,.009,.006],SKIN,hand)
    # Straight wide trousers fold gently over light everyday trainers.
    body("Trousers / seat",[(.807,0,-.004,.067,.063),(.837,0,-.005,.143,.090),
         (.918,0,-.002,.158,.109),(.955,0,-.002,.150,.103)],PANTS,"Hips",30)
    for side,s in (("Right",1),("Left",-1)):
        thigh,shin,foot=side+"UpperLeg",side+"LowerLeg",side+"Foot"
        hip,knee,ankle=[PIVOTS[k] for k in (thigh,shin,foot)]
        # Continuous horizontal sections meet at one knee profile. The skinning
        # joint is the same point as the seam, so both a standing and bent leg stay clothed.
        def center(y):
            a,b=(ankle,knee) if y<knee[1] else (knee,hip)
            t=(y-a[1])/(b[1]-a[1])
            return (a[0]+t*(b[0]-a[0]),a[2]+t*(b[2]-a[2]))
        def trouser_ring(y,w,d):
            x,z=center(y)
            return [(x+sin(a)*(w+.0014*cos(5*a)),y,z+cos(a)*(d+.0014*cos(5*a)))
                    for a in [tau*j/32 for j in range(32)]]
        lo=knee[1]
        shell(side+" / draped thigh",[trouser_ring(y,w,d) for y,w,d in
              ((lo-.005,.064,.068),(lo+.045,.067,.072),(.642,.072,.080),
               (.735,.082,.093),(.835,.087,.102),(.941,.081,.092))],PANTS,thigh,False)
        shell(side+" / draped shin",[trouser_ring(y,w,d) for y,w,d in
              ((.113,.050,.053),(.144,.053,.056),(.195,.056,.060),(.27,.053,.060),
               (.375,.059,.067),(lo-.045,.062,.069),(lo+.003,.064,.068))],PANTS,shin,False)
        x,y,z=ankle
        def shoes(name,prof,tint):
            rows=[]
            for dz,w,low,high in prof:
                row=[]
                for j in range(20):
                    a=tau*j/20
                    row.append((x+w*(1 if sin(a)>0 else -1)*abs(sin(a))**.71,
                         (low+high)/2+(high-low)/2*(1 if cos(a)>0 else -1)*abs(cos(a))**.60,z+dz))
                rows.append(row)
            shell(side+" / "+name,rows,tint,foot)
        shoes("gum sole",[(-.074,.042,.010,.040),(-.047,.054,.003,.042),(.04,.056,.003,.041),
              (.141,.057,.007,.041),(.180,.042,.017,.042)],"ad9473")
        shoes("canvas trainer",[(-.073,.040,.038,.107),(-.049,.051,.038,.135),
              (.019,.053,.038,.131),(.085,.057,.036,.084),(.147,.056,.037,.071),(.178,.039,.040,.060)],"d4cebf")
        for j in range(3):
            h=.121-j*.010; dz=.035+j*.016
            line("Trainer / lace",[(x-.028,h-.003,z+dz),(x,h+.002,z+dz+.002),(x+.028,h-.003,z+dz)],
                 .0025,"b3aea2",foot,7)
    # Strap lies flush to the front of the tee and continues over the upper shoulder to the case.
    def frontstrap(x,y):
        lo,hi=-pi/2,pi/2
        for _ in range(15):
            a=(lo+hi)/2
            if shirt(a,y)[0]<x: lo=a
            else: hi=a
        px,py,pz=shirt((lo+hi)/2,y,.0040)
        return px,py,pz
    strap_profile=[(-.178,.882),(-.153,.907),(-.141,.970),(-.113,1.042),(-.065,1.135),
                  (0,1.250),(.061,1.354),(.098,1.410),(.122,1.454)]
    centerline=[]
    for (x0,y0),(x1,y1) in zip(strap_profile,strap_profile[1:]):
        for k in range(12):
            u=k/12
            centerline.append((x0+(x1-x0)*u,y0+(y1-y0)*u))
    centerline.append(strap_profile[-1])
    strap=[]
    for x,y in centerline:
        strap.extend((frontstrap(x-.012,y),frontstrap(x+.012,y)))
    mesh("Harness / crossbody sling",strap,[(i,i+1,i+3,i+2) for i in range(0,len(strap)-2,2)],
         "50463a",chest)
    for sign in (-1,1):
        line("Harness / sewn edge",[frontstrap(x+.010*sign,y) for x,y in centerline],.0012,"978467",chest,5)
    for y,x in ((1.148,-.057),(1.162,-.050)):
        line("Harness / brass buckle",[(a,b,c+.0013) for a,b,c in
             (frontstrap(x-.014,y),frontstrap(x+.014,y))],.0026,GOLD,chest,6)
    loop=[]
    for x,y,z in ((.122,1.454,frontstrap(.122,1.454)[2]),(.129,1.463,.028),
                  (.128,1.465,-.005),(.125,1.459,-.043),(.120,1.428,-.085),(.111,1.425,-.145)):
        loop.extend(((x-.012,y,z),(x+.012,y,z)))
    mesh("Harness / shoulder loop",loop,[(i,i+1,i+3,i+2) for i in range(0,len(loop)-2,2)],"50463a",chest)
    # The lower end turns around the hip and buckles to the rear of the case.
    loop=[]
    start_angle=asin(strap_profile[0][0]/shirt(-pi/2,.882)[0]) * -1
    for i in range(30):
        u=i/29
        a=start_angle-(2.52-abs(start_angle))*u
        y=.882+.020*u
        if i==0:
            loop.extend((frontstrap(strap_profile[0][0]-.012,y),frontstrap(strap_profile[0][0]+.012,y)))
        else:
            loop.extend((shirt(a,y-.013,.0045),shirt(a,y+.013,.0045)))
    mesh("Harness / hip wrap",loop,[(i,i+1,i+3,i+2) for i in range(0,len(loop)-2,2)],"50463a",chest)
    build_face(mesh,shell,line,body,limb)
    build_case(mesh,shell,line,limb)
    build_sword(mesh,shell,line,limb)


def build_case(mesh,shell,line,limb):
    # Long lacquered sword box. The jade case is above the loose shirt and follows the chest joint.
    bone="Chest"; start=(-.178,.852,-.164); end=(.177,1.555,-.184)
    d=unit(tuple(q-p for p,q in zip(start,end)))
    across=unit(cross(d,(0,0,1))); normal=unit(cross(across,d))
    def p(t,x=0,z=0):
        return plus(plus(start,scale(tuple(q-p for p,q in zip(start,end)),t)),
                    plus(scale(across,x),scale(normal,z)))
    def band(label,t0,t1,w,h,tint):
        octagon=[(-w+.012,-h),(w-.012,-h),(w,-h+.008),(w,h-.008),(w-.012,h),(-w+.012,h),(-w,h-.008),(-w,-h+.008)]
        shell("Case / "+label,[[p(t,x,z) for x,z in octagon] for t in (t0,t1)],tint,bone)
    band("lacquered casket",0,1,.067,.044,"254749")
    band("end shoe",-.013,.051,.069,.046,"ac8753")
    band("throat",.960,1.020,.070,.047,"c29d64")
    for t in (.126,.790):
        band("braided leather cinch",t,t+.056,.069,.046,"584c3b")
        band("clasp",t+.021,t+.032,.070,.047,GOLD)
    for x in (-.047,.047):
        line("Case / brass pinstripe",[p(.062,x,-.045),p(.949,x,-.045)],.0024,GOLD,bone,6)
    for t in (.27,.48,.69):
        line("Case / mountain engraving",[p(t-.053,-.018,-.046),p(t,0,-.046),p(t+.053,-.018,-.046)],
             .0026,"92aa8f",bone,6)
        line("Case / mountain engraving",[p(t-.053,.012,-.046),p(t,.030,-.046),p(t+.053,.012,-.046)],
             .0020,"728b7d",bone,6)
    band("recessed throat plate",1.019,1.023,.062,.039,"3a4542")
    for x,z,ext in ((-.039,-.007,.16),(0,.011,.22),(.039,-.007,.14)):
        line("Case / sword ferrule",[p(1.019,x,z),p(1.034,x,z)],.013,"7e794f",bone,10)
        line("Case / swept hilt guard",[p(1.056,x-.021,z),p(1.037,x-.014,z),p(1.041,x,z),
             p(1.037,x+.014,z),p(1.056,x+.021,z)],[.0038,.005,.006,.005,.0038],GOLD,bone,8)
        line("Case / sword grip",[p(1,x,z),p(1+ext,x,z)],.0098,"23413f",bone,10)
        for i in range(4):
            line("Case / hilt binding",[p(1.03+ext*i/5,x,z),p(1.035+ext*i/5,x,z)],.0108,GOLD,bone,10)
        line("Case / pommel",[p(1+ext-.008,x,z),p(1+ext+.009,x,z)],.015,GOLD,bone,8)
    # Visible cord and tassel terminate near the rear-left waist.
    line("Case / knot cord",[p(.08,-.055,-.032),(-.232,.839,-.192),(-.208,.817,-.201),
                            (-.180,.836,-.202)],.0038,"9e583d",bone,7)
    for i in range(9):
        line("Case / red silk", [(-.224,.836,-.197),(-.229+(i-4)*.0018,.803,-.194),
             (-.215+(i-4)*.0027,.758+(i%3)*.004,-.200)], [.0029,.0027,.0013],"a3553e",bone,5)


def build_sword(mesh,shell,line,limb):
    bone="Ride"
    # A broad steel jian, standing-width under the soles. Tip faces forward, hilt trails behind.
    f=unit((.38,0,.925)); lateral=unit(cross((0,1,0),f))
    def p(t,x,h): return plus(scale(f,t),plus(scale(lateral,x),(0,h,0)))
    profiles=[(-.638,.076,.009),(-.544,.101,.011),(-.421,.123,.013),
              (-.19,.139,.016),(.15,.138,.016),(.43,.117,.014),(.71,.086,.012),
              (.94,.051,.008),(1.16,.001,.002)]
    for sign in (-1,1):
        for top in (-1,1):
            for a,b,tint in ((0,.14,"477f81"),(.14,.79,"74ada9"),(.79,1,"d3e8d9")):
                verts=[]
                for t,w,h in profiles:
                    for r in (a,b):
                        verts.append(p(t,sign*w*r,-.015+top*h*(1-r)))
                mesh("Blade / tempered facet",verts,[(i,i+1,i+3,i+2) for i in range(0,len(verts)-2,2)],
                     tint if top>0 else "628d8b",bone)
    line("Blade / jade central ridge",[p(t,0,-.014+h) for t,w,h in profiles],.0021,MINT,bone,7)
    for side in (-1,1):
        # Cast angular cloud guard with swept tips and a jade cabochon in its center.
        poly=[(-.660,0),(-.632,.095),(-.647,.155),(-.597,.206),
              (-.600,.223),(-.668,.196),(-.709,.114),(-.716,.026)]
        surface=[p(t,side*x,-.0005) for t,x in poly]; base=[p(t,side*x,-.032) for t,x in poly]
        n=len(poly)
        mesh("Sword / bronze quillon",surface+base,[tuple(range(n)),tuple(range(n,2*n))]+
             [(j,(j+1)%n,n+(j+1)%n,n+j) for j in range(n)],GOLD,bone)
        line("Sword / chased guard",[p(-.689,side*.028,.002),p(-.662,side*.123,.002),
              p(-.627,side*.179,.002)],.0032,"e0c78d",bone,6)
    for t,w,tint in ((-.71,.039,GOLD),(-.746,.025,"354746"),(-.96,.023,"354746")):
        line("Sword / grip collar",[p(t,0,-.015),p(t-.024,0,-.015)],w,tint,bone,12)
    line("Sword / jade-wrapped grip",[p(-.732,0,-.015),p(-1.025,0,-.015)],.023,"33574f",bone,14)
    cord=[]
    for i in range(94):
        u=i/93
        cord.append(p(-.752-.237*u,.0232*sin(u*tau*7),-.015+.0232*cos(u*tau*7)))
    line("Sword / golden wrap",cord,.0028,"ae975e",bone,6)
    line("Sword / ornate pommel",[p(-1.014,0,-.015),p(-1.049,0,-.015),p(-1.075,0,-.015)],
         [.026,.037,.017],GOLD,bone,12)


def build_face(mesh,shell,line,body,limb):
    bone="Head"
    rows=[(1.514,.023,.069,-.029),(1.521,.045,.087,-.045),(1.540,.066,.095,-.061),
          (1.564,.081,.100,-.077),(1.589,.089,.102,-.094),(1.620,.099,.103,-.104),
          (1.644,.103,.103,-.110),(1.664,.105,.099,-.115),(1.687,.105,.106,-.117),
          (1.714,.104,.110,-.116),(1.743,.102,.107,-.115),(1.772,.092,.097,-.110)]
    def dims(y):
        for lo,hi in zip(rows,rows[1:]):
            if y<=hi[0]:
                f=max(0,(y-lo[0])/(hi[0]-lo[0]))
                return tuple(a+(b-a)*f for a,b in zip(lo[1:],hi[1:]))
        return rows[-1][1:]
    def front(x,y):
        w,d,_=dims(y); u=min(.999,abs(x)/w)
        return (d*max(0,1-u**2.42)**.54+.006*exp(-((y-1.635)/.025)**2-((u-.63)/.3)**2)
                -.008*exp(-((y-1.664)/.018)**2-((u-.46)/.29)**2)
                +.004*exp(-((y-1.571)/.018)**2-((u-.3)/.28)**2))
    rings=[]
    # The hidden upper scalp terminates inside the cap, clear of its swept locks.
    for y,width,dep,back in rows[:-1]:
        rings.append([(width*sin(a),y,front(width*sin(a),y) if cos(a)>=0 else back*abs(cos(a))**.85)
                     for a in [j*tau/54 for j in range(54)]])
    shell("Portrait / soft cheek and jaw",rings,SKIN,bone)
    def patch(name,rows,tint,s=1):
        vs=[(s*x,y,front(s*x,y)+z) for row in rows for x,y,z in row]
        w=len(rows[0])
        mesh(name,vs,[(j*w+i,j*w+i+1,(j+1)*w+i+1,(j+1)*w+i) for j in range(len(rows)-1)
                      for i in range(w-1)],tint,bone)
    for s in (-1,1):
        xx=[.018,.027,.038,.049,.061,.071]
        low=[1.6585,1.6558,1.654,1.6546,1.6581,1.6626]
        high=[1.659,1.664,1.667,1.6688,1.668,1.6633]
        reg=lambda ys,h:[(x,y,h) for x,y in zip(xx,ys)]
        patch("Eye / warm whites",[reg(low,.0021),reg(high,.0024)],"d8c9b9",s)
        patch("Eye / brown iris",[
              [(.029,1.658,.003),(.034,1.655,.0037),(.044,1.6547,.0042),(.050,1.6562,.0037),(.054,1.66,.003)],
              [(.029,1.664,.003),(.034,1.666,.0037),(.044,1.668,.0042),(.050,1.668,.0037),(.054,1.665,.003)]],
              "382e2b",s)
        patch("Eye / pupil",[[ (.033,1.660,.004),(.041,1.6558,.0047),(.049,1.660,.0042)],
                              [(.033,1.665,.004),(.041,1.6674,.0047),(.049,1.666,.0042)]],"1b1b1e",s)
        patch("Eye / catchlight",[[ (.036,1.6643,.005),(.039,1.6643,.005)],
                                [(.036,1.666,.005),(.039,1.6665,.005)]],"f0dccc",s)
        patch("Eye / soft upper lashes",[reg(high,.0042),reg([v+h for v,h in zip(high,
              (.0005,.0012,.0021,.0020,.0014,.0008))],.0039)],"503e34",s)
        patch("Eye / fold",[reg([v+.004 for v in high],.0014),reg([v+.0048 for v in high],.0014)],"9e715b",s)
        patch("Brow / soft straight brow",[
              [(.016,1.681,.003),(.027,1.683,.005),(.044,1.686,.005),(.061,1.688,.004),(.075,1.684,.003)],
              [(.018,1.686,.003),(.029,1.691,.005),(.044,1.693,.005),(.061,1.693,.004),(.076,1.6845,.003)]],
              "403632",s)
        body("Ear / pinna",[(1.611,s*.100,-.002,.005,.008),(1.623,s*.105,-.004,.011,.014),
             (1.648,s*.107,-.008,.011,.019),(1.664,s*.103,-.011,.007,.014),
             (1.669,s*.101,-.011,.004,.008)],SKIN,bone,16)
        line("Ear / concha",[(s*.116,1.649,-.006),(s*.117,1.643,-.015),(s*.114,1.629,-.011)],
             .0027,"a8775e",bone,7)
    nose=[]
    for y,w,h,wing in [(1.605,.017,.005,.002),(1.608,.024,.018,.009),(1.614,.026,.029,.017),
               (1.620,.022,.034,.011),(1.630,.017,.030,.005),(1.651,.014,.018,.001),
               (1.672,.011,.006,.001),(1.682,.008,.001,0)]:
        nose.append([(w*u,y,v) for u,v in zip((-1,-.77,-.46,0,.46,.77,1),
                   (.0005,wing,h*.76,h,h*.76,wing,.0005))])
    patch("Nose / soft bridge",nose,SKIN)
    for s in (-1,1):
        patch("Nose / nostril", [[(.010,1.608,.016),(.016,1.608,.016),(.021,1.610,.012)],
              [(.010,1.610,.019),(.016,1.612,.019),(.021,1.611,.014)]],"865647",s)
    xs=[-.038,-.030,-.018,-.008,0,.009,.019,.030,.040]
    def contour(ys,ds): return list(zip(xs,ys,ds))
    seams=[1.5745,1.572,1.5693,1.569,1.569,1.5696,1.5704,1.5735,1.578]
    dark=contour(seams,[.001,.003,.004,.005,.005,.005,.004,.003,.001])
    patch("Mouth / upper lip",[dark,contour([1.575,1.575,1.576,1.579,1.577,1.579,1.577,1.576,1.578],
          [.001,.003,.005,.006,.005,.006,.004,.003,.001])],"aa7567")
    patch("Mouth / lower lip",[contour([1.574,1.570,1.563,1.562,1.561,1.562,1.565,1.571,1.5775],
          [.001,.003,.005,.007,.007,.006,.005,.003,.001]),dark],"bb8674")
    patch("Mouth / small smile", [[(x,y-(.003 if abs(x)<.022 else .001),z+.0006) for x,y,z in dark],
                             [(x,y+.0003,z+.0006) for x,y,z in dark]],"724e48")
    # Oversize clear acetate frames. The lens area stays open in the opaque character
    # atlas, preserving transparent optics on every renderer. Two thin rims carry the reflections.
    rim=[(.009,1.670),(.013,1.675),(.027,1.6785),(.065,1.6810),(.091,1.680),
         (.102,1.676),(.104,1.669),(.099,1.643),(.095,1.634),(.089,1.630),
         (.070,1.627),(.039,1.626),(.023,1.628),(.016,1.634),(.011,1.647),(.009,1.661),(.009,1.670)]
    def glass(s,x,y,offset=0):
        return (s*x,y,.129-.034*(x/.109)**1.85+.20*(y-1.655)+offset)
    for s in (-1,1):
        path=[glass(s,x,y) for x,y in rim]
        line("Glasses / clear acetate frame",path,.0024,"999fa2",bone,10)
        line("Glasses / front rim",[[x,y+.0004,z+.0018] for x,y,z in path],.00115,"c3cdca",bone,9)
        for a,b in ((1,4),(6,7),(9,12)):
            line("Glasses / reflected edge",[(x,y+.00045,z+.0031) for x,y,z in path[a:b+1]],
                 .00092,"f3e8cf",bone,7)
        line("Glasses / nosepad",[glass(s,.015,1.660,-.001),glass(s,.012,1.650,-.007),
             glass(s,.011,1.646,-.009)],.0024,"c1b49f",bone,7)
        line("Glasses / arm",[glass(s,.101,1.672),(.110*s,1.670,.081),(.114*s,1.666,.042),
             (.114*s,1.658,.003),(.109*s,1.645,-.020)],.0024,"b3ac9b",bone,9)
        line("Glasses / temple pin",[glass(s,.098,1.671,.0022),glass(s,.101,1.671,.0022)],
             .00165,"dfd3aa",bone,8)
    line("Glasses / bridge",[glass(-1,.011,1.665),(-.004,1.668,.130),( .004,1.668,.130),
                            glass(1,.011,1.665)],.00255,"d7cbba",bone,9)
    build_hair(mesh,shell)


def build_hair(mesh,shell):
    rng=Random(1587)
    # Thick dark hair with an offset part. Swept sides expose the face through the clear frames.
    def edge(a):
        f=max(0,cos(a)); s=sin(a)
        return 1.591+.111*f**.70+.063*abs(s)**1.8+.017*exp(-((s+.48)/.20)**2)*f
    def surface(a,t,off=0):
        a0=asin(max(-.98,min(.98,(edge(a)-1.686)/.146)))
        lat=a0+(pi/2-a0)*t
        r=max(.007,cos(lat))
        return ((.1215*r+off)*sin(a)+.004*t,
                1.686+.146*sin(lat)-.015*max(0,sin(lat))**3+.003*sin(2*a+.5)*sin(pi*t),
                (.132*r+off)*cos(a)-.005+.007*max(0,cos(a))*(1-t))
    n=88
    rings=[[surface(j*tau/n,t) for j in range(n)] for t in (0,.06,.15,.27,.41,.54,.68,.81,.91,.97,.994,1)]
    rings.insert(0,[(x*.957,y-.003,z*.955) for x,y,z in rings[0]])
    shell("Hair / swept volume",rings,"17191e","Head")
    pigments=["16181b","1b1d20","202126","26282c","2c2d2f"]
    groups=[([],[]) for _ in pigments]
    def add_lock(path,width,ridge,idx):
        vs,faces=groups[idx]; base=len(vs)
        for a,t,lift,drop,rad in path:
            for sign in (-1,0,1):
                x,y,z=surface(a+sign*width*rad,t,lift+(.0018 if sign==0 else 0)*rad)
                vs.append((x,y+drop,z))
        for j in range(len(path)-1):
            k=base+j*3
            faces.extend(((k,k+1,k+4,k+3),(k+1,k+2,k+5,k+4)))
    for i in range(345):
        a=rng.uniform(-pi,pi); t=rng.uniform(.08,.995)
        f=max(0,cos(a)); direction=-1 if sin(a)<-.51 else 1
        length=rng.uniform(.19,.33); travel=.09+.27*f
        width=rng.uniform(.047,.098)
        path=[]
        for u,r in ((0,.28),(.25,.83),(.5,1),(.76,.62),(1,.022)):
            tt=t-length*u
            path.append((a+direction*travel*u,max(0,tt),.0009+.003*sin(pi*u),min(0,tt)*.048,r))
        add_lock(path,width,.0015,rng.choices(range(5),(.16,.32,.31,.18,.03))[0])
    # Broad irregular fringe across the top rim, with small gaps at the off-centre part.
    for i in range(49):
        a=-1.31+2.66*(i+rng.uniform(-.34,.34))/48
        t=rng.uniform(.20,.37); direction=-1 if sin(a)<-.47 else 1
        drop=rng.uniform(.006,.022)*(1-.68*exp(-((sin(a)+.48)/.19)**2))
        path=[]
        for u,r in ((0,.35),(.23,.87),(.49,1),(.75,.62),(1,.015)):
            path.append((a+direction*(.060+.06*cos(a))*u,max(0,t*(1-1.18*u)),.001+.0035*sin(pi*u),
                         -drop*u*u,r))
        add_lock(path,rng.uniform(.032,.079),.0018,rng.choice((0,1,1,2,2,3)))
    for i in range(64):
        a=rng.uniform(-pi,pi); t=rng.uniform(.47,.96)
        path=[(a+.12*u,min(.995,t+.05*u),.001+.003*u,.0048*u*u,r) for u,r in
              ((0,.25),(.38,1),(.74,.50),(1,.02))]
        add_lock(path,.017,.0007,rng.choice((1,2,3)))
    for i,(vs,fs) in enumerate(groups):
        mesh("Hair / lock "+str(i),vs,fs,pigments[i],"Head")
