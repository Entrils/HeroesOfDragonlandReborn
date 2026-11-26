"""Render the approach from the rebuilt Blender LOD0 sources, in metres."""
import math
import random
import sys
from pathlib import Path

import bpy
from mathutils import Vector, noise

root = Path(sys.argv[sys.argv.index('--') + 1]).resolve()
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
rng = random.Random(410)
sources = {}


def place(name, x, y, z=0, yaw=0, scale=1):
    if name not in sources:
        folder = {'FR': 'FortressRuins', 'AF': 'AncientForest', 'BS': 'Biomes'}[name[:2]]
        asset = 'SM_' + name
        source = root / 'ArtSource/Environment' / folder / (asset + '.blend')
        with bpy.data.libraries.load(str(source)) as (_, loaded):
            loaded.objects = [asset + '_LOD0']
        sources[name] = loaded.objects[0].data
    obj = bpy.data.objects.new(name, sources[name])
    scene.collection.objects.link(obj)
    obj.location = (x, y, z)
    obj.rotation_euler.z = math.radians(yaw)
    obj.scale = (scale,) * 3 if isinstance(scale, (int, float)) else scale
    return obj


def path_x(y):
    return 1.25 * math.sin((y - 8) * .17)


def ground(x, y):
    shoulder = min(1, max(0, (abs(x-path_x(y))-2)/4))
    return -.14 + (.10 + .42 * shoulder) * noise.noise(Vector((x*.18, y*.17, .52)))


# One continuous road. Its shoulders blend the two actual terrain surfaces.
vertices, faces, coordinates = [], [], []
for j in range(61):
    for i in range(61):
        x, y = (i-30)*1.4, (j-21)*1.4
        vertices.append((x, y, ground(x, y)))
        coordinates.append((x/3, y/3))
        if i and j:
            k = j*61+i
            faces.append((k-62, k-61, k, k-1))
mesh = bpy.data.meshes.new('Road and verges')
mesh.from_pydata(vertices, [], faces)
road = bpy.data.objects.new('Approach', mesh)
scene.collection.objects.link(road)
uv = mesh.uv_layers.new()
verge = mesh.attributes.new('verge', 'FLOAT', 'POINT')
for i, (x, y, z) in enumerate(vertices):
    verge.data[i].value = min(1, max(0, (abs(x-path_x(y)) - 1.65
                                      + noise.noise(Vector((x*.65, y*.65, 0))))/.95))
for loop in mesh.loops:
    uv.data[loop.index].uv = coordinates[loop.vertex_index]
ground_mat = bpy.data.materials.new('Mud to needle litter')
mesh.materials.append(ground_mat)
ground_mat.use_nodes = True
nodes = ground_mat.node_tree.nodes
link = ground_mat.node_tree.links.new
blend = nodes.new('ShaderNodeMixRGB')
mask = nodes.new('ShaderNodeAttribute')
mask.attribute_name = 'verge'
link(mask.outputs['Fac'], blend.inputs[0])
for i, name in enumerate(('DirtPath', 'ForestFloor')):
    tex = nodes.new('ShaderNodeTexImage')
    tex.image = bpy.data.images.load(str(root / 'Assets/Art/Textures/Environment/AncientForest'
                                         / ('T_AF_'+name+'_BaseColor.png')))
    link(tex.outputs['Color'], blend.inputs[i+1])
surface = nodes.get('Principled BSDF')
link(blend.outputs[0], surface.inputs['Base Color'])
bump = nodes.new('ShaderNodeBump')
bump.inputs['Strength'].default_value = .28
bump.inputs['Distance'].default_value = .052
link(blend.outputs[0], bump.inputs['Height'])
link(bump.outputs[0], surface.inputs['Normal'])
surface.inputs['Roughness'].default_value = .72

place('FR_Gate', 0, 8, -.06)
with bpy.data.libraries.load(str(root/'ArtSource/Environment/SM_FortressGate/SM_FortressGate.blend')) as (_, loaded):
    loaded.objects = ['FortressGate_TeamA']
doors = loaded.objects[0]
scene.collection.objects.link(doors)
doors.location = (0,8.12,-.08)
doors.rotation_euler.z = math.pi
doors.scale = (1.045,1,1.07)
for side in (-1, 1):
    for i in range(3):
        x = side*(6+4*i)
        place('FR_WallStraight', x, 8, -.1)
        place('FR_Battlement', x, 8, 2.90)
    place('FR_Tower', side*6.25, 8.9, -.10, scale=(1.03, 1.03, 1.13))
    place('FR_Tower', side*4, 19, -.08, scale=(1.1, 1.12, 1.65 if side<0 else 1.42))
    place('FR_Tower', side*13.5, 12, -.1, scale=(1,1,1.08))
