"""Author the Cui Ge moves as poses of the original 15-bone athlete rig.

Run with Python 3 and NumPy. Targets and offsets use metres. A track
stores parent-relative rotations in the rig's unrotated character axes (+Y up,
+Z forward, +X anatomical right). Plate and Shaker rotations are absolute in
character space. Hips is the only translated bone. Actions are sampled in
normalized time so the same throw/punch can follow a gameplay speed modifier.
"""
import json
from pathlib import Path

import numpy as np
class Rotation:
    """Unit quaternions in the same XYZW layout used by Unity."""
    def __init__(self,q):
        self.q=np.asarray(q,dtype=float)
        self.q=self.q/np.linalg.norm(self.q)
    @classmethod
    def identity(cls):
        return cls((0,0,0,1))
    @classmethod
    def from_rotvec(cls,v):
        v=np.asarray(v); theta=np.linalg.norm(v)
        if theta<1e-12: return cls.identity()
        return cls(np.r_[v*(np.sin(theta/2)/theta),np.cos(theta/2)])
    @classmethod
    def from_euler(cls,xyz):
        axes=np.eye(3)
        q=cls.identity()
        for axis,angle in zip(axes,xyz):
            q=cls.from_rotvec(np.radians(angle)*axis)*q
        return q
    @classmethod
    def from_matrix(cls,m):
        # Largest diagonal term gives a stable branch near a half turn.
        choices=[1+m[0,0]-m[1,1]-m[2,2],1+m[1,1]-m[0,0]-m[2,2],
                 1+m[2,2]-m[0,0]-m[1,1],1+np.trace(m)]
        i=int(np.argmax(choices)); v=.5*np.sqrt(max(choices[i],1e-12)); k=.25/v
        if i==0: q=(v,(m[0,1]+m[1,0])*k,(m[0,2]+m[2,0])*k,(m[2,1]-m[1,2])*k)
        elif i==1: q=((m[0,1]+m[1,0])*k,v,(m[1,2]+m[2,1])*k,(m[0,2]-m[2,0])*k)
        elif i==2: q=((m[0,2]+m[2,0])*k,(m[1,2]+m[2,1])*k,v,(m[1,0]-m[0,1])*k)
        else: q=((m[2,1]-m[1,2])*k,(m[0,2]-m[2,0])*k,(m[1,0]-m[0,1])*k,v)
        return cls(q)
    def __mul__(self,other):
        a,b=self.q,other.q
        return Rotation(np.r_[a[3]*b[:3]+b[3]*a[:3]+np.cross(a[:3],b[:3]),a[3]*b[3]-np.dot(a[:3],b[:3])])
    def inv(self):
        return Rotation(np.r_[-self.q[:3],self.q[3]])
    def apply(self,v):
        v=np.asarray(v);d=2*np.cross(self.q[:3],v)
        return v+self.q[3]*d+np.cross(self.q[:3],d)
    def as_quat(self):
        return self.q


ROOT = Path(__file__).resolve().parents[1]
BONES = ["Hips", "Chest", "Head"]
REST = {"Hips": (0,.92,0), "Chest": (0,1.25,0), "Head": (0,1.52,0)}
PARENT = {"Chest":"Hips", "Head":"Chest"}
GRIP = {}
for side, sign in (("Left",-1),("Right",1)):
    for part,pivot,parent in (
        ("UpperArm",(.235,1.435,0),"Chest"),("LowerArm",(.455,1.235,0),side+"UpperArm"),
        ("Hand",(.64,1.075,0),side+"LowerArm"),("UpperLeg",(.112,.92,0),"Hips"),
        ("LowerLeg",(.115,.50,.012),side+"UpperLeg"),("Foot",(.115,.105,0),side+"LowerLeg")):
        bone=side+part; BONES.append(bone); PARENT[bone]=parent
        REST[bone]=(sign*pivot[0],pivot[1],pivot[2])
    GRIP[side]=np.array((sign*.035,-.032,.015))
REST={k:np.array(v) for k,v in REST.items()}

def vector(v):
    return np.asarray(v,dtype=float)

def unit(v):
    return v/max(1e-9,np.linalg.norm(v))

def angles(xyz):
    return xyz if isinstance(xyz,Rotation) else Rotation.from_euler(xyz)

def frame(axis,front):
    axis=unit(axis)
    up=unit(front-axis*np.dot(front,axis))
    if np.linalg.norm(up)<.9:
        up=unit(np.cross(axis,np.array((0,1,0))))
    return np.stack([axis,up,np.cross(axis,up)],axis=1)

