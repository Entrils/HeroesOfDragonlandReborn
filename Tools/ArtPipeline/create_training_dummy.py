"""Build the straw practice dummy for the Training Grounds capsule target."""

import argparse
import math
from pathlib import Path
from random import Random
import sys

import bpy
from mathutils import Matrix,Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0,str(Path(__file__).resolve().parent))
from character_art import bake_atlas,material,studio_camera
from create_camp_chest import Chest,oak
from creature_mesh import select
from export_fbx import export_collection


ROOT=Path(__file__).resolve().parents[2]
STEM="SM_TrainingDummy"
SOURCE=ROOT/"ArtSource/Environment"/STEM
MODEL=ROOT/"Assets/Art/Environment/Models"/(STEM+".fbx")
TEXTURES=ROOT/"Assets/Art/Textures/Environment/TrainingDummy"


class TrainingDummy(Chest):
    def materials(self):
        self.wood=oak("Dummy_WeatheredOak",(.104,.056,.029),(5.4,5.2,.27))
        self.base=oak("Dummy_BaseHeartwood",(.084,.041,.022),(.32,6,7.7))
        self.canvas=material("Dummy_StainedSackcloth",(.172,.118,.065),0,.87,311,.00137,"cloth")
        self.patch=material("Dummy_CoarseRepairCanvas",(.106,.075,.043),0,.90,323,.00102,"cloth")
        self.red=material("Dummy_FadedIronOxidePaint",(.147,.023,.015),0,.90,339,.00101,"cloth")
        self.dark=material("Dummy_DarkTornCloth",(.033,.019,.011),0,.91,291,.00089,"cloth")
        self.rope=material("Dummy_RoughHempRope",(.149,.100,.048),0,.84,387,.00070,"cloth")
        self.thread=material("Dummy_LightThreadInRope",(.215,.157,.080),0,.84,470,.00039,"cloth")
        self.straw=material("Dummy_DryOatStraw",(.265,.165,.065),0,.79,338,.00054,"cloth")
        self.leather=material("Dummy_PatchedLeather",(.075,.036,.018),0,.78,403,.00066,"leather")
        self.iron=material("Dummy_RustDarkenedIron",(.067,.058,.048),.56,.62,210,.00061,"steel")
        self.brass=material("Dummy_OldBuckle",(.181,.109,.045),.73,.50,234,.00033,"steel")

    def sack(self,name,origin,widths,mat,axis=0,phase=0):
        vertices=[]
        n=48
        for j,(level,rx,ry) in enumerate(widths):
            for i in range(n):
                a=i*math.tau/n
                wr=1+.048*math.sin(5*a+.46*j+phase)+.021*math.sin(11*a-.39*j+phase)
                x=rx*math.sin(a)*wr
                y=ry*math.cos(a)*wr
                h=level+.011*math.sin(a*3+.69*j+phase)
                delta=Vector((axis*h,y,x) if axis else (x,y,h))
                vertices.append(Vector(origin)+delta)
        faces=[(j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i)
               for j in range(len(widths)-1) for i in range(n)]
        faces.extend((tuple(reversed(range(n))),tuple(range(len(vertices)-n,len(vertices)))))
        obj=self.mesh(name,vertices,faces,mat)
        for face in obj.data.polygons:
            face.use_smooth=face.index<len(faces)-2
        return obj

    def rope_binding(self,name,center,radius,length,axis=0,turns=3):
        # One continuous winding plus a thinner twisted strand along its face.
        center=Vector(center)
        main=[]
        thread=[]
        for i in range(turns*32+1):
            u=i/(turns*32)
            t=math.tau*u*turns
            length_at=(u-.5)*length
            r=radius+.0028*math.sin(t*1.7)
            def point(radial,height):
                x=radial*math.sin(t)
                y=radial*math.cos(t)
                return center+Vector((axis*height,y,x) if axis else (x,y,height))
            main.append(point(r,length_at))
            thread.append(point(r+.0075+.0024*math.sin(t*6),length_at+.0037*math.cos(t*6)))
        self.tube(name,main,.0092,self.rope)
        self.tube("Raised twist in the hemp",thread,.0023,self.thread)

    def bracing(self):
        self.box("Central oak upright",(0,-.067,.762),(.130,.138,1.51),self.wood,.019)
        for offset,size in (
            ((0,-.068,.052),(.84,.147,.102)),((0,-.053,.053),(.145,.775,.105)),
        ):
            self.box("Hewn foot of the cross-stand",offset,size,self.base,.017)
        for direction in (Vector((1,0,0)),Vector((-1,0,0)),Vector((0,-1,0)),Vector((0,1,0))):
            start=Vector((0,-.067,.326))
            end=Vector((0,-.067,.095))+direction*.328
            span=end-start
            self.box("Short timber compression brace",(start+end)*.5,(.072,.076,span.length),self.wood,.011)
            self.parts[-1].rotation_euler=span.to_track_quat("Z","Y").to_euler()
            peg=end+Vector((0,-.035,.015))
            self.nail(peg,(0,-1,0),.013,self.iron)
        for sign in (-1,1):
            self.box("Small foot binding",(sign*.274,-.069,.103),(.073,.156,.025),self.iron,.007)
            self.nail((sign*.274,-.036,.117),(0,0,1),.009,self.brass)
        self.box("Square through-tenon at back",(0,-.139,.568),(.124,.040,.077),self.base,.008)
        self.nail((0,-.162,.563),(0,-1,0),.017,self.iron)

    @staticmethod
    def tree(obj):
        return BVHTree.FromPolygons([v.co for v in obj.data.vertices],
                                    [tuple(p.vertices) for p in obj.data.polygons])

    @staticmethod
    def on_cloth(tree,x,z,offset=.001,side=1):
        point,_,_,_=tree.ray_cast((x,side,z),(0,-side,0))
        if point is not None:
            point.y+=side*offset
        return point

    def paint(self,tree):
        rng=Random(6710)
        vertices,faces=[],[]
        for inner,outer in ((.142,.158),(.095,.109),(.052,.065),(.002,.029)):
            for i in range(60):
                if rng.random()<.13 and inner>.010:
                    continue
                points=[]
                for j,r in ((i,inner),(i,outer),(i+1,outer),(i+1,inner)):
                    a=math.tau*j/60
                    r+=.0016*math.sin(a*15)+.001*math.sin(a*27+inner)
                    points.append(self.on_cloth(tree,r*math.sin(a),1.09+r*math.cos(a),.00115))
                index=len(vertices)
                if all(p is not None for p in points):
                    vertices.extend(points)
                    faces.append(tuple(range(index,index+4)))
        self.mesh("Worn iron-oxide aiming rings",vertices,faces,self.red)

    def tear(self,tree,coordinates):
        points=[self.on_cloth(tree,x,z,.0017) for x,z in coordinates]
        self.tube("Split in the cloth",points,.0042,self.dark)
        rng=self.rng
        for i in range(21):
            p=points[i%(len(points)-1)].lerp(points[i%(len(points)-1)+1],rng.uniform(.02,.99))
            end=p+Vector((rng.uniform(-.025,.026),rng.uniform(.012,.031),rng.uniform(-.015,.034)))
            self.tube("Straw showing through the split",[p,p.lerp(end,.49)+Vector((0,.003,0)),end],
                      rng.uniform(.00066,.00155),self.straw)

    def body(self):
        torso=self.sack("Bulging sackcloth torso",(0,.012,0),[
            (.66,.103,.075),(.712,.157,.112),(.810,.178,.143),(.931,.198,.170),
            (1.060,.222,.176),(1.188,.238,.177),(1.300,.251,.157),(1.398,.243,.124),
            (1.474,.198,.110),(1.532,.125,.074),(1.555,.066,.054),
        ],self.canvas,phase=.8)
        tree=self.tree(torso)
        self.paint(tree)
        for path in (
            ((-.162,1.243),(-.137,1.210),(-.126,1.186),(-.102,1.165),(-.092,1.139)),
            ((.183,1.358),(.154,1.323),(.147,1.297),(.128,1.275)),
            ((.103,.947),(.131,.924),(.149,.905),(.158,.879)),
        ):
            self.tear(tree,path)
        for sign in (-1,1):
            points=[]
            for i in range(17):
                t=i/16
                z=.888+.570*t
                x=sign*(.143+.041*math.sin(t*math.pi)-.061*t**4)
                point=self.on_cloth(tree,x,z,.004)
                if point is not None:
                    points.append(point)
            for i in range(len(points)-1):
                p,q=points[i],points[i+1]
                self.tube("Canvas saddle stitch",[(p+Vector((-.006,0,0))),p.lerp(q,.30),
                          p.lerp(q,.64)+Vector((.006,.001,0))],.0018,self.thread)
            for y in (-.083,.082):
                self.nail((sign*.090,y,.658),(0,0,-1),.014,self.iron)
        self.rope_binding("Old waist cinch",(0,.003,.721),.145,.046,turns=3)
        self.tube("Hemp waist knot and short ends",[(.136,.066,.731),(.160,.090,.723),
                  (.163,.100,.697),(.152,.092,.686),(.167,.088,.661)],.010,self.rope)
        for z,r in ((1.432,.099),(1.050,.145)):
            self.tube("Mended back seam",[(-.018,-r,z+.018),(0,-r-.016,z+.001),
                      (.024,-r-.006,z-.027)],.0039,self.patch)
        for sign in (-1,1):
            anchors=((.054,1.458),(.114,1.382),(.123,1.304),(.095,1.226),(.049,1.129))
            verts=[]
            for x,z in anchors:
                for dx in (-.021,.021):
                    p=self.on_cloth(tree,sign*(x+dx),z,.006,side=-1)
                    verts.append(p)
            faces=[(i,i+1,i+3,i+2) for i in range(0,len(verts)-2,2)]
            belt=self.mesh("Leather suspension strap at the rear",verts,faces,self.leather)
            select([belt])
            solid=belt.modifiers.new("Leather strip thickness","SOLIDIFY")
            solid.thickness=.005
            bpy.ops.object.modifier_apply(modifier=solid.name)

    def arms(self):
        self.box("Oak yoke across the shoulders",(0,-.043,1.388),(.866,.108,.095),self.wood,.014)
        for sign in (-1,1):
            start=sign*.155
            self.sack("Short shoulder roll stuffed with straw",(start,.012,1.388),[
                (.008,.095,.090),(.049,.108,.103),(.112,.103,.105),
                (.159,.079,.087),(.209,.063,.067),(.229,.052,.055),
            ],self.patch if sign>0 else self.canvas,sign,.25*sign)
            end=Vector((sign*.360,.01,1.389))
            self.rope_binding("Binding around each straw arm",end,.068,.055,axis=sign,turns=3)
            rng=self.rng
            for _ in range(38):
                a=rng.uniform(0,math.tau)
                r=rng.uniform(.020,.059)
                p=Vector((sign*rng.uniform(.365,.397),end.y+r*math.cos(a),end.z+r*math.sin(a)))
                q=p+Vector((sign*rng.uniform(.008,.038),rng.uniform(-.022,.022),rng.uniform(-.020,.033)))
                self.tube("Straw escaping an arm cuff",[p,p.lerp(q,.52)+Vector((0,.002,.002)),q],
                          rng.uniform(.00072,.00175),self.straw)
            self.nail((sign*.414,-.043,1.425),(sign,0,0),.010,self.iron)

    def head(self):
        self.box("Narrow neck peg behind the wrapped head",(0,-.039,1.593),(.072,.070,.238),self.wood,.012)
        head=self.sack("Hessian head without features",(0,.029,0),[
            (1.525,.049,.046),(1.562,.063,.059),(1.607,.094,.079),(1.663,.120,.103),
            (1.747,.127,.110),(1.824,.103,.100),(1.867,.071,.070),(1.891,.043,.045),
        ],self.canvas,phase=.4)
        self.rope_binding("Padded throat tied with hemp",(0,.032,1.578),.067,.040,turns=3)
        cap=self.sack("Old leather skullcap",(0,.028,0),[
            (1.786,.129,.135),(1.813,.128,.132),(1.867,.115,.117),
            (1.919,.078,.079),(1.953,.050,.057),(1.974,.019,.027),
        ],self.leather,phase=.25)
        hat=self.tree(cap)
        stitch=[]
        for i in range(19):
            x=-.091+i*.010
            z=1.829-.007*math.cos(i*.26)
            p=self.on_cloth(hat,x,z,.0029)
            if p is not None:
                stitch.append(p)
        for p,q in zip(stitch[::2],stitch[1::2]):
            self.tube("Cap stitch",[p,q],.0015,self.thread)
        tree=self.tree(head)
        self.tear(tree,((-.057,1.764),(-.034,1.737),(-.021,1.710)))

    def finish_model(self):
        self.bracing()
        self.body()
        self.arms()
        self.head()
        select(self.parts)
        bpy.ops.object.join()
        mesh=bpy.context.object
        mesh.name=STEM
        self.scene.cursor.location=(0,0,0)
        bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.mesh.normals_make_consistent(inside=False)
        bpy.ops.uv.smart_project(angle_limit=math.radians(65),island_margin=.002,margin_method="FRACTION")
        bpy.ops.object.mode_set(mode="OBJECT")
        uv=mesh.data.uv_layers.active
        for layer in list(mesh.data.uv_layers):
            if layer!=uv:
                mesh.data.uv_layers.remove(layer)
        uv.name="UV0"
        tri=mesh.modifiers.new("Export triangulation","TRIANGULATE")
        tri.keep_custom_normals=True
        bpy.ops.object.modifier_apply(modifier=tri.name)
        print(f"{STEM}: {len(mesh.data.polygons)} triangles",flush=True)
        return mesh

    def export_model(self,mesh):
        bake_atlas(mesh,2048,TEXTURES,"T_TrainingDummy","UV0")
        original=list(mesh.data.materials)
        indices=[p.material_index for p in mesh.data.polygons]
        mesh.data.materials.clear()
        mesh.data.materials.append(material("M_TrainingDummy_Atlas",(.172,.118,.065),0,.81,0,0,""))
        # The model faces the tutorial entrance, at negative Z from the saved dummy.
        turn=Matrix.Rotation(math.pi,4,"Z")
        mesh.data.transform(turn)
        export_collection(MODEL,bake_animation=False)
        mesh.data.transform(turn.inverted())
        mesh.data.materials.clear()
        for mat in original:
            mesh.data.materials.append(mat)
        for polygon,index in zip(mesh.data.polygons,indices):
            polygon.material_index=index


