"""Build the Team B sparring guard for the tutorial's block and parry lessons."""

import argparse
import math
from pathlib import Path
import sys

import bpy
from mathutils import Quaternion,Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
from character_art import bake_atlas,material,studio_camera
from create_camp_chest import oak
from create_neutral_archer import view
from create_neutral_knight import Knight
from creature_mesh import ramp,select
from export_fbx import export_collection


ROOT=Path(__file__).resolve().parents[2]
STEM="SK_SparringGuard"
SOURCE=ROOT/"ArtSource/Characters"/STEM
TEXTURES=ROOT/"Assets/Art/Textures/Characters/SparringGuard"
MODEL=ROOT/"Assets/Art/Characters/Models"/(STEM+".fbx")
RIGHT_GRIP=Vector((.845,.014,1.42))
LEFT_GRIP=Vector((-.837,.013,1.42))
SWORD_TIP=Vector((RIGHT_GRIP.x,.827,RIGHT_GRIP.z))


class SparringGuard(Knight):
    rig_stem=STEM

    def __init__(self):
        super().__init__()
        self.red=material("Sparring_VermilionTrainingCloth",(.141,.026,.016),0,.84,310,.00069,"cloth")
        self.quilt=material("Sparring_DarkQuiltedCanvas",(.059,.044,.033),0,.87,336,.00078,"cloth")
        self.bone_color=material("Sparring_FadedIvoryCrest",(.334,.263,.168),0,.88,360,.00048,"cloth")
        self.burnished=material("Sparring_SmokedSteel",(.118,.129,.136),.87,.43,209,.00046,"steel")
        self.wood=oak("Sparring_BluntTrainerHeartwood",(.110,.058,.023),(6.6,.27,6.6))

    def coat(self):
        self.profile("Padded arming coat below the belt","Hips",self.quilt,(0,0,0),
                     [(.825,.139,.091,0),(.896,.161,.105,0),(1.010,.152,.110,0),
                      (1.106,.143,.105,0)])
        coat=self.profile("Doublet in the training yard colors","Chest",self.red,(0,0,0),[
            (1.056,.147,.108,.008),(1.146,.166,.120,.009),(1.239,.183,.131,.008),
            (1.320,.200,.125,0),(1.396,.199,.112,0),(1.453,.176,.089,0),
            (1.483,.112,.064,0),(1.516,.066,.052,0),
        ],steps=58,keel=.008)
        tree=self.bvh(coat)
        for sign in (-1,1):
            for x in (.078,.104,.134,.163):
                path=[self.front_point(tree,sign*x*scale,z,.0016) for scale,z in
                      ((.84,1.075),(.93,1.151),(.99,1.262),(1,1.356),(.88,1.427))]
                self.tube("Raised vertical seams of the padded coat",[path],.0029,
                          self.thread,"Chest",sides=6,taper=.51)
        self.profile("Blackened throat and shoulder half-collar","Chest",self.steel,(0,0,0),[
            (1.350,.210,.137,.009),(1.368,.215,.142,.008),(1.405,.210,.129,.007),
            (1.448,.171,.113,.003),(1.484,.102,.081,0),
        ],arc=(-1.58,1.58),keel=.010,steps=49,slope=.015)
        self.profile("Riveted leather back yoke","Chest",self.leather,(0,0,0),[
            (1.298,.177,.120,-.006),(1.330,.197,.133,-.004),(1.419,.209,.121,-.004),
            (1.459,.164,.098,0),(1.487,.112,.078,0),
        ],arc=(1.25,5.03),steps=53)
        self.fitted_crest(tree)
        for sign in (-1,1):
            x=sign*.151
            for i in range(3):
                z=1.283-i*.053
                y=self.front_point(tree,x,z,.005).y
                self.plate("Oiled shoulder lacing","Chest",self.leather,
                           [(x-.021,y,z-.016),(x+.035,y,z-.012),
                            (x+.033,y,z+.012),(x-.023,y,z+.016)],.009,.004)
                self.rivet("Chest",(x+.012,y+.004,z),.0057)
        self.profile("Broad leather waist belt","Hips",self.leather,(0,0,0),[
            (1.009,.168,.127,.008),(1.024,.173,.130,.010),
            (1.056,.169,.131,.009),(1.067,.165,.126,.006),
        ],steps=45)
        self.plate("Square course buckle","Hips",self.brass,
                   [(-.031,.152,1.015),(.032,.152,1.016),(.029,.153,1.057),(-.030,.153,1.059)],.010,.007)
        self.plate("Recess of the square buckle","Hips",self.iron,
                   [(-.019,.158,1.024),(.019,.158,1.024),(.019,.158,1.049),(-.019,.158,1.049)],.004,.003)
        for sign in (-1,1):
            self.skirt(sign)
            self.rivet("Hips",(sign*.076,.130,1.041),.0053)

    def fitted_crest(self,tree):
        # Let the cloth carry the same three vertical bars used on the Team B gates.
        for x in (-.048,0,.048):
            verts=[self.front_point(tree,x+side*w,z,.0032) for z,w in
                   ((1.205,.011),(1.221,.014),(1.264,.014),(1.305,.012),(1.324,.009))
                   for side in (-1,1)]
            faces=[(i,i+1,i+3,i+2) for i in range(0,len(verts)-2,2)]
            self.finish(self.surface("Team B three bars in faded stitching",verts,faces,smooth=False),
                        self.bone_color,"Chest")

    def skirt(self,sign):
        leg=("Left" if sign<0 else "Right")+"UpperLeg"

        def weights(point):
            amount=.70*(1-ramp(point.z,.687,1.010))
            return {"Hips":1-amount,leg:amount}

        rows=((.158,.124,1.055),(.167,.133,.998),(.163,.142,.924),(.151,.155,.815),
              (.134,.160,.720))
        n=34
        for front in (-1,1):
            verts=[]
            for row,(width,depth,z) in enumerate(rows):
                for i in range(n):
                    u=i/(n-1)
                    pleat=.013*math.cos(u*math.pi*5+row*.26)+.004*math.sin(11*u+row)
                    hem=(.018*math.sin(u*14)+.021*u) if row==4 else 0
                    verts.append((sign*(.011+width*u),front*(depth+pleat),z+hem))
            faces=[(i,i+1,i+n+1,i+n) for i in range(4*n) if i%n<n-1]
            cloth=self.surface("Split padded skirt",verts,faces)
            select([cloth])
            solid=cloth.modifiers.new("Doubled padded cloth","SOLIDIFY")
            solid.thickness=.007
            bpy.ops.object.modifier_apply(modifier=solid.name)
            self.finish(cloth,self.red if front==1 else self.quilt,weights)
            self.tube("Binding along the hem",[[(x,y+front*.003,z+.010) for x,y,z in verts[-n:]]],
                      .0025,self.thread,weights,sides=5,taper=.14)

    def helmet(self):
        self.profile("Padded hood under the training helmet","Head",self.shadow,(0,0,0),[
            (1.519,.055,.054,0),(1.559,.077,.074,.002),(1.632,.095,.091,.003),
            (1.725,.099,.092,0),(1.793,.071,.067,0),
        ])
        self.profile("Forged low training skull","Head",self.burnished,(0,0,0),[
            (1.570,.080,.085,-.018),(1.629,.099,.107,-.012),(1.704,.108,.114,-.016),
            (1.757,.102,.103,-.015),(1.806,.084,.089,-.018),(1.849,.052,.062,-.020),
            (1.871,.016,.034,-.023),(1.879,.004,.012,-.028),
        ],steps=62,keel=.012)
        self.profile("Low helmet brim with rolled edge","Head",self.steel,(0,0,0),[
            (1.702,.106,.121,.005),(1.713,.129,.151,.009),(1.727,.125,.145,.008),
            (1.742,.108,.119,.002),
        ],steps=56,slope=.016,keel=.012)
        self.profile("Bright turned lip of the brim","Head",self.edge,(0,0,0),[
            (1.711,.128,.149,.010),(1.715,.130,.153,.009),(1.720,.127,.149,.009),
        ],steps=56,slope=.016,keel=.012)
        lower=self.profile("Hinged lower steel visor","Head",self.burnished,(0,0,0),[
            (1.535,.046,.065,.014),(1.563,.067,.088,.013),(1.596,.090,.102,.010),
            (1.636,.101,.108,.008),(1.670,.096,.105,.005),
        ],arc=(-1.50,1.50),keel=.027,steps=56,slope=.012)
        self.profile("Shadow inside the slit","Head",self.shadow,(0,0,0),[
            (1.667,.089,.102,.005),(1.702,.092,.102,.005),
        ],arc=(-1.43,1.43),keel=.013,steps=43,slope=.011)
        self.profile("Rear segmented neck guard","Head",self.iron,(0,-.009,0),[
            (1.544,.065,.072,0),(1.566,.084,.093,0),(1.619,.094,.102,0),
            (1.657,.100,.101,0),
        ],arc=(1.37,4.91),steps=47)
        tree=self.bvh(lower)
        for sign in (-1,1):
            vents=[]
            for x in (.026,.042,.057):
                vents.append([self.front_point(tree,sign*x,1.606+x*.43,.0012),
                              self.front_point(tree,sign*x,1.623+x*.43,.0012)])
            self.tube("Thin openings stamped in the visor",vents,.0023,self.shadow,"Head",sides=6,taper=.41)
            self.oval("Black iron hinge for the visor","Head",self.iron,
                      (sign*.101,.023,1.661),(.008,.020,.022),18,10)
            self.oval("Bronze peened hinge pin","Head",self.brass,
                      (sign*.109,.023,1.661),(.005,.009,.011),15,8)
            abrasion=[self.front_point(tree,sign*x,z,.0008) for x,z in
                      ((.073,1.603),(.065,1.587),(.048,1.576))]
            self.tube("Light scuff on the visor",[abrasion],.0018,self.edge,"Head",sides=6,taper=.62)
        self.chainmail("Riveted throat protection","Neck",(0,0,1.468),.076,5,29,ring=.0062)

    def arm(self,side,sign):
        upper,lower,hand=[side+suffix for suffix in ("UpperArm","LowerArm","Hand")]
        axis={"axis":sign}
        self.profile("Vermilion quilted sleeve",upper,self.red,(sign*.225,0,1.42),[
            (0,.079,.079,0),(.053,.087,.084,0),(.123,.078,.075,0),(.195,.065,.067,0),
            (.279,.052,.054,0),
        ],**axis,steps=44)
        for i in range(3 if sign>0 else 2):
            x=.236+.041*i
            r=.110-.011*i
            self.profile("Low worn shoulder plates",upper,self.steel,(sign*x,0,1.42),[
                (0,r*.84,r*.87,0),(.012,r,r,0),(.047,r*.85,r*.84,0),
            ],**axis,arc=(-1.83,1.83),steps=37,slope=.008)
            self.rivet(upper,(sign*(x+.025),r*.92,1.43),.007)
        self.profile("Soft lower sleeve and elbow",lower,self.quilt,(sign*.478,0,1.42),[
            (0,.053,.055,0),(.046,.065,.064,0),(.116,.060,.061,0),
            (.199,.045,.046,0),(.276,.044,.044,0),
        ],**axis,steps=42)
        self.chainmail("Riveted links in the bend of arm",lower,(sign*.489,0,1.42),
                       .063,6,17,axis=sign,arc=(-1.45,1.45),ring=.0061)
        self.profile("Braced steel forearm guard",lower,self.burnished,(sign*.575,0,1.42),[
            (0,.061,.066,0),(.045,.059,.065,0),(.135,.048,.049,0),(.183,.047,.048,0),
        ],**axis,arc=(-1.67,1.67),steps=43,keel=.008)
        for x,r in ((.589,.063),(.725,.051)):
            self.profile("Forearm fastening straps",lower,self.leather,(sign*x,0,1.42),[
                (0,r,r,0),(.019,r*.98,r*.96,0),
            ],**axis,steps=25)
            self.rivet(lower,(sign*(x+.012),r+.004,1.42),.0062)
        self.profile("Leather training glove",hand,self.leather,(sign*.777,.006,1.42),[
            (0,.040,.039,0),(.026,.047,.043,0),(.063,.041,.038,0),(.089,.034,.025,0),
        ],**axis,steps=33)
        for i in range(4):
            z=1.387+i*.020
            outward=.020+(4-i)*.005
            path=[(sign*.829,.038,z),(sign*.852,.045,z),(sign*(.842+outward),.030,z),
                  (sign*.865,.010,z),(sign*.850,-.006,z)]
            self.tube("Leather-covered fingers around the grip",[path],.0081,self.leather,
                      hand,sides=8,taper=.39)
        self.oval("Leather-covered thumb",hand,self.leather,(sign*.817,.036,1.391),(.025,.019,.019))
        self.profile("Thin steel plate on the glove",hand,self.iron,(sign*.788,0,1.42),[
            (0,.044,.048,0),(.021,.048,.052,0),(.044,.044,.048,0),
        ],**axis,arc=(-1.44,1.44),steps=32)

    def practice_sword(self):
        bone="RightHand"
        x,y,z=RIGHT_GRIP
        sections=((.052,.022,.009),(.088,.037,.013),(.132,.050,.014),(.222,.047,.013),
                  (.452,.042,.012),(.645,.034,.010),(.747,.025,.009),
                  (.794,.017,.007),(.810,.009,.004),(.813,.005,.003))
        verts=[(x+u*w,y+length,z+v*d) for length,w,d in sections for u,v in
               ((-1,0),(-.74,.76),(0,1),(.74,.76),(1,0),(.74,-.76),(0,-1),(-.74,-.76))]
        count=8
        faces=[(i*count+j,i*count+(j+1)%count,(i+1)*count+(j+1)%count,(i+1)*count+j)
               for i in range(len(sections)-1) for j in range(count)]
        faces.extend((tuple(reversed(range(count))),tuple(range(len(verts)-count,len(verts)))))
        blade=self.finish(self.surface("Blunt ash training blade with an iron rim",verts,faces,smooth=False),
                          self.wood,bone)
        blade.data.materials.append(self.burnished)
        for face in blade.data.polygons:
            if face.index%8 in (0,3,4,7) or face.index>=7*8:
                face.material_index=1
        guard=[(-.117,.072),(-.101,.084),(-.041,.067),(.041,.067),(.103,.085),
               (.116,.072),(.110,.053),(.039,.037),(-.042,.037),(-.110,.053)]
        self.plate("Blunted oiled crossguard",bone,self.iron,
                   [(x+u,y+v,z+.011) for u,v in guard],.031,.005)
        self.oval("Dark leather hand grip",bone,self.leather,(x,y-.006,z),(.020,.065,.019),20,12)
        self.oval("Round iron pommel",bone,self.iron,(x,y-.069,z),(.024,.024,.021),21,12)
        self.tube("Visible training blade ridge",[[(x,y+length,z+d+.0005) for length,_,d in sections[2:-3]]],
                  .0015,self.brass,bone,sides=6,taper=.87)

    def geometry(self):
        self.coat()
        self.helmet()
        for side,sign in (("Left",-1),("Right",1)):
            self.arm(side,sign)
            self.legs(side,sign)
        self.practice_sword()

    def pose_training(self,ready,grip,pitch,yaw,left,look=0):
        super().pose(not ready)
        right_rotation=Quaternion((0,0,1),math.radians(yaw))@Quaternion((1,0,0),math.radians(pitch))
        self.arm_to("Right",Vector(grip),right_rotation,RIGHT_GRIP,Vector((.94,-.35,.12)))
        off_rotation=Quaternion((0,0,1),math.radians(-34))@Quaternion((0,1,0),math.radians(-67))
        self.arm_to("Left",Vector(left),off_rotation,LEFT_GRIP,Vector((-.9,-.04,-.58)))
        self.set_rotation("Head",Quaternion((0,0,1),math.radians(look)))

    def review_action(self):
        scene=self.scene
        scene.render.fps=25
        idle=(True,(.416,.121,.970),-44,-38,(-.302,.152,.991),5)
        controls=(
            (1,idle),
            (9,(False,(.405,.107,1.34),18,-28,(-.335,.220,1.110),3)),
            (18,(False,(.389,.043,1.735),67,-32,(-.335,.251,1.220),0)),
            (23,(False,(.382,.055,1.788),77,-32,(-.332,.246,1.249),-3)),
            (26,(False,(.374,.138,1.723),68,-24,(-.295,.219,1.199),-2)),
            (29,(False,(.342,.445,1.370),-15,-12,(-.257,.218,1.130),1)),
            (34,(False,(.326,.362,1.131),-37,-5,(-.276,.191,1.093),5)),
            (44,idle),
        )
        for frame,params in controls:
            scene.frame_set(frame)
            self.pose_training(*params)
            for bone in self.rig.pose.bones:
                bone.keyframe_insert(data_path="rotation_quaternion",frame=frame,group=bone.name)
                bone.keyframe_insert(data_path="location",frame=frame,group=bone.name)
        for label,frame in (("Ready",1),("Hold windup",23),("Contact at 1.12 s",29),("Recover",44)):
            scene.timeline_markers.new(label,frame=frame)
        self.rig.animation_data.action.name="Sparring guard - tutorial windup"
        scene.frame_start,scene.frame_end=1,44
        scene.frame_set(1)


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--quick",action="store_true")
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    SOURCE.mkdir(parents=True,exist_ok=True)
    guard=SparringGuard()
    mesh=guard.finish_model()
    if not args.quick:
        bake_atlas(mesh,2048,TEXTURES,"T_SparringGuard","UV0")
        slots=list(mesh.data.materials)
        indices=[p.material_index for p in mesh.data.polygons]
        mesh.data.materials.clear()
        mesh.data.materials.append(material("M_SparringGuard_Atlas",(.115,.062,.044),.39,.61,0,0,""))
        export_collection(MODEL,bake_animation=False)
        mesh.data.materials.clear()
        for mat in slots:
            mesh.data.materials.append(mat)
        for polygon,index in zip(mesh.data.polygons,indices):
            polygon.material_index=index
    print(f"{STEM}: {len(mesh.data.polygons)} triangles, {len(guard.rig.data.bones)} bones",flush=True)
    scene=guard.scene
    camera=studio_camera(scene)
    bpy.data.objects["Studio shadow floor"].location.z=0
    bpy.data.objects["Soft key"].data.energy=510
    bpy.data.objects["Front fill"].data.energy=158
    bpy.data.objects["Rim"].data.energy=340
    scene.render.resolution_x,scene.render.resolution_y=1060,1325
    scene.render.resolution_percentage=52 if args.quick else 100
    scene.cycles.samples=12 if args.quick else 28
    guard.review_action()
    frames=(("",(2.39,6.51,1.94),(.11,.01,.94),2.30,1),
            ("Windup",(2.61,6.92,2.34),(.19,.05,1.28),3.02,23),
            ("Detail",(-1.26,4.92,1.75),(0,.06,1.53),.89,1),
            ("Back",(-2.84,-5.96,1.91),(.10,-.09,.94),2.31,1))
    view(camera,*frames[0][1:4])
    if not args.quick:
        select([mesh])
        bpy.context.preferences.filepaths.save_version=0
        bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(STEM+".blend")))
    for suffix,pos,target,scale,frame in frames:
        scene.frame_set(frame)
        view(camera,pos,target,scale)
        scene.render.filepath=str(SOURCE/("SparringGuard"+suffix+".png"))
        bpy.ops.render.render(write_still=True)


if __name__=="__main__":
    main()
