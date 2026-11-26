"""M56 Ancient Forest candidate assets. Blender --background --python THIS -- ROOT.

Original deterministic geometry/textures, shared FBX exporter, no map/YAML edits.
Rebuilds only named AF source/export files. Do not run over hand-edited sources.
"""
import json
import math
import random
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector
from mathutils import noise

sys.path.insert(0, str(Path(__file__).resolve().parent))
from export_fbx import export_collection
from environment_mesh_names import preserve_mesh_names

ROOT = Path(sys.argv[sys.argv.index('--')+1]).resolve()
SOURCE = ROOT/'ArtSource/Environment/AncientForest'
MODELS = ROOT/'Assets/Art/Environment/Models/AncientForest'
TEXTURES = ROOT/'Assets/Art/Textures/Environment/AncientForest'
PREVIEW = ROOT/'Documentation/AncientForest'
for folder in (SOURCE, MODELS, TEXTURES, PREVIEW):
    folder.mkdir(parents=True, exist_ok=True)
PALETTE = {
    'Bark': ('726E65',.86), 'Leaves': ('475449',.9), 'LeavesLight': ('7A805E',.9),
    'Rock': ('747974',.87), 'ForestFloor': ('555749',.90), 'DirtPath': ('807364',.89),
    'Heartwood': ('826448',.85), 'Moss': ('586348',.93),
}


def linear(v):
    return v/12.92 if v <= .04045 else ((v+.055)/1.055)**2.4


def rgb(hexcode):
    return [int(hexcode[i:i+2],16)/255 for i in (0,2,4)]


def save_image(name, pixels, size=512, data=False):
    image = bpy.data.images.new(name, width=size, height=size, alpha=True)
    if data:
        image.colorspace_settings.name = 'Non-Color'
    image.pixels.foreach_set(pixels)
    image.filepath_raw = str(TEXTURES/(name+'.png'))
    image.file_format = 'PNG'
    image.save()
    bpy.data.images.remove(image)


def textures():
    """Periodic height/albedo fields: no baked lighting, +Y tangent normals, URP masks."""
    size = 512
    for kind in ('Bark','Rock','ForestFloor','DirtPath'):
        base = rgb(PALETTE[kind][0])
        heights = []
        colors = []
        for y in range(size):
            v = y/size
            for x in range(size):
                u = x/size
                # Sample noise on a torus: seamless in U/V without directional grids.
                torus=Vector(((3+math.cos(math.tau*v))*math.cos(math.tau*u),
                              (3+math.cos(math.tau*v))*math.sin(math.tau*u),math.sin(math.tau*v)))
                broad=noise.noise(torus*2)+.35*noise.noise(torus*5)
                fine=noise.noise(torus*27)
                if kind == 'Bark':
                    grooves = abs(math.sin(math.tau*(u*12+.21*broad+.09*math.sin(math.tau*v))))**8
                    h = .55-.34*grooves+.11*broad+.055*fine
                    tone = 1-.32*grooves+.17*broad+.06*fine
                elif kind == 'Rock':
                    fissure=max(0,1-abs(math.sin(math.tau*(u+2*v+.35*broad)))/.14)
                    h = .5+.14*broad+.055*fine-.13*fissure
                    tone = 1+.19*broad+.065*fine-.22*fissure
                else:
                    h = .5+.07*broad+.018*fine
                    tone = 1+.09*broad+.02*fine
                heights.append(h)
                # Image.save writes these generated sRGB image samples directly.
                # Keep sRGB swatches here; shader constants use linear() separately.
                colors.extend([max(0,min(1,c*tone)) for c in base]+[1])
        normal, mask = [], []
        for y in range(size):
            for x in range(size):
                dx = heights[y*size+(x+1)%size]-heights[y*size+(x-1)%size]
                dy = heights[((y+1)%size)*size+x]-heights[((y-1)%size)*size+x]
                n = Vector((-dx*5,-dy*5,1)).normalized()
                normal.extend((n.x*.5+.5,n.y*.5+.5,n.z*.5+.5,1))
                # R metallic, G AO (unoccluded), B reserved, A smoothness.
                mask.extend((0,1,0,max(.058,min(.19,1-PALETTE[kind][1]+.075*(heights[y*size+x]-.5)))))
        save_image('T_AF_'+kind+'_BaseColor',colors)
        save_image('T_AF_'+kind+'_Normal',normal,data=True)
        save_image('T_AF_'+kind+'_Mask',mask,data=True)
    mote = []
    size = 64
    for y in range(size):
        for x in range(size):
            radius = math.hypot((x+.5-size/2)/(size/2),(y+.5-size/2)/(size/2))
            mote.extend((1,1,1,max(0,1-radius)**2))
    save_image('T_AF_AmbientMote_BaseColor',mote,size)


