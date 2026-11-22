"""Build the neutral archer's arrow and the neutral mage's amethyst bolt."""

import argparse
import math
from pathlib import Path
from random import Random
import sys

import bpy
from mathutils import Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
from character_art import bake_atlas,material,studio_camera
from create_camp_chest import Chest,oak
from creature_mesh import select
from export_fbx import export_collection


ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/"ArtSource/Vfx/NeutralProjectiles"
MODELS=ROOT/"Assets/Art/Vfx/Models"
TEXTURES=ROOT/"Assets/Art/Textures/Vfx/NeutralProjectiles"
ARROW="NeutralArrow"
BOLT="AmethystBolt"
FORWARD=Vector((0,1,0))


def emissive(name,base,light,power,roughness):
    mat=material(name,base,.12,roughness,0,0,"")
    shader=mat.node_tree.nodes["Principled BSDF"]
    shader.inputs["Emission Color"].default_value=(*light,1)
    shader.inputs["Emission Strength"].default_value=power
    return mat


class Projectile(Chest):
    def materials(self):
        self.ash=oak("Arrow_Ash",(.186,.095,.044),(7.5,.35,7.5))
        self.iron=material("Arrow_OiledIron",(.082,.091,.098),.86,.44,210,.00023,"steel")
        self.edge=material("Arrow_BurnishedEdge",(.237,.252,.259),.83,.32,220,.00012,"steel")
        self.cord=material("Arrow_WaxedBinding",(.109,.065,.035),0,.73,500,.00021,"leather")
        self.feather=material("Arrow_OxbloodFeather",(.104,.018,.023),0,.79,360,.00026,"cloth")
        self.pale=material("Arrow_AshGreyFeather",(.205,.171,.127),0,.77,350,.00028,"cloth")
        self.amethyst=emissive("Bolt_FracturedViolet",(.123,.033,.231),(.34,.057,.61),.75,.31)
        self.light=emissive("Bolt_BrightAmethyst",(.27,.11,.51),(.62,.21,.91),1.1,.29)
        self.dark=emissive("Bolt_DeepPurple",(.052,.009,.14),(.19,.021,.40),.42,.44)
        self.core=emissive("Bolt_Core",(.66,.38,.77),(.85,.57,1),1.75,.27)

    def loft(self,name,path,mat,sides=10,smooth=True):
        """Build a tapered surface along a path of center, width and depth samples."""
        vertices=[]
        for i,(p,width,depth) in enumerate(path):
            before=path[max(0,i-1)][0]
            after=path[min(len(path)-1,i+1)][0]
            frame=(after-before).to_track_quat("Z","X")
            for j in range(sides):
                a=j*math.tau/sides
                vertices.append(p+frame@Vector((math.cos(a)*width,math.sin(a)*depth,0)))
        faces=[(i*sides+j,i*sides+(j+1)%sides,(i+1)*sides+(j+1)%sides,(i+1)*sides+j)
               for i in range(len(path)-1) for j in range(sides)]
        faces.extend((tuple(reversed(range(sides))),tuple(range(len(vertices)-sides,len(vertices)))))
        obj=self.mesh(name,vertices,faces,mat)
        for polygon in obj.data.polygons:
            polygon.use_smooth=smooth
        return obj

    def arrow(self):
        shaft=[(Vector((0,y,0)),r,r) for y,r in (
            (-.752,.005),(-.736,.007),(-.715,.0062),(-.593,.0052),
            (-.322,.0058),(-.134,.0054),(-.075,.0044),
        )]
        self.loft("Tapered ash shaft",shaft,self.ash,12)
        for a,b in ((-.719,-.698),(-.590,-.573),(-.126,-.100)):
            line=[(.0062*math.cos(t),a+(b-a)*i/48,.0062*math.sin(t))
                  for i in range(49) for t in (i*math.tau*4/48,)]
            self.tube("Visible cord binding",line,.0011,self.cord)
        head=self.loft("Four-ridged dark iron point",[(Vector((0,y,0)),x,z)
            for y,x,z in ((-.139,.005,.005),(-.118,.008,.007),(-.082,.009,.007),
                          (-.073,.019,.009),(-.046,.015,.007),(-.015,.006,.0035),(0,.00035,.00035))],
                          self.iron,8,False)
        head.data.materials.append(self.edge)
        for face in head.data.polygons:
            if face.index>=3*8 and face.index<6*8 and face.index%8 in (0,3,4,7):
                face.material_index=1
        for i in range(3):
            rotation=math.tau*i/3+1.27
            wing=Vector((math.cos(rotation),0,math.sin(rotation)))
            tangent=wing.cross(FORWARD)
            rows=[(-.707,.009),(-.695,.023),(-.678,.028),(-.669,.023),(-.664,.028),
                  (-.633,.026),(-.617,.023),(-.609,.022),(-.591,.013),(-.572,.0065)]
            verts=[]
            for y,r in rows:
                for u in (0,.37,.71,1):
                    verts.append(FORWARD*y+wing*(.0055+(r-.0055)*u)+tangent*(u*u*.005))
            faces=[(j,j+1,j+5,j+4) for j in range(4*(len(rows)-1)) if j%4<3]
            mat=self.feather if i==0 else self.pale
            vane=self.mesh("Curved feather with ragged trailing edge",verts,faces,mat)
            select([vane])
            solid=vane.modifiers.new("Feather thickness","SOLIDIFY")
            solid.thickness=.00065
            bpy.ops.object.modifier_apply(modifier=solid.name)
            for y,r in rows[1:-1]:
                self.tube("Fine feather vane",
                          [FORWARD*(y-.011)+wing*.008,
                           FORWARD*(y-.004)+wing*(r*.54)+tangent*.0013,
                           FORWARD*y+wing*(r*.94)+tangent*.0047],.00036,self.cord)

    def bolt(self):
        rng=Random(167)
        crystal=self.loft("Amethyst leading shard",[(Vector((0,y,0)),r,r*.93)
            for y,r in ((-.379,.006),(-.307,.045),(-.233,.086),
                        (-.142,.119),(-.078,.083),(-.025,.045),(0,.001))],
                        self.amethyst,9,False)
        crystal.data.materials.append(self.light)
        crystal.data.materials.append(self.dark)
        for face in crystal.data.polygons:
            if face.index%9 in (0,3,5):
                face.material_index=1
            elif face.index%9 in (2,6,7):
                face.material_index=2
        for i in range(3):
            phase=i*math.tau/3+.41
            path=[]
            highlights=[]
            for j in range(30):
                t=j/29
                a=phase+t*3.45
                r=.041+.127*math.sin(math.pi*(t*.89+.05))
                point=Vector((r*math.cos(a),-.026-.766*t,r*math.sin(a)))
                width=.0011+.011*math.sin(math.pi*t)**.8
                path.append((point,width,width*.30))
                if j<21:
                    radial=Vector((math.cos(a),0,math.sin(a)))
                    highlights.append((point+radial*(width*.80),width*.23,width*.20))
            self.loft("Tapered violet ribbon of the bolt",path,self.dark,7)
            self.loft("Bright moving seam on the ribbon",highlights,self.light,6)
        self.loft("Light visible inside the tapered tail",[(Vector((0,y,0)),r,r) for y,r in
                  ((-.714,.001),(-.56,.008),(-.414,.017),(-.291,.036),(-.209,.052))],self.core,10)
        for i in range(15):
            a=rng.uniform(0,math.tau)
            y=rng.uniform(-.825,-.166)
            r=rng.uniform(.077,.153)
            center=Vector((r*math.cos(a),y,r*math.sin(a)))
            size=rng.uniform(.0045,.0115)
            path=[(center+FORWARD*d,width,width*.78)
                  for d,width in ((-size*2.1,size*.12),(0,size),(size*1.42,size*.14))]
            self.loft("Detached amethyst fragment",path,self.light if i%3 else self.dark,5,False)

    def join(self,name):
        select(self.parts)
        bpy.ops.object.join()
        mesh=bpy.context.object
        mesh.name="SM_"+name
        self.scene.cursor.location=(0,0,0)
        bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.mesh.normals_make_consistent(inside=False)
        bpy.ops.uv.smart_project(angle_limit=math.radians(65),island_margin=.0035,margin_method="FRACTION")
        bpy.ops.object.mode_set(mode="OBJECT")
        uv=mesh.data.uv_layers.active
        for layer in list(mesh.data.uv_layers):
            if layer!=uv:
                mesh.data.uv_layers.remove(layer)
        uv.name="UV0"
        mod=mesh.modifiers.new("Export triangulation","TRIANGULATE")
        mod.keep_custom_normals=True
        bpy.ops.object.modifier_apply(modifier=mod.name)
        assert mesh.location.length<.00001
        assert abs(max(v.co.y for v in mesh.data.vertices))<.001
        print(f"{mesh.name}: {len(mesh.data.polygons)} triangles",flush=True)
        return mesh


