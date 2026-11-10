"""Wei Ge's original likeness sculpt, open quilted jacket, controls and stacked fitness-watch display."""
from math import asin, cos, exp, pi, sin, sqrt, tau
from random import Random
from build_body import cross, plus, scale, unit
from sculpt_tools import sculpt_tools

PIVOTS={"Hips":(0,.904,0),"Chest":(0,1.214,0),"Head":(0,1.508,-.004),
    "RightUpperArm":(.230,1.399,0),"RightLowerArm":(.335,1.162,.080),"RightHand":(.320,1.330,.225),
    "LeftUpperArm":(-.230,1.396,-.002),"LeftLowerArm":(-.326,1.144,.021),"LeftHand":(-.310,1.149,.233),
    "RightUpperLeg":(.113,.900,0),"RightLowerLeg":(.138,.510,.049),"RightFoot":(.150,.114,.060),
    "LeftUpperLeg":(-.113,.900,0),"LeftLowerLeg":(-.133,.510,-.011),"LeftFoot":(-.151,.114,-.049)}
SKIN="b68065"
OLIVE="545847"
SEAM="474d40"
BIND="384034"
SHIRT="a3a39e"
INK="363b3a"
ACTIVES=("67f1b9","70f3bb","7af5bd")
PULSE="f6bb68"

def sample_profile(profile,y):
    for lo,hi in zip(profile,profile[1:]):
        if y<=hi[0]:
            t=max(0,(y-lo[0])/(hi[0]-lo[0]))
            return tuple(a+t*(b-a) for a,b in zip(lo[1:],hi[1:]))
    return profile[-1][1:]