def material(kind):
    if kind in M:
        return M[kind]
    color, roughness = PALETTE[kind]
    value = bpy.data.materials.new('M_AF_'+kind)
    value.use_nodes = True
    node = value.node_tree.nodes['Principled BSDF']
    value.diffuse_color = (*[linear(c) for c in rgb(color)],1)
    node.inputs['Base Color'].default_value = value.diffuse_color
    node.inputs['Roughness'].default_value = roughness
    if kind in ('Bark','Rock','ForestFloor','DirtPath'):
        for suffix, space in [('BaseColor','sRGB'),('Normal','Non-Color'),('Mask','Non-Color')]:
            tex = value.node_tree.nodes.new('ShaderNodeTexImage')
            tex.image = bpy.data.images.load(str(TEXTURES/f'T_AF_{kind}_{suffix}.png'),check_existing=True)
            tex.image.colorspace_settings.name = space
            tex.image.pack()
            if suffix == 'BaseColor':
                value.node_tree.links.new(tex.outputs['Color'],node.inputs['Base Color'])
            elif suffix == 'Normal':
                normal = value.node_tree.nodes.new('ShaderNodeNormalMap')
                normal.inputs['Strength'].default_value = .6
                value.node_tree.links.new(tex.outputs['Color'],normal.inputs['Color'])
                value.node_tree.links.new(normal.outputs['Normal'],node.inputs['Normal'])
    M[kind] = value
    return value


def mesh_object(name, vertices, faces, kind, smooth=False, uv=None):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices,[],faces)
    mesh.update()
    obj = bpy.data.objects.new(name,mesh)
    bpy.context.scene.collection.objects.link(obj)
    mesh.materials.append(material(kind))
    for polygon in mesh.polygons:
        polygon.use_smooth = smooth
    if uv:
        layer = mesh.uv_layers.new(name='UVMap')
        for p in mesh.polygons:
            for index in p.loop_indices:
                layer.data[index].uv = uv[mesh.loops[index].vertex_index]
    PARTS.append(obj)
    return obj


def tube(points, radii, sides=12, kind='Bark'):
    vertices, uv = [], []
    points = [Vector(p) for p in points]
    distance = 0
    for i,(point,radius) in enumerate(zip(points,radii)):
        tangent = (points[min(i+1,len(points)-1)]-points[max(0,i-1)]).normalized()
        axis = tangent.cross(Vector((0,1,0))).normalized()
        if axis.length < .1:
            axis = tangent.cross(Vector((1,0,0))).normalized()
        second = tangent.cross(axis).normalized()
        if i:
            distance += (point-points[i-1]).length
        for j in range(sides+1):
            angle = math.tau*j/sides
            r = radius*(1+.055*math.sin(angle*5+i*.6))
            vertices.append(point+r*(math.cos(angle)*axis+math.sin(angle)*second))
            uv.append((j/sides,distance/2))
    faces=[]
    for i in range(len(points)-1):
        for j in range(sides):
            a=i*(sides+1)+j
            faces.append((a,a+1,a+sides+2,a+sides+1))
    faces.extend([tuple(range(sides-1,-1,-1)),tuple((len(points)-1)*(sides+1)+j for j in range(sides))])
    return mesh_object('TaperedBranch',vertices,faces,kind,True,uv)


def stone(center, size, seed, subdivisions=2, kind='Rock'):
    rng=random.Random(seed)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions,radius=1,location=center)
    obj=bpy.context.object
    for vertex in obj.data.vertices:
        original=vertex.co.copy()
        factor=1+rng.uniform(-.12,.12) if subdivisions<=2 else (
            1+.18*noise.noise(original*2.7+Vector((seed,0,0)))+
            .038*noise.noise(original*9)+.025*math.sin(original.z*17+seed))
        vertex.co *= factor
        vertex.co.x *= size[0]/2
        vertex.co.y *= size[1]/2
        vertex.co.z *= size[2]/2
    obj.data.materials.append(material(kind))
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    PARTS.append(obj)
    return obj


