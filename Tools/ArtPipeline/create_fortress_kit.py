"""M55: deterministic small fortress kit. Blender --background --python THIS -- ROOT.

Rebuilds only this generated kit's sources/exports; never touches existing art.
Uses the project's shared FBX profile. No Unity YAML is authored.
"""
import json
import math
import random
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from export_fbx import export_collection
from environment_mesh_names import preserve_mesh_names
from fortress_surfaces import make_surfaces, bind_surface

ROOT = Path(sys.argv[sys.argv.index('--') + 1]).resolve()
SOURCE = ROOT / 'ArtSource/Environment/FortressRuins'
MODELS = ROOT / 'Assets/Art/Environment/Models/FortressRuins'
SOURCE.mkdir(parents=True, exist_ok=True)
MODELS.mkdir(parents=True, exist_ok=True)
PARTS = []
COURSES = {}
TEXTURES=ROOT/'Assets/Art/Textures/Environment/FortressRuins'
make_surfaces(TEXTURES)


def mat(name, rgb, metallic=0):
    value = bpy.data.materials.new(name)
    value.use_nodes = True
    linear = [v / 12.92 if v <= .04045 else ((v+.055)/1.055)**2.4 for v in rgb]
    value.diffuse_color = (*linear, 1)
    node = value.node_tree.nodes['Principled BSDF']
    node.inputs['Base Color'].default_value = (*linear, 1)
    node.inputs['Roughness'].default_value = .5 if metallic else .85
    node.inputs['Metallic'].default_value = metallic
    bind_surface(value,TEXTURES)
    return value


def piece(obj, material, detail=False, collision=True):
    obj.data.materials.clear()
    obj.data.materials.append(M[material])
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    PARTS.append((obj, detail, collision))
    return obj


def box(pos, size, material='Stone', detail=False, collision=True, angle=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    obj = bpy.context.object
    obj.scale = size
    obj.rotation_euler.z = angle
    original = piece(obj, material, detail, collision)
    if not detail and not angle and material in ('Stone','Wood'):
        COURSES[original] = dressed_blocks(pos,size,material)
    return original


def dressed_blocks(pos,size,kind):
    """Closed staggered ashlar or stave panels inside the original block envelope."""
    x,y,z=pos
    w,d,h=size
    along_x=w>=d
    length,depth=(w,d) if along_x else (d,w)
    rows=max(1,round(h/.52)) if kind=='Stone' else 1
    columns=max(1,round(length/(1.16 if kind=='Stone' else .19)))
    if rows*columns==1:
        return None
    rng=random.Random(repr((pos,size)))
    vertices,faces=[],[]
    levels=[0]+[(i+rng.uniform(-.13,.13))*h/rows for i in range(1,rows)]+[h]
    for row in range(rows):
        cuts=[0]+[(i-(.45 if row%2 else 0)+rng.uniform(-.09,.09))*length/columns
                  for i in range(1,columns)]+[length]
        for a,b in zip(cuts,cuts[1:]):
            inset=rng.uniform(.002,.018)
            first=len(vertices)
            left,right=a+.006-length/2,b-.006-length/2
            front,back=-depth/2+inset,depth/2-inset
            ring=[(left,front),(right,front),(right,back),(left,back)]
            if kind=='Stone' and rng.random()<.54:
                # A spalled arris makes a broken plane large enough to catch side light.
                cut=min((right-left)*.17,(back-front)*.2,rng.uniform(.035,.11))
                ring=[(left+cut,front),(right,front),(right,back),(left,back),(left,front+cut)]
                if row%2:
                    ring=[(left+right-u,front+back-v) for u,v in ring]
            count=len(ring)
            for k in (0,1):
                for lateral,normal in ring:
                    vertices.append((x+(lateral if along_x else normal),y+(normal if along_x else lateral),
                                     z-h/2+levels[row+k]+(.006 if k==0 else -.006)))
            faces.append(tuple(first+i for i in reversed(range(count))))
            faces.append(tuple(first+count+i for i in range(count)))
            for i in range(count):
                j=(i+1)%count
                faces.append((first+i,first+j,first+count+j,first+count+i))
    data=bpy.data.meshes.new('Coursed '+kind)
    data.from_pydata(vertices,[],faces)
    data.update()
    obj=bpy.data.objects.new(data.name,data)
    bpy.context.scene.collection.objects.link(obj)
    data.materials.append(M[kind])
    return obj


def cylinder(pos, radius, depth, material='Stone', vertices=12, rotation=(0,0,0), detail=False):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth,
                                       location=pos, rotation=rotation)
    return piece(bpy.context.object, material, detail, not detail)


