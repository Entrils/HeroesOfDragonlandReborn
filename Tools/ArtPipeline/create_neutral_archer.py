"""Author the camp archer on the neutral knight's rig and material palette."""

import argparse
import math
from pathlib import Path
import sys

import bpy
from mathutils import Matrix,Quaternion,Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
from character_art import bake_atlas,material,studio_camera
from create_camp_chest import oak
from create_neutral_knight import Knight
from creature_mesh import select
from export_fbx import export_collection


ROOT=Path(__file__).resolve().parents[2]
STEM="SK_NeutralArcher"
SOURCE=ROOT/"ArtSource/Characters"/STEM
TEXTURES=ROOT/"Assets/Art/Textures/Characters/NeutralArcher"
MODEL=ROOT/"Assets/Art/Characters/Models"/(STEM+".fbx")
BOW_GRIP=Vector((-.848,.022,1.42))
RIGHT_GRIP=Vector((.843,.018,1.42))
NOCK=Vector((BOW_GRIP.x,-.142,BOW_GRIP.z))
DRAW_DELTA=Vector((0,-.380,.063))


class Archer(Knight):
    rig_stem=STEM

    def __init__(self):
        super().__init__()
        self.hood=material("Archer_WornOxbloodHood",(.041,.010,.014),0,.84,350,.00071,"cloth")
        self.glove=material("Archer_DarkOiledLeather",(.035,.022,.018),0,.72,460,.00036,"leather")
        self.bowwood=oak("Archer_YewHeartwood",(.128,.057,.025),(4.1,5.1,.41))
        self.sap=oak("Archer_YewSapwood",(.274,.157,.076),(4.4,5.7,.40))
        self.feather=material("Archer_AshGreyFeather",(.167,.151,.119),0,.85,330,.00048,"cloth")
        self.string_mat=material("Archer_WaxedBowstring",(.246,.192,.117),0,.79,180,.0002,"cloth")

    def hood_and_mask(self):
        self.profile("Leather hood over quilted cap","Head",self.hood,(0,0,0),
                     [(1.503,.066,.065,-.012),(1.573,.092,.099,-.021),
                      (1.671,.109,.118,-.012),(1.731,.111,.115,-.015),
                      (1.779,.095,.095,-.022),(1.823,.057,.064,-.028),
                      (1.859,.025,.035,-.039),(1.873,.009,.019,-.047)],steps=62,
                     arc=(.88,5.40))
        self.profile("Folded hood crown","Head",self.hood,(0,0,0),
                     [(1.735,.103,.106,-.014),(1.757,.103,.109,-.021),
                      (1.790,.085,.096,-.027),(1.827,.053,.061,-.027),
                      (1.860,.026,.034,-.038),(1.873,.009,.019,-.047)],
                     steps=42,arc=(-1.02,1.02),keel=.008)
        self.profile("Padded face covering","Head",self.shadow,(0,0,0),
                     [(1.515,.074,.062,.003),(1.554,.095,.087,.004),
                      (1.623,.098,.093,.007),(1.691,.103,.090,.009),(1.745,.093,.070,.005)])
        mask=self.profile("Pointed face guard","Head",self.steel,(0,0,0),
                          [(1.527,.043,.075,.004),(1.563,.061,.095,.011),
                           (1.600,.087,.107,.008),(1.632,.094,.102,.010),
                           (1.648,.096,.098,.010)],arc=(-1.35,1.35),
                          keel=.039,steps=54,slope=.013)
        self.profile("Sharp forged brow","Head",self.steel,(0,0,0),
                     [(1.655,.101,.111,.012),(1.677,.107,.120,.009),
                      (1.707,.112,.106,.008),(1.741,.100,.082,.007)],
                     arc=(-1.53,1.53),keel=.020,slope=.024,steps=54)
        guard=self.bvh(mask)
        for sign in (-1,1):
            hem=[(sign*x,y,z) for x,y,z in ((.067,.046,1.536),(.096,.058,1.581),
                  (.108,.051,1.638),(.111,.061,1.706),(.095,.063,1.762),(.060,.055,1.812))]
            self.tube("Rolled hood edge",[hem],.0048,self.glove,"Head",sides=7,taper=.35)
            vents=[]
            for x,z in ((.031,1.593),(.048,1.605),(.063,1.615)):
                vents.append([self.front_point(guard,sign*x,z,.0009),
                              self.front_point(guard,sign*x,z+.014,.0009)])
            self.tube("Dark slots in the face guard",vents,.0028,self.shadow,"Head",sides=7,taper=.24)
            self.rivet("Head",(sign*.091,.065,1.658),.0068,self.edge)
        self.tube("Soot worn mask keel",[[(0,.115,1.540),(0,.147,1.592),(0,.145,1.637)]],
                  .0026,self.edge,"Head",sides=7,taper=.66)
        self.profile("Folded neck wrap","Neck",self.hood,(0,0,0),
                     [(1.458,.087,.081,0),(1.477,.093,.085,0),(1.514,.080,.068,-.002),
                      (1.546,.070,.063,0)],steps=38)
        self.chainmail("Mail under the hood","Neck",(0,0,1.474),.084,5,27,ring=.007)

    def torso(self):
        self.profile("Padded gambeson hips","Hips",self.linen,(0,0,0),
                     [(.840,.142,.095,0),(.919,.154,.105,0),(1.029,.147,.101,0),
                      (1.105,.145,.110,0)],steps=42)
        gambeson=self.profile("Padded archer's gambeson","Chest",self.hood,(0,0,0),
                     [(1.048,.143,.105,0),(1.185,.162,.115,0),(1.298,.191,.112,0),
                      (1.415,.203,.097,0),(1.484,.132,.075,-.005),(1.511,.062,.057,0)],steps=49)
        cuirass=self.profile("Light blackened breastplate","Chest",self.steel,(0,0,0),
                     [(1.105,.140,.109,.009),(1.167,.155,.125,.012),(1.252,.181,.130,.011),
                      (1.324,.187,.121,.008),(1.390,.157,.112,.002),(1.439,.119,.091,0)],
                     arc=(-1.45,1.45),keel=.020,steps=60,slope=.009)
        self.harness_surface=(self.bvh(gambeson),self.bvh(cuirass))
        self.profile("Flexible back lining","Chest",self.glove,(0,0,0),
                     [(1.093,.144,.113,0),(1.231,.171,.122,0),(1.349,.181,.124,0),
                      (1.449,.137,.101,0)],arc=(1.30,4.98))
        tree=self.bvh(cuirass)
        for sign in (-1,1):
            for i in range(2):
                arcs=[self.front_point(tree,sign*x,z-.033*i,.0014) for x,z in
                      ((.015,1.265),(.058,1.283),(.097,1.302),(.132,1.330),(.151,1.356))]
                self.tube("Forged diagonal breastplate flute",[arcs],.0024,self.edge,"Chest",sides=7,taper=.61)
            self.garment(sign)
        # The same open diamond used on the knight's shield.
        emblem=[self.front_point(tree,x,z,.0025) for x,z in
                ((0,1.368),(.026,1.339),(0,1.308),(-.026,1.339),(0,1.368))]
        self.tube("Neutral diamond on the breastplate",[emblem],.0037,self.brass,"Chest",sides=7,taper=0)
        self.profile("Leather waist belt","Hips",self.leather,(0,0,0),
                     [(.994,.156,.123,.003),(1.041,.156,.124,.002)])
        self.plate("Bronze belt clasp","Hips",self.brass,[(-.027,.132,1.00),(.023,.132,1.00),
                  (.026,.130,1.037),(-.025,.130,1.037)],.009,.007)
        self.plate("Belt clasp inset","Hips",self.glove,[(-.016,.139,1.009),(.015,.139,1.009),
                  (.015,.138,1.028),(-.015,.138,1.027)],.004,.003)

    def archer_arm(self,side,sign):
        upper,lower,hand=[side+s for s in ("UpperArm","LowerArm","Hand")]
        axis={"axis":sign}
        self.profile("Fitted wrapped sleeve",upper,self.hood,(sign*.24,0,1.42),
                     [(0,.074,.075,0),(.061,.085,.081,0),(.179,.067,.071,0),
                      (.266,.050,.053,0)],**axis,steps=38)
        self.profile("Flexible elbow lining",lower,self.glove,(sign*.47,0,1.42),
                     [(0,.050,.052,0),(.053,.063,.061,0),(.133,.058,.057,0),(.280,.042,.039,0)],**axis)
        for i in range(3 if sign<0 else 2):
            x=.229+.040*i
            w=.111-.011*i
            self.profile("Low overlapping shoulder lame",upper,self.steel,(sign*x,0,1.42),
                         [(0,w*.88,w*.92,0),(.015,w,w,0),(.050,w*.87,w*.85,0)],
                         **axis,arc=(-1.42,1.85),steps=41,keel=.008)
            self.rivet(upper,(sign*(x+.026),w*.92,1.448),.0054)
        self.chainmail("Mail visible in the upper arm gap",upper,(sign*.346,0,1.42),
                       .069,7,22,axis=sign,arc=(-1.7,1.85),ring=.0057)
        self.profile("Layered leather bracer",lower,self.leather,(sign*.55,0,1.42),
                     [(0,.063,.066,0),(.033,.065,.066,0),(.135,.048,.048,0),
                      (.213,.044,.045,0)],**axis,steps=41)
        if sign<0:
            self.profile("Steel bow-arm guard",lower,self.steel,(sign*.582,0,1.42),
                         [(0,.064,.070,0),(.054,.060,.067,0),(.123,.054,.058,0),
                          (.168,.048,.051,0)],**axis,arc=(-1.32,1.32),keel=.008)
        for x,r in ((.585,.064),(.714,.050)):
            self.profile("Bracer fastening strap",lower,self.glove,(sign*x,0,1.42),
                         [(0,r,r,0),(.020,r*.98,r*.98,0)],**axis,steps=20)
            self.rivet(lower,(sign*(x+.011),r+.001,1.423),.0054,self.brass)
        self.profile("Curled glove",hand,self.leather,(sign*.773,.005,1.42),
                     [(0,.043,.040,0),(.028,.046,.045,0),(.059,.044,.039,.005),
                      (.091,.036,.032,.011),(.113,.027,.030,.015)],**axis)
        for i in range(4):
            z=1.389+.020*i
            points=[(sign*.836,.035,z),(sign*.864,.037,z),(sign*.879,.020,z),(sign*.872,.005,z)]
            self.tube("Wrapped glove finger",[points],.0090,self.glove,hand,sides=8,taper=.21)
        self.oval("Glove thumb",hand,self.leather,(sign*.818,.035,1.398),(.029,.019,.017))

    def quiver(self):
        bone="Chest"
        start=len(self.parts)
        x,y=.170,-.161
        self.profile("Quiver sewn oak and leather shell",bone,self.leather,(x,y,0),
                     [(.875,.058,.049,0),(.922,.085,.064,0),(1.044,.088,.065,0),
                      (1.332,.087,.067,0),(1.490,.101,.072,0)],steps=45)
        for z,w in ((.910,.079),(1.080,.092),(1.472,.102)):
            self.profile("Rolled quiver band",bone,self.iron,(x,y,z),
                         [(0,w,.061,0),(.022,w+.002,.071,0),(.043,w,.066,0)],steps=34)
            self.rivet(bone,(x,y-.068,z+.014),.0063)
        self.oval("Dark recess under arrows",bone,self.shadow,(x,y,1.504),(.099,.064,.009),29,9)
        for i,(dx,dy) in enumerate(((.040,.035),(-.044,.025),(-.025,-.022),
                                     (.021,-.030),(.053,-.024),(.004,.006),(-.057,0))):
            dz=.024*(i%4)
            self.tube("Ash arrow shaft",[[(x+dx,y+dy,1.417),(x+dx*1.24,y+dy*1.24,1.752+dz)]],
                      .0035,self.sap,bone,sides=7,taper=0)
            for index in range(3):
                theta=index*math.tau/3+i*.37
                base=Vector((x+dx*1.18,y+dy*1.18,1.648+dz))
                wing=Vector((math.cos(theta),math.sin(theta),0))
                corners=[base,base+wing*.017+Vector((0,0,.024)),
                         base+wing*.015+Vector((0,0,.072)),base+Vector((0,0,.085))]
                self.finish(self.surface("Split feather fletching",corners,[(0,1,2,3)],
                            smooth=False),self.feather if i%3 else self.cloth,bone)
        pivot=Vector((x,y,.975))
        tilt=Matrix.Translation(pivot)@Matrix.Rotation(math.radians(23),4,"Y")@Matrix.Translation(-pivot)
        for obj in self.parts[start:]:
            obj.data.transform(obj.matrix_world.inverted()@tilt@obj.matrix_world)
        anchors=list(map(Vector,((-.139,1.452),(-.101,1.39),(.004,1.26),(.088,1.162),(.126,1.083))))
        seam=[a.lerp(b,i/7) for a,b in zip(anchors,anchors[1:]) for i in range(7)]
        seam.append(anchors[-1])
        for rear in (False,True):
            for offset,mat,width in ((0,self.leather,.036),(.014,self.glove,.0035)):
                if rear:
                    line=[(-.145,-.081,1.455),(-.090,-.135,1.398),(.073,-.151,1.308),(.234,-.168,1.248)]
                    vertices=[(x+offset+dx,y,z) for x,y,z in line for dx in (-width/2,width/2)]
                else:
                    vertices=[]
                    for x,z in seam:
                        for dx in (-width/2,width/2):
                            u=x+offset+dx
                            hits=[tree.ray_cast((u,1,z),(0,-1,0))[0] for tree in self.harness_surface]
                            depth=max((p.y for p in hits if p is not None),default=.092)
                            vertices.append((u,depth+.005+(offset>0)*.003,z))
                faces=[(i,i+1,i+3,i+2) for i in range(0,len(vertices)-2,2)]
                obj=self.surface("Quiver shoulder strap",vertices,faces)
                select([obj])
                mod=obj.modifiers.new("Leather thickness","SOLIDIFY")
                mod.thickness=.005
                bpy.ops.object.modifier_apply(modifier=mod.name)
                self.finish(obj,mat,bone)
        self.rivet(bone,(-.115,.106,1.394),.008)

    def bow(self):
        bone="LeftHand"
        x,y,z=BOW_GRIP
        stations=((-.668,-.170,.006,.007),(-.630,-.127,.012,.011),(-.567,-.085,.016,.015),
                  (-.467,-.016,.021,.018),(-.335,.051,.024,.022),(-.182,.054,.028,.022),
                  (-.086,.020,.026,.021),(0,0,.022,.023),(.084,.023,.024,.022),
                  (.178,.052,.026,.022),(.335,.053,.024,.019),(.472,-.016,.020,.016),
                  (.563,-.088,.015,.013),(.628,-.142,.009,.009),(.667,-.165,.006,.007))
        rows=tuple(Vector(a).lerp(Vector(b),i/6) for a,b in zip(stations,stations[1:]) for i in range(6))
        rows+= (Vector(stations[-1]),)
        width=14
        verts=[]
        for h,d,w,t in rows:
            for j in range(width):
                a=math.tau*j/width
                verts.append((x+w*math.sin(a),y+d+t*math.cos(a),z+h))
        faces=[(i-i%width+j,i-i%width+(j+1)%width,i-i%width+width+(j+1)%width,i-i%width+width+j)
               for i in range(0,(len(rows)-1)*width,width) for j in range(width)]
        faces.extend((tuple(reversed(range(width))),tuple(range(len(verts)-width,len(verts)))))
        bow=self.finish(self.surface("Carved yew field bow",verts,faces),self.bowwood,bone)
        bow.data.materials.append(self.sap)
        for polygon in bow.data.polygons:
            if polygon.index%width in (0,1,12,13):
                polygon.material_index=1
        self.profile("Padded leather bow grip",bone,self.glove,(x,y,z),
                     [(-.095,.024,.024,.009),(-.075,.027,.029,.005),(0,.029,.025,0),
                      (.076,.026,.026,.015),(.098,.024,.023,.021)],steps=29)
        for i in range(12):
            h=-.073+i*.013
            spiral=[(x+.030*math.sin(a),y+.028*math.cos(a)+abs(h)*.135,z+h+.012*j/16)
                    for j in range(17) for a in (math.tau*j/16,)]
            self.tube("Waxed wrapping over bow grip",[spiral],.0015,self.thread,bone,sides=5,taper=0)
        tips=[Vector((x,y+d,z+h)) for h,d,_,_ in (stations[0],stations[-1])]
        for tip in tips:
            sign=math.copysign(1,tip.z-z)
            self.oval("Horn reinforced bow tip",bone,self.iron,tip,(.007,.009,.026),16,9)
            self.tube("Bound bowstring slip knot",[[tip,tip+Vector((.007,0,-sign*.013)),
                      tip+Vector((-.005,-.004,-sign*.023))]],.0019,self.string_mat,bone,sides=7,taper=.2)
        self.tube("Waxed string",[[tips[0],NOCK,tips[1]]],.0016,self.string_mat,bone,sides=8,taper=0)

    def geometry(self):
        self.torso()
        self.hood_and_mask()
        for side,sign in (("Left",-1),("Right",1)):
            self.archer_arm(side,sign)
            self.legs(side,sign)
        self.quiver()
        self.bow()

    def finish_model(self):
        self.geometry()
        mesh=self.join_geometry()
        candidates=[v.index for v in mesh.data.vertices if (v.co-NOCK).length<.0021]
        assert len(candidates)==8,candidates
        mesh.shape_key_add(name="Basis")
        shape=mesh.shape_key_add(name="BowDraw")
        for vertex in mesh.data.vertices:
            x,_,z=vertex.co
            height=z-BOW_GRIP.z
            if abs(x-BOW_GRIP.x)<.04 and abs(height)>.100:
                flex=min(1,abs(height)/.668)**2.35
                shape.data[vertex.index].co+=Vector((0,-.057*flex,-math.copysign(.031*flex,height)))
        for index in candidates:
            shape.data[index].co+=DRAW_DELTA
        return mesh

    def archer_pose(self,mesh,draw):
        super().pose(False)
        shape=mesh.data.shape_keys.key_blocks["BowDraw"]
        shape.value=float(draw)
        if draw:
            yaw=Quaternion((0,0,1),math.radians(-53))
            self.set_rotation("Chest",yaw)
            self.set_rotation("Head",Quaternion((0,0,1),math.radians(23)))
            rotation=Quaternion((0,0,1),math.radians(26))
            grip=Vector((-.249,.596,1.514))
            nock=grip+rotation@(NOCK+DRAW_DELTA-BOW_GRIP)
            self.arm_to("Left",grip,rotation,BOW_GRIP,Vector((-.35,0,-1)))
            self.arm_to("Right",nock,Quaternion((0,0,1),math.radians(156)),
                        RIGHT_GRIP,Vector((.66,-.91,.09)))
            assert (self.skin_point("LeftHand",NOCK+DRAW_DELTA)-self.skin_point("RightHand",RIGHT_GRIP)).length<.004
        else:
            rotation=Quaternion((0,1,0),math.radians(-17))@Quaternion((1,0,0),math.radians(20))
            self.arm_to("Left",Vector((-.468,.205,.961)),rotation,BOW_GRIP,Vector((-1,-.3,-.32)))