def leaf(center, direction, width, length, kind='Leaves'):
    p=Vector(center)
    long=Vector(direction).normalized()*length
    side=long.cross(Vector((0,0,1))).normalized()*width
    if side.length < .001:
        side=Vector((width,0,0))
    # Opaque folded leaf blade: geometry defines silhouette, no alpha-card overdraw.
    ridge=Vector((0,0,.025))
    vertices=[p-long*.5,p-long*.23+side*.72,p+long*.12+side,
              p+long*.55-ridge*.8,p+long*.14-side*.82,p-long*.23-side*.65,p+ridge]
    return mesh_object('FoldedLeaf',vertices,[(i,(i+1)%6,6) for i in range(6)],kind)


def moss_top(obj):
    obj.data.materials.append(material('Moss'))
    for face in obj.data.polygons:
        if face.normal.z>.45:
            face.material_index=len(obj.data.materials)-1


def tree(seed, lod, dead=False):
    rng=random.Random(seed)
    sides=(24,12,5)[lod]
    height=(5.5 if seed==1 else 5.8) if not dead else 6.8
    points=[(.13*math.sin(i*.7+seed)-.13*math.sin(seed),.085*math.sin(i),i*height/8) for i in range(9)]
    if lod==2:
        points=[(.07*math.sin(i*.7),.045*math.sin(i),i*height/8) for i in range(9)]
    radii=[.405*(1-i/10)**1.3 for i in range(9)]
    tube(points,radii,sides)
    if lod<2:
        for j in range(7 if lod==0 else 4):
            a=j*2.399+seed
            radial=Vector((math.cos(a),math.sin(a),0))
            tube([radial*.20+Vector((0,0,.62)),radial*.39+Vector((0,0,.21)),
                  radial*.60+Vector((0,0,.075)),radial*.81+Vector((0,0,.021))],
                 [.108,.111,.060,.014],8)
    for i in range((8,6,4)[lod]):
        angle=i*2.399+seed*.4
        base=3.58+(i%4)*.39
        reach=1.16-.108*(i%4)
        end=Vector((math.cos(angle)*reach,math.sin(angle)*reach,base+.61+.16*(i%3)))
        origin=Vector(points[min(8,round(base/height*8))])
        origin.z=base
        elbow=origin.lerp(end,.44)+Vector((.09*math.sin(angle),-.09*math.cos(angle),-.18))
        if lod==2:
            base=3.65+(i%3)*.6
            end=Vector((math.cos(angle)*1.2,math.sin(angle)*1.2,base+.7))
            tube([(0,0,base),end*.5+Vector((0,0,base*.5+.15)),end],[.13,.075,.018],4)
        else:
            tube([origin,elbow,elbow.lerp(end,.62)+Vector((0,0,.10)),end],
                 [.123,.082,.047,.013],max(4,sides-3))
        if lod<2:
            radial=Vector((math.cos(angle),math.sin(angle),0))
            lateral=Vector((-radial.y,radial.x,0))
            for j in range(6 if lod==0 else 3):
                t=.43+.083*j
                origin=elbow.lerp(end,(t-.4)/.6)
                side=1 if j%2 else -1
                fork=origin+lateral*(.23*side)+Vector((0,0,.18))
                tip=fork+lateral*(side*.20)+radial*.18+Vector((0,0,.21+(j%3)*.11))
                tube([origin,fork,tip],[.05,.033,.009],7 if lod==0 else 5)
                if not dead:
                    for k in range(9 if lod==0 else 5):
                        fraction=(k+.35)/10
                        axis=lateral*(side*.6)+radial*(1 if k%2 else -1)
                        leaf(fork.lerp(tip,fraction)+axis*.08,axis+Vector((0,0,-.26)),
                             .066 if lod==0 else .09,.28,'LeavesLight' if (k+j)%5==0 else 'Leaves')
        if dead:
            continue
        if lod==2:
            stone(end+Vector((0,0,.22)),(1.2,1.2,1.05),seed*20+i,1,'Leaves')
        else:
            for j in range(42 if lod==0 else 18):
                theta=rng.uniform(0,math.tau)
                r=rng.uniform(.05,.58)
                position=end+Vector((math.cos(theta)*r,math.sin(theta)*r,rng.uniform(-.15,.65)))
                # Stay within old placeholder crown radius. Clear below 3.4m.
                position.x=max(-1.45,min(1.45,position.x))
                position.y=max(-1.45,min(1.45,position.y))
                leaf(position,(math.cos(theta),math.sin(theta),rng.uniform(-.3,.4)),
                     .09 if lod==0 else .13,.3 if lod==0 else .4,'LeavesLight' if j%5==0 else 'Leaves')