def wedge(cx, cy, base, radius, thickness, depth, start, end):
    # Closed four-sided ring segment extruded through the wall; preserves opening.
    corners = [(cx+r*math.cos(a), cy+y, base+r*math.sin(a))
               for y in (-depth/2, depth/2)
               for r, a in ((radius,start),(radius,end),(radius+thickness,end),(radius+thickness,start))]
    faces = [(0,3,2,1), (4,5,6,7), (0,1,5,4), (1,2,6,5), (2,3,7,6), (3,0,4,7)]
    mesh = bpy.data.meshes.new('ArchVoussoir')
    mesh.from_pydata(corners, [], faces)
    mesh.update()
    obj = bpy.data.objects.new('ArchVoussoir', mesh)
    bpy.context.scene.collection.objects.link(obj)
    piece(obj, 'Stone')


def arch(width=4, opening=2, height=4, depth=1, ruined=False):
    radius = opening / 2
    spring = height-radius-.5
    pier = (width-opening)/2
    for sign in (-1, 1):
        h = spring if not ruined or sign < 0 else spring*.65
        box((sign*(opening/2+pier/2),0,h/2), (pier,depth,h))
    for i in range(8):
        if ruined and i in (1,2,3):
            continue
        wedge(0,0,spring,radius,.5,depth,i*math.pi/8,(i+1)*math.pi/8)
    if not ruined:
        for sign in (-1,1):
            box((sign*(width/2-.12),0,height/2),(.24,depth+.12,height),detail=True,collision=False)


