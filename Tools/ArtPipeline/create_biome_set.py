"""M57 shared biome extension. Run inside Blender with -- PROJECT_ROOT.

Reuses AF geometry/maps, one FBX profile and URP Lit. No gameplay scene edits.
Writes only generated BS assets. Do not regenerate over hand-edited sources.
"""
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector, noise

sys.path.insert(0,str(Path(__file__).resolve().parent))
from export_fbx import export_collection

ROOT=Path(sys.argv[sys.argv.index('--')+1]).resolve()
SOURCE=ROOT/'ArtSource/Environment/Biomes'
MODELS=ROOT/'Assets/Art/Environment/Models/Biomes'
TEXTURES=ROOT/'Assets/Art/Textures/Environment/Biomes'
PREVIEW=ROOT/'Documentation/Biomes'
for path in (SOURCE,MODELS,TEXTURES,PREVIEW):
    path.mkdir(parents=True,exist_ok=True)
SIZE=512
PALETTE={
    'HighlandGravel':('96978C',.9),'BattlefieldPaving':('8E877A',.9),
    'CorruptedAsh':('756F7D',.94),'StrataStone':('9A9E98',.88),
    'WarIron':('545D61',.55),'OldCloth':('B3AA93',.95),
    'CorruptedStone':('777080',.9),'PaleVein':('B1A4B8',.7),
}
PAIRS=[('Forest','Ruins'),('Highlands','Ruins'),('Forest','Corrupted'),
       ('Highlands','Corrupted'),('Ruins','Corrupted')]
BIOMES={'Forest':'AF_ForestFloor','Highlands':'BS_HighlandGravel',
        'Ruins':'BS_BattlefieldPaving','Corrupted':'BS_CorruptedAsh'}


def rgb(value):
    return [int(value[i:i+2],16)/255 for i in (0,2,4)]


def save_map(stem,suffix,pixels):
    image=bpy.data.images.new('T_'+stem+'_'+suffix,width=SIZE,height=SIZE,alpha=True)
    # Generated pixels are already encoded for their intended output space.
    if suffix!='BaseColor':
        image.colorspace_settings.name='Non-Color'
    image.pixels.foreach_set(pixels)
    image.filepath_raw=str(TEXTURES/(image.name+'.png'))
    image.file_format='PNG'
    image.save()
    bpy.data.images.remove(image)


def terrain(kind):
    base=rgb(PALETTE[kind][0])
    colors=[]
    height=[]
    for y in range(SIZE):
        v=y/SIZE
        for x in range(SIZE):
            u=x/SIZE
            torus=Vector(((3+math.cos(math.tau*v))*math.cos(math.tau*u),
                          (3+math.cos(math.tau*v))*math.sin(math.tau*u),math.sin(math.tau*v)))
            broad=noise.noise(torus*2)
            fine=noise.noise(torus*23)
            if kind=='HighlandGravel':
                h=.5+.05*broad+.025*fine
                tone=1+.1*broad+.06*fine
            elif kind=='BattlefieldPaving':
                # Broad worn courses; shallow seams, no noisy dark checkerboard.
                row=int(v*4)
                col=(u*4+(row%2)*.5)%1
                edge=min(col,1-col,(v*4)%1,1-(v*4)%1)
                seam=max(0,1-edge/.035)
                h=.5-.055*seam+.015*broad
                tone=1-.13*seam+.07*broad
            else:
                # Dry cracked ash, no glowing poison pool or false hazard outline.
                vein=max(0,1-abs(noise.noise(torus*4))/.035)
                h=.5-.025*vein+.03*broad
                tone=1-.10*vein+.07*broad
            height.append(h)
            colors.extend([max(0,min(1,c*tone)) for c in base]+[1])
    normal=[]
    mask=[]
    for y in range(SIZE):
        for x in range(SIZE):
            dx=height[y*SIZE+(x+1)%SIZE]-height[y*SIZE+(x-1)%SIZE]
            dy=height[((y+1)%SIZE)*SIZE+x]-height[((y-1)%SIZE)*SIZE+x]
            n=Vector((-dx*4,-dy*4,1)).normalized()
            normal.extend((n.x*.5+.5,n.y*.5+.5,n.z*.5+.5,1))
            mask.extend((0,1,0,1-PALETTE[kind][1]))
    return {'BaseColor':colors,'Normal':normal,'Mask':mask}


def decode(v):
    return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4


def encode(v):
    return 12.92*v if v<=.0031308 else 1.055*v**(1/2.4)-.055