def build(kind,lod):
    rng=random.Random(128+sum(map(ord,kind)))
    if kind.startswith('TreeAncient'):
        tree(int(kind[-2:]),lod)
    elif kind.startswith('DeadTree'):
        tree(int(kind[-2:])+2,lod,True)
    elif kind.startswith('Rock'):
        index=int(kind[-2:])
        obj=stone((0,0,.5+index*.1),(1.5+index*.3,1.2+index*.2,1+index*.2),index,(4,2,1)[lod])
        low=min(v.co.z for v in obj.data.vertices)
        for vertex in obj.data.vertices:
            vertex.co.z-=low
    elif kind.startswith('Bush'):
        for i in range((120,45,12)[lod]):
            a=rng.uniform(0,math.tau)
            r=rng.uniform(0,.65)
            pos=(math.cos(a)*r,math.sin(a)*r,rng.uniform(.2,.6))
            leaf(pos,(math.cos(a),math.sin(a),.3),.1,.28,'LeavesLight' if i%6==0 else 'Leaves')
        tube([(0,0,0),(0,0,.5)],[.045,.02],(8,6,4)[lod],'Heartwood')
        if lod<2:
            for j in range(7 if lod==0 else 4):
                a=j*2.399
                tube([(0,0,0),(.16*math.cos(a),.16*math.sin(a),.24),
                      (.48*math.cos(a),.48*math.sin(a),.44)],[.029,.021,.008],5,'Heartwood')
    elif kind.startswith('GrassClump'):
        for i in range((88,28,8)[lod]):
            a=rng.uniform(0,math.tau)
            r=rng.uniform(0,.35)
            x,y=math.cos(a)*r,math.sin(a)*r
            h=rng.uniform(.08,.22)
            mesh_object('GrassBlade',[(x-.016,y,0),(x+.016,y,0),(x+.04,y+.02,h*.65),(x+.055,y+.02,h)],
                        [(0,1,2),(0,2,3)],'Moss')
    elif kind=='FallenLog01':
        tube([(-1.8,0,.37),(-.6,.04,.4),(.6,.06,.37),(1.8,.08,.34)],
             [.34,.37,.34,.28],(16,10,6)[lod])
        if lod<2:
            tube([(.64,.10,.47),(.76,.28,.66),(.89,.43,.83)],[.13,.089,.046],12)
            tube([(-.80,-.10,.45),(-.89,-.34,.52),(-1.01,-.50,.56)],[.09,.061,.024],10)
        tube([(-1.81,0,.37),(-1.83,0,.37)],[.29,.29],(16,10,6)[lod],'Heartwood')
    elif kind=='Stump01':
        if lod<2:
            for j in range(6):
                a=j*2.399
                tube([(.20*math.cos(a),.20*math.sin(a),.32),(.39*math.cos(a),.39*math.sin(a),.12),
                      (.56*math.cos(a),.56*math.sin(a),.02)],[.09,.074,.013],8)
        tube([(0,0,0),(.02,0,.3),(.04,0,.65)],[.4,.35,.31],(16,10,6)[lod])
        tube([(.04,0,.65),(.04,0,.66)],[.28,.28],(16,10,6)[lod],'Heartwood')
    elif kind=='CampCairn01':
        for i in range(3):
            stone((0,0,.18+i*.28),(.9-i*.15,.7-i*.1,.4),i+11,(3,1,1)[lod])
        # Three broad dark recess-like bars; neutral tier may overlay a UI icon later.
        for i in range(3 if lod==0 else 1):
            stone((0,.36,.15+i*.23),(.3,.08,.07),i,1,'Heartwood')
    elif kind=='RuinWall01':
        for row in range(4):
            for col in range(3 if row<2 else 2 if row==2 else 1):
                obj=stone((-1+col*.95+(.08 if row%2 else 0),0,.24+row*.43),
                          (1,.68,.5),row*7+col,(3,1,1)[lod])
                if row>=2:
                    moss_top(obj)
    elif kind=='RuinPillar01':
        for row in range(5):
            obj=stone((.025*math.sin(row),0,.22+row*.4),
                      (.85 if row==0 else .63,.8 if row==0 else .63,.5),row+50,(3,1,1)[lod])
            if row==4:
                moss_top(obj)
    elif kind in ('ForestFloorTile','DirtPathTile'):
        # Flat visual surfaces, no added traversal height or collider.
        half_width=2 if kind=='ForestFloorTile' else 1.6
        uv=[(x/12,y/12) for y in range(13) for x in range(13)]
        vertices=[((2*u-1)*half_width,4*v-2,.015*math.sin(math.pi*u)*math.sin(math.pi*v)*
                   noise.noise(Vector((u*7,v*7,1)))) for u,v in uv]
        faces=[(k,k+1,k+14,k+13) for y in range(12) for x in range(12) for k in [13*y+x]]
        mesh_object(kind,vertices,faces,'ForestFloor' if kind=='ForestFloorTile' else 'DirtPath',True,uv)
    elif kind=='DistantTreeline':
        for i in range((7,5,3)[lod]):
            x=(i-3)*2
            h=4.65+1.13*math.sin(i*2.39)
            tube([(x,0,0),(x+.07,0,h*.58),(x-.05,0,h)],[.19,.11,.02],6)
            for j in range((7,5,3)[lod]):
                t=j/(7,5,3)[lod]
                z=h*(.32+.61*t)
                radius=(1.36-.92*t)*(1+.09*math.cos(i+j))
                sides=(25,17,12)[lod]
                verts=[(x,0,z+.25),(x-.05,0,z+h*(.29-.06*t))]
                for k in range(sides):
                    a=k*math.tau/sides+i+j*.42
                    r=radius*(.82+.17*math.cos(k*2.67+j))
                    verts.append((x+r*math.cos(a),r*.78*math.sin(a),z-.20*(r/radius)+.12*math.sin(k*2.67)))
                faces=[tri for k in range(sides) for tri in
                       ((1,k+2,(k+1)%sides+2),(0,(k+1)%sides+2,k+2))]
                mesh_object('Wind-torn boughs',verts,faces,'Leaves')
    else:
        raise ValueError(kind)


