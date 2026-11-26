"""Build a hinged oak camp chest to replace the battlefield loot chest visuals."""

import argparse
import math
from pathlib import Path
from random import Random
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from character_art import bake_atlas, material, studio_camera
from creature_mesh import select
from export_fbx import export_collection


ROOT = Path(__file__).resolve().parents[2]
STEM = "SM_CampChest"
SOURCE = ROOT/"ArtSource/Environment"/STEM
MODEL = ROOT/"Assets/Art/Environment/Models"/(STEM+".fbx")
TEXTURES = ROOT/"Assets/Art/Textures/Environment/CampChest"
HINGE = (0,-.5,.8)


def oak(name,color,grain_scale):
    wood = material(name,color,0,.68,93,.0018,"leather")
    nodes,links = wood.node_tree.nodes,wood.node_tree.links
    shader = nodes["Principled BSDF"]
    coords = next(node for node in nodes if node.type=="TEX_COORD").outputs["Object"]
    scale = nodes.new("ShaderNodeVectorMath")
    scale.operation = "MULTIPLY"
    scale.inputs[1].default_value = grain_scale
    links.new(coords,scale.inputs[0])
    for node in list(nodes):
        if node.type=="TEX_NOISE":
            links.new(scale.outputs["Vector"],node.inputs["Vector"])
    growth = nodes.new("ShaderNodeTexNoise")
    links.new(scale.outputs["Vector"],growth.inputs["Vector"])
    growth.inputs["Scale"].default_value = 23
    growth.inputs["Detail"].default_value = 4
    growth.inputs["Roughness"].default_value = .69
    colors = nodes.new("ShaderNodeValToRGB")
    for i,(position,factor) in enumerate(((.25,.32),(.48,.84),(.65,1.23),(.82,1.49))):
        stop = colors.color_ramp.elements[i] if i<2 else colors.color_ramp.elements.new(position)
        stop.position = position
        stop.color = (*[c*factor for c in color],1)
    links.new(growth.outputs["Fac"],colors.inputs["Fac"])
    links.new(colors.outputs["Color"],shader.inputs["Base Color"])
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = .54
    bump.inputs["Distance"].default_value = .0022
    links.new(shader.inputs["Normal"].links[0].from_socket,bump.inputs["Normal"])
    links.new(growth.outputs["Fac"],bump.inputs["Height"])
    links.new(bump.outputs["Normal"],shader.inputs["Normal"])
    return wood