def build(add):
    mesh,shell,line,body,limb,plaque,box=sculpt_tools(add)
    chest="Chest"
    cloth=[(.826,.208,.168,-.004),(.881,.222,.178,-.004),(.971,.239,.201,.002),
           (1.051,.246,.210,.003),(1.148,.238,.193,.004),(1.237,.225,.172,.003),
           (1.324,.231,.158,.001),(1.399,.239,.133,-.005),(1.429,.218,.115,-.006),
           (1.452,.164,.089,-.008),(1.476,.082,.067,-.010)]
    def tshirt(y,a,margin=0):
        w,d,z=sample_profile(cloth,y)
        fold=.002*sin(9*a+y*8)*max(0,(1.43-y)/.58)
        return ((w+margin+fold)*sin(a),y,z+(d+margin+fold)*cos(a))
    def neck(a): return 1.476-.042*max(0,cos(a))**3
    aa=[tau*i/54 for i in range(54)]
    shell("Tee / heather jersey",[[tshirt(.826+(neck(a)-.826)*j/39,a) for a in aa] for j in range(40)],
          SHIRT,chest,False)
    shell("Tee / ribbed neck",[[tshirt(neck(a)-d,a,.0013) for a in aa] for d in (.014,.006,0)],"8a8c85",chest,False)
    body("Neck",[(1.414,0,-.004,.118,.086),(1.448,0,-.006,.098,.079),
             (1.493,0,-.010,.080,.072),(1.560,0,-.017,.074,.066)],"b07f64","Head",40)
    # Graphic printed directly against the jersey: two sweeping wings, tapered flight feathers
    # and broken contour lines; all points follow the belly and chest, without a floating decal.
    def graphic(x,y):
        w,_,_=sample_profile(cloth,y)
        return tshirt(y,asin(min(.98,max(-.98,x/w))),.0026)
    def printpoly(name,xy,tint):
        # Tessellate in the print's flat pattern, then place a fine triangular grid on the
        # actual tee. Long flat polygons would cut into the belly.
        from math import ceil
        from mathutils import Vector
        from mathutils.geometry import tessellate_polygon
        vs=[];fs=[]
        outline=[Vector((x,y,0)) for x,y in xy]
        for ids in tessellate_polygon([outline]):
            a,b,c=[outline[i] for i in ids]
            n=max(1,ceil(max((a-b).length,(b-c).length,(a-c).length)/.008))
            table={}
            for i in range(n+1):
                for j in range(n-i+1):
                    point=a+(b-a)*(i/n)+(c-a)*(j/n)
                    table[i,j]=len(vs);vs.append(graphic(point.x,point.y))
            for i in range(n):
                for j in range(n-i):
                    fs.append((table[i,j],table[i+1,j],table[i,j+1]))
                    if i+j<n-1: fs.append((table[i+1,j],table[i+1,j+1],table[i,j+1]))
        if fs: mesh("Print / "+name,vs,fs,tint,chest)
    # An original two-tone bird, with fanned primaries and a hooked beak.
    printpoly("eagle body",[(-.010,1.130),(-.020,1.143),(-.014,1.174),(-.007,1.185),
              (.001,1.193),(.009,1.194),(.017,1.189),(.022,1.182),(.010,1.184),
              (.008,1.173),(.017,1.160),(.012,1.139),(.006,1.130)],"343a37")
    for ss in (1,-1):
        def sidepoly(name,points,color):
            printpoly(name,[(ss*x,y+(.004 if ss<0 else 0)) for x,y in points],color)
        sidepoly("arched wing",[(.005,1.149),(.013,1.173),(.032,1.187),(.050,1.204),(.083,1.219),
                 (.071,1.203),(.059,1.192),(.045,1.170),(.023,1.153)],"404641")
        for j in range(6):
            x=.028+j*.006; y=1.176+j*.005
            sidepoly("flight feather",[(x,y),(x+.009,y+.013),(x+.044-j*.003,y+.058+j*.001),
                     (x+.031-j*.002,y+.028),(x+.006,y-.002)],"353b38" if j%2 else "555a53")
        for j in range(4):
            x=.019+j*.009; y=1.162+j*.004
            sidepoly("wing mark",[(x,y),(x+.004,y+.009),(x+.011,y+.014),(x+.004,y+.003)],"72756b")
    for j in range(5):
        x=(j-2)*.0065
        printpoly("fanned tail",[(x*.42,1.143),(x-.005,1.113-abs(j-2)*.0017),(x+.002,1.110),
                                 (x*.42+.004,1.141)],"464b44")
    for j in range(2):
        line("Print / underline",[graphic(x,1.094-j*.008+.0015*sin(x*32+j)) for x in
             (-.057,-.034,-.014,.009,.028,.054)],.0012,"62665e",chest,5)
    # Loft the open bomber over the shirt. A real diamond quilt modulates its radius, so every
    # stitched line sits in a valley rather than being drawn through the raised part of a cell.
    ybottom=.850
    def opening(y):
        return sample_profile([(.868,.55),(1.00,.48),(1.18,.45),(1.31,.42),(1.39,.41),(1.48,.58)],y)[0]
    def jacket(y,a,extra=0):
        w,d,z=sample_profile(cloth,y)
        v=y-ybottom; u=(abs(a)-.40)*.225
        aa=(u+.78*v)/.107; bb=(u-.78*v)/.107
        puff=.010*min(abs(sin(pi*aa)),abs(sin(pi*bb)))**.76
        padding=.014+puff+extra
        top=max(0,(y-1.402)/.074)
        collar_drop=.022*max(0,cos(a))**2*top**2
        return ((w+padding)*sin(a),y-collar_drop,z+(d+padding)*cos(a))
    for side,s in (("Right",1),("Left",-1)):
        def rows(j,n):
            y=ybottom+(1.476-ybottom)*j/n
            start=opening(y)
            return [jacket(y,s*(start+(pi-start)*i/69)) for i in range(70)]
        grid=[rows(j,59) for j in range(60)]
        mesh(side+" / quilted olive shell",[p for row in grid for p in row],
             [(j*70+i,j*70+i+1,(j+1)*70+i+1,(j+1)*70+i) for j in range(59) for i in range(69)],OLIVE,chest)
        # A dark facing and a rolled olive edge give the opening its thickness both in front and behind.
        facing=[]; zipedge=[]
        for j in range(69):
            y=ybottom+(1.476-ybottom)*j/68
            a=s*opening(y)
            facing.extend((jacket(y,a,-.0135),jacket(y,a,.0009),jacket(y,a+s*.045,.0009)))
            zipedge.append(jacket(y,a,.0014))
        mesh(side+" / jacket facing",facing,[(i,i+1,i+4,i+3) for i in range(len(facing)-3) if i%3!=2],BIND,chest)
        line(side+" / edge cord",zipedge,.0028,"595c4b",chest,7)
        for direction in (-1,1):
            for k in range(-5,13):
                runs=[]
                for j in range(43):
                    y=ybottom+(1.476-ybottom)*j/42; v=y-ybottom
                    a=.40+(k*.107-direction*.78*v)/.225
                    if opening(y)+.045<a<pi-.014: runs.append(jacket(y,s*a,.0026))
                    elif len(runs)>1:
                        line(side+" / quilt seam",runs,.00140,SEAM,chest,5);runs=[]
                    else: runs=[]
                if len(runs)>1: line(side+" / quilt seam",runs,.00140,SEAM,chest,5)
        # Slanted zipped hand pockets and blackened metal pull.
        zips=[]
        for j in range(19):
            f=j/18; zips.append(jacket(.974+.115*f,s*(.80+.32*f),.0032))
        line(side+" / hand pocket",zips,.0054,BIND,chest,7)
        line(side+" / zipper",[(x,y+.0018,z+.0015) for x,y,z in zips],.0017,"a09e88",chest,6)
        px,py,pz=zips[-1]
        line(side+" / zipper pull",[(px,py,pz),(px+s*.005,py-.020,pz+.003)],.0036,"535649",chest,6)
    # Collar band is interrupted at the throat of the open jacket.
    aa=[.59+(tau-1.18)*i/50 for i in range(51)]
    collar=[]
    for a in aa:
        p=jacket(1.4755,a)
        collar.extend((p,(p[0]*.90,p[1]+.012,p[2]*.86-.001)))
    mesh("Jacket / stand collar",collar,[(i,i+1,i+3,i+2) for i in range(0,len(collar)-2,2)],"404738",chest)
    for j in range(3):
        y=1.003+j*.154; p=jacket(y,opening(y)+.030,.0017)
        limb("Jacket / brass snap",p,plus(p,(0,0,.0032)),[(0,.005,.005),(1,.005,.005)],"8d8971",chest,10)
    # A tonal small home/control insignia on the left chest, like the small dark print in the photo.
    for coords in (((-.17,1.268),(-.151,1.290),(-.127,1.281)),((-.162,1.270),(-.159,1.247),(-.135,1.251),(-.136,1.270))):
        path=[]
        for x,y in coords:
            w,d,z=sample_profile(cloth,y)
            path.append(jacket(y,asin(x/(w+.018)),.0035))
        line("Jacket / house stitch",path,.0028,"3f453e",chest,6)
    build_sleeves(mesh,shell,line,limb)
    build_hands(mesh,shell,line,limb)
    build_watch(mesh,shell,line,limb)
    build_legs(shell,line,body)
    build_face(mesh,shell,line,body)


