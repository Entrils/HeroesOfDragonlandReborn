"""Build the neutral camp mage on the shared humanoid skeleton."""

import argparse
import math
from pathlib import Path
from random import Random
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
STEM="SK_NeutralMage"
SOURCE=ROOT/"ArtSource/Characters"/STEM
TEXTURES=ROOT/"Assets/Art/Textures/Characters/NeutralMage"
MODEL=ROOT/"Assets/Art/Characters/Models"/(STEM+".fbx")
STAFF_GRIP=Vector((.842,.025,1.42))
SPELL_PALM=Vector((-.838,.018,1.42))


class Mage(Knight):
    rig_stem=STEM

    def __init__(self):
        super().__init__()
        self.ash=material("Mage_CharcoalWool",(.029,.031,.037),0,.88,349,.00084,"cloth")
        self.wine=material("Mage_FadedOxblood",(.057,.011,.016),0,.86,352,.00068,"cloth")
        self.trim=material("Mage_TarnishedGoldThread",(.193,.123,.061),.14,.72,410,.00054,"cloth")
        self.mask=material("Mage_AntiqueDarkBronze",(.132,.092,.064),.74,.55,206,.00058,"steel")
        self.staff_wood=oak("Mage_AncientAshWood",(.078,.041,.022),(5.3,6.1,.27))
        self.boot_binding=material("Mage_StainedBootBinding",(.042,.031,.022),0,.79,310,.00048,"leather")
        self.amethyst=material("Mage_DullAmethyst",(.119,.061,.183),.18,.35,160,.00038,"steel")
        self.crystal_edge=material("Mage_AmethystCleavage",(.28,.117,.407),.11,.28,240,.00033,"steel")
        shader=self.amethyst.node_tree.nodes["Principled BSDF"]
        shader.inputs["Emission Color"].default_value=(.31,.116,.49,1)
        shader.inputs["Emission Strength"].default_value=.18

    def robes(self):
        for side in (-1,1):
            leg=("Left" if side<0 else "Right")+"UpperLeg"

            def weights(point):
                fraction=(1-ramp(point.z,.23,1.01))*.83
                return {"Hips":1-fraction,leg:fraction}

            sections=((.153,.122,1.060),(.166,.138,.972),(.182,.150,.858),
                      (.199,.168,.729),(.214,.183,.590),(.236,.199,.451),
                      (.257,.213,.320),(.268,.221,.195))
            width=41
            verts=[]
            for row,(rx,ry,z) in enumerate(sections):
                growth=row/(len(sections)-1)
                for col in range(width):
                    u=col/(width-1)
                    theta=.11+u*(math.pi-.18)
                    fold=(.012+.013*growth)*(.68*math.sin(11*theta+growth*.82)+
                                             .29*math.sin(23*theta-growth*1.7+side))
                    hem=0 if row<len(sections)-1 else .066*max(0,1-abs(u-.68)/.065)+.012*math.sin(u*39)
                    verts.append((side*(rx+fold)*math.sin(theta),
                                  (ry+fold)*math.cos(theta),z+hem))
            faces=[(idx,idx+1,idx+width+1,idx+width) for idx in range((len(sections)-1)*width)
                   if idx%width<width-1]
            robe=self.surface("Full length gown with shaped folds",verts,faces)
            select([robe])
            solid=robe.modifiers.new("Heavy doubled wool","SOLIDIFY")
            solid.thickness=.005
            bpy.ops.object.modifier_apply(modifier=solid.name)
            self.finish(robe,self.ash,weights)
            edge=[(x,y+.003,z) for x,y,z in verts[::width]]
            self.tube("Front opening binding",[edge],.0034,self.trim,weights,sides=6,taper=0)
            hem=verts[-width:]
            self.tube("Worn double hem",[[(x,y+(.002 if y>0 else -.002),z+.017)
                        for x,y,z in hem[:23]]],.0022,self.trim,weights,sides=6,taper=.55)
            fringes=[[(x,y,z+.002),(x+.002*side,y,z-.008-.0017*(i%4))]
                     for i,(x,y,z) in enumerate(hem) if i%4!=1]
            self.tube("Fray at robe hem",fringes,.00086,self.ash,weights,sides=5,taper=.8)
        self.tabard(1,.314,self.wine)
        self.tabard(-1,.617,self.ash)

    def tabard(self,front,bottom,mat):
        width=27
        rows=((.113,.124,1.045),(.111,.151,.949),(.102,.164,.833),(.099,.178,.703),
              (.090,.198,bottom+(.045 if front<0 else .087)),(.080,.202,bottom))
        verts=[]
        for row,(rx,depth,z) in enumerate(rows):
            for i in range(width):
                u=2*i/(width-1)-1
                y=depth+.010*math.cos(u*11.5+row*.44)
                cut=.038*max(0,1-abs(u-.27)/.26) if row==len(rows)-1 else 0
                verts.append((rx*u+.011*(row/5)**2,front*y,z+cut))
        faces=[(i,i+1,i+width+1,i+width) for i in range((len(rows)-1)*width) if i%width!=width-1]
        obj=self.surface("Long livery panel",verts,faces)
        select([obj])
        solid=obj.modifiers.new("Wool panel thickness","SOLIDIFY")
        solid.thickness=.004
        bpy.ops.object.modifier_apply(modifier=solid.name)

        def weights(point):
            step=(1-ramp(point.z,.36,1.03))*.67
            left=1-ramp(point.x,-.062,.062)
            return {"Hips":1-step,"LeftUpperLeg":step*left,"RightUpperLeg":step*(1-left)}

        self.finish(obj,mat,weights)
        borders=[]
        for i in (2,width-3):
            line=[verts[j+i] for j in range(0,len(verts),width)]
            borders.append([(x,y+front*.003,z) for x,y,z in line])
        self.tube("Frayed gold stripes",borders,.0021,self.trim,weights,sides=6,taper=.42)

    def chest_and_mantle(self):
        self.profile("Fitted padded coat","Hips",self.ash,(0,0,0),
                     [(.87,.136,.094,0),(.97,.153,.106,0),(1.043,.145,.104,0),
                      (1.131,.145,.107,0)])
        coat=self.profile("Upper coat","Chest",self.wine,(0,0,0),
                     [(1.083,.139,.107,0),(1.202,.159,.111,0),(1.310,.186,.116,0),
                      (1.421,.204,.108,0),(1.482,.135,.082,0),(1.515,.066,.053,0)])
        yoke=self.profile("Layered ritual shoulder mantle","Chest",self.ash,(0,0,0),
                          [(1.289,.187,.131,0),(1.312,.207,.134,0),(1.395,.228,.121,0),
                           (1.452,.194,.110,0),(1.502,.103,.073,-.008)],
                          arc=(-math.pi,math.pi-.004),steps=58,slope=.019)
        mantle=self.bvh(yoke)
        coat_surface=self.bvh(coat)
        for side in (-1,1):
            for band in range(2):
                points=[self.front_point(mantle,side*x,z+band*.030,.0025) for x,z in
                        ((.040,1.302),(.080,1.316),(.124,1.342),(.167,1.365),(.195,1.391))]
                self.tube("Worked mantle border",[points],.0021,self.trim,"Chest",sides=6,taper=.28)
            guard=[self.front_point(mantle,side*x,z,.008) for x,z in
                   ((.087,1.459),(.130,1.429),(.168,1.432),(.194,1.404),
                    (.177,1.371),(.139,1.384),(.100,1.397))]
            self.plate("Blackened mantle guard","Chest",self.steel,guard,.014,.005)
            self.rivet("Chest",self.front_point(mantle,side*.139,1.415,.015),.007,self.brass)
            seam=[self.front_point(coat_surface,side*x,z,.0016) for x,z in
                  ((.025,1.104),(.044,1.141),(.055,1.179),(.062,1.233),(.065,1.285))]
            self.tube("Raised coat dart",[seam],.0025,self.trim,"Chest",sides=6,taper=.61)
            for x,z in ((.059,1.207),(.058,1.234),(.062,1.261)):
                self.rivet("Chest",self.front_point(coat_surface,side*x,z,.0024),.0045,self.brass)
        self.tube("Crossover lacing under the mantle",[
            [self.front_point(coat_surface,x,z,.003) for x,z in
             ((-.056,1.209),(-.025,1.216),(0,1.223),(.025,1.230),(.056,1.237))],
            [self.front_point(coat_surface,x,z,.003) for x,z in
             ((.056,1.210),(.026,1.217),(0,1.224),(-.027,1.231),(-.056,1.238))],
        ],.0021,self.trim,"Chest",sides=6,taper=0)
        plaque=[(-.052,.142,1.359),(0,.153,1.416),(.052,.142,1.359),(0,.152,1.304)]
        self.plate("Neutral diamond mantle clasp","Chest",self.brass,plaque,.010,.006)
        inner=[Vector((x,y+.006,1.36+(z-1.36)*.63)) for x,y,z in plaque]
        inner=[Vector((p.x*.63,p.y,p.z)) for p in inner]
        self.plate("Recess of the neutral clasp","Chest",self.iron,inner,.007,.003)
        self.tube("Bronze thread linked to the clasp",
                  [[(-.087,.137,1.385),(-.052,.152,1.358)],[(.052,.152,1.358),(.087,.137,1.385)]],
                  .0035,self.brass,"Chest",sides=7,taper=0)
        self.profile("Heavy outer belt","Hips",self.leather,(0,0,0),
                     [(.972,.168,.137,0),(.988,.175,.137,0),(1.034,.175,.134,0),
                      (1.047,.168,.130,0)],steps=47)
        self.plate("Hammered belt clasp","Hips",self.brass,[(-.032,.145,.989),(.031,.145,.989),
                   (.031,.145,1.034),(-.032,.145,1.034)],.014,.006)
        self.plate("Belt clasp dark inset","Hips",self.iron,[(-.020,.152,1.001),(.019,.152,1.001),
                   (.020,.152,1.024),(-.020,.152,1.024)],.006,.003)

    def hood_and_mask(self):
        self.profile("Tall shadowed cowl","Head",self.ash,(0,0,0),
                     [(1.501,.078,.079,-.027),(1.566,.101,.100,-.018),
                      (1.660,.116,.119,-.009),(1.736,.112,.111,-.003),
                      (1.798,.094,.102,-.016),(1.838,.061,.075,-.021),
                      (1.871,.034,.054,-.038),(1.888,.013,.028,-.054)],
                     arc=(.90,5.38),steps=65)
        self.profile("Heavy hanging hood forehead","Head",self.ash,(0,0,0),
                     [(1.726,.110,.121,.006),(1.741,.115,.118,0),(1.779,.104,.113,-.009),
                      (1.817,.076,.090,-.017),(1.853,.052,.069,-.026),
                      (1.883,.016,.031,-.051)],arc=(-1.05,1.05),keel=.008,steps=52,slope=.012)
        self.oval("Joined fold at the top of the hood","Head",self.ash,(0,-.052,1.880),
                  (.014,.028,.012),19,11)
        self.profile("Dark cloth below the carved mask","Head",self.shadow,(0,0,0),
                     [(1.510,.054,.058,-.005),(1.553,.081,.088,0),(1.634,.100,.095,.001),
                      (1.711,.106,.099,0),(1.763,.092,.081,-.007)],steps=53)
        mask=self.profile("Faceted bronze ritual mask","Head",self.mask,(0,0,0),
                          [(1.523,.031,.070,.014),(1.543,.055,.084,.014),
                           (1.589,.078,.102,.010),(1.631,.094,.108,.007),
                           (1.658,.099,.104,.005)],arc=(-1.37,1.37),keel=.039,steps=58,slope=.020)
        self.profile("Bronze mask upper rim","Head",self.mask,(0,0,0),
                     [(1.672,.102,.110,.004),(1.690,.100,.116,.005),(1.710,.096,.108,.007),
                      (1.731,.086,.103,0)],arc=(-1.33,1.33),keel=.018,steps=55,slope=.009)
        tree=self.bvh(mask)
        for side in (-1,1):
            trim=[(side*x,y,z) for x,y,z in ((.068,.040,1.520),(.092,.051,1.566),
                  (.114,.057,1.635),(.116,.079,1.701),(.086,.104,1.753),(.053,.092,1.789))]
            self.tube("Rolled edge around the hood opening",[trim],.0048,self.wine,"Head",sides=8,taper=.21)
            marks=[]
            for x,z in ((.030,1.549),(.044,1.574),(.063,1.595)):
                marks.append([self.front_point(tree,side*(x-.009),z,.0011),
                              self.front_point(tree,side*x,z+.018,.0011)])
            self.tube("Dark slits in the mask",marks,.0030,self.shadow,"Head",sides=6,taper=.56)
            curls=[self.front_point(tree,side*x,z,.0018) for x,z in
                   ((.030,1.633),(.045,1.643),(.065,1.640),(.076,1.620))]
            self.tube("Engraved mark on the mask",[curls],.0017,self.edge,"Head",sides=6,taper=.50)
        self.profile("Layered neck scarf","Neck",self.wine,(0,0,0),
                     [(1.467,.079,.074,0),(1.486,.088,.081,0),(1.518,.079,.070,0),
                      (1.548,.069,.061,-.005)],steps=43)
        self.chainmail("Fine links below the scarf","Neck",(0,0,1.454),.087,4,28,ring=.0069)

    def arm(self,side,sign):
        upper,lower,hand=[side+s for s in ("UpperArm","LowerArm","Hand")]
        axis={"axis":sign}
        self.profile("Gown's gathered upper sleeve",upper,self.ash,(sign*.230,0,1.42),
                     [(0,.083,.073,0),(.073,.087,.090,0),(.146,.070,.074,0),
                      (.249,.059,.062,0),(.282,.053,.056,0)],**axis,steps=48)
        for i in range(3):
            x=.249+i*.037
            radius=.084-i*.004
            self.profile("Oxblood pleat at the shoulder",upper,self.wine,(sign*x,0,1.42),
                         [(0,radius,.94*radius,0),(.020,radius*1.08,radius,0),(.040,radius,.91*radius,0)],
                         **axis,arc=(-1.63,1.67),steps=38)
        self.profile("Loose belled lower sleeve",lower,self.wine,(sign*.493,0,1.42),
                     [(0,.054,.055,0),(.042,.065,.063,0),(.126,.065,.064,0),
                      (.207,.055,.053,0),(.244,.054,.052,0)],**axis,steps=49)
        self.profile("Dark steel forearm cuff",lower,self.steel,(sign*.681,0,1.42),
                     [(0,.059,.057,0),(.014,.061,.058,0),(.049,.056,.055,0),
                      (.072,.050,.051,0)],**axis,steps=41,keel=.006)
        for x in (.690,.739):
            self.profile("Bronze wire on the cuff",lower,self.brass,(sign*x,0,1.42),
                         [(0,.059,.059,0),(.007,.058,.057,0)],**axis,steps=37)
        self.profile("Hand in reinforced leather glove",hand,self.leather,(sign*.773,.006,1.42),
                     [(0,.037,.038,0),(.027,.044,.046,0),(.066,.038,.032,0),
                      (.088,.034,.025,0)],**axis,steps=34)
        for i in range(4):
            z=1.387+i*.021
            extent=(.084,.111,.105,.074)[i]
            splay=(i-1.5)*(.008 if sign<0 else .001)
            if sign<0:
                points=[(-.832,.022,z),(-(.834+extent*.48),.028,z+splay*.5),
                        (-(.834+extent*.84),.044,z+splay),(-(.834+extent),.062,z+splay)]
            else:
                points=[(.825,.053,z),(.850,.056,z),(.876,.041,z),
                        (.876,.010,z),(.854,-.005,z)]
            self.tube("Glove finger with shaped joints",[points],.0094,self.leather,hand,sides=9,taper=.39)
        self.tube("Leather thumb", [[(sign*.797,.023,1.395),(sign*.822,.038,1.381),
                  (sign*.847,.041,1.381),(sign*.862,.044,1.388)]],.013,self.leather,hand,sides=10,taper=.51)

    def boots(self,side,sign):
        x=sign*.11
        foot,shin=side+"Foot",side+"LowerLeg"
        self.profile("Soft leather boot",foot,self.leather,(x,0,0),
                     [(.011,.056,.123,.061),(.023,.063,.139,.060),(.047,.061,.130,.058),
                      (.072,.054,.098,.032),(.108,.050,.068,-.003),(.159,.051,.051,-.006)],steps=38)
        self.profile("Soft leather boot shaft",shin,self.ash,(x,0,0),
                     [(.118,.051,.052,0),(.182,.055,.057,0),(.276,.060,.065,-.004),
                      (.339,.064,.068,-.006)],steps=37)
        for i in range(3):
            z=.076-i*.012
            self.tube("Folded seams on the boot toe",
                      [[(x-.043,.102+i*.028,z-.010),(x,.099+i*.027,z),
                        (x+.042,.101+i*.028,z-.013)]],.0022,self.boot_binding,foot,sides=6,taper=.31)

    def accessories(self):
        for x in (-.151,.148):
            self.profile("Small folded leather reagent purse","Hips",self.leather,(x,.083,0),
                         [(.801,.044,.037,0),(.822,.058,.052,0),(.895,.059,.049,0),
                          (.948,.045,.042,0),(.968,.032,.032,0)],steps=34)
            self.tube("Old purse stitching",
                      [[(x-.036,.124,.840),(x-.048,.129,.872),(x-.043,.130,.908),(x-.031,.116,.932)]],
                      .0022,self.trim,"Hips",sides=6,taper=.28)
            self.rivet("Hips",(x,.131,.916),.006,self.brass)
        for x,y,height in ((.102,-.138,.98),(.165,-.115,.948)):
            self.profile("Faceted opaque reagent phial","Hips",self.amethyst,(x,y,height),
                         [(-.118,.019,.023,0),(-.107,.029,.026,0),(-.061,.028,.025,0),
                          (-.046,.015,.014,0),(-.017,.014,.013,0)],steps=10)
            self.profile("Old bronze phial stopper","Hips",self.brass,(x,y,height),
                         [(-.028,.015,.015,0),(-.020,.020,.019,0),(-.008,.018,.018,0)],steps=14)

    def staff(self):
        bone="RightHand"
        x,y,z=STAFF_GRIP
        stations=[(-.978,-.027,.010,.018),(-.904,-.024,.003,.023),(-.702,-.034,.025,.027),
                  (-.483,-.027,.009,.024),(-.251,-.016,-.016,.021),(-.084,-.008,-.003,.023),
                  (.030,.006,.001,.025),(.248,.034,.011,.024),(.511,.054,.019,.029),
                  (.705,.052,.016,.037),(.811,.071,.025,.040),(.894,.094,.016,.025)]
        n=14
        profiles=[Vector(a).lerp(Vector(b),i/5) for a,b in zip(stations,stations[1:]) for i in range(5)]
        profiles.append(Vector(stations[-1]))
        verts=[]
        for row,(height,dx,dy,r) in enumerate(profiles):
            for j in range(n):
                t=math.tau*j/n
                radius=r*(1+.094*math.sin(t*3+row*.19)+.038*math.sin(t*7-row*.08))
                verts.append((x+dx+radius*math.sin(t),y+dy+radius*math.cos(t),z+height))
        faces=[(row*n+j,row*n+(j+1)%n,(row+1)*n+(j+1)%n,(row+1)*n+j)
               for row in range(len(profiles)-1) for j in range(n)]
        faces.extend((tuple(reversed(range(n))),tuple(range(len(verts)-n,len(verts)))))
        self.finish(self.surface("Knotted ash staff",verts,faces),self.staff_wood,bone)
        for h,dx,dy,r in ((-.923,-.027,.008,.026),(-.089,-.006,-.003,.026),
                          (.071,.011,.004,.031),(.621,.052,.018,.037),(.787,.065,.025,.045)):
            self.profile("Bronze ferrule",bone,self.brass,(x+dx,y+dy,z+h),
                         [(-.023,r*.92,r*.89,0),(-.013,r,r,0),(.013,r*.96,r*.95,0),
                          (.021,r*.87,r*.88,0)],steps=26)
        self.profile("Staff leather grip",bone,self.leather,(x,y,z),
                     [(-.087,.026,.025,0),(-.064,.030,.030,0),(.074,.030,.027,0),
                      (.089,.027,.026,0)],steps=29)
        for i in range(13):
            h=-.074+i*.012
            line=[(x+.031*math.sin(a)+h*.053,y+.031*math.cos(a),z+h+.010*j/16)
                  for j in range(17) for a in (j*math.tau/16,)]
            self.tube("Spiral leather wrap on the staff",[line],.0018,self.trim,bone,sides=6,taper=0)
        center=Vector((x+.072,y+.024,z+.914))
        halo=[]
        for i in range(12):
            t=i*math.tau/12
            halo.append(center+Vector((.112*math.sin(t),0,.145*math.cos(t))))
        self.tube("Hammered halo of the staff crown",[halo+[halo[0]]],.010,self.iron,bone,sides=8,taper=0)
        for sign in (-1,1):
            branch=[(x+.061,y+.021,z+.711),(x+.069+sign*.075,y+.022,z+.795),
                    (x+.072+sign*.112,y+.009,z+.914),(x+.077+sign*.080,y+.006,z+1.013),
                    (x+.078+sign*.045,y+.020,z+1.063)]
            self.tube("Bronze terminal claw",[branch],.018,self.brass,bone,sides=9,taper=.59)
            self.oval("Small hinge pin in crown",bone,self.brass,halo[3 if sign>0 else 9],
                      (.013,.015,.015),15,8)
        self.crystal(center,bone)

    def crystal(self,center,bone):
        rng=Random(747)
        sides=7
        verts=[]
        for level,radius in ((-.110,.008),(-.058,.041),(.046,.047),(.108,.009)):
            for i in range(sides):
                theta=i*math.tau/sides+.24
                r=radius*rng.uniform(.88,1.12)
                verts.append(center+Vector((r*math.sin(theta),r*.79*math.cos(theta),
                                            level+rng.uniform(-.008,.008))))
        faces=[(row*sides+i,row*sides+(i+1)%sides,(row+1)*sides+(i+1)%sides,(row+1)*sides+i)
               for row in range(3) for i in range(sides)]
        faces.extend((tuple(reversed(range(sides))),tuple(range(len(verts)-sides,len(verts)))))
        obj=self.finish(self.surface("Rough amethyst focus",verts,faces,smooth=False),self.amethyst,bone)
        obj.data.materials.append(self.crystal_edge)
        for i in (1,4,7,10,13,16,18):
            obj.data.polygons[i].material_index=1

    def geometry(self):
        self.chest_and_mantle()
        self.robes()
        self.hood_and_mask()
        for side,sign in (("Left",-1),("Right",1)):
            self.arm(side,sign)
            self.boots(side,sign)
        self.accessories()
        self.staff()

    def mage_pose(self,casting):
        super().pose(casting)
        staff_angle=Quaternion((0,1,0),math.radians(12 if casting else 6))
        self.arm_to("Right",Vector((.466,.170,1.047) if casting else (.438,.147,1.009)),
                    staff_angle,STAFF_GRIP,Vector((1,-.62,-.24)))
        if casting:
            self.set_rotation("Head",Quaternion((0,0,1),math.radians(11)))
            palm_rotation=Quaternion((0,0,1),math.radians(-76))@Quaternion((0,1,0),math.radians(26))
            self.arm_to("Left",Vector((-.299,.508,1.369)),palm_rotation,
                        SPELL_PALM,Vector((-.30,-.15,-1)))
        else:
            palm_rotation=Quaternion((0,1,0),math.radians(-76))
            self.arm_to("Left",Vector((-.278,.127,.987)),palm_rotation,
                        SPELL_PALM,Vector((-.95,-.55,-.12)))
        return self.skin_point("RightHand",STAFF_GRIP)


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--quick",action="store_true")
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    SOURCE.mkdir(parents=True,exist_ok=True)
    mage=Mage()
    mesh=mage.finish_model()
    if not args.quick:
        bake_atlas(mesh,4096,TEXTURES,"T_NeutralMage","UV0")
        slots=list(mesh.data.materials)
        indices=[p.material_index for p in mesh.data.polygons]
        mesh.data.materials.clear()
        mesh.data.materials.append(material("M_NeutralMage_Atlas",(.05,.032,.039),.2,.72,0,0,""))
        export_collection(MODEL,bake_animation=False)
        mesh.data.materials.clear()
        for mat in slots:
            mesh.data.materials.append(mat)
        for polygon,index in zip(mesh.data.polygons,indices):
            polygon.material_index=index
    print(f"{STEM}: {len(mesh.data.polygons)} triangles, {len(mage.rig.data.bones)} bones",flush=True)
    scene=mage.scene
    camera=studio_camera(scene)
    bpy.data.objects["Soft key"].data.energy=548
    bpy.data.objects["Front fill"].data.energy=171
    bpy.data.objects["Rim"].data.energy=361
    bpy.data.objects["Studio shadow floor"].location.z=0
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value=.21
    scene.cycles.samples=13 if args.quick else 28
    scene.render.resolution_x,scene.render.resolution_y=1080,1350
    scene.render.resolution_percentage=52 if args.quick else 100
    for label,frame,casting in (("Ready",1,False),("Cast",31,True)):
        scene.frame_set(frame)
        mage.mage_pose(casting)
        for bone in mage.rig.pose.bones:
            bone.keyframe_insert(data_path="rotation_quaternion",frame=frame,group=bone.name)
            bone.keyframe_insert(data_path="location",frame=frame,group=bone.name)
        scene.timeline_markers.new(label,frame=frame)
    mage.rig.animation_data.action.name="Neutral Mage - review poses"
    scene.frame_start,scene.frame_end=1,31
    scene.frame_set(1)
    view(camera,(-2.72,6.60,1.94),(.075,.01,1.06),2.52)
    if not args.quick:
        select([mesh])
        bpy.context.preferences.filepaths.save_version=0
        bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(STEM+".blend")))
    for suffix,position,target,scale,frame in (
        ("",(-2.72,6.60,1.94),(.075,.01,1.06),2.52,1),
        ("Cast",(-3.92,4.94,2.01),(.05,.19,1.09),2.65,31),
        ("Detail",(-1.67,5.48,1.87),(.13,0,1.60),1.18,1),
        ("Back",(2.90,-5.92,1.97),(.08,0,1.08),2.58,1),
    ):
        scene.frame_set(frame)
        view(camera,position,target,scale)
        scene.render.filepath=str(SOURCE/("NeutralMage"+suffix+".png"))
        bpy.ops.render.render(write_still=True)


if __name__=="__main__":
    main()