place('FR_Arch', .5, 17.8)
place('FR_RuinedArch', -7.0, -.8, -.22, 28)
place('AF_RuinPillar01', 5.5, -1.6, -.17, -23)
place('FR_CollapsedPillar', -5.2, -6.1, -.19, -35)
for kind,x,y,scale,tilt in [('TreeAncient02',-4.9,-10.4,1.20,-.15),
                          ('DeadTree02',4.85,-12.5,1.16,.24),
                          ('TreeAncient01',7.3,-6.8,1.42,.09),
                          ('DeadTree01',-6.4,-2.0,1.33,-.13),
                          ('TreeAncient01',-7.2,-6.2,1.25,.13)]:
    trunk=place('AF_'+kind,x,y,ground(x,y)-.09,rng.uniform(-30,30),scale)
    trunk.rotation_euler.y=tilt
place('BS_ShatteredStandard',-3.8,4.7,-.13,37,1.12)
for i in range(285):
    y = rng.uniform(-21, 27)
    side = rng.choice((-1, 1))
    x = path_x(y) + side*rng.uniform(2.6, 21)
    if abs(x)<17 and y>6:
        continue
    if i % 9 == 0 and abs(x)>7:
        kind = rng.choice(('TreeAncient01','TreeAncient02','DeadTree01','DeadTree02'))
        scale = rng.uniform(.77, 1.48)
    elif i%6==0:
        kind = rng.choice(('Rock01','Rock02','Rock03','Stump01'))
        scale = rng.uniform(.28, 1.05)
    else:
        kind = rng.choice(('Bush01','Bush02','GrassClump01','GrassClump02'))
        scale = rng.uniform(.45, .94)
    place('AF_'+kind, x, y, ground(x,y)-.055, rng.uniform(0,360), scale)
for i in range(240):
    y = rng.uniform(-19, 12)
    x = path_x(y) + rng.choice((-1, 1))*rng.uniform(.68, 3.35)
    place('AF_Rock0'+str(i%3+1), x, y, ground(x,y)-.015, rng.uniform(0,360),
          (rng.uniform(.04,.23),rng.uniform(.04,.19),rng.uniform(.025,.115)))
for i in range(7):
    place('AF_DistantTreeline', (i-3)*12, rng.uniform(34,45), -.4,
          rng.randrange(-12,12), rng.uniform(.85,1.25))

scene.world = bpy.data.worlds.new('Borderwood night')
scene.world.use_nodes = True
nodes = scene.world.node_tree.nodes
link = scene.world.node_tree.links.new
coords = nodes.new('ShaderNodeTexCoord')
orientation = nodes.new('ShaderNodeVectorRotate')
orientation.rotation_type = 'Z_AXIS'
orientation.inputs['Angle'].default_value = math.pi
link(coords.outputs['Generated'], orientation.inputs['Vector'])
panorama = nodes.new('ShaderNodeTexEnvironment')
panorama.image = bpy.data.images.load(str(root/'Assets/Art/Textures/Environment/T_BorderwoodNight.exr'))
link(orientation.outputs['Vector'], panorama.inputs['Vector'])
# Approximate the battlefield's separate trilight fill on stone and timber.
fill = nodes.new('ShaderNodeMixRGB')
fill.blend_type = 'ADD'
fill.inputs[2].default_value = (.068,.086,.124,1)
light_path = nodes.new('ShaderNodeLightPath')
link(light_path.outputs['Is Diffuse Ray'], fill.inputs[0])
link(panorama.outputs['Color'], fill.inputs[1])
link(fill.outputs[0], nodes['Background'].inputs['Color'])

bpy.ops.object.light_add(type='SUN')
sun = bpy.context.object
sun.rotation_euler = Vector((.267,-.874,-.407)).to_track_quat('-Z','Y').to_euler()
sun.data.energy, sun.data.angle, sun.data.color = 1.15, .075, (.64,.77,1)
bpy.ops.mesh.primitive_cube_add(size=1, location=(0,18,8))
mist = bpy.context.object
mist.scale = (104,77,25)
fog = bpy.data.materials.new('Lowland mist')
fog.use_nodes = True
tree = fog.node_tree
tree.nodes.remove(tree.nodes['Principled BSDF'])
volume = tree.nodes.new('ShaderNodeVolumeScatter')
volume.inputs['Density'].default_value = .003
volume.inputs['Color'].default_value = (.71,.78,.85,1)
volume.inputs['Anisotropy'].default_value = .37
tree.links.new(volume.outputs[0], tree.nodes['Material Output'].inputs['Volume'])
mist.data.materials.append(fog)

bpy.ops.object.camera_add(location=(1.3,-20.5,1.72))
scene.camera = bpy.context.object
scene.camera.rotation_euler = (Vector((1.1,9,8.2))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
scene.camera.data.lens = 25
scene.render.engine = 'CYCLES'
scene.cycles.samples = 36
scene.cycles.use_denoising = True
scene.render.resolution_x, scene.render.resolution_y = 1440, 810
scene.render.resolution_percentage = 100
scene.render.filepath = str(root / 'Documentation/ArtDirection/BorderwoodApproach.png')
bpy.ops.render.render(write_still=True)