bpy.ops.wm.read_factory_settings(use_empty=True)
maps={}
for kind in ('HighlandGravel','BattlefieldPaving','CorruptedAsh'):
    maps['BS_'+kind]=terrain(kind)
    for suffix,pixels in maps['BS_'+kind].items():
        save_map('BS_'+kind,suffix,pixels)
forest={}
for suffix in ('BaseColor','Normal','Mask'):
    path=ROOT/f'Assets/Art/Textures/Environment/AncientForest/T_AF_ForestFloor_{suffix}.png'
    image=bpy.data.images.load(str(path),check_existing=False)
    image.colorspace_settings.name='Non-Color'
    forest[suffix]=list(image.pixels)
    bpy.data.images.remove(image)
maps['AF_ForestFloor']=forest
transitions=[]
for first,second in PAIRS:
    name='BS_Transition'+first+second
    a,b=maps[BIOMES[first]],maps[BIOMES[second]]
    blended={key:[] for key in a}
    for y in range(SIZE):
        for x in range(SIZE):
            u=x/(SIZE-1)
            perturb=.08*math.sin(math.tau*y/SIZE*3)*math.sin(math.pi*u)
            t=max(0,min(1,(u-.15)/.7+perturb))
            t=t*t*(3-2*t)
            index=(y*SIZE+x)*4
            blended['BaseColor'].extend([encode(decode(a['BaseColor'][index+i])*(1-t)+decode(b['BaseColor'][index+i])*t) for i in range(3)]+[1])
            na=Vector(tuple(a['Normal'][index+i]*2-1 for i in range(3)))
            nb=Vector(tuple(b['Normal'][index+i]*2-1 for i in range(3)))
            n=(na*(1-t)+nb*t).normalized()
            blended['Normal'].extend((n.x*.5+.5,n.y*.5+.5,n.z*.5+.5,1))
            blended['Mask'].extend(a['Mask'][index+i]*(1-t)+b['Mask'][index+i]*t for i in range(4))
    for suffix,pixels in blended.items():
        save_map(name,suffix,pixels)
    transitions.append(dict(name='M_'+name,first=first,second=second,stem=name))


def source_lod(name,index):
    directory='AncientForest' if name.startswith('SM_AF_') else 'FortressRuins'
    path=ROOT/f'ArtSource/Environment/{directory}/{name}.blend'
    with bpy.data.libraries.load(str(path),link=False) as (src,dst):
        dst.objects=[name+'_LOD'+str(index)]
    mesh=dst.objects[0].data.copy()
    return mesh


def material(kind):
    value=bpy.data.materials.get('M_BS_'+kind)
    if value:
        return value
    color,roughness=PALETTE[kind]
    value=bpy.data.materials.new('M_BS_'+kind)
    value.use_nodes=True
    value.diffuse_color=(*[decode(c) for c in rgb(color)],1)
    node=value.node_tree.nodes['Principled BSDF']
    node.inputs['Base Color'].default_value=value.diffuse_color
    node.inputs['Roughness'].default_value=roughness
    node.inputs['Metallic'].default_value=.7 if kind=='WarIron' else 0
    return value


def box(center,size,kind):
    bpy.ops.mesh.primitive_cube_add(size=1,location=center)
    obj=bpy.context.object
    obj.scale=size
    obj.data.materials.append(material(kind))
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    return obj