def build(kind):
    if kind in ('WallStraight','WallDamaged','BrokenWall'):
        heights = [3,3,3,3] if kind == 'WallStraight' else ([3,2.7,2.35,3] if kind == 'WallDamaged' else [2.3,1.7,.9,.5])
        for i, h in enumerate(heights):
            box((-1.5+i,0,h/2),(1,1,h))
        if kind == 'WallStraight':
            box((0,0,.15),(4,1.12,.3),detail=True,collision=False)
            box((0,0,2.8),(4,1.12,.2),detail=True,collision=False)
        for i in range(3):
            # Sparse face blocks, not noisy brick-by-brick masonry.
            box((-1.3+i*1.3,.505,.9),(1.1,.06,.55),detail=True,collision=False)
    elif kind == 'WallCorner':
        box((2,.5,1.5),(4,1,3))
        box((.5,2.5,1.5),(1,3,3))
    elif kind == 'Tower':
        # Hollow 4m square tower; no false solid interior collider.
        for x in (-1.5,1.5):
            box((x,0,3),(1,4,6))
        box((0,1.5,3),(2,1,6))
        # Front opening 2m wide, 3m high.
        box((0,-1.5,4.5),(2,1,3))
        for x in (-1.5,-.5,.5,1.5):
            for y in (-1.5,1.5):
                box((x,y,6.4),(.6,1,.8))
        for x in (-1.5,1.5):
            for y in (-.5,.5):
                box((x,y,6.4),(1,.6,.8))
        box((0,0,5.85),(2,2,.3))
    elif kind in ('Gate','Arch','RuinedArch'):
        arch(8 if kind == 'Gate' else 4, 6 if kind == 'Gate' else 2,
             6 if kind == 'Gate' else 4, ruined=kind == 'RuinedArch')
    elif kind == 'Stairs':
        for i in range(10):
            h = (i+1)*.2
            box((0,-1.8+i*.4,h/2),(2,.4,h))
    elif kind == 'Battlement':
        box((0,0,.2),(4,1,.4))
        for x in (-1.6,0,1.6):
            box((x,0,.9),(.8,1,1))
    elif kind == 'Pillar':
        box((0,0,.2),(1,1,.4))
        box((0,0,1.5),(.7,.7,2.2))
        box((0,0,2.8),(1,1,.4))
    elif kind == 'FloorSection':
        box((0,0,-.125),(4,4,.25))
    elif kind == 'CollapsedPillar':
        cylinder((0,0,.4),.4,2.4,rotation=(0,math.pi/2,0))
        box((-1.25,0,.5),(.3,1,1))
    elif kind.startswith('Rubble'):
        index = int(kind[-2:])
        for i in range(index):
            size = (.45+i*.13,.5+i*.07,.3+i*.12)
            box(((i-(index-1)/2)*.5, .12*(i%2), size[2]/2),size,angle=.3*i)
    elif kind == 'Crate':
        box((0,0,.5),(.88,.88,.88),'Wood')
        for x in (-.44,.44):
            for y in (-.44,.44):
                box((x,y,.5),(.12,.12,1),'Wood')
        for z in (.07,.93):
            for y in (-.44,.44):
                box((0,y,z),(.8,.12,.14),'Wood',detail=True,collision=False)
        box((0,.5,.5),(.12,.04,.18),'Iron',detail=True,collision=False)
    elif kind == 'Barrel':
        cylinder((0,0,.6),.42,1.2,'Wood',vertices=16)
        for z in (.15,1.05):
            cylinder((0,0,z),.445,.12,'Iron',vertices=16,detail=True)
    elif kind == 'TorchHolder':
        # Pivot is bottom-center of wall plate, mounting plane Y=0; front +Y.
        box((0,.035,.3),(.18,.07,.6),'Iron')
        box((0,.2,.2),(.08,.35,.08),'Iron')
        cylinder((0,.35,.45),.08,.55,'Wood')
        cylinder((0,.35,.72),.14,.18,'Iron',vertices=8)
    elif kind == 'WoodenBarricade':
        for x in (-1.2,0,1.2):
            box((x,0,.8),(.22,.25,1.6),'Wood')
            box((x,0,.12),(.3,1,.24),'Wood')
        for z in (.6,1.1):
            box((0,.05,z),(3,.18,.25),'Wood')
    elif kind == 'WeaponRack':
        for x in (-.85,.85):
            box((x,0,.9),(.2,.25,1.8),'Wood')
            box((x,0,.1),(.3,1,.2),'Wood')
        for z in (.5,1.5):
            box((0,0,z),(2,.2,.18),'Wood')
        for x in (-.6,-.2,.2,.6):
            box((x,.18,1.5),(.08,.4,.08),'Iron',detail=True,collision=False)
    else:
        raise ValueError(kind)
    if kind in ('Gate','Arch','RuinedArch'):
        radius=3 if kind=='Gate' else 1
        spring=(6 if kind=='Gate' else 4)-radius-.5
        # A second, stepped archivolt catches the low sun on both faces.
        for side in (-1,1):
            for i in range(12):
                if kind=='RuinedArch' and 1<=i<=5:
                    continue
                wedge(0,side*.51,spring,radius+.13,.16,.12,
                      (i+.014)*math.pi/12,(i+.986)*math.pi/12)
                stone,_,_=PARTS[-1]
                PARTS[-1]=(stone,True,False)
            if kind!='RuinedArch':
                box((0,side*.54,spring+radius+.27),(.34,.22,.48),detail=True,collision=False)
    if kind=='Tower':
        for sign in (-1,1):
            for along_x in (True,False):
                for p in (-1.48,-.52,.52,1.48):
                    x,y=(p,sign*1.99) if along_x else (sign*1.99,p)
                    box((x,y,5.56),(.24,.29,.44),detail=True,collision=False)
                x,y=(0,sign*1.99) if along_x else (sign*1.99,0)
                box((x,y,5.84),(4.05,.30,.19) if along_x else (.30,4.05,.19),detail=True,collision=False)
        for sign in (-1,1):
            for z in (3.88,4.77):
                for dx in (-.10,.10):
                    box((sign*1.50+dx,-2.027,z),(.082,.11,.43),detail=True,collision=False)
                box((sign*1.50,-2.058,z+.216),(.32,.16,.075),detail=True,collision=False)
    if kind=='Barrel':
        for i in range(18):
            a=i*math.tau/18
            box((.415*math.cos(a),.415*math.sin(a),.60),(.036,.136,.94),'Wood',
                detail=True,collision=False,angle=a)
        for x in (-.20,0,.20):
            box((x,0,1.201),(.184,2*math.sqrt(.385**2-x*x)-.07,.014),'Wood',detail=True,collision=False)
    if kind in ('Gate','Arch','RuinedArch','Tower','Pillar','WallStraight','WallDamaged'):
        width={'Gate':8,'Arch':4,'RuinedArch':4,'Tower':4,'Pillar':1}.get(kind,4)
        depth=4 if kind=='Tower' else 1
        offsets=[0] if kind=='Pillar' else [-(width/2-.38),width/2-.38]
        for x in offsets:
            height=1.85 if kind=='RuinedArch' and x>0 else 3.0 if kind in ('Gate','Tower') else 1.95
            for side in (-1,1):
                y=side*(depth/2+.035)
                for lateral in (-.18,.18):
                    box((x+lateral,y,height*.46),(.10,.13,height*.92),detail=True,collision=False)
                    finial=box((x+lateral*.53,y,height*.985),(.094,.14,.30),detail=True,collision=False)
                    # Tilt the moulding in its own frame to meet at a pointed apex.
                    for vert in finial.data.vertices:
                        vert.co.x-=lateral*.85*(vert.co.z-height*.985)/.30
                box((x,y,.13),(.57,.22,.26),detail=True,collision=False)
                box((x,y,height*.88),(.51,.19,.09),detail=True,collision=False)