def view(camera,pos,target,scale):
    camera.location=pos
    camera.rotation_euler=(Vector(target)-camera.location).to_track_quat("-Z","Y").to_euler()
    camera.data.ortho_scale=scale


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--quick",action="store_true")
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    SOURCE.mkdir(parents=True,exist_ok=True)
    dummy=TrainingDummy()
    mesh=dummy.finish_model()
    if not args.quick:
        dummy.export_model(mesh)
    scene=dummy.scene
    camera=studio_camera(scene)
    bpy.data.objects["Studio shadow floor"].location.z=0
    bpy.data.objects["Soft key"].data.energy=455
    bpy.data.objects["Front fill"].data.energy=158
    bpy.data.objects["Rim"].data.energy=300
    scene.render.resolution_x,scene.render.resolution_y=1040,1300
    scene.render.resolution_percentage=52 if args.quick else 100
    scene.cycles.samples=12 if args.quick else 29
    frames=(("",(2.24,5.2,2.11),(0,.027,.98),2.28),
            ("Detail",(-1.13,4.86,1.80),(0,.10,1.39),1.29),
            ("Back",(2.24,-5.28,2.17),(0,-.04,.98),2.30))
    view(camera,*frames[0][1:])
    if not args.quick:
        select([mesh])
        bpy.context.preferences.filepaths.save_version=0
        bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(STEM+".blend")))
    for suffix,pos,target,scale in frames:
        view(camera,pos,target,scale)
        scene.render.filepath=str(SOURCE/("TrainingDummy"+suffix+".png"))
        bpy.ops.render.render(write_still=True)


if __name__=="__main__":
    main()