def asset_parts(kind,lod):
    parts=[]
    if kind=='StrataOutcrop':
        for i in range(3):
            mesh=source_lod('SM_AF_Rock02',lod)
            obj=bpy.data.objects.new('SharedRockStratum',mesh)
            bpy.context.scene.collection.objects.link(obj)
            for vertex in mesh.vertices:
                vertex.co.x*=1.4-i*.15
                vertex.co.y*=1.2-i*.1
                vertex.co.z=vertex.co.z*.6+i*.65
            mesh.materials.clear()
            mesh.materials.append(material('StrataStone'))
            parts.append(obj)
    elif kind=='ShatteredStandard':
        parts.extend([box((0,0,1.6),(.12,.12,3.2),'WarIron'),box((0,0,.15),(.6,.6,.3),'StrataStone')])
        # Opaque torn banner, deliberately neutral. No ownership/interaction marker.
        mesh=bpy.data.meshes.new('TornNeutralCloth')
        mesh.from_pydata([(0,0,2.9),(1.2,0,2.9),(1,0,2.6),(1.15,0,2.2),(.65,0,2.3),(0,0,2.1)],[],[(0,1,2,3,4,5)])
        mesh.materials.append(material('OldCloth'))
        obj=bpy.data.objects.new('NeutralWarRelic',mesh)
        bpy.context.scene.collection.objects.link(obj)
        parts.append(obj)
    elif kind=='CorruptedSpire':
        mesh=source_lod('SM_AF_Rock03',lod)
        obj=bpy.data.objects.new('SharedRockSpire',mesh)
        bpy.context.scene.collection.objects.link(obj)
        for vertex in mesh.vertices:
            h=vertex.co.z
            vertex.co.x=vertex.co.x*.48*(1-h*.22)+h*.11
            vertex.co.y*=.48*(1-h*.2)
            vertex.co.z*=2.4
        mesh.materials.clear()
        mesh.materials.append(material('CorruptedStone'))
        # Pale upper facets, never an emissive attack-shaped ground ring.
        mesh.materials.append(material('PaleVein'))
        for face in mesh.polygons:
            if face.index%7==0:
                face.material_index=1
        parts.append(obj)
    elif kind=='TwistedRoot':
        mesh=source_lod('SM_AF_DeadTree02',lod)
        obj=bpy.data.objects.new('SharedDeadwoodRoot',mesh)
        bpy.context.scene.collection.objects.link(obj)
        for vertex in mesh.vertices:
            h=vertex.co.z
            vertex.co.x+=math.sin(h*.8)*.55
            vertex.co.z*=.45
        mesh.materials.clear()
        mesh.materials.append(material('CorruptedStone'))
        parts.append(obj)
    elif kind=='TerrainTile':
        mesh=bpy.data.meshes.new('SharedTerrainSurface')
        mesh.from_pydata([(-2,-2,0),(2,-2,0),(2,2,0),(-2,2,0)],[],[(0,1,2,3)])
        mesh.materials.append(material('HighlandGravel'))
        obj=bpy.data.objects.new('SharedTerrainSurface',mesh)
        bpy.context.scene.collection.objects.link(obj)
        parts.append(obj)
    return parts


assets=[]
for kind in ('StrataOutcrop','ShatteredStandard','CorruptedSpire','TwistedRoot','TerrainTile'):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene
    scene.unit_settings.system='METRIC'
    scene.unit_settings.scale_length=1
    export=bpy.data.collections.new('EXPORT')
    scene.collection.children.link(export)
    name='SM_BS_'+kind
    root=bpy.data.objects.new(name,None)
    export.objects.link(root)
    lods=[]
    count=1 if kind=='TerrainTile' else 2 if kind=='ShatteredStandard' else 3
    for index in range(count):
        parts=asset_parts(kind,index)
        bpy.ops.object.select_all(action='DESELECT')
        for obj in parts:
            obj.hide_set(False)
            obj.select_set(True)
        bpy.context.view_layer.objects.active=parts[0]
        if len(parts)>1:
            bpy.ops.object.join()
        obj=bpy.context.object
        for collection in list(obj.users_collection):
            collection.objects.unlink(obj)
        export.objects.link(obj)
        obj.name=name+'_LOD'+str(index)
        scene.cursor.location=(0,0,0)
        bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
        obj.parent=root
        if kind=='ShatteredStandard' and index==0:
            mod=obj.modifiers.new('EdgeBevel','BEVEL')
            mod.width=.012
            mod.segments=1
            bpy.ops.object.modifier_apply(modifier=mod.name)
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bm=bmesh.from_edit_mesh(obj.data)
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        bmesh.update_edit_mesh(obj.data)
        bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.025)
        bpy.ops.mesh.quads_convert_to_tris(quad_method='BEAUTY',ngon_method='BEAUTY')
        bpy.ops.object.mode_set(mode='OBJECT')
        if kind=='TerrainTile':
            uv=obj.data.uv_layers.active
            for loop in obj.data.loops:
                vertex=obj.data.vertices[loop.vertex_index].co
                uv.data[loop.index].uv=(vertex.x/4+.5,vertex.y/4+.5)
        obj.hide_render=index>0
        obj.hide_set(index>0)
        lods.append(obj)
    # Markers support the existing importer calibration, even for symmetric rocks.
    for axis,loc in [('Front',(0,1,0)),('Right',(1,0,0)),('Up',(0,0,1))]:
        socket=bpy.data.objects.new('Socket_'+axis,None)
        export.objects.link(socket)
        socket.parent=root
        socket.location=loc
    triangles=[len(o.data.polygons) for o in lods]
    assert all(a>b for a,b in zip(triangles,triangles[1:])),(kind,triangles)
    assert all(p.area>1e-10 for o in lods for p in o.data.polygons),kind
    low=Vector(tuple(min(v.co[i] for v in lods[0].data.vertices) for i in range(3)))
    high=Vector(tuple(max(v.co[i] for v in lods[0].data.vertices) for i in range(3)))
    entry=dict(name=name,fbx=f'Assets/Art/Environment/Models/Biomes/{name}.fbx',triangles=triangles,
               minimumBlender=list(low),maximumBlender=list(high),dimensionsUnity=[high.x-low.x,high.z-low.z,high.y-low.y])
    for index,obj in enumerate(lods):
        entry[f'lod{index}Materials']=[m.name for m in obj.data.materials]
    assets.append(entry)
    bpy.ops.object.select_all(action='DESELECT')
    lods[0].select_set(True)
    bpy.context.view_layer.objects.active=lods[0]
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(name+'.blend')))
    export_collection(MODELS/(name+'.fbx'))