def build_sleeves(mesh,shell,line,limb):
    for side,s in (("Right",1),("Left",-1)):
        upper,lower,hand=(side+t for t in ("UpperArm","LowerArm","Hand"))
        shoulder,elbow,wrist=[PIVOTS[k] for k in (upper,lower,hand)]
        centers=[(.137*s,1.397,-.006),(.185*s,1.407,-.005),shoulder]
        for t in (.23,.47,.71,.90,1):
            centers.append(plus(shoulder,scale(tuple(q-p for p,q in zip(shoulder,elbow)),t)))
        for t in (.12,.30,.53,.72,.84):
            centers.append(plus(elbow,scale(tuple(q-p for p,q in zip(elbow,wrist)),t)))
        widths=[(.043,.068),(.051,.078),(.062,.083),(.073,.084),(.078,.085),(.076,.083),(.071,.080),
                (.070,.078),(.069,.077),(.066,.074),(.061,.068),(.055,.062),(.053,.059)]
        # Transport an up vector along a rounded spline. The sleeve curvature and
        # the quilting both flow around the bent elbow, with a broad dropped shoulder.
        frames=[]
        prev=(0,1,0)
        for k,pt in enumerate(centers):
            lo=centers[max(0,k-1)];hi=centers[min(len(centers)-1,k+1)]
            axis=unit(tuple(b-a for a,b in zip(lo,hi)))
            prev=unit(plus(prev,scale(axis,-sum(a*b for a,b in zip(prev,axis)))))
            frames.append(prev)
        def sleeve(j,t,extra=0):
            i=min(len(centers)-2,int(j)); f=j-i
            a,b,c,d=(centers[max(0,i-1)],centers[i],centers[i+1],centers[min(len(centers)-1,i+2)])
            center=[];deriv=[]
            for aa,bb,cc,dd in zip(a,b,c,d):
                u=cc-aa;v=2*aa-5*bb+4*cc-dd;w=-aa+3*bb-3*cc+dd
                center.append(bb+.5*(u*f+v*f*f+w*f*f*f))
                deriv.append(.5*(u+2*v*f+3*w*f*f))
            ax=unit(deriv)
            out=plus(scale(frames[i],1-f),scale(frames[i+1],f))
            outer=unit(plus(out,scale(ax,-sum(a*b for a,b in zip(out,ax)))))
            tangent=unit(cross(ax,outer))
            w,d=[a+(b-a)*f for a,b in zip(widths[i],widths[i+1])]
            puff=.003*abs(sin(pi*(j/1.9-t/tau*2))*sin(pi*(j/1.9+t/tau*2)))
            return plus(center,plus(scale(outer,(w+puff+extra)*cos(t)),scale(tangent,(d+puff+extra)*sin(t))))
        def wrap(name,js,col,bone):
            shell(side+" / "+name,[[sleeve(j,tau*k/32) for k in range(32)] for j in js],col,bone,True)
        wrap("upper padded sleeve",[k/4 for k in range(30)],OLIVE,upper)
        wrap("lower padded sleeve",[7+k/4 for k in range(21)],OLIVE,lower)
        for j in (3.3,5.1,6.72,8.35,10.10,11.7):
            bone=upper if j<7 else lower
            path=[sleeve(j+.38*cos(tau*k/48+.3*j),tau*k/48,.0008) for k in range(49)]
            line(side+" / stitched channel",path,.0015,SEAM,bone,5)
        for j in (4.2,9.08):
            bone=upper if j<7 else lower
            path=[sleeve(j+.71*sin(tau*k/48),tau*k/48,.0008) for k in range(49)]
            line(side+" / sleeve diagonal",path,.0013,SEAM,bone,5)
        limb(side+" / ribbed cuff",elbow,wrist,[(.78,.055,.059),(.86,.050,.054),(.955,.043,.046),(.985,.036,.039)],"444b3c",lower,22)
        limb(side+" / wrist",elbow,wrist,[(.88,.034,.038),(.97,.034,.036),(1.12,.032,.035)],SKIN,hand,20)