class Chest:
    mesh_stem = "CampChest"
    textures = TEXTURES
    hinge = HINGE
    moving_name = "Lid"

    def __init__(self):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        self.scene = bpy.context.scene
        self.scene.unit_settings.system = "METRIC"
        self.collection = bpy.data.collections.new("EXPORT")
        self.scene.collection.children.link(self.collection)
        self.body_parts = []
        self.lid_parts = []
        self.parts = self.body_parts
        self.rng = Random(4041)
        self.materials()

    def materials(self):
        self.woods = [oak("Oak plank "+str(i),(.069*t,.037*t,.020*t),(.34,5,8.0))
                      for i,t in enumerate((.84,1,1.24,.91,1.12))]
        self.cross = oak("Oak lengthwise side",(.084,.041,.018),(6,.30,7))
        self.end = oak("Oak end grain",(.072,.035,.016),(5,4,1.6))
        self.dark = oak("Interior rubbed oak",(.041,.022,.011),(.39,5,8))
        self.iron = material("Hammered black iron",(.069,.078,.084),.83,.52,165,.0012,"steel")
        self.edge = material("Burnished iron edge",(.21,.226,.221),.87,.41,220,.00047,"steel")
        self.brass = material("Tarnished pale brass",(.36,.222,.084),.81,.43,200,.00085,"steel")
        self.patina = material("Old brass patina",(.121,.088,.043),.62,.62,210,.0010,"steel")
        self.shadow = material("Dark recess",(.008,.006,.004),0,.89,0,0,"")

    def finish(self,obj,name,mat):
        obj.name = name
        obj.data.materials.append(mat)
        for collection in list(obj.users_collection):
            if collection!=self.collection:
                collection.objects.unlink(obj)
        if self.collection not in obj.users_collection[:]:
            self.collection.objects.link(obj)
        self.parts.append(obj)
        return obj

    def mesh(self,name,verts,faces,mat):
        data = bpy.data.meshes.new(name)
        data.from_pydata(verts,[],faces)
        data.update()
        obj = bpy.data.objects.new(name,data)
        self.collection.objects.link(obj)
        return self.finish(obj,name,mat)

    @staticmethod
    def bevel(obj,width):
        select([obj])
        mod = obj.modifiers.new("Rounded worn edges","BEVEL")
        mod.width = width
        mod.segments = 3
        bpy.ops.object.modifier_apply(modifier=mod.name)

    def box(self,name,point,dimensions,mat,radius=.009):
        bpy.ops.mesh.primitive_cube_add(size=1,location=point)
        obj = bpy.context.object
        obj.dimensions = dimensions
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
        self.finish(obj,name,mat)
        self.bevel(obj,radius)

    def nail(self,point,normal,radius=.012,mat=None):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=7,location=point)
        obj = bpy.context.object
        obj.scale = (radius,radius,.51*radius)
        obj.rotation_euler = Vector(normal).to_track_quat("Z","Y").to_euler()
        bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
        for face in obj.data.polygons:
            face.use_smooth = True
        self.finish(obj,"Peened rivet",mat or self.iron)

    def tube(self,name,points,radius,mat,closed=False):
        points = list(map(Vector,points))
        sides = 9
        verts = []
        for i,p in enumerate(points):
            before = points[(i-1)%len(points)] if closed else points[max(0,i-1)]
            after = points[(i+1)%len(points)] if closed else points[min(len(points)-1,i+1)]
            rotation = (after-before).to_track_quat("Z","Y")
            for j in range(sides):
                a = j*math.tau/sides
                verts.append(p+rotation@Vector((radius*math.cos(a),radius*math.sin(a),0)))
        faces = []
        for i in range(len(points) if closed else len(points)-1):
            row,next_row = i*sides,((i+1)%len(points))*sides
            for j in range(sides):
                k = (j+1)%sides
                faces.append((row+j,row+k,next_row+k,next_row+j))
        if not closed:
            faces.extend((tuple(reversed(range(sides))),tuple(range(len(verts)-sides,len(verts)))))
        obj = self.mesh(name,verts,faces,mat)
        for face in obj.data.polygons:
            face.use_smooth = True

    def front_plate(self,name,outline,y,depth,mat,radius=.004):
        verts = [(x,t,z) for t in (y-depth,y) for x,z in outline]
        n = len(outline)
        faces = [tuple(reversed(range(n))),tuple(range(n,n*2))]
        faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
        self.bevel(self.mesh(name,verts,faces,mat),radius)

    def keyhole(self):
        outline = [(-.099,.607),(-.122,.651),(-.114,.719),(-.083,.758),(0,.777),
                   (.083,.758),(.114,.719),(.122,.651),(.099,.607),(0,.575)]
        self.front_plate("Peaked iron lock base",[(x*1.11,.68+(z-.68)*1.1) for x,z in outline],
                         .530,.038,self.iron,.009)
        self.front_plate("Cast brass escutcheon",outline,.545,.025,self.brass,.007)
        light = [(x*.882,.547,.68+(z-.68)*.871) for x,z in outline]
        self.tube("Engraved lock border",light,.0025,self.patina,True)
        for sign in (-1,1):
            self.tube("Chased lock leaf",[(sign*x,.547,z) for x,z in
                      ((.046,.630),(.071,.653),(.064,.687),(.077,.716),(.047,.733),(.033,.719))],
                      .0034,self.edge)
        hole = [(.020*math.sin(a),.680+.020*math.cos(a))
                for a in (i*math.tau/32 for i in range(32)) if abs(a-math.pi)>.46]
        middle = len(hole)//2
        hole[middle:middle] = [(.014,.631),(-.014,.631)]
        self.front_plate("Recessed keyway",[(x,z) for x,z in hole],.551,.007,self.shadow,.001)
        for x,z in ((-.089,.696),(.089,.696),(0,.610)):
            self.nail((x,.55,z),(0,1,0),.010,self.patina)

    def build_body(self):
        for i in range(7):
            x = (i-3)*.210
            self.box("Separate oak floorboard",(x,0,.079),(.205,.889,.097),self.dark,.011)
        for i in range(5):
            z = .153+.132*i
            for side in (-1,1):
                variation = .0022*self.rng.uniform(-1,1)
                self.box("Long hand-planed oak stave",(0,side*(.458+variation),z),
                         (1.563,.076,.128),self.woods[(i+(side==1))%5],.007)
                self.box("Oak end wall plank",(side*(.736+variation),0,z),
                         (.089,.859,.128),self.cross,.007)
        for x in (-.753,.753):
            for y in (-.474,.474):
                self.box("Squat corner foot",(x,y,.050),(.102,.106,.099),self.iron,.018)
                self.box("Iron-wrapped corner post",(x,y,.402),(.104,.102,.708),self.iron,.018)
        for z,thickness in ((.126,.090),(.764,.067)):
            for side in (-1,1):
                self.box("Long oak sill",(0,side*.463,z),(1.587,.092,thickness),self.woods[0],.010)
                self.box("End oak sill",(side*.754,0,z),(.090,.892,thickness),self.cross,.010)
        for sx in (-1,1):
            for sy in (-1,1):
                for z in (.134,.728):
                    self.box("Brass corner wrap on face",(sx*.711,sy*.514,z),(.176,.019,.093),self.brass,.011)
                    self.box("Brass corner wrap on end",(sx*.808,sy*.451,z),(.018,.137,.093),self.brass,.011)
                    for x in (.66,.772):
                        self.nail((sx*x,sy*.524,z),(0,sy,0),.013)
                    for y in (.405,.491):
                        self.nail((sx*.819,sy*y,z),(sx,0,0),.011)
            for sy in (-1,1):
                self.box("D-shaped handle mount",(sx*.796,sy*.122,.508),(.030,.086,.121),self.iron,.011)
                self.nail((sx*.816,sy*.120,.547),(sx,0,0),.01,self.brass)
            points = [(sx*(.824+.023*math.sin(a)**2),.121*math.cos(a),.441+.096*math.sin(a))
                      for a in (i*math.tau/32 for i in range(32))]
            self.tube("Forged pull handle",points,.016,self.iron,True)
            self.tube("Handle wear at lower grip",points[20:29],.004,self.edge)
        for x in (-.516,.516):
            self.box("Lower hinge leaf",(x,-.511,.725),(.151,.022,.147),self.iron,.010)
            for dx in (-.047,.047):
                self.nail((x+dx,-.525,.697),(0,-1,0),.010,self.brass)
            for dx in (-.064,.064):
                self.tube("Outer hinge knuckle",[(x+dx-.028,-.50,.8),(x+dx+.028,-.50,.8)],.025,self.iron)
            self.tube("Brass hinge pin",[(x-.102,-.50,.8),(x+.102,-.50,.8)],.009,self.brass)
        self.keyhole()
        for x,y,z,length in ((-.37,.5,.483,.095),(.31,.5,.348,.118),(-.29,.5,.617,.072),(.52,-.5,.435,.058)):
            self.tube("Old splits in front staves",
                      [(x,y,z),(x+.28*length,y+.0007,z+.006),(x+.60*length,y,z+.003),
                       (x+length,y,z+.008)],.0026,self.shadow)

    @staticmethod
    def dome(angle,offset=0):
        return ((.5+offset)*math.sin(angle),.808+(.263+offset)*math.cos(angle))

    def dome_panel(self,name,x0,x1,a0,a1,mat,offset,depth,steps=12):
        angles = [a0+(a1-a0)*i/steps for i in range(steps+1)]
        verts = [(x,*self.dome(angle,d)) for angle in angles
                 for x,d in ((x0,offset-depth),(x1,offset-depth),(x1,offset),(x0,offset))]
        faces = [(i+j,i+(j+1)%4,i+4+(j+1)%4,i+4+j)
                 for i in range(0,steps*4,4) for j in range(4)]
        faces.extend(((3,2,1,0),tuple(range(steps*4,steps*4+4))))
        obj = self.mesh(name,verts,faces,mat)
        for face in obj.data.polygons:
            face.use_smooth = face.index<steps*4 and face.index%4 in (0,2)
        self.bevel(obj,.003 if depth>.02 else .002)

    def build_lid(self):
        self.parts = self.lid_parts
        staves = 11
        for i in range(staves):
            a0 = -math.pi/2+math.pi*i/staves
            a1 = a0+math.pi/staves
            self.dome_panel("Curved oak lid stave",-.801,.801,a0+.006,a1-.006,
                            self.woods[(i*3)%5],0,.074,9)
        for side in (-1,1):
            edge = [(side*.793,*self.dome(a)) for a in
                    (-math.pi/2+i*math.pi/40 for i in range(41))]
            self.tube("Rolled arched end band",edge,.017,self.iron)
            top = [(side*.781,*self.dome(a,-.014)) for a in
                   (-math.pi/2+i*math.pi/32 for i in range(33))]
            points = top+[(side*.781,.475,.805),(side*.781,-.475,.805)]
            back = [(x-side*.046,y,z) for x,y,z in points]
            n = len(points)
            faces = [tuple(range(n)),tuple(reversed(range(n,2*n)))]
            faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
            self.bevel(self.mesh("Solid oak arched end",points+back,faces,self.end),.006)
        for x in (-.516,.516):
            self.dome_panel("Broad curved iron strap",x-.060,x+.060,-math.pi/2,math.pi/2,
                            self.iron,.022,.024,58)
            for dx in (-.050,.050):
                line = [(x+dx,*self.dome(a,.023))
                        for a in (-math.pi/2+i*math.pi/49 for i in range(50))]
                self.tube("Burnished edge of strap",line,.003,self.edge)
            for a in (-1.39,-1.06,-.64,-.28,.1,.47,.86,1.22,1.45):
                y,z = self.dome(a,.024)
                self.nail((x,y,z),(0,math.sin(a),math.cos(a)),.011,self.brass)
            self.tube("Center hinge knuckle",[(x-.036,-.50,.8),(x+.036,-.50,.8)],.025,self.iron)
        for y in (-.499,.499):
            self.box("Lid turned lip",(0,y,.821),(1.636,.055,.041),self.iron,.009)
        self.front_plate("Curved hasp base",[(-.056,.819),(-.060,.884),(-.043,.922),
                         (0,.940),(.043,.922),(.060,.884),(.056,.819)],.523,.057,self.iron,.010)
        self.front_plate("Brass clasp tongue",[(-.029,.781),(-.041,.832),(-.034,.873),
                         (.033,.873),(.041,.832),(.029,.781)],.551,.040,self.brass,.006)
        for x in (-.021,.021):
            self.nail((x,.554,.849),(0,1,0),.008,self.patina)

    def assemble(self,bake):
        sections = []
        for pieces,suffix in ((self.body_parts,"Body"),(self.lid_parts,self.moving_name)):
            select(pieces)
            bpy.ops.object.join()
            part = bpy.context.object
            part.name = self.mesh_stem+"_"+suffix
            self.scene.cursor.location = (0,0,0)
            bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
            bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
            if pieces is self.lid_parts:
                group = part.vertex_groups.new(name="Lid")
                group.add(list(range(len(part.data.vertices))),1,"REPLACE")
            sections.append(part)
        select(sections)
        bpy.ops.object.join()
        body = bpy.context.object
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.mesh.normals_make_consistent(inside=False)
        bpy.ops.uv.smart_project(angle_limit=math.radians(62),island_margin=.003,margin_method="FRACTION")
        bpy.ops.object.mode_set(mode="OBJECT")
        for layer in list(body.data.uv_layers):
            if layer!=body.data.uv_layers.active:
                body.data.uv_layers.remove(layer)
        body.data.uv_layers.active.name = "UV0"
        tri = body.modifiers.new("Export triangulation","TRIANGULATE")
        tri.keep_custom_normals = True
        bpy.ops.object.modifier_apply(modifier=tri.name)
        if bake:
            bake_atlas(body,2048,self.textures,"T_"+self.mesh_stem,"UV0")
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="DESELECT")
        body.vertex_groups.active_index = body.vertex_groups["Lid"].index
        bpy.ops.object.vertex_group_select()
        bpy.ops.mesh.separate(type="SELECTED")
        bpy.ops.object.mode_set(mode="OBJECT")
        lid = next(obj for obj in self.collection.objects if obj!=body)
        body.name = self.mesh_stem+"_Body"
        lid.name = self.mesh_stem+"_"+self.moving_name
        for part in (body,lid):
            part.vertex_groups.clear()
            for i in reversed(range(len(part.data.materials))):
                if all(poly.material_index!=i for poly in part.data.polygons):
                    part.data.materials.pop(index=i)
        select([lid])
        self.scene.cursor.location = self.hinge
        bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
        lid.parent = body
        self.scene.cursor.location = (0,0,0)
        assert (lid.location-Vector(self.hinge)).length<.0001
        assert body.location.length<.0001
        assert all(len(p.vertices)==3 for part in (body,lid) for p in part.data.polygons)
        print(f"SM_{self.mesh_stem}: {len(body.data.polygons)+len(lid.data.polygons)} triangles",flush=True)
        return body,lid

    def review(self,camera,lid,quick):
        self.scene.cycles.samples = 12 if quick else 28
        self.scene.render.resolution_percentage = 53 if quick else 100
        for name,pos,target,scale,angle in (
            ("CampChest", (2.8,4.8,2.87),(0,0,.52),2.27,0),
            ("CampChestOpen",(2.26,4.1,3.10),(0,-.17,.82),3.26,105),
            ("CampChestDetail",(-1.33,4.2,1.54),(0,.25,.69),1.28,0),
        ):
            lid.rotation_euler.x = math.radians(angle)
            camera.location = pos
            camera.rotation_euler = (Vector(target)-camera.location).to_track_quat("-Z","Y").to_euler()
            camera.data.ortho_scale = scale
            self.scene.render.filepath = str(SOURCE/(name+".png"))
            bpy.ops.render.render(write_still=True)

    def save_and_export(self,body,lid,source,model,color):
        bpy.context.preferences.filepaths.save_version = 0
        select([body])
        bpy.ops.wm.save_as_mainfile(filepath=str(source))
        parts = (body,lid)
        originals = [(list(obj.data.materials),[p.material_index for p in obj.data.polygons]) for obj in parts]
        atlas = material("M_"+self.mesh_stem+"_Atlas",color,0,.68,0,0,"")
        for part in parts:
            part.data.materials.clear()
            part.data.materials.append(atlas)
        export_collection(model,bake_animation=False)
        for part,(materials,indices) in zip(parts,originals):
            part.data.materials.clear()
            for mat in materials:
                part.data.materials.append(mat)
            for polygon,index in zip(part.data.polygons,indices):
                polygon.material_index = index


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--quick",action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    SOURCE.mkdir(parents=True,exist_ok=True)
    chest = Chest()
    chest.build_body()
    chest.build_lid()
    body,lid = chest.assemble(not args.quick)
    camera = studio_camera(chest.scene)
    camera.location = (2.8,4.8,2.87)
    camera.rotation_euler = (Vector((0,0,.52))-camera.location).to_track_quat("-Z","Y").to_euler()
    camera.data.ortho_scale = 2.27
    chest.scene.render.resolution_x = 1200
    chest.scene.render.resolution_y = 1000
    bpy.data.objects["Studio shadow floor"].location.z = 0
    if not args.quick:
        chest.save_and_export(body,lid,SOURCE/(STEM+".blend"),MODEL,(.091,.047,.021))
    chest.review(camera,lid,args.quick)


if __name__=="__main__":
    main()