def joined(name,parts,collection):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    if len(parts)>1:
        bpy.ops.object.join()
    obj=bpy.context.object
    obj.name=name
    for previous in list(obj.users_collection):
        previous.objects.unlink(obj)
    collection.objects.link(obj)
    bpy.context.scene.cursor.location=(0,0,0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    # Preserve the cylindrical bark UVs; unwrap only geometry without authored UVs.
    # Joining can create zero UVs for unwrapped leaves, so unwrap those faces only.
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='DESELECT')
    bm=bmesh.from_edit_mesh(obj.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001)
    uv=bm.loops.layers.uv.verify()
    for face in bm.faces:
        coords=[loop[uv].uv for loop in face.loops]
        area=abs(sum(coords[i].x*coords[(i+1)%len(coords)].y-
                     coords[(i+1)%len(coords)].x*coords[i].y for i in range(len(coords))))*.5
        if area<1e-8:
            face.select_set(True)
    bmesh.update_edit_mesh(obj.data)
    bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.02)
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.quads_convert_to_tris(quad_method='BEAUTY',ngon_method='BEAUTY')
    bpy.ops.object.mode_set(mode='OBJECT')
    return obj


KINDS=['TreeAncient01','TreeAncient02','DeadTree01','DeadTree02','Rock01','Rock02','Rock03',
       'Bush01','Bush02','GrassClump01','GrassClump02','FallenLog01','Stump01',
       'CampCairn01','RuinWall01','RuinPillar01','ForestFloorTile','DirtPathTile','DistantTreeline']
