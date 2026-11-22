"""Build matched fortress gates and a breached variant for the existing battlefield opening."""

import argparse
import math
from pathlib import Path
from random import Random
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
from character_art import bake_atlas,material,studio_camera
from create_camp_chest import Chest,oak
from creature_mesh import select
from export_fbx import export_collection


ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/"ArtSource/Environment/SM_FortressGate"
MODELS=ROOT/"Assets/Art/Environment/Models/FortressRuins"
TEXTURES=ROOT/"Assets/Art/Textures/Environment/FortressGate"
VARIANTS=("TeamA","TeamB","Breached")


class Gates(Chest):
    def materials(self):
        self.wood=[oak(f"Weathered vertical oak {i}",(.083*t,.043*t,.021*t),(4.7,5.1,.27))
                   for i,t in enumerate((.79,1.07,.94,1.27,1.12))]
        self.beam_wood=oak("Old timber framing",(.046,.025,.012),(.26,4.5,5.2))
        self.fresh=oak("Exposed splintered oak",(.32,.172,.068),(6,6,.39))
        self.iron=material("Hammered dark iron",(.058,.066,.071),.84,.57,107,.0015,"steel")
        self.edge=material("Worn iron edge",(.158,.173,.177),.84,.41,185,.00071,"steel")
        self.brass=material("Tarnished bronze",(.242,.131,.055),.77,.52,123,.0009,"steel")
        self.shade=material("Dark wood checks",(.022,.010,.005),0,.85,0,0,"")
        self.azure=material("Worn azure pigment",(.058,.206,.36),.14,.78,163,.0009,"leather")
        self.red=material("Worn vermilion pigment",(.37,.082,.042),.14,.78,163,.0009,"leather")
        self.crest=material("Faded crest enamel",(.55,.469,.321),.23,.65,170,.00069,"leather")

    def plate_side(self,name,outline,side,y,depth,mat,bevel=.012):
        n=len(outline)
        verts=[(x,side*t,z) for t in (y-depth,y) for x,z in outline]
        faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]
        faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
        obj=self.mesh(name,verts,faces,mat)
        self.bevel(obj,bevel)
        return obj

    def plank(self,sign,i,top,broken):
        a=.032+i*.353
        b=a+.339
        if broken:
            tops=[(b,top-.24),(a+.268,top+.08),(a+.225,top-.34),(a+.155,top),
                  (a+.089,top-.14),(a+.030,top+.16),(a,top-.06)]
        else:
            tops=[(b,top-.030),(a+.221,top+.003),(a+.092,top-.006),(a,top-.036)]
        outline=[(sign*x,z) for x,z in [(a,.040),(b,.042)]+tops]
        mat=self.wood[(i+(sign==1)*2)%len(self.wood)]
        plank=self.plate_side("Split oak plank" if broken else "Adzed oak plank",
                              outline,1,.243+.005*(i%3),.479,mat,.012)
        if broken:
            plank.data.materials.append(self.fresh)
            for face in plank.data.polygons:
                if abs(face.normal.y)<.96 and face.center.z>top-.42:
                    face.material_index=1
        for side in (-1,1):
            x=sign*(a+.085+self.rng.uniform(0,.17))
            z1=min(top-.30,.64+.14*(i%3))
            z2=z1+min(.58,top-z1-.18)
            y=side*(.246+.005*(i%3))
            self.tube("Seasoned timber split",[(x,y,z1),(x+.016,y,z1+.14),
                      (x+.009,y,z2-.056),(x+.010,y,z2)],.0045,self.shade)

    def brace(self,name,start,end,width,depth,mat):
        start,end=Vector(start),Vector(end)
        displacement=end-start
        self.box(name,(start+end)*.5,(width,depth,displacement.length),mat,.016)
        self.parts[-1].rotation_euler=displacement.to_track_quat("Z","Y").to_euler()

    def fitting(self,sign,damaged):
        for side in (-1,1):
            for row,z in enumerate((.55,1.75,3.11)):
                length=2.79 if not damaged else ((2.06,1.98,2.44)[row] if sign<0 else (1.94,2.20,2.54)[row])
                bend=(.08 if row%2 else -.14) if damaged else 0
                outline=[(2.83,z-.126),(2.79,z+.124),(2.52,z+.105),(length+.08,z+.084+bend),
                         (length,z+.021+bend),(length+.046,z-.076+bend),(2.47,z-.099)]
                if not damaged:
                    outline=[(2.83,z-.126),(2.79,z+.124),(2.55,z+.105),(.39,z+.087),
                             (.19,z+.036),(.13,z),(.30,z-.079),(2.44,z-.100)]
                self.plate_side("Forged hinge strap",[(sign*x,h) for x,h in outline],
                                side,.299,.063,self.iron)
                for x in ([2.70,2.50,2.13,.46,.25,1.03,1.60] if not damaged else [2.70,2.57]):
                    self.nail((sign*x,side*.304,z),(0,side,0),.036,self.edge)
                self.tube("Worn strap arris",[(sign*x,side*.301,h) for x,h in outline[1:5]],
                          .009,self.edge)
            if not damaged:
                self.brace("End binding",(sign*.14,side*.259,.12),(sign*.14,side*.259,3.92),
                           .165,.071,self.iron)
                for z in (.29,.96,1.38,2.32,2.88,3.53,3.74):
                    self.nail((sign*.14,side*.299,z),(0,side,0),.028,self.edge)
        for z in (.55,1.75,3.11):
            self.tube("Forged barrel of pintle hinge",
                      [(sign*2.844,0,z-.155),(sign*2.844,0,z+.155)],.055,self.iron)
            self.tube("Bronze hinge pin",
                      [(sign*2.844,0,z-.182),(sign*2.844,0,z+.178)],.025,self.brass)

    def heraldry(self,variant,sign):
        x=sign*1.37
        outline=[(-.246,2.89),(0,2.996),(.246,2.89),(.231,2.51),(0,2.387),(-.231,2.51)]
        for side in (-1,1):
            self.plate_side("Bronze plaque frame",[(x+u,z) for u,z in outline],
                            side,.314,.071,self.brass)
            self.plate_side("Team enamel field",[(x+u*.87,2.70+(z-2.70)*.87) for u,z in outline],
                            side,.325,.029,self.azure if variant=="TeamA" else self.red,.005)
            if variant=="TeamA":
                marks=[[(-.171,2.646),(-.019,2.812),(-.019,2.721),(-.127,2.605)],
                       [(.171,2.646),(.019,2.812),(.019,2.721),(.127,2.605)]]
            else:
                marks=[[(u-.022,2.611),(u-.032,2.772),(u+.024,2.797),(u+.029,2.611)]
                       for u in (-.11,0,.11)]
            for mark in marks:
                self.plate_side("Raised team crest",[(x+u,z) for u,z in mark],
                                side,.340,.019,self.crest,.004)

    def intact_leaf(self,variant,sign):
        for i in range(8):
            top=3.982-.195*(i/7)**3
            self.plank(sign,i,top,False)
        self.brace("Rear cross brace",(sign*2.65,-.227,.37),(sign*.29,-.227,3.57),
                   .30,.202,self.beam_wood)
        self.brace("Rear foot rail",(sign*.16,-.213,.31),(sign*2.76,-.213,.31),
                   .28,.189,self.beam_wood)
        self.brace("Back locking beam",(sign*.095,-.371,1.462),(sign*2.70,-.371,1.462),
                   .24,.251,self.beam_wood)
        self.fitting(sign,False)
        self.heraldry(variant,sign)
        for side in (-1,1):
            crown=[(.050,3.94),(.79,3.94),(1.47,3.91),(2.13,3.87),(2.825,3.76),
                   (2.825,3.62),(2.10,3.72),(1.43,3.76),(.77,3.78),(.050,3.78)]
            self.plate_side("Forged cap following the top of the leaf",[(sign*x,z) for x,z in crown],
                            side,.279,.061,self.iron)
            self.tube("Worn cap edge",[(sign*x,side*.282,z) for x,z in crown[:5]],
                      .011,self.edge)
            for x,z in ((.27,3.87),(.82,3.86),(1.43,3.83),(2,3.80),(2.58,3.73)):
                self.nail((sign*x,side*.282,z),(0,side,0),.025,self.edge)
            z=1.32
            self.box("Ring escutcheon",(sign*.45,side*.285,z),(.185,.090,.245),self.iron,.023)
            path=[(sign*.45+.086*math.cos(t),side*(.345+.043*math.sin(t)**2),z-.083+.113*math.sin(t))
                  for t in (i*math.tau/26 for i in range(26))]
            self.tube("Forged pull ring",path,.017,self.edge,True)

    def broken_leaf(self,sign):
        heights=(1.04,1.34,2.24,3.71) if sign<0 else (.78,1.75,2.94,3.87)
        for i,top in zip(range(4,8),heights):
            self.plank(sign,i,top,True)
        self.fitting(sign,True)
        for start,end in (
            ((sign*2.82,.27,.14),(sign*1.64,.58,.24)),
            ((sign*2.74,-.51,.085),(sign*1.94,-.85,.10)),
            ((sign*2.37,.63,.062),(sign*1.36,1.06,.072)),
        ):
            self.brace("Heavily splintered fallen brace",start,end,.135,.105,self.beam_wood)
        grain=[(1.37,.14),(1.47,.27),(1.61,.25),(1.71,.45),(1.88,.49),(2.10,.87),
               (2.25,.89),(2.43,1.21),(2.48,1.31),(2.52,1.20),(2.30,.74),(1.96,.40),(1.56,.13)]
        self.plate_side("Torn plank caught on the back of the door",[(sign*x,z) for x,z in grain],
                        -1,.457,.123,self.wood[1],.009)
        self.tube("Exposed grain on the dislodged plank",
                  [(sign*x,-.464,z+.004) for x,z in grain[2:8]],.012,self.fresh)
        for _ in range(10):
            x=sign*self.rng.uniform(1.34,2.80)
            y=self.rng.choice((-1,1))*self.rng.uniform(.42,1.06)
            z=self.rng.uniform(.041,.063)
            self.brace("Oak wreckage at jamb",(x,y,z),(x+sign*self.rng.uniform(.09,.19),
                       y+self.rng.uniform(-.14,.14),z+.008),.064,.054,self.fresh)

    def variant(self,name):
        self.parts=[]
        self.rng=Random(621)
        for sign in (-1,1):
            if name=="Breached":
                self.broken_leaf(sign)
            else:
                self.intact_leaf(name,sign)
        select(self.parts)
        bpy.ops.object.join()
        mesh=bpy.context.object
        mesh.name=name
        self.scene.cursor.location=(0,0,0)
        bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        tagged=mesh.vertex_groups.new(name=name)
        tagged.add(list(range(len(mesh.data.vertices))),1,"REPLACE")
        return mesh

    def pack(self,bake):
        parts=[self.variant(name) for name in VARIANTS]
        select(parts)
        bpy.ops.object.join()
        atlas=bpy.context.object
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.mesh.normals_make_consistent(inside=False)
        bpy.ops.uv.smart_project(angle_limit=math.radians(65),island_margin=.0015,margin_method="FRACTION")
        bpy.ops.object.mode_set(mode="OBJECT")
        uv=atlas.data.uv_layers.active
        for layer in list(atlas.data.uv_layers):
            if layer!=uv:
                atlas.data.uv_layers.remove(layer)
        uv.name="UV0"
        mod=atlas.modifiers.new("Triangulate export","TRIANGULATE")
        mod.keep_custom_normals=True
        bpy.ops.object.modifier_apply(modifier=mod.name)
        if bake:
            bake_atlas(atlas,2048,TEXTURES,"T_FortressGate","UV0")
        sections={VARIANTS[0]:atlas}
        for name in VARIANTS[1:]:
            select([atlas])
            before=set(self.collection.objects)
            bpy.ops.object.mode_set(mode="EDIT")
            bpy.ops.mesh.select_all(action="DESELECT")
            atlas.vertex_groups.active_index=atlas.vertex_groups[name].index
            bpy.ops.object.vertex_group_select()
            bpy.ops.mesh.separate(type="SELECTED")
            bpy.ops.object.mode_set(mode="OBJECT")
            sections[name],=set(self.collection.objects)-before
        for name,obj in sections.items():
            obj.name="FortressGate_"+name
            obj.vertex_groups.clear()
            used={p.material_index for p in obj.data.polygons}
            for index in reversed(range(len(obj.data.materials))):
                if index not in used:
                    obj.data.materials.pop(index=index)
            print(f"{obj.name}: {len(obj.data.polygons)} triangles",flush=True)
        breached=sections["Breached"]
        assert min(abs(v.co.x) for v in breached.data.vertices)>1.30
        return sections