def build_hands(mesh,shell,line,limb):
    hand="RightHand";w=PIVOTS[hand]
    # The fingers curl into a fist while the thumb extends vertically, as in the photo.
    limb("Thumbs-up / palm",w,(.313,1.408,.245),[(-.12,.030,.035),(.18,.041,.038),
         (.58,.044,.037),(.87,.041,.033),(1.05,.034,.031)],SKIN,hand,22)
    for i in range(4):
        y=1.404-i*.016; x=.313
        line("Thumbs-up / curled finger",[(x+.034,y,.239),(x+.036,y+.001,.259),
             (x+.025,y+.003,.276),(x+.001,y+.002,.280),(x-.026+i*.0015,y-.001,.273)],
             [.011,.012,.012,.011,.0097],SKIN,hand,10)
        line("Thumbs-up / nail",[(x-.020,y+.001,.281),(x-.025,y,.280)],.0033,"cda086",hand,7)
    line("Thumbs-up / thumb",[(.284,1.367,.247),(.278,1.389,.249),(.280,1.416,.246),
         (.286,1.447,.245),(.291,1.462,.246)],[.018,.017,.0148,.013,.0108],SKIN,hand,14)
    line("Thumbs-up / thumbnail",[(.284,1.446,.256),(.288,1.459,.256)],.0057,"d3ac98",hand,10)
    # The relaxed hand is palm-down. Its smartwatch screen faces upward and slightly toward camera.
    hand="LeftHand";w=PIVOTS[hand]
    limb("Control / palm",w,(-.295,1.139,.297),[(-.14,.032,.027),(.19,.041,.028),(.65,.043,.025),
         (1.06,.036,.023)],SKIN,hand,20)
    for i in range(4):
        x=-.325+i*.017;y=1.143+(i%2)*.001;z=.285
        length=(.060,.072,.077,.065)[i]
        line("Control / fingers",[(x,y,z),(x+.004,y-.003,z+.029),(x+.009,y-.012,z+length-.014),
             (x+.014,y-.018,z+length)],[.010,.010,.0085,.0066],SKIN,hand,9)
    line("Control / thumb",[(-.264,1.141,.254),(-.246,1.126,.270),(-.239,1.118,.292),
                           (-.236,1.115,.306)],[.015,.014,.010,.0075],SKIN,hand,10)