def point_bone(a,b,rest_hint,world_hint):
    return Rotation.from_matrix(frame(b,world_hint) @ frame(a,rest_hint).T)

def swing(a,b):
    a,b=unit(a),unit(b)
    axis=np.cross(a,b); c=np.clip(np.dot(a,b),-1,1)
    if np.linalg.norm(axis)<1e-8:
        return Rotation.identity() if c>0 else Rotation.from_rotvec(unit(np.cross(a,[0,0,1]))*np.pi)
    return Rotation.from_rotvec(unit(axis)*np.arccos(c))

def solve(start,target,pole,first,second,twist_hint):
    ray=unit(target-start)
    length1,length2=np.linalg.norm(first),np.linalg.norm(second)
    distance=np.clip(np.linalg.norm(target-start),abs(length1-length2)+.0001,length1+length2-.0003)
    radial=(distance**2+length1**2-length2**2)/(2*distance)
    ortho=pole-start; ortho=unit(ortho-ray*np.dot(ortho,ray))
    middle=start+ray*radial+ortho*np.sqrt(max(0,length1**2-radial**2))
    end=start+ray*distance
    first_orient=point_bone(first,middle-start,np.array((0,0,1)),twist_hint)
    second_orient=swing(first_orient.apply(second),end-middle)*first_orient
    return first_orient,second_orient

# Foot pivots and palm grips are expressed in stage space. A pose can override
# any target. The two-bone solve maintains bone lengths and aims elbows/knees.
def pose(*, root=(0,-.055,0), hip=(2,10,0), chest=(5,14,0), head=(-2,-1,0),
         left=(-.16,1.495,.22), right=(.155,1.472,.205),
         feet=((- .165,.105,.135),(.169,.105,-.158)), toes=((0,-9,0),(0,13,0)),
         poles=None, knees=None, plate=None, shaker=None):
    root=vector(root)
    g={"Hips":angles(hip),"Chest":angles(chest),"Head":angles(head)}
    pos={"Hips":REST["Hips"]+root}
    for b in ("Chest","Head"):
        par=PARENT[b]
        pos[b]=pos[par]+g[par].apply(REST[b]-REST[par])
    if plate:
        pc=vector(plate[0]); pr=angles(plate[1])
        left, right=[pc+pr.apply([s*.153,0,0]) for s in (-1,1)]
    else:
        # Ordinary guard targets follow body sway rather than sticking in space.
        left,right=vector(left)+root,vector(right)+root
    for i,(s,side,target) in enumerate(((-1,"Left",left),(1,"Right",right))):
        u,l,h=[side+x for x in ("UpperArm","LowerArm","Hand")]
        sh=pos["Chest"]+g["Chest"].apply(REST[u]-REST["Chest"])
        # Natural boxing elbow points out and down. The local palm orientation
        # follows the forearm so fingers curl toward the correct side of a fist.
        pole=sh+g["Chest"].apply((s*.34,-.44,-.17)) if poles is None else vector(poles[i])+root
        v1,v2=REST[l]-REST[u],REST[h]+GRIP[side]-REST[l]
        gu,gl=solve(sh,target,pole,v1,v2,target-pole)
        g[u],g[l],g[h]=gu,gl,gl
        u,l,f=[side+x for x in ("UpperLeg","LowerLeg","Foot")]
        hp=pos["Hips"]+g["Hips"].apply(REST[u]-REST["Hips"])
        target=vector(feet[i]); knee=hp+np.array((s*.15,-.27,.61)) if knees is None else vector(knees[i])
        g[u],g[l]=solve(hp,target,knee,REST[l]-REST[u],REST[f]-REST[l],knee-hp)
        g[f]=angles(toes[i])
    rotations={}
    for b in BONES:
        q=g[b] if b not in PARENT else g[PARENT[b]].inv()*g[b]
        rotations[b]=q.as_quat().tolist()
    if plate: rotations["Plate"]=pr.as_quat().tolist()
    if shaker: rotations["Shaker"]=angles(shaker).as_quat().tolist()
    return rotations,root.tolist()

def mix_pose(a,b,f):
    rots={}
    for bone,qa in a[0].items():
        if bone not in b[0]: continue
        qa,qb=vector(qa),vector(b[0][bone])
        if np.dot(qa,qb)<0: qb=-qb
        q=qa*(1-f)+qb*f
        rots[bone]=(q/np.linalg.norm(q)).tolist()
    return rots,((1-f)*vector(a[1])+f*vector(b[1])).tolist()