def set_camera(camera,position,target,scale):
    camera.location=position
    camera.rotation_euler=(Vector(target)-camera.location).to_track_quat("-Z","Y").to_euler()
    camera.data.ortho_scale=scale


def export_variants(gate,sections):
    atlas=material("M_FortressGate_Atlas",(.083,.043,.021),0,.65,0,0,"")
    for obj in sections.values():
        gate.collection.objects.unlink(obj)
    for name,original in sections.items():
        mesh=original.data.copy()
        # These prefabs are 5.8 m along local Unity Z. Turn the width into Blender Y for export.
        mesh.transform(Matrix.Rotation(math.pi/2,4,"Z"))
        mesh.materials.clear()
        mesh.materials.append(atlas)
        export=bpy.data.objects.new("Gate_"+name,mesh)
        gate.collection.objects.link(export)
        export_collection(MODELS/("SM_FortressGate_"+name+".fbx"),bake_animation=False)
        bpy.data.objects.remove(export,do_unlink=True)
        bpy.data.meshes.remove(mesh)
    for obj in sections.values():
        gate.collection.objects.link(obj)


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--quick",action="store_true")
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    SOURCE.mkdir(parents=True,exist_ok=True)
    gate=Gates()
    sections=gate.pack(not args.quick)
    scene=gate.scene
    camera=studio_camera(scene)
    set_camera(camera,(5.5,10.5,5.60),(0,0,1.93),7.8)
    for name,position,power in (
        ("Soft key",(2.5,5.9,7.5),1550),("Front fill",(-5.2,3.7,4.2),740),
        ("Rim",(4,-4,6.5),1060),("Back fill",(-3.2,-4.4,3.3),470),
    ):
        light=bpy.data.objects[name]
        light.location=position
        light.rotation_euler=(Vector((0,0,1.82))-light.location).to_track_quat("-Z","Y").to_euler()
        light.data.energy=power
    bpy.data.objects["Studio shadow floor"].location.z=0
    scene.render.resolution_x=1260
    scene.render.resolution_y=940
    scene.render.resolution_percentage=53 if args.quick else 100
    scene.cycles.samples=12 if args.quick else 28
    for name,obj in sections.items():
        obj.hide_set(name!="TeamA")
        obj.hide_render=name!="TeamA"
    if not args.quick:
        bpy.context.preferences.filepaths.save_version=0
        select([sections["TeamA"]])
        bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/"SM_FortressGate.blend"))
        export_variants(gate,sections)
    for name,obj in sections.items():
        for other in sections.values():
            other.hide_set(other!=obj)
            other.hide_render=other!=obj
        scene.render.filepath=str(SOURCE/("FortressGate"+name+".png"))
        bpy.ops.render.render(write_still=True)


if __name__=="__main__":
    main()