bpy.ops.wm.read_factory_settings(use_empty=True)
textures()
assets=[]
for kind in KINDS:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version=0
    scene=bpy.context.scene
    scene.unit_settings.system='METRIC'
    scene.unit_settings.scale_length=1
    M={}
    export=bpy.data.collections.new('EXPORT')
    scene.collection.children.link(export)
    name='SM_AF_'+kind
    root_obj=bpy.data.objects.new(name,None)
    export.objects.link(root_obj)
    count=1 if kind.endswith('Tile') else 2 if kind in ('CampCairn01','RuinWall01','RuinPillar01') else 3
    lods=[]
    for index in range(count):
        PARTS=[]
        build(kind,index)
        obj=joined(f'{name}_LOD{index}',PARTS,export)
        obj.parent=root_obj
        obj.hide_render=index>0
        obj.hide_set(index>0)
        lods.append(obj)
    collider='none' if kind.startswith(('Bush','Grass','Distant')) or kind.endswith('Tile') else 'capsule' if 'Tree' in kind else 'mesh'
    if collider=='mesh':
        collision=lods[-1].copy()
        collision.data=lods[-1].data.copy()
        collision.name='COL_'+kind
        export.objects.link(collision)
        collision.hide_render=True
        collision.hide_set(True)
    for axis,loc in [('Front',(0,1,0)),('Right',(1,0,0)),('Up',(0,0,1))]:
        marker=bpy.data.objects.new('Socket_'+axis,None)
        export.objects.link(marker)
        marker.parent=root_obj
        marker.location=loc
    vertices=lods[0].data.vertices
    low=Vector(tuple(min(v.co[i] for v in vertices) for i in range(3)))
    high=Vector(tuple(max(v.co[i] for v in vertices) for i in range(3)))
    triangles=[len(obj.data.polygons) for obj in lods]
    assert all(all(len(p.vertices)==3 and p.area>1e-10 for p in obj.data.polygons) for obj in lods),kind
    if count>1:
        assert all(a>b for a,b in zip(triangles,triangles[1:])),(kind,triangles)
    assert triangles[0]<14000,(kind,triangles)
    # Tree assets stay within the existing radius-1.7m canopy envelope horizontally.
    if kind.startswith('TreeAncient'):
        assert max(abs(low.x),abs(low.y),abs(high.x),abs(high.y))<=1.7,(kind,low,high)
    entry=dict(name=name,source=f'ArtSource/Environment/AncientForest/{name}.blend',
               fbx=f'Assets/Art/Environment/Models/AncientForest/{name}.fbx',
               dimensionsUnity=[round(high.x-low.x,5),round(high.z-low.z,5),round(high.y-low.y,5)],
               minimumBlender=list(low),maximumBlender=list(high),triangles=triangles,
               lodMaterials=[[m.name for m in o.data.materials] for o in lods],collider=collider)
    for index,obj in enumerate(lods):
        entry[f'lod{index}Materials']=[m.name for m in obj.data.materials]
    assets.append(entry)
    bpy.ops.object.select_all(action='DESELECT')
    lods[0].select_set(True)
    bpy.context.view_layer.objects.active=lods[0]
    preserve_mesh_names(name, export)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(name+'.blend')),compress=True)
    export_collection(MODELS/(name+'.fbx'))

# Independent look-development patch. It is not an alternate map layout.
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
scene.unit_settings.system='METRIC'
scene.unit_settings.scale_length=1
meshes={}
for kind in KINDS:
    name='SM_AF_'+kind
    with bpy.data.libraries.load(str(SOURCE/(name+'.blend')),link=False) as (src,dst):
        dst.objects=[name+'_LOD0']
    meshes[name]=dst.objects[0].data
for kind in ('Crate','Barrel'):
    name='SM_FR_'+kind
    path=ROOT/f'ArtSource/Environment/FortressRuins/{name}.blend'
    if not path.is_file():
        raise FileNotFoundError('Build M55 sources first: '+str(path))
    with bpy.data.libraries.load(str(path),link=False) as (src,dst):
        dst.objects=[name+'_LOD0']
    meshes[name]=dst.objects[0].data
placements=[]


def place(name,x,y,z=0,yaw=0,scale=1):
    obj=bpy.data.objects.new(f'{name}_Patch_{len(placements):03}',meshes[name])
    scene.collection.objects.link(obj)
    obj.location=(x,y,z)
    obj.rotation_euler.z=math.radians(yaw)
    obj.scale=(scale,)*3
    placements.append(dict(asset=name,position=[x,z,y],yaw=-yaw,scale=scale))
    return obj