# Shared-material comparison board; not a proposed replacement map layout.
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
scene.unit_settings.system='METRIC'
scene.unit_settings.scale_length=1
meshes={}
names=['SM_AF_TreeAncient01','SM_AF_DeadTree01','SM_AF_Rock01','SM_AF_RuinWall01',
       'SM_AF_RuinPillar01','SM_AF_CampCairn01','SM_AF_GrassClump01','SM_FR_Crate']+[a['name'] for a in assets]
for name in names:
    directory='Biomes' if name.startswith('SM_BS_') else 'AncientForest' if name.startswith('SM_AF_') else 'FortressRuins'
    with bpy.data.libraries.load(str(ROOT/f'ArtSource/Environment/{directory}/{name}.blend'),link=False) as (src,dst):
        dst.objects=[name+'_LOD0']
    meshes[name]=dst.objects[0].data


def textured_material(stem):
    name='M_'+stem
    old=bpy.data.materials.get(name)
    if old:
        return old
    value=bpy.data.materials.new(name)
    value.use_nodes=True
    node=value.node_tree.nodes['Principled BSDF']
    node.inputs['Roughness'].default_value=.9
    directory='AncientForest' if stem.startswith('AF_') else 'Biomes'
    for suffix in ('BaseColor','Normal','Mask'):
        path=ROOT/f'Assets/Art/Textures/Environment/{directory}/T_{stem}_{suffix}.png'
        tex=value.node_tree.nodes.new('ShaderNodeTexImage')
        tex.image=bpy.data.images.load(str(path),check_existing=True)
        tex.image.colorspace_settings.name='sRGB' if suffix=='BaseColor' else 'Non-Color'
        tex.image.pack()
        if suffix=='BaseColor':
            value.node_tree.links.new(tex.outputs['Color'],node.inputs['Base Color'])
        elif suffix=='Normal':
            normal=value.node_tree.nodes.new('ShaderNodeNormalMap')
            normal.inputs['Strength'].default_value=.45
            value.node_tree.links.new(tex.outputs['Color'],normal.inputs['Color'])
            value.node_tree.links.new(normal.outputs['Normal'],node.inputs['Normal'])
    return value


placements=[]
def place(name,x,y,z=0,yaw=0,mat=None):
    obj=bpy.data.objects.new(name+f'_Board{len(placements):03}',meshes[name])
    scene.collection.objects.link(obj)
    obj.location=(x,y,z)
    obj.rotation_euler.z=math.radians(yaw)
    if mat:
        obj.data=obj.data.copy()
        obj.data.materials.clear()
        obj.data.materials.append(mat)
        for p in obj.data.polygons:
            p.material_index=0
    placements.append(dict(asset=name,position=[x,z,y],yaw=-yaw,material=mat.name if mat else ''))


