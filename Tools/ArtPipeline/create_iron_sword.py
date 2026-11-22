"""Create the player's common Iron Sword with the existing grip origin and blade axis."""

import argparse
import math
from pathlib import Path
import sys

import bpy
from mathutils import Quaternion,Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0,str(Path(__file__).resolve().parent))
from character_art import bake_atlas,material,studio_camera
from create_camp_chest import Chest
from creature_mesh import select
from export_fbx import export_collection


ROOT=Path(__file__).resolve().parents[2]
STEM="SM_IronSword"
SOURCE=ROOT/"ArtSource/Weapons"/STEM
MODEL=ROOT/"Assets/Art/Weapons/Models"/(STEM+".fbx")
TEXTURES=ROOT/"Assets/Art/Textures/Weapons/IronSword"
TIP=(0,1.30,0)


class IronSword(Chest):
    stem=STEM
    model_path=MODEL
    textures=TEXTURES
    attachment_points=(("Socket_Grip",(0,0,0)),("Socket_Tip",TIP))

    def materials(self):
        self.steel=material("IronSword_WorkedBlade",(.131,.146,.156),.89,.43,231,.00049,"steel")
        self.edge=material("IronSword_WornCuttingEdges",(.260,.279,.282),.90,.31,290,.00023,"steel")
        self.fuller=material("IronSword_OxideInFuller",(.068,.087,.098),.80,.50,180,.00038,"steel")
        self.iron=material("IronSword_BlackenedFittings",(.056,.062,.064),.82,.50,203,.00048,"steel")
        self.brass=material("IronSword_AgedFerrule",(.186,.117,.058),.67,.54,220,.00035,"steel")
        self.leather=material("IronSword_OiledLeather",(.037,.018,.012),0,.77,371,.00053,"leather")
        self.thread=material("IronSword_WaxedThread",(.120,.076,.044),0,.85,447,.00029,"cloth")

    def revolved(self,name,stations,mat,sides=36,power=1,outline=None):
        verts=[]
        for y,rx,rz in stations:
            for i in range(sides):
                a=i*math.tau/sides
                c,s=math.cos(a),math.sin(a)
                scale=outline(a) if outline else 1
                verts.append((rx*math.copysign(abs(c)**power,c)*scale,y,
                              rz*math.copysign(abs(s)**power,s)*scale))
        faces=[(i*sides+j,i*sides+(j+1)%sides,(i+1)*sides+(j+1)%sides,(i+1)*sides+j)
               for i in range(len(stations)-1) for j in range(sides)]
        faces.extend((tuple(reversed(range(sides))),tuple(range(len(verts)-sides,len(verts)))))
        obj=self.mesh(name,verts,faces,mat)
        for face in obj.data.polygons:
            face.use_smooth=face.index<len(faces)-2
        return obj

    def blade(self):
        # Each longitudinal row defines the blade silhouette and the shallow central fuller.
        stations=((.161,.046,.022,.48,0),(.205,.060,.021,.50,0),
                  (.244,.061,.022,.44,0),(.285,.058,.019,.25,0),
                  (.439,.055,.018,.19,0),(.537,.053,.017,.19,0),
                  (.544,.053,.017,.19,.0031),(.551,.053,.017,.19,0),
                  (.673,.051,.016,.20,0),(.837,.047,.015,.22,0),
                  (.942,.045,.014,.27,0),(.949,.045,.014,.28,-.0026),
                  (.958,.045,.014,.31,0),(1.016,.042,.013,.42,0),
                  (1.123,.031,.011,.59,0),(1.216,.019,.008,.71,0),
                  (1.278,.006,.004,.70,0),(TIP[1],.00045,.0006,.68,0))
        verts=[]
        section=((-1,.035),(-.72,.67),(-.42,1),(-.27,.90),(-.16,.64),(0,.56),
                 (.16,.64),(.27,.90),(.42,1),(.72,.67),(1,.035),(.72,-.67),
                 (.42,-1),(.27,-.90),(.16,-.64),(0,-.56),(-.16,-.64),
                 (-.27,-.90),(-.42,-1),(-.72,-.67))
        n=len(section)
        for y,w,h,depth,chip in stations:
            for x,z in section:
                nick=-math.copysign(abs(chip),x)*abs(x)**7 if x*chip>0 else 0
                fuller=math.copysign(1-(1-abs(z))*min(1,depth/.19),z) if .16<=abs(x)<=.27 else z
                if abs(x)<.16:
                    fuller=math.copysign(depth,z)
                verts.append((x*w+nick,y,h*fuller))
        faces=[(i*n+j,i*n+(j+1)%n,(i+1)*n+(j+1)%n,(i+1)*n+j)
               for i in range(len(stations)-1) for j in range(n)]
        faces.extend((tuple(reversed(range(n))),tuple(range(len(verts)-n,len(verts)))))
        obj=self.mesh("Double-edged blade with central fuller",verts,faces,self.steel)
        obj.data.materials.append(self.edge)
        obj.data.materials.append(self.fuller)
        for face in obj.data.polygons:
            row,col=divmod(face.index,n)
            if row<len(stations)-1:
                face.use_smooth=True
                if col in (0,9,10,19):
                    face.material_index=1
                elif 1<row<13 and col in (4,5,14,15):
                    face.material_index=2
        for edge in obj.data.edges:
            a,b=edge.vertices
            if a%n==b%n:
                edge.use_edge_sharp=True
        surface=BVHTree.FromPolygons(verts,faces)
        for side in (-1,1):
            for i,(x,y) in enumerate(((.033,.326),(-.035,.448),(.024,.784),(-.030,.912))):
                sign=1 if x>=0 else -1
                line=[]
                for u,v in ((x,y),(x-sign*.005,y+.011),(x-sign*.010,y+.021+i*.001)):
                    p,_,_,_=surface.ray_cast((u,v,side),(0,0,-side))
                    p.z+=side*.00015
                    line.append(p)
                self.tube("Fine blade scuff",line,.00049,self.edge)

    def crossguard(self):
        outline=[(-.222,.130),(-.219,.155),(-.188,.177),(-.121,.185),(-.073,.209),
                 (-.054,.219),(.051,.219),(.076,.206),(.123,.185),(.188,.176),
                 (.218,.154),(.221,.130),(.204,.118),(.165,.124),(.106,.146),
                 (.053,.150),(-.051,.150),(-.109,.146),(-.165,.125),(-.203,.118)]
        verts=[(x,y,side*(.022+.006*(1-abs(x)/.23))) for side in (-1,1) for x,y in outline]
        n=len(outline)
        faces=[tuple(reversed(range(n))),tuple(range(n,n*2))]
        faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
        guard=self.mesh("Swept forged crossguard",verts,faces,self.iron)
        self.bevel(guard,.010)
        for side in (-1,1):
            for sign in (-1,1):
                path=[(sign*x,y,side*z) for x,y,z in
                      ((.053,.191,.028),(.078,.185,.027),(.113,.169,.026),
                       (.156,.154,.025),(.196,.143,.023))]
                self.tube("Burnished groove in the guard",path,.0024,self.edge)
                self.nail((sign*.181,.147,side*.025),(0,0,side),.007,self.brass)

    def hilt(self):
        self.revolved("Oval tapered leather grip",
                      [(-.164,.033,.025),(-.141,.037,.029),(-.104,.037,.031),
                       (-.050,.036,.031),(.037,.036,.031),(.107,.040,.031),
                       (.152,.039,.028),(.162,.035,.027)],self.leather,40,.85)
        wrap=[]
        for i in range(281):
            t=i/280
            theta=math.tau*9.0*t
            y=-.14+.280*t+.0018*math.sin(theta*2)
            xradius=.038+.003*max(0,(t-.58)*2)
            wrap.append((xradius*math.cos(theta),y,.0317*math.sin(theta)))
        self.tube("Waxed cord in the leather seam",wrap,.0019,self.thread)
        for y in (-.143,.131):
            self.revolved("Dull brass oval grip ferrule",
                          [(y-.015,.035,.029),(y-.012,.039,.033),(y+.009,.039,.033),
                           (y+.015,.035,.028)],self.brass,34,.85)
        self.revolved("Faceted iron pommel",
                      [(-.257,.025,.023),(-.251,.045,.041),(-.227,.049,.046),
                       (-.199,.047,.045),(-.172,.036,.030),(-.157,.031,.027)],
                      self.iron,48,.88,lambda a:1+.042*math.cos(8*a))
        self.revolved("Thin bronze ring on pommel",
                      [(-.242,.045,.041),(-.235,.050,.046),(-.228,.050,.047)],
                      self.brass,48,.88,lambda a:1+.042*math.cos(8*a))
        self.nail((0,-.252,0),(0,-1,0),.017,self.edge)

    def build_parts(self):
        self.blade()
        self.crossguard()
        self.hilt()

    def finish_sword(self):
        self.build_parts()
        select(self.parts)
        bpy.ops.object.join()
        mesh=bpy.context.object
        mesh.name=self.stem
        self.scene.cursor.location=(0,0,0)
        bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.mesh.normals_make_consistent(inside=False)
        bpy.ops.uv.smart_project(angle_limit=math.radians(62),island_margin=.002,margin_method="FRACTION")
        bpy.ops.object.mode_set(mode="OBJECT")
        uv=mesh.data.uv_layers.active
        for layer in list(mesh.data.uv_layers):
            if layer!=uv:
                mesh.data.uv_layers.remove(layer)
        uv.name="UV0"
        mod=mesh.modifiers.new("Export triangulation","TRIANGULATE")
        mod.keep_custom_normals=True
        bpy.ops.object.modifier_apply(modifier=mod.name)
        for name,pos in self.attachment_points:
            socket=bpy.data.objects.new(name,None)
            self.collection.objects.link(socket)
            socket.parent=mesh
            socket.location=pos
            socket.empty_display_type="ARROWS"
            socket.empty_display_size=.069
        print(f"{self.stem}: {len(mesh.data.polygons)} triangles",flush=True)
        return mesh

    def save_and_export_sword(self,mesh):
        name=self.stem.removeprefix("SM_")
        bake_atlas(mesh,2048,self.textures,"T_"+name,"UV0")
        originals=list(mesh.data.materials)
        faces=[p.material_index for p in mesh.data.polygons]
        mesh.data.materials.clear()
        mesh.data.materials.append(material("M_"+name+"_Atlas",(.131,.146,.156),.84,.42,0,0,""))
        export_collection(self.model_path,bake_animation=False)
        mesh.data.materials.clear()
        for mat in originals:
            mesh.data.materials.append(mat)
        for polygon,index in zip(mesh.data.polygons,faces):
            polygon.material_index=index