def build_watch(mesh,shell,line,limb):
    hand="LeftHand"
    center=(-.311,1.154,.225)
    # Oblique local display axes put the screen on the outside of the left wrist.
    u=unit((1,.11,.08)); v=unit((.015,.48,-.88)); normal=unit(cross(u,v)); v=unit(cross(normal,u))
    if normal[1]<0: normal=scale(normal,-1)
    def pos(x,y,h): return plus(center,plus(scale(u,x),plus(scale(v,y),scale(normal,h))))
    def rounded(name,w,d,heights,tint,cut=.006):
        octagon=[(-w+cut,-d),(w-cut,-d),(w,-d+cut),(w,d-cut),(w-cut,d),(-w+cut,d),(-w,d-cut),(-w,-d+cut)]
        shell("Watch / "+name,[[pos(x,y,h) for x,y in octagon] for h in heights],tint,hand)
    # Rubber strap merges into the metal case at both lugs, passing around the wrist.
    rings=[]
    for h in (-.019,.019):
        rings.append([pos(.042*cos(tau*j/24),h,.038*sin(tau*j/24)) for j in range(24)])
    shell("Watch / graphite strap",rings,"343c39",hand)
    rounded("steel case",.039,.037,(.027,.042),"65746d",.008)
    rounded("dark bezel",.037,.035,(.041,.045),"2b3735",.007)
    rounded("glass",.033,.031,(.044,.046),"20332e",.006)
    def ink(name,path,r,tint,h=.0475):
        line("Watch / "+name,[pos(-x,y,h) for x,y in path],r,tint,hand,7)
    # Each bar has a unique swatch; the runtime picks from four shared palettes at
    # zero through three stacks, dimming the unused swatches to the glass color.
    for i,tint in enumerate(ACTIVES):
        a=-.022+i*.015
        pts=[pos(-x,y,.0478) for x,y in ((a,-.023),(a+.009,-.023),(a+.011,-.008),(a+.002,-.008))]
        mesh("Watch / charge "+str(i+1),pts,[(0,1,2,3)],tint,hand)
    ink("activity", [(-.024,.011),(-.016,.011),(-.011,.020),(-.006,.004),(0,.016),(.006,.016)],
        .0016,"96b4a2")
    ink("step plus",[(.010,.011),(.018,.011)],.00115,PULSE)
    ink("step plus",[(.014,.007),(.014,.015)],.00115,PULSE)
    ink("step one",[(.020,.014),(.024,.017),(.024,.005)],.00135,PULSE)
    line("Watch / crown",[pos(.035,.007,.036),pos(.041,.007,.036)],.007,"5a6861",hand,10)


def build_legs(shell,line,body):
    # Fuller charcoal trousers with a gentle taper and broad casual shoes.
    body("Trousers / seat",[(.808,0,-.004,.066,.061),(.836,0,-.005,.158,.095),
         (.907,0,-.002,.185,.123),(.936,0,-.003,.170,.111)],"343d40","Hips",30)
    for side,s in (("Right",1),("Left",-1)):
        thigh,shin,foot=(side+t for t in ("UpperLeg","LowerLeg","Foot"))
        hip,knee,ankle=[PIVOTS[k] for k in (thigh,shin,foot)]
        def ring(y,w,d):
            a,b=(ankle,knee) if y<knee[1] else (knee,hip)
            t=(y-a[1])/(b[1]-a[1]);x=a[0]+t*(b[0]-a[0]);z=a[2]+t*(b[2]-a[2])
            return [(x+sin(q)*(w+.0016*cos(4*q)),y,z+d*cos(q)) for q in [k*tau/26 for k in range(26)]]
        shell(side+" / straight trouser", [ring(*r) for r in ((.505,.071,.077),(.550,.075,.085),(.65,.084,.100),
             (.746,.096,.108),(.835,.099,.111),(.921,.090,.099))],"384144",thigh,False)
        shell(side+" / cuffed trouser", [ring(*r) for r in ((.111,.052,.058),(.15,.059,.065),(.20,.061,.064),
             (.28,.060,.066),(.39,.072,.077),(.474,.070,.078),(.514,.072,.078))],"384144",shin,False)
        x,y,z=ankle
        def shoe(name,profile,col):
            rows=[]
            for dz,w,lo,hi in profile:
                rows.append([(x+w*(1 if sin(a)>0 else -1)*abs(sin(a))**.70,
                        (lo+hi)/2+(hi-lo)/2*(1 if cos(a)>0 else -1)*abs(cos(a))**.58,z+dz)
                        for a in [j*tau/20 for j in range(20)]])
            shell(side+" / "+name,rows,col,foot)
        shoe("sole",[(-.078,.045,.011,.048),(-.047,.059,.005,.047),(.060,.064,.005,.046),
                    (.151,.064,.009,.046),(.192,.041,.015,.047)],"a4a491")
        shoe("trainer", [(-.076,.044,.044,.111),(-.047,.057,.041,.147),(.022,.060,.042,.139),
                    (.089,.062,.041,.091),(.156,.061,.044,.071),(.187,.039,.044,.060)],"51564c")
        for i in range(4):
            h=.130-i*.010; dz=.03+i*.017
            line("Trainer / sand laces",[(x-.031,h-.005,z+dz),(x,h+.002,z+dz+.004),(x+.030,h-.005,z+dz)],
                 .0027,"a4a391",foot,7)