centers={'Forest':(-8,8),'Highlands':(8,8),'Ruins':(-8,-8),'Corrupted':(8,-8)}
for biome,(cx,cy) in centers.items():
    for dx in (-4,0,4):
        for dy in (-4,0,4):
            place('SM_BS_TerrainTile',cx+dx,cy+dy,mat=textured_material(BIOMES[biome]))
    place('SM_AF_CampCairn01',cx+3,cy+3)
    place('SM_AF_Rock01',cx-3,cy+3)
    if biome=='Forest':
        for dx,dy in [(-4,-3),(4,1),(-2,3)]:
            place('SM_AF_TreeAncient01',cx+dx,cy+dy)
        place('SM_AF_GrassClump01',cx-2,cy-3)
    elif biome=='Highlands':
        place('SM_BS_StrataOutcrop',cx-3,cy+1)
        place('SM_BS_StrataOutcrop',cx+3,cy-2,yaw=90)
        place('SM_AF_DeadTree01',cx+4,cy+3)
    elif biome=='Ruins':
        place('SM_AF_RuinWall01',cx-3,cy+1)
        place('SM_AF_RuinPillar01',cx+3,cy+2)
        place('SM_BS_ShatteredStandard',cx-2,cy-3)
        place('SM_FR_Crate',cx+3,cy-3)
    else:
        for dx,dy in [(-3,1),(3,2)]:
            place('SM_BS_CorruptedSpire',cx+dx,cy+dy)
        place('SM_BS_TwistedRoot',cx-3,cy-3)
        place('SM_AF_RuinWall01',cx+3,cy-2,mat=material('CorruptedStone'))
    # Legible stage label only; no labels are added to the gameplay map.
    curve=bpy.data.curves.new(biome,'FONT')
    curve.body=biome.upper()
    curve.align_x='CENTER'
    curve.size=.65
    label=bpy.data.objects.new('Label_'+biome,curve)
    scene.collection.objects.link(label)
    label.location=(cx,cy-5.5,.04)

for index,item in enumerate(transitions):
    x=-10+index*5
    place('SM_BS_TerrainTile',x,20,.02,mat=textured_material(item['stem']))
    curve=bpy.data.curves.new('TransitionLabel','FONT')
    curve.body=item['first']+' > '+item['second']
    curve.align_x='CENTER'
    curve.size=.25
    label=bpy.data.objects.new(curve.body,curve)
    scene.collection.objects.link(label)
    label.location=(x,17.7,.05)

manifest=dict(version=1,milestone=57,blender=bpy.app.version_string,
              status='Offline biome-set candidate; Unity setup, map application and production acceptance pending',
              assets=assets,placements=placements,transitions=transitions,
              palette=[dict(name='M_BS_'+name,color=value[0],roughness=value[1],
                            stem='BS_'+name if name in ('HighlandGravel','BattlefieldPaving','CorruptedAsh') else '')
                       for name,value in PALETTE.items()],
              reuse=[name for name in names if not name.startswith('SM_BS_')],
              accents=[dict(biome='Forest',color='C6D0B7',intensity=.15,range=4),
                       dict(biome='Highlands',color='C2CED4',intensity=.15,range=4),
                       dict(biome='Ruins',color='D4BB91',intensity=.25,range=4),
                       dict(biome='Corrupted',color='B2A1BD',intensity=.15,range=4)])
(SOURCE/'Biomes.manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
scene.world=bpy.data.worlds.new('SharedOvercast')
scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.5,.57,.65,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.7
for item in manifest['accents']:
    cx,cy=centers[item['biome']]
    bpy.ops.object.light_add(type='POINT',location=(cx,cy,1.5))
    lamp=bpy.context.object
    lamp.name='AccentReference_'+item['biome']
    lamp.data.color=tuple(decode(c) for c in rgb(item['color']))
    lamp.data.energy=250*item['intensity']
    lamp.data.shadow_soft_size=.4
bpy.ops.object.light_add(type='AREA',location=(-4,-10,25))
key=bpy.context.object
key.data.energy=5000
key.data.shape='DISK'
key.data.size=18
key.rotation_euler=(Vector((0,0,0))-key.location).to_track_quat('-Z','Y').to_euler()
scene.render.engine='CYCLES'
scene.cycles.samples=20
scene.render.resolution_x=1600
scene.render.resolution_y=1100
scene.render.resolution_percentage=100
scene.view_settings.view_transform='Standard'
views=[]
for name,pos,target in [('Overview',(34,-44,38),(0,3,0))]+[(name,(cx+12,cy-16,12),(cx,cy,1)) for name,(cx,cy) in centers.items()]+[('Transitions',(3,12,14),(0,20,0))]:
    bpy.ops.object.camera_add(location=pos)
    camera=bpy.context.object
    camera.name=name
    camera.data.lens=43
    camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
    views.append(camera)
scene.camera=views[0]
scene['M57_Status']=manifest['status']
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Biomes_LookDev.blend'))
for camera in views:
    scene.camera=camera
    scene.render.filepath=str(PREVIEW/(camera.name+'.png'))
    bpy.ops.render.render(write_still=True)
print('[M57] PASS 5 shared kit extensions, 24 maps, four biome comparison panels and five transitions.',flush=True)