def joined(name, selected, collection, bevel=False):
    bpy.ops.object.select_all(action='DESELECT')
    copies = []
    for original in selected:
        obj = original.copy()
        obj.data = original.data.copy()
        collection.objects.link(obj)
        obj.select_set(True)
        copies.append(obj)
    bpy.context.view_layer.objects.active = copies[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    bpy.context.scene.cursor.location = (0,0,0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    if bevel:
        modifier = obj.modifiers.new('BroadEdgeBevel', 'BEVEL')
        modifier.width = .005 if name=='SM_FR_Barrel_LOD0' else .018
        modifier.segments = 1 if name=='SM_FR_Tower_LOD0' else 2
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bm = bmesh.from_edit_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bmesh.update_edit_mesh(obj.data)
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=.025)
    bpy.ops.mesh.quads_convert_to_tris(quad_method='BEAUTY', ngon_method='BEAUTY')
    bpy.ops.object.mode_set(mode='OBJECT')
    if not name.startswith('COL_'):
        uv=obj.data.uv_layers.active
        for face in obj.data.polygons:
            n=face.normal
            for idx in face.loop_indices:
                p=obj.data.vertices[obj.data.loops[idx].vertex_index].co
                uv.data[idx].uv=((p.x,p.y) if abs(n.z)>.7 else (p.y,p.z) if abs(n.x)>abs(n.y) else (p.x,p.z))
    return obj


KINDS = ['WallStraight','WallDamaged','WallCorner','Tower','Gate','Arch','Stairs',
         'Battlement','Pillar','FloorSection','BrokenWall','RuinedArch','CollapsedPillar',
         'Rubble01','Rubble02','Rubble03','Crate','Barrel','TorchHolder','WoodenBarricade','WeaponRack']
assets = []
for kind in KINDS:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version=0
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1
    M = {'Stone':mat('M_FR_Stone',(.59,.596,.545)),
         'Wood':mat('M_FR_Wood',(.396,.349,.294)),
         'Iron':mat('M_FR_Iron',(.27,.29,.31),.9)}
    PARTS.clear()
    COURSES.clear()
    build(kind)
    name = 'SM_FR_' + kind
    export = bpy.data.collections.new('EXPORT')
    scene.collection.children.link(export)
    root_obj = bpy.data.objects.new(name, None)
    export.objects.link(root_obj)
    lod0 = joined(name+'_LOD0',[COURSES.get(p[0]) or p[0] for p in PARTS],export,True)
    lod1 = joined(name+'_LOD1',[p[0] for p in PARTS if not p[1]],export)
    collider = joined('COL_'+kind,[p[0] for p in PARTS if p[2]],export)
    for obj in (lod0,lod1,collider):
        obj.parent = root_obj
    lod1.hide_render = True
    collider.hide_render = True
    lod1.hide_set(True)
    collider.hide_set(True)
    for obj, _, _ in PARTS:
        bpy.data.objects.remove(obj, do_unlink=True)
    for obj in COURSES.values():
        if obj:
            bpy.data.objects.remove(obj,do_unlink=True)
    for axis, loc in [('Front',(0,1,0)),('Right',(1,0,0)),('Up',(0,0,1))]:
        socket = bpy.data.objects.new('Socket_'+axis,None)
        export.objects.link(socket)
        socket.parent = root_obj
        socket.location = loc
    low = Vector(tuple(min(v.co[i] for v in lod0.data.vertices) for i in range(3)))
    high = Vector(tuple(max(v.co[i] for v in lod0.data.vertices) for i in range(3)))
    triangles = [len(o.data.polygons) for o in (lod0,lod1,collider)]
    assert 0 < triangles[1] < triangles[0] < 12000, (name,triangles)
    assert triangles[2] > 0
    for obj in (lod0,lod1,collider):
        assert obj.location.length < 1e-6 and len(obj.data.uv_layers) == 1
        assert all(len(face.vertices) == 3 and face.area > 1e-9 for face in obj.data.polygons)
        assert all(math.isfinite(c) for v in obj.data.vertices for c in v.co)
    entry = dict(name=name, source=f'ArtSource/Environment/FortressRuins/{name}.blend',
                 fbx=f'Assets/Art/Environment/Models/FortressRuins/{name}.fbx',
                 dimensionsUnity=[round(high.x-low.x,4),round(high.z-low.z,4),round(high.y-low.y,4)],
                 minimumBlender=list(low), maximumBlender=list(high), triangles=triangles,
                 lod0Materials=[m.name for m in lod0.data.materials],
                 lod1Materials=[m.name for m in lod1.data.materials],
                 pivot='mount plate bottom' if kind == 'TorchHolder' else 'surface center' if kind == 'FloorSection' else 'outside corner' if kind == 'WallCorner' else 'ground center')
    assets.append(entry)
    bpy.ops.object.select_all(action='DESELECT')
    lod0.select_set(True)
    bpy.context.view_layer.objects.active = lod0
    preserve_mesh_names(name, export)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(name+'.blend')),compress=True)
    export_collection(MODELS/(name+'.fbx'))