def build_face(mesh,shell,line,body):
    bone="Head"
    # Full lower cheeks, a soft jaw and a pad below the chin, wider through the
    # nose than at the temples. Feature depths use the same surface as the skin.
    rows=[(1.480,.042,.050,-.045),(1.486,.065,.073,-.051),(1.494,.079,.094,-.058),
          (1.505,.088,.107,-.064),(1.518,.097,.113,-.069),(1.536,.104,.115,-.080),
          (1.554,.112,.115,-.092),(1.579,.120,.118,-.104),(1.605,.124,.119,-.114),(1.628,.122,.113,-.120),(1.654,.119,.107,-.121),
          (1.674,.115,.102,-.123),(1.699,.114,.108,-.121),(1.728,.111,.109,-.118),
          (1.753,.105,.102,-.112),(1.779,.092,.088,-.099)]
    def front(x,y):
        w,d,_=sample_profile(rows,y);r=min(.9999,abs(x)/w)
        return (d*max(0,1-r**2.57)**.57+.008*exp(-((y-1.623)/.030)**2-((r-.70)/.28)**2)
                -.007*exp(-((y-1.666)/.020)**2-((r-.45)/.28)**2)
                +.0027*exp(-((y-1.566)/.027)**2-((r-.25)/.31)**2))
    rings=[]
    for y,w,d,back in rows[:-1]:
        rings.append([(w*sin(a),y,front(w*sin(a),y) if cos(a)>=0 else back*abs(cos(a))**.88)
                      for a in [j*tau/60 for j in range(60)]])
    shell("Portrait / cheek jaw and lower chin",rings,SKIN,bone)
    def patch(name,rows,tint,s=1):
        vs=[(s*x,y,front(s*x,y)+z) for row in rows for x,y,z in row]
        w=len(rows[0])
        mesh(name,vs,[(j*w+i,j*w+i+1,(j+1)*w+i+1,(j+1)*w+i) for j in range(len(rows)-1)
                      for i in range(w-1)],tint,bone)
    for s in (-1,1):
        # Relaxed, slightly narrowed eyes under the black upper rim.
        xx=[.022,.032,.046,.059,.072,.081]
        lo=[1.663,1.6598,1.659,1.6605,1.663,1.6654]
        hi=[1.6635,1.6683,1.6704,1.6723,1.6705,1.666]
        if s<0:
            hi=[y-.0011 for y in hi]
        reg=lambda ys,z: [(x,y,z) for x,y in zip(xx,ys)]
        patch("Eye / almond", [reg(lo,.002),reg(hi,.0024)],"d3c6b6",s)
        patch("Eye / brown iris",[[ (.036,1.662,.0033),(.041,1.6598,.0036),(.050,1.6598,.0040),
                  (.057,1.6608,.0038),(.059,1.664,.0033)],
              [(.036,1.667,.0033),(.041,1.669,.0037),(.050,1.670,.0044),(.057,1.670,.0039),(.059,1.668,.0033)]],
              "3d3029",s)
        patch("Eye / pupil", [[(.040,1.663,.004),(.047,1.6605,.0048),(.054,1.663,.004)],
                               [(.040,1.667,.004),(.047,1.6695,.0048),(.054,1.668,.004)]],"211e20",s)
        patch("Eye / glint", [[(.042,1.6662,.0051),(.045,1.6662,.0051)],
                               [(.042,1.6681,.0051),(.045,1.6684,.0051)]],"ead9c4",s)
        patch("Eye / lashes",[reg(hi,.0044),reg([v+h for v,h in zip(hi,(.0005,.0016,.0022,.0024,.0017,.0005))],.0043)],"493b34",s)
        patch("Eye / lid fold",[reg([v+.0035 for v in hi],.0017),reg([v+.0043 for v in hi],.0017)],"a1735e",s)
        patch("Brow / soft arch",[
              [(.020,1.685,.004),(.031,1.689,.005),(.048,1.691,.005),(.063,1.691,.004),(.079,1.687,.003)],
              [(.020,1.690,.004),(.032,1.696,.005),(.049,1.696,.005),(.066,1.696,.004),(.079,1.688,.003)]],
              "4a3930",s)
        body("Ear / broad helix",[(1.607,s*.112,-.005,.005,.008),(1.619,s*.120,-.009,.013,.018),
              (1.646,s*.122,-.010,.012,.021),(1.666,s*.117,-.009,.009,.016),
              (1.670,s*.114,-.010,.005,.008)],SKIN,bone,20)
        line("Ear / concha",[(s*.132,1.648,-.007),(s*.132,1.639,-.019),(s*.128,1.621,-.016)],
             .0026,"a1745e",bone,7)
    nose=[]
    for y,w,height,wing in [(1.609,.017,.003,.003),(1.613,.027,.016,.012),
              (1.621,.033,.030,.020),(1.630,.028,.036,.015),(1.640,.021,.030,.007),
              (1.656,.016,.021,.003),(1.670,.014,.011,.001),(1.685,.010,.002,.0005)]:
        nose.append([(w*u,y,off) for u,off in zip((-1,-.80,-.53,-.24,0,.24,.53,.80,1),
                   (.0004,wing,.76*height,.93*height,height,.93*height,.76*height,wing,.0004))])
    patch("Nose / broad soft alae",nose,SKIN)
    for s in (-1,1):
        patch("Nose / nostril",[[ (.012,1.614,.0204),(.019,1.614,.0206),(.025,1.616,.0172)],
              [(.013,1.616,.0232),(.019,1.618,.0240),(.025,1.618,.0188)]],"85604e",s)
    # Neutral, gently pursed mouth (photo), with a small shadow in the hollow underneath.
    xs=[-.038,-.029,-.018,-.008,0,.008,.018,.030,.038]
    def contour(ys,ds): return list(zip(xs,ys,ds))
    seam=contour([1.569,1.572,1.574,1.575,1.575,1.575,1.574,1.572,1.569],
                 [.001,.003,.005,.007,.007,.007,.005,.003,.001])
    patch("Mouth / upper",[seam,contour([1.569,1.576,1.580,1.582,1.580,1.582,1.580,1.576,1.569],
          [.001,.003,.005,.006,.006,.006,.005,.003,.001])],"9d6960")
    patch("Mouth / lower",[contour([1.568,1.568,1.567,1.566,1.566,1.566,1.567,1.568,1.568],
          [.001,.002,.005,.007,.008,.007,.005,.002,.001]),seam],"b68372")
    patch("Mouth / seam",[[(x,y-.0008,z+.001) for x,y,z in seam],[(x,y+.0005,z+.001) for x,y,z in seam]],"77534c")
    patch("Chin / soft fold",[contour([1.553,1.5515,1.55,1.549,1.5488,1.549,1.55,1.551,1.553],
          [.0007]*9),contour([1.553,1.552,1.5513,1.5505,1.5503,1.5505,1.551,1.5518,1.553],[.0007]*9)],"b58268")
    # Dark browline and clear lower wire are separate materials. The lens interior
    # is open, showing the eyes without an opaque polygon on Unity's clothing shader.
    upper=[(.010,1.675),(.014,1.679),(.033,1.681),(.061,1.684),(.086,1.686),(.106,1.685),(.112,1.681)]
    lower=[(.112,1.681),(.111,1.667),(.107,1.643),(.102,1.635),(.088,1.632),(.054,1.630),
           (.030,1.631),(.022,1.634),(.018,1.641),(.012,1.662),(.010,1.675)]
    def optics(s,x,y,d=0):
        return (s*x*.963,y,.136-.035*(x/.115)**1.86+.19*(y-1.658)+d)
    for s in (-1,1):
        line("Glasses / black browline",[optics(s,x,y) for x,y in upper],
             [.0024,.0030,.0035,.0038,.0037,.0036,.0027],"252729",bone,10)
        wire=[optics(s,x,y) for x,y in lower]
        line("Glasses / clear lower rim",wire,.0017,"acaea5",bone,9)
        line("Glasses / bright lower reflection",[(x,y-.0001,z+.00095) for x,y,z in wire[3:8]],.0011,"d9d7c8",bone,7)
        line("Glasses / temple",[optics(s,.110,1.681),(s*.119,1.677,.075),
             (s*.124,1.668,.028),(s*.125,1.657,-.008),(s*.118,1.645,-.026)],.0028,"343638",bone,8)
        line("Glasses / nose pad",[optics(s,.017,1.662,-.003),optics(s,.014,1.650,-.010)],.0025,"bcad9a",bone,7)
    line("Glasses / bridge",[optics(-1,.013,1.667),(-.004,1.671,.135),(.004,1.671,.135),optics(1,.013,1.667)],
         .00235,"63615b",bone,9)
    build_hair(mesh,shell)