def view(camera,location,target,scale):
    camera.location=location
    camera.rotation_euler=(Vector(target)-camera.location).to_track_quat("-Z","Y").to_euler()
    camera.data.ortho_scale=scale


def preview_arrow(archer):
    nock=archer.skin_point("LeftHand",NOCK+DRAW_DELTA)
    grip=archer.skin_point("LeftHand",BOW_GRIP)
    direction=(grip+Vector((0,0,.047))-nock).normalized()
    tip=nock+direction*.742
    parts_start=len(archer.parts)
    archer.tube("Preview arrow shaft",[[nock,tip]],.0038,archer.sap,"LeftHand",sides=8,taper=0)
    side=Vector((0,0,1)).cross(direction).normalized()
    archer.plate("Preview arrowhead","LeftHand",archer.edge,
                 [tip+direction*.023,tip+side*.012,tip-direction*.031,tip-side*.012],.006,.002)
    for i in range(3):
        fan=Quaternion(direction,i*math.tau/3)@Vector((0,0,1))
        a=nock+direction*.044
        feathers=[a,a+direction*.018+fan*.014,a+direction*.091+fan*.011,a+direction*.105]
        archer.finish(archer.surface("Preview arrow feather",feathers,[(0,1,2,3)],smooth=False),
                      archer.feather,"LeftHand")
    return archer.parts[parts_start:]


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--quick",action="store_true")
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    SOURCE.mkdir(parents=True,exist_ok=True)
    archer=Archer()
    mesh=archer.finish_model()
    if not args.quick:
        bake_atlas(mesh,4096,TEXTURES,"T_NeutralArcher","UV0")
        slots=list(mesh.data.materials)
        indices=[p.material_index for p in mesh.data.polygons]
        mesh.data.materials.clear()
        mesh.data.materials.append(material("M_NeutralArcher_Atlas",(.062,.070,.074),.4,.58,0,0,""))
        export_collection(MODEL,bake_animation=False)
        mesh.data.materials.clear()
        for mat in slots:
            mesh.data.materials.append(mat)
        for polygon,index in zip(mesh.data.polygons,indices):
            polygon.material_index=index
    print(f"{STEM}: {len(mesh.data.polygons)} triangles and {len(archer.rig.data.bones)} bones",flush=True)
    scene=archer.scene
    camera=studio_camera(scene)
    bpy.data.objects["Soft key"].data.energy=535
    bpy.data.objects["Front fill"].data.energy=171
    bpy.data.objects["Rim"].data.energy=345
    bpy.data.objects["Studio shadow floor"].location.z=0
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value=.24
    scene.cycles.samples=13 if args.quick else 28
    scene.render.resolution_x=1024
    scene.render.resolution_y=1280
    scene.render.resolution_percentage=52 if args.quick else 100
    frames=(("Ready",1,False),("Full draw",21,True))
    for label,frame,draw in frames:
        scene.frame_set(frame)
        archer.archer_pose(mesh,draw)
        for bone in archer.rig.pose.bones:
            bone.keyframe_insert(data_path="rotation_quaternion",frame=frame,group=bone.name)
            bone.keyframe_insert(data_path="location",frame=frame,group=bone.name)
        mesh.data.shape_keys.key_blocks["BowDraw"].keyframe_insert(data_path="value",frame=frame)
        scene.timeline_markers.new(label,frame=frame)
    archer.rig.animation_data.action.name="Neutral Archer - review poses"
    scene.frame_start,scene.frame_end=1,21
    scene.frame_set(1)
    view(camera,(2.35,6.31,1.88),(0,.04,.95),2.28)
    if not args.quick:
        select([mesh])
        bpy.context.preferences.filepaths.save_version=0
        bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(STEM+".blend")))
    for suffix,position,target,scale,frame in (
        ("",(2.35,6.31,1.88),(0,.04,.95),2.28,1),
        ("Draw",(5.27,4.75,2.10),(0,.24,1.02),2.52,21),
        ("Detail",(1.60,5.50,1.79),(0,0,1.52),.91,1),
        ("Back",(2.55,-5.8,1.90),(0,0,.97),2.29,1),
    ):
        scene.frame_set(frame)
        view(camera,position,target,scale)
        arrow=preview_arrow(archer) if frame==21 else []
        scene.render.filepath=str(SOURCE/("NeutralArcher"+suffix+".png"))
        bpy.ops.render.render(write_still=True)
        for obj in arrow:
            bpy.data.objects.remove(obj,do_unlink=True)


if __name__=="__main__":
    main()