def clip(name,duration,points,loop=False):
    tracks=[]
    for bone in points[0][1][0]:
        keys=[]; prev=None
        for t,(rot,pos) in points:
            q=vector(rot[bone])
            if prev is not None and np.dot(q,prev)<0: q=-q
            prev=q
            key={"t":round(t,5),"r":np.round(q,6).tolist()}
            if bone=="Hips": key["p"]=np.round(pos,5).tolist()
            keys.append(key)
        tracks.append({"bone":bone,"keys":keys})
    return {"name":name,"duration":duration,"loop":loop,"tracks":tracks}

def build():
    guard=pose()
    breathe=pose(root=(.012,-.045,-.004),hip=(1,8,-1),chest=(3,11,1),
                 head=(-3,1,0),left=(-.16,1.508,.235),right=(.155,1.485,.194))
    load=pose(root=(.035,-.094,-.038),hip=(4,-2,-3),chest=(9,-8,-4),
              head=(1,0,-3),left=(-.195,1.45,.195),right=(.144,1.486,.193))
    jab=pose(root=(-.035,-.064,.075),hip=(4,24,-2),chest=(8,30,2),head=(-1,1,0),
             left=(-.07,1.46,.625),right=(.104,1.50,.16))
    jab_back=pose(root=(-.019,-.068,.040),hip=(4,21,0),chest=(7,25,1),head=(-2,1,0),
                  left=(-.130,1.478,.39),right=(.122,1.50,.183))
    # Short right hook: wind out to the side with elbow high, then pivot the
    # hips and carry the knuckles laterally across the target, never punching
    # the arm straight toward it. `cross` is the stable state/clip identifier.
    cross_load=pose(root=(.030,-.112,-.021),hip=(6,18,-4),chest=(7,28,-6),
                    right=(.307,1.40,.144),left=(-.118,1.48,.22),
                    poles=((- .43,1.16,.10),(.56,1.39,.005)))
    hook_arc=pose(root=(.014,-.094,.018),hip=(5,-1,-1),chest=(8,7,-3),
                  right=(.264,1.46,.371),left=(-.118,1.48,.18),
                  poles=((- .40,1.15,.04),(.59,1.47,.093)))
    cross=pose(root=(-.027,-.083,.046),hip=(5,-23,2),chest=(6,-38,3),head=(-2,-4,0),
               right=(-.070,1.456,.444),left=(-.115,1.492,.145),
               feet=((- .165,.105,.135),(.166,.128,-.108)),toes=((0,-9,0),(14,-22,-3)),
               poles=((- .415,1.13,.070),(.52,1.453,.27)))
    hook_follow=pose(root=(-.037,-.085,.043),hip=(5,-26,4),chest=(7,-49,4),head=(-2,-4,1),
                     right=(-.193,1.443,.327),left=(-.138,1.48,.126),
                     poles=((- .40,1.14,.050),(.43,1.42,.29)))
    cross_back=pose(root=(-.014,-.074,.022),hip=(5,-8,1),chest=(8,-13,2),head=(-1,-2,0),
                    right=(.053,1.418,.276),left=(-.139,1.487,.171))
    coil=pose(root=(.036,-.208,.003),hip=(13,22,-9),chest=(24,31,-14),head=(4,-5,-5),
              left=(-.141,1.47,.173),right=(.235,1.16,.18))
    upper=pose(root=(-.015,-.043,.105),hip=(-3,-16,3),chest=(-7,-31,6),head=(-5,0,1),
               left=(-.15,1.49,.172),right=(.006,1.74,.349),
               feet=((- .165,.105,.135),(.178,.132,-.114)),toes=((0,-9,0),(17,-16,0)))
    follow=pose(root=(-.024,-.048,.096),hip=(-3,-21,3),chest=(-8,-37,6),
                head=(-7,-2,0),left=(-.156,1.46,.156),right=(-.018,1.78,.303))
    clips=[clip("idle",2.4,[(0,guard),(.33,breathe),(.61,mix_pose(guard,breathe,.64)),
                            (.83,mix_pose(guard,breathe,.13)),(1,guard)],True),
           clip("jab",.32,[(0,guard),(.11,load),(.28125,jab),(.37,jab),(.62,jab_back),(1,guard)]),
           clip("cross",.32,[(0,guard),(.12,cross_load),(.212,hook_arc),(.28125,cross),(.45,hook_follow),(.71,cross_back),(1,guard)]),
           clip("uppercut",.48,[(0,guard),(.15,coil),(.205,coil),(.33333,upper),(.48,follow),
                                 (.70,cross_back),(1,guard)])]

    # The two hands solve directly onto opposite sides of the disc. The thick
    # slam reaches overhead and crumples down; the normal is a diagonal chop.
    def pp(center,angle,**body): return pose(plate=(center,angle),**body)
    cradle=pp((0,1.22,.285),(10,-6,-13))
    diagonal=pp((-.145,1.535,.235),(-12,17,49),root=(-.055,-.10,-.03),
                hip=(4,20,6),chest=(-7,27,10),head=(-5,1,3))
    impact=pp((.088,.757,.394),(72,9,-20),root=(.053,-.21,.065),hip=(19,-17,-6),
              chest=(36,-21,-9),head=(14,-4,-5))
    recoil=pp((.070,.872,.367),(57,3,-12),root=(.037,-.167,.035),
              hip=(14,-10,-3),chest=(26,-15,-4),head=(9,-3,-3))
    overhead=pp((-.01,1.895,.025),(-72,2,1),root=(0,-.026,-.025),hip=(-4,3,0),
                chest=(-9,4,0),head=(-8,0,0))
    hammer=pp((.008,.636,.337),(84,0,2),root=(0,-.306,.064),hip=(30,1,-1),
              chest=(51,-3,0),head=(23,-2,0))
    backlift=pp((-.03,.79,.36),(69,-1,-2),root=(-.015,-.225,.041),hip=(19,1,0),
                chest=(37,2,0),head=(14,-1,0))
    clips+= [clip("slam",.65,[(0,cradle),(.19,diagonal),(.25,diagonal),(.40,impact),
                              (.47,impact),(.64,recoil),(.83,mix_pose(recoil,cradle,.66)),(1,cradle)]),
             clip("slamHeavy",.65,[(0,cradle),(.19,overhead),(.25,overhead),(.40,hammer),
                                    (.50,hammer),(.68,backlift),(1,cradle)])]

    dash_load=pose(root=(.01,-.20,-.076),hip=(15,-13,-3),chest=(33,-24,-5),
                   head=(9,-2,0),left=(-.22,1.20,.29),right=(.18,1.38,.27))
    dash1=pose(root=(.015,-.173,.060),hip=(21,-15,0),chest=(49,-37,-6),
               head=(12,-4,-1),left=(-.23,1.13,-.17),right=(.11,1.43,.46),
               feet=((- .17,.12,-.245),(.16,.17,.23)),toes=((21,-4,0),(-9,0,0)))
    dash2=pose(root=(-.025,-.155,.105),hip=(23,-8,1),chest=(46,-32,-7),
               head=(9,-2,-2),left=(-.27,1.13,.010),right=(.11,1.40,.45),
               feet=((- .16,.21,.19),(.15,.115,-.23)),toes=((-8,-5,0),(23,0,0)))
    brake=pose(root=(-.022,-.202,.125),hip=(19,-8,-2),chest=(28,-20,-2),
               head=(3,-4,-3),left=(-.25,1.25,.15),right=(.17,1.37,.38))
    clips.append(clip("charge",.48,[(0,dash_load),(.13,dash1),(.34,dash1),(.67,dash2),
                                    (.83,dash2),(1,brake)]))

    def bottle(ang,**kw): return pose(shaker=ang,**kw)
    pickup=bottle((0,-8,-12),right=(.24,1.14,.17),left=(-.19,1.23,.13),chest=(6,6,0))
    lift=bottle((7,2,2),right=(.155,1.40,.21),left=(-.16,1.28,.18),chest=(2,4,0))
    # Right-hand targets normally follow root offset. Align lip with the upper
    # end of the tilted shaker during the sip, leaving room below the chin.
    sip=bottle((-70,-3,1),root=(0,-.044,0),chest=(-3,2,0),head=(-13,4,-1),
               right=(.018,1.566,.190),left=(-.16,1.255,.15))
    gulp=bottle((-81,-4,-1),root=(0,-.040,0),chest=(-4,0,0),head=(-17,3,-1),
                right=(.013,1.575,.180),left=(-.165,1.245,.147))
    aim1=bottle((-5,5,10),root=(-.016,-.07,0),hip=(4,12,-1),chest=(7,13,1),head=(-2,-3,0),
                left=(-.145,1.445,.44),right=(.232,1.125,.065))
    aim2=bottle((-3,8,8),root=(-.009,-.059,-.007),hip=(2,10,-2),chest=(5,10,1),head=(-3,-1,0),
                left=(-.152,1.456,.43),right=(.239,1.134,.056))
    wind=bottle((-32,-10,-10),root=(.04,-.145,-.057),hip=(9,25,2),chest=(12,36,5),
                head=(-3,2,0),right=(.29,.995,-.195),left=(-.165,1.423,.356))
    release=bottle((51,-9,-9),root=(-.028,-.075,.095),hip=(3,-16,0),chest=(9,-25,-3),
                   head=(-3,-1,0),right=(.028,1.37,.522),left=(-.19,1.31,.105))
    thrown=bottle((63,-9,-8),root=(-.04,-.062,.113),hip=(0,-22,-1),chest=(4,-33,-5),
                  head=(-4,-1,0),right=(-.043,1.57,.473),left=(-.205,1.28,.065))
    clips+=[clip("drink",.95,[(0,pickup),(.18,lift),(.40,sip),(.53,gulp),(.65,gulp),
                               (.79,lift),(1,pickup)]),
            clip("aim",1.5,[(0,aim1),(.5,aim2),(1,aim1)],True),
            clip("throw",.65,[(0,aim1),(.18,wind),(.24,wind),(.42,release),(.55,thrown),
                               (.75,thrown),(1,aim1)])]

    arms_out=pose(root=(0,-.070,0),hip=(1,0,0),chest=(-2,0,0),head=(-5,0,0),
                  left=(-.273,1.409,.235),right=(.273,1.409,.235),
                  poles=((- .54,1.29,.03),(.54,1.29,.03)))
    def knuckles(gap,height,depth,settle):
        return pose(root=(0,settle,0),hip=(2,0,0),chest=(4,0,0),head=(2,0,0),
                    left=(-gap,height,depth),right=(gap,height,depth),
                    poles=((- .55,1.31,.070),(.55,1.31,.070)))
    first_tap=knuckles(.0905,1.404,.271,-.068)
    second=knuckles(.0905,1.431,.270,-.054)
    clips.append(clip("ready",.85,[(0,guard),(.15,arms_out),(.30,first_tap),(.34,first_tap),
                                   (.46,arms_out),(.57,second),(.62,second),(.80,arms_out),(1,guard)]))
    run=[]
    for i in range(9):
        phase=2*np.pi*i/8
        legs=[]; toes=[]; arms=[]
        for j,sgn in enumerate((-1,1)):
            angle=phase+np.pi*j; swing_z=np.sin(angle)
            airtime=max(0,np.cos(angle))
            legs.append((sgn*.132,.108+.235*airtime**1.6,.305*swing_z))
            toes.append((20*max(0,-swing_z)-14*max(0,swing_z),sgn*4,0))
            arms.append((sgn*(.211+.027*airtime),1.275-.07*swing_z,.149-.23*swing_z))
        run.append((i/8,pose(root=(.016*np.sin(phase),-.077-.036*np.sin(phase)**2,0),
                    hip=(9,8*np.sin(phase),2*np.cos(phase)),chest=(16,-12*np.sin(phase),-4*np.cos(phase)),
                    head=(3,-2*np.sin(phase),0),left=arms[0],right=arms[1],feet=legs,toes=toes)))
    clips.append(clip("run",.64,run,True))
    air1=pose(root=(0,-.024,0),hip=(-5,-8,5),chest=(-4,10,-3),head=(-4,-3,0),
              feet=((- .153,.28,.18),(.160,.22,-.18)),toes=((-12,-6,0),(17,8,0)),
              left=(-.32,1.54,.22),right=(.27,1.45,-.11))
    air2=pose(root=(0,.006,0),hip=(6,-4,2),chest=(7,4,1),head=(7,-2,0),
              feet=((- .177,.22,.16),(.164,.24,-.12)),toes=((-8,-6,0),(9,8,0)),
              left=(-.42,1.33,.14),right=(.39,1.39,.06))
    landing=pose(root=(.006,-.246,.028),hip=(24,2,2),chest=(39,6,-2),head=(12,-2,0),
                 left=(-.28,1.235,.33),right=(.26,1.205,.29))
    clips.extend([clip("air",.65,[(0,air1),(.45,air1),(.82,air2),(1,air2)]),
                  clip("land",.29,[(0,air2),(.25,landing),(.43,landing),(1,guard)])])
    relaxed=pose(root=(.018,-.027,0),hip=(1,-9,3),chest=(-2,-4,-2),head=(-1,-6,0),
                 left=(-.24,1.00,.12),right=(.258,1.025,.14),
                 feet=((- .176,.105,.072),(.169,.105,-.045)))
    wraps=pose(root=(-.008,-.047,0),hip=(2,-3,-1),chest=(6,-5,1),head=(10,-8,2),
               left=(-.083,1.27,.25),right=(-.006,1.289,.259),
               feet=((- .176,.105,.072),(.169,.105,-.045)))
    clips.append(clip("lobby",4.8,[(0,relaxed),(.22,relaxed),(.42,wraps),(.59,wraps),(.81,relaxed),(1,relaxed)],True))
    # Rise from a low, one-hand-braced spawn. The feet remain planted while
    # the free hand comes off the floor and both elbows settle into the guard.
    sp0=pose(root=(.045,-.416,.065),hip=(40,-13,-7),chest=(57,-18,-7),head=(22,4,1),
             left=(-.22,.581,.437),right=(.255,1.11,.237),
             feet=((- .193,.105,.19),(.16,.129,-.22)),toes=((0,-8,0),(24,10,0)))
    sp1=pose(root=(.03,-.32,.074),hip=(28,-7,-3),chest=(37,-9,-3),head=(7,1,1),
             left=(-.203,.82,.39),right=(.238,1.208,.244))
    sp2=pose(root=(-.012,-.073,.022),hip=(3,6,1),chest=(1,8,1),head=(-4,0,0),
             left=(-.181,1.409,.241),right=(.192,1.392,.232))
    clips.append(clip("spawn",.65,[(0,sp0),(.14,sp0),(.42,sp1),(.73,sp2),(1,guard)]))
    flinch=pose(root=(.045,-.107,-.069),hip=(-3,-5,-4),chest=(-15,-23,-12),head=(-20,-24,-8),
                left=(-.24,1.565,.075),right=(.178,1.341,.071))
    recoil_back=pose(root=(.015,-.083,-.031),hip=(4,-3,-1),chest=(1,-7,-5),head=(-5,-7,-3),
                     left=(-.19,1.50,.185),right=(.196,1.414,.17))
    clips.append(clip("hit",.24,[(0,guard),(.22,flinch),(.36,flinch),(.66,recoil_back),(1,guard)]))

    # Collapse on the left side. World objectives keep the body above the floor;
    # the last two keys are identical, so retaining the death clip holds still.
    stagger=pose(root=(-.044,-.118,-.04),hip=(8,5,8),chest=(-9,-8,17),head=(-11,-10,16),
                 left=(-.274,1.367,.10),right=(.12,1.42,.225))
    buckle=pose(root=(-.17,-.36,-.02),hip=(15,9,36),chest=(17,-5,51),head=(30,-2,46),
                left=(-.36,1.04,.22),right=(-.01,1.28,.30),
                feet=((- .235,.137,.121),(.153,.136,-.18)),
                toes=((3,-17,24),(12,3,-5)))
    def fall(turn,bend,roll):
        return angles((0,turn,0))*angles((bend,0,0))*angles((0,roll,0))
    down=pose(root=(-.257,-.673,-.055),hip=fall(-66,63,-26),chest=fall(-70,79,-23),head=fall(-76,92,-26),
              left=(-.80,.874,.15),right=(-.542,1.045,.304),
              feet=((- .08,.155,.18),(.197,.15,-.20)),toes=((8,-24,60),(5,-18,37)),
              poles=((- .75,.96,.26),(-.48,1.15,.20)),knees=((- .14,.37,.64),(.19,.37,.56)))
    floor=pose(root=(-.260,-.754,-.055),hip=fall(-75,80,-16),chest=fall(-78,94,-17),head=fall(-83,107,-27),
               left=(-.765,.875,.225),right=(-.533,.883,.055),
               feet=((- .075,.149,.24),(.307,.151,-.20)),toes=((8,-29,62),(8,-13,41)),
               poles=((- .78,.97,.25),(-.43,1.15,.22)),knees=((- .12,.36,.63),(.17,.37,.51)))
    clips.append(clip("death",.85,[(0,guard),(.14,stagger),(.37,buckle),(.65,down),(.80,floor),(1,floor)]))
    return {"clips":clips}

if __name__=="__main__":
    data=build()
    path=ROOT/"art/generated/motion.json"
    path.write_text(json.dumps(data,separators=(",",":"),allow_nan=False),encoding="utf-8")
    print(f"Exported {sum(len(c['tracks']) for c in data['clips'])} joint tracks across {len(data['clips'])} clips to {path}")