def build_hair(mesh,shell):
    rnd=Random(8541)
    # Clip the sides close around the ears. The front rises into one broad rounded
    # quiff swept backward, keeping the tall open forehead from the reference.
    def env(a,t,pad=0):
        f=max(0,cos(a))
        edge=1.593+.151*f**.79+.047*abs(sin(a))**1.8+.0045*sin(3*a-.4)*f
        edge+=.005*exp(-((sin(a)+.45)/.23)**2)*f-.005*exp(-((sin(a)-.24)/.21)**2)*f
        lat0=asin(max(-.985,min(.985,(edge-1.686)/.145)))
        lat=lat0+(pi/2-lat0)*t
        r=max(.007,cos(lat));lift=sin(pi*t)
        y=1.686+.145*sin(lat)-.010*max(0,sin(lat))**3+.022*f*f*lift+.0065*sin(a*3+.5)*f*lift
        y-=.62*max(0,y-1.809)
        return ((.131*r+pad)*sin(a)-.006*t+.0025*f*sin(2*a)*lift,y,
                (.143*r+pad)*cos(a)-.007+.018*f*lift)
    n=84
    rings=[[env(j*tau/n,t) for j in range(n)] for t in (0,.05,.13,.22,.33,.46,.60,.72,.83,.92,.973,.994,1)]
    rings.insert(0,[(x*.965,y-.005,z*.968) for x,y,z in rings[0]])
    shell("Hair / close sides and quiff",rings,"1a1c1f","Head")
    tones=["181a1d","1d2023","23262a","292b2f","303136"]
    groups=[([],[]) for _ in tones]
    def lock(coords,width,tone):
        pts,fs=groups[tone];k=len(pts)
        for a,t,pad,dy,w in coords:
            for sign in (-1,0,1):
                x,y,z=env(a+sign*width*w,t,pad+(.0014 if sign==0 else 0)*w)
                pts.append((x,y+dy,z))
        for j in range(len(coords)-1):
            i=k+j*3
            fs.extend(((i,i+1,i+4,i+3),(i+1,i+2,i+5,i+4)))
    for i in range(465):
        a=rnd.uniform(-pi,pi);t=rnd.uniform(.0,.986);f=max(0,cos(a))
        reach=rnd.uniform(.11,.27);side=(1 if sin(a)<.22 else -1)*(.09+.11*f)
        path=[]
        for u,w in ((0,.28),(.26,.87),(.51,1),(.76,.62),(1,.016)):
            # The front sections follow the surface UP; side sections comb BACK and down.
            tt=max(0,min(.995,t+reach*u*(1 if f>.26 else -.76)))
            roll=.0006+(.0013+.0012*f)*sin(pi*u)
            path.append((a+side*u,tt,roll,.002*f*sin(pi*u),w))
        lock(path,rnd.uniform(.024,.061),rnd.choices(range(5),(.18,.32,.31,.17,.02))[0])
    # Larger lifted finger-combed waves at the forehead are blunt near the base,
    # then merge back into the top without exposed ends shooting out of the crown.
    for i in range(38):
        a=-1.08+(i+rnd.uniform(-.26,.26))*2.13/37
        reach=rnd.uniform(.34,.54);start=rnd.uniform(0,.08)
        path=[]
        for u,w in ((0,.22),(.18,.82),(.42,1),(.67,.85),(.86,.43),(1,.01)):
            path.append((a-(.19+.22*cos(a))*u,start+reach*u,
                         .001+.0054*sin(pi*u),.0054*sin(pi*u),w))
        lock(path,rnd.uniform(.033,.062),rnd.choice((0,1,1,2,2,3)))
    for i in range(165):
        a=rnd.uniform(-2.5,2.5); t=rnd.uniform(.04,.97);reach=rnd.uniform(.09,.21)
        path=[(a-.14*u,min(.996,t+reach*u),.0011+.0015*sin(pi*u),
               .0014*sin(pi*u),r) for u,r in ((0,.26),(.32,1),(.70,.69),(1,.012))]
        lock(path,rnd.uniform(.0065,.015),rnd.choice((1,2,2,3)))
    # A small loose forelock dips over one temple (the lifted central forehead stays open).
    for i in range(32):
        a=.31+rnd.uniform(-.26,.31);st=rnd.uniform(.15,.29)
        length=rnd.uniform(.005,.019)*(1-abs(a-.31)*1.9)
        path=[]
        for u,r in ((0,.24),(.22,.85),(.51,1),(.78,.62),(1,.011)):
            path.append((a+.22*u,max(0,st*(1-1.20*u)),.0009+.0024*sin(pi*u),
                         -length*u*u,r))
        lock(path,rnd.uniform(.015,.039),rnd.choice((0,1,1,2)))
    for i in range(87):
        a=rnd.uniform(-1.70,1.70);st=rnd.uniform(.015,.095)
        path=[(a+.04*u,st*(1-u),.0008,-.0039*u,r) for u,r in ((0,.25),(.36,1),(.74,.61),(1,.01))]
        lock(path,rnd.uniform(.009,.020),rnd.choice((0,1,2)))
    for i,(vs,fs) in enumerate(groups):
        mesh("Hair / brushed bundles "+str(i),vs,fs,tones[i],"Head")