def view(camera,location,target,scale,roll):
    camera.location=location
    orient=(Vector(target)-camera.location).to_track_quat("-Z","Y")
    camera.rotation_mode="QUATERNION"
    camera.rotation_quaternion=orient@Quaternion((0,0,1),math.radians(roll))
    camera.data.ortho_scale=scale


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--quick",action="store_true")
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    SOURCE.mkdir(parents=True,exist_ok=True)
    sword=IronSword()
    mesh=sword.finish_sword()
    if not args.quick:
        sword.save_and_export_sword(mesh)
    scene=sword.scene
    camera=studio_camera(scene)
    floor=bpy.data.objects["Studio shadow floor"]
    floor.location.z=-.104
    floor.data.materials.clear()
    floor.data.materials.append(material("Matte charcoal backdrop",(.024,.031,.039),0,1,0,0,""))
    for name,position,power,size in (
        ("Soft key",(1.64,1.02,2.93),302,2.35),
        ("Front fill",(-1.18,-.49,1.12),91,1.41),
        ("Rim",(-1.76,1.86,2.00),176,1.41),
        ("Back fill",(2.18,-1.27,.55),90,1.09),
    ):
        obj=bpy.data.objects[name]
        obj.location=position
        obj.rotation_euler=(Vector((0,.46,0))-obj.location).to_track_quat("-Z","Y").to_euler()
        obj.data.energy=power
        obj.data.size=size
    scene.render.resolution_x,scene.render.resolution_y=1310,1000
    scene.render.resolution_percentage=53 if args.quick else 100
    scene.cycles.samples=12 if args.quick else 32
    full=((2.62,-1.02,4.32),(0,.516,0),1.83,-18)
    view(camera,*full)
    if not args.quick:
        select([mesh])
        bpy.context.preferences.filepaths.save_version=0
        bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(STEM+".blend")))
    for suffix,framing in (("",full),("Hilt",((1.14,-.38,2.31),(0,.082,0),.86,-16))):
        view(camera,*framing)
        scene.render.filepath=str(SOURCE/("IronSword"+suffix+".png"))
        bpy.ops.render.render(write_still=True)


if __name__=="__main__":
    main()