def bake_emission(mesh,size,path):
    select([mesh])
    img=bpy.data.images.new(path.stem,width=size,height=size,alpha=False)
    img.colorspace_settings.name="sRGB"
    targets=[]
    for mat in mesh.data.materials:
        nodes,links=mat.node_tree.nodes,mat.node_tree.links
        shader=nodes["Principled BSDF"]
        output=nodes["Material Output"]
        target=nodes.new("ShaderNodeTexImage")
        target.image=img
        nodes.active=target
        emitter=nodes.new("ShaderNodeEmission")
        if shader.inputs["Emission Strength"].default_value>0:
            emitter.inputs["Color"].default_value=shader.inputs["Emission Color"].default_value
        else:
            emitter.inputs["Color"].default_value=(0,0,0,1)
        links.new(emitter.outputs[0],output.inputs["Surface"])
        targets.append((nodes,links,shader,output,emitter,target))
    bpy.ops.object.bake(type="EMIT",margin=4,uv_layer="UV0")
    img.file_format="PNG"
    img.filepath_raw=str(path)
    img.save()
    for nodes,links,shader,output,emitter,target in targets:
        links.new(shader.outputs["BSDF"],output.inputs["Surface"])
        nodes.remove(emitter)
        nodes.remove(target)


def export_model(mesh,name):
    slots=list(mesh.data.materials)
    ids=[p.material_index for p in mesh.data.polygons]
    mesh.data.materials.clear()
    mesh.data.materials.append(material("M_"+name+"_Atlas",(.18,.09,.19),.25,.54,0,0,""))
    export_collection(MODELS/("SM_"+name+".fbx"),bake_animation=False)
    mesh.data.materials.clear()
    for mat in slots:
        mesh.data.materials.append(mat)
    for polygon,index in zip(mesh.data.polygons,ids):
        polygon.material_index=index