# Reopen sources for a linked-data demonstration; separate from export assets.
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
meshes = {}
for kind in KINDS:
    name = 'SM_FR_'+kind
    with bpy.data.libraries.load(str(SOURCE/(name+'.blend')),link=False) as (src,dst):
        dst.objects = [name+'_LOD0']
    obj = dst.objects[0]
    meshes[kind] = obj.data

placements = []


def place(kind, x, y, z=0, yaw=0):
    name = 'SM_FR_'+kind
    obj = bpy.data.objects.new(f'{kind}_Demo_{len(placements):02}',meshes[kind])
    scene.collection.objects.link(obj)
    obj.location = (x,y,z)
    obj.rotation_euler.z = math.radians(yaw)
    # Blender +Y front maps to Unity +Z. Handedness reverses yaw.
    placements.append(dict(asset=name, position=[x,z,y], yaw=-yaw))
    return obj


for x in (-6,-2,2,6):
    for y in (0,4,8):
        place('FloorSection',x,y)
place('Gate',0,-1.5)
place('WallCorner',-8,-2)
place('WallCorner',8,-2,yaw=90)
for x in (-6,6):
    place('Battlement',x,-1.5,3)
for x in (-7.5,7.5):
    for y in (2,6):
        place('Battlement',x,y,3,yaw=90)
    for y in (4,8):
        place('WallStraight',x,y,yaw=90)
place('Tower',-7.5,10)
place('Tower',7.5,10)
for x in (-4,0,4):
    place('WallDamaged' if x == 0 else 'WallStraight',x,10)
place('Stairs',-6,7)
place('Pillar',-3,6)
place('Arch',0,6)
place('Crate',5,1)
place('Barrel',6.3,1)
place('WeaponRack',-5,1)
place('TorchHolder',-3.5,-1.45,1)
place('WoodenBarricade',4,5)
for kind, x in [('Crate',-5),('Barrel',-3),('WoodenBarricade',3),('WeaponRack',7)]:
    place(kind,x,-7)
place('Pillar',-1,-7)
place('TorchHolder',-1,-6.48,1)
for i, kind in enumerate(('BrokenWall','RuinedArch','CollapsedPillar','Rubble01','Rubble02','Rubble03','WallCorner')):
    place(kind,14+(i%3)*5,(i//3)*5)

manifest = dict(version=1, milestone=55, blender=bpy.app.version_string,
                status='Offline exports validated; Unity MCP import/prefabs/scene pending',
                materialNames=['M_FR_Stone','M_FR_Wood','M_FR_Iron'], assets=assets, placements=placements)
(SOURCE/'FortressRuins.manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
scene.world = bpy.data.worlds.new('Overcast')
scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.21,.27,.35,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value = .57
bpy.ops.object.light_add(type='AREA',location=(0,-6,18))
light = bpy.context.object
light.data.energy = 4500
light.data.color = (1,.83,.63)
light.data.shape = 'DISK'
light.data.size = 12
light.rotation_euler = (Vector((0,4,0))-light.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(35,-38,30))
camera = bpy.context.object
camera.rotation_euler = (Vector((5,4,1))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.lens = 43
scene.camera = camera
scene.render.engine = 'CYCLES'
scene.cycles.samples = 24
scene.render.resolution_x = 1600
scene.render.resolution_y = 1000
scene.render.resolution_percentage = 100
scene.view_settings.view_transform = 'Standard'
scene['M55_Status'] = manifest['status']
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'FortressRuins_TestSection.blend'),compress=True)
preview = ROOT/'Documentation/FortressRuins'
preview.mkdir(parents=True,exist_ok=True)
scene.render.filepath = str(preview/'TestSection.png')
bpy.ops.render.render(write_still=True)
print(f'[M55] PASS: {len(assets)} sources and shared-profile FBXs; dimensions/UV/triangles/transforms checked.',flush=True)