for x in (-10,-6,-2,2,6,10):
    for y in (-10,-6,-2,2,6,10):
        place('SM_AF_ForestFloorTile',x,y)
for y in (-10,-6,-2,2,6,10):
    place('SM_AF_DirtPathTile',0,y,.012)
for y in range(14,51,4):
    for x in (-10,-6,-2,2,6,10):
        place('SM_AF_ForestFloorTile',x,y)
    place('SM_AF_DirtPathTile',0,y,.012)
tree_positions=[(-10,-7),(-7,-3),(-10,2),(-7,6),(-10,10),(-4,10),
                (5,-8),(9,-4),(10,2),(7,7),(10,10),(3,11)]
for i,(x,y) in enumerate(tree_positions):
    place('SM_AF_TreeAncient0'+str(i%2+1),x,y,yaw=i*37)
place('SM_AF_DeadTree01',-10,-11,yaw=30)
place('SM_AF_DeadTree02',11,6,yaw=65)
for i,(x,y) in enumerate([(-8,-9),(-8,4),(8,1),(9,8),(-5,9)]):
    place('SM_AF_Rock0'+str(i%3+1),x,y,yaw=i*43)
place('SM_AF_FallenLog01',-7,1,yaw=70)
place('SM_AF_Stump01',8,-6)
place('SM_AF_RuinWall01',-6,8)
place('SM_AF_RuinPillar01',-9,8)
place('SM_AF_CampCairn01',4,6)
place('SM_FR_Crate',5.5,6)
place('SM_FR_Barrel',6.5,6)
rng=random.Random(56)
for i in range(110):
    x,y=rng.uniform(-11,11),rng.uniform(-11,11)
    # Path half-width 1.6m plus a 1m shoulder; camp clearing remains sparse.
    if abs(x)<3.4 or math.hypot(x-4,y-4)<4:
        continue
    name='SM_AF_Bush0'+str(i%2+1) if i%5==0 else 'SM_AF_GrassClump0'+str(i%2+1)
    place(name,x,y,yaw=rng.uniform(0,360))
place('SM_AF_DistantTreeline',0,50)

manifest=dict(version=1,milestone=56,blender=bpy.app.version_string,
              status='Art candidate; Unity biome replacement, prefabs, performance and gameplay acceptance pending',
              assets=assets,placements=placements,
              palette=[dict(name='M_AF_'+name,color=hexcode,roughness=roughness,
                            textured=name in ('Bark','Rock','ForestFloor','DirtPath'))
                       for name,(hexcode,roughness) in PALETTE.items()],
              ambient=dict(maxParticles=16,emissionPerSecond=2,lifetimeSeconds=6),
              reuse=['SM_FR_Crate','SM_FR_Barrel'])
(SOURCE/'AncientForest.manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
scene.world=bpy.data.worlds.new('OvercastFill')
scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.24,.30,.38,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.48
bpy.ops.object.light_add(type='AREA',location=(-4,-8,16))
key=bpy.context.object
key.data.energy=3200
key.data.color=(1,.83,.64)
key.data.shape='DISK'
key.data.size=12
key.rotation_euler=(Vector((0,3,0))-key.location).to_track_quat('-Z','Y').to_euler()
scene.render.engine='CYCLES'
scene.cycles.samples=24
scene.render.resolution_x=1600
scene.render.resolution_y=1000
scene.render.resolution_percentage=100
scene.view_settings.view_transform='Standard'
views=[]
for name,pos,target in [('Overview',(29,-34,25),(0,2,1)),('PathView',(0,-11,2.1),(0,5,1.3))]:
    bpy.ops.object.camera_add(location=pos)
    camera=bpy.context.object
    camera.name=name
    camera.data.lens_unit='FOV'
    camera.data.angle=math.radians(60)
    camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
    views.append(camera)
scene.camera=views[0]
scene['M56_Status']=manifest['status']
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'AncientForest_LookDev.blend'),compress=True)
for camera in views:
    scene.camera=camera
    scene.render.filepath=str(PREVIEW/(camera.name+'.png'))
    bpy.ops.render.render(write_still=True)
print(f'[M56] Exported {len(assets)} AF assets, 13 texture maps and forest look-development patch.',flush=True)