def stage(scene,name):
    camera=studio_camera(scene)
    floor=bpy.data.objects["Studio shadow floor"]
    floor.location.z=-.174
    key=bpy.data.objects["Soft key"]
    key.location=(.62,.45,1.43)
    key.rotation_euler=(Vector((0,-.36,0))-key.location).to_track_quat("-Z","Y").to_euler()
    key.data.energy=77 if name==BOLT else 97
    key.data.size=1.08
    fill=bpy.data.objects["Front fill"]
    fill.location=(-1.2,-.10,.78)
    fill.rotation_euler=(Vector((0,-.36,0))-fill.location).to_track_quat("-Z","Y").to_euler()
    fill.data.energy=35
    fill.data.size=.94
    camera.location=(2.9,1.39,1.06)
    camera.rotation_euler=(Vector((0,-.374,0))-camera.location).to_track_quat("-Z","Y").to_euler()
    camera.data.ortho_scale=1.015
    scene.render.resolution_x,scene.render.resolution_y=1160,630
    scene.cycles.samples=25
    if name==BOLT:
        # Glow is a review effect. The exported material also has an independent emission map.
        floor.data.materials.clear()
        floor.data.materials.append(material("Bolt dark backdrop",(.013,.015,.026),0,1,0,0,""))
        scene.render.compositor_device="CPU"
        tree=scene.compositing_node_group=bpy.data.node_groups.new("Bolt glow","CompositorNodeTree")
        tree.interface.new_socket(name="Image",in_out="OUTPUT",socket_type="NodeSocketColor")
        render=tree.nodes.new("CompositorNodeRLayers")
        glare=tree.nodes.new("CompositorNodeGlare")
        glare.inputs["Type"].default_value="Fog Glow"
        glare.inputs["Threshold"].default_value=.76
        glare.inputs["Strength"].default_value=.33
        output=tree.nodes.new("NodeGroupOutput")
        tree.links.new(render.outputs["Image"],glare.inputs["Image"])
        tree.links.new(glare.outputs["Image"],output.inputs["Image"])


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--quick",action="store_true")
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    SOURCE.mkdir(parents=True,exist_ok=True)
    for name in (ARROW,BOLT):
        projectile=Projectile()
        if name==ARROW:
            projectile.arrow()
        else:
            projectile.bolt()
        mesh=projectile.join(name)
        if not args.quick:
            bake_atlas(mesh,1024,TEXTURES,"T_"+name,"UV0")
            if name==BOLT:
                bake_emission(mesh,1024,TEXTURES/"T_AmethystBolt_Emission.png")
            export_model(mesh,name)
        scene=projectile.scene
        stage(scene,name)
        if args.quick:
            scene.render.resolution_percentage=54
            scene.cycles.samples=12
        else:
            select([mesh])
            bpy.context.preferences.filepaths.save_version=0
            bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/("SM_"+name+".blend")))
        scene.render.filepath=str(SOURCE/(name+".png"))
        bpy.ops.render.render(write_still=True)


if __name__=="__main__":
    main()
