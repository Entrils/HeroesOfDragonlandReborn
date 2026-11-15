"""M54 offline look board; run Blender --background --python THIS_FILE -- PROJECT_ROOT.

Reads existing sources; writes only ArtSource/Benchmarks/VisualDirection and
Documentation/ArtDirection. Never exports/replaces gameplay or Unity assets.
"""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def material(name, color, metallic=0.0, roughness=0.8):
    # Design swatches are sRGB; Blender shader inputs are scene-linear.
    rgb = [int(color[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    linear = [v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in rgb]
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*linear, 1)
    mat.use_nodes = True
    node = mat.node_tree.nodes.get('Principled BSDF')
    node.inputs['Base Color'].default_value = (*linear, 1)
    node.inputs['Metallic'].default_value = metallic
    node.inputs['Roughness'].default_value = roughness
    return mat


def box(name, pos, size, mat):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    obj = bpy.context.object
    obj.name = name
    obj.scale = size
    obj.data.materials.append(mat)
    return obj


def text(label, pos, size=0.3):
    curve = bpy.data.curves.new(label, 'FONT')
    curve.body = label
    curve.align_x = 'CENTER'
    curve.size = size
    obj = bpy.data.objects.new(label, curve)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = pos
    obj.rotation_euler = (math.pi / 2, 0, 0)
    obj.data.materials.append(M['bone'])
    return obj


def line(name, points, mat, width=0.035):
    data = bpy.data.curves.new(name, 'CURVE')
    data.dimensions = '3D'
    data.bevel_depth = width
    data.bevel_resolution = 0
    spline = data.splines.new('POLY')
    spline.points.add(len(points) - 1)
    for point, xyz in zip(spline.points, points):
        point.co = (*xyz, 1)
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(mat)
    return obj


def source_meshes(path):
    if not path.is_file() or path.read_bytes()[:7] != b'BLENDER':
        raise RuntimeError(f'Missing Blender source (check Git LFS): {path}')
    with bpy.data.libraries.load(str(path), link=False) as (source, target):
        if 'EXPORT' not in source.collections:
            raise RuntimeError(f'No EXPORT collection: {path}')
        target.collections = ['EXPORT']
    collection = target.collections[0]
    bpy.context.scene.collection.children.link(collection)
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    meshes = []
    for obj in collection.all_objects:
        if obj.type == 'MESH':
            mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(depsgraph))
            mesh.transform(obj.matrix_world)
            meshes.append(mesh)
    for obj in list(collection.all_objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.collections.remove(collection)
    if not meshes:
        raise RuntimeError(f'No meshes in {path}')
    return meshes


def instance(meshes, name, pos, mat, scale=1):
    for index, mesh in enumerate(meshes):
        obj = bpy.data.objects.new(f'{name}_{index}', mesh.copy())
        bpy.context.scene.collection.objects.link(obj)
        obj.location = pos
        obj.scale = (scale,) * 3
        obj.data.materials.clear()
        obj.data.materials.append(mat)
        for polygon in obj.data.polygons:
            polygon.material_index = 0


def crest(x, y, z, team):
    if team == 'A':
        for offset in (-0.14, 0.14):
            line('A_Chevron', [(x-.18, y, z+offset), (x, y, z+.2+offset),
                              (x+.18, y, z+offset)], M['bone'], .025)
    else:
        for offset in (-.17, 0, .17):
            box('B_ThreeBars', (x, y, z+offset), (.38, .025, .065), M['bone'])


def camera(name, pos, target):
    data = bpy.data.cameras.new(name)
    data.lens_unit = 'FOV'
    data.angle = math.radians(60)
    data.clip_end = 200
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = pos
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()
    return obj


root = Path(sys.argv[sys.argv.index('--') + 1]).resolve()
out = root / 'ArtSource/Benchmarks/VisualDirection'
previews = root / 'Documentation/ArtDirection'
out.mkdir(parents=True, exist_ok=True)
previews.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
scene.render.engine = 'CYCLES'
scene.cycles.samples = 16
scene.render.resolution_x = 1280
scene.render.resolution_y = 720
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'None'
scene.view_settings.exposure = 0
scene.world = bpy.data.worlds.new('OvercastFill')
scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.5, .57, .65, 1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value = .65
M = {name: material('M54_' + name, color) for name, color in {
    'ground': '626B70', 'stone': '97988B', 'wood': '65594B',
    'A': '478DD9', 'B': 'D96B52', 'bone': 'C8B994',
    'brass': 'D6B66F', 'danger': 'FF913D', 'dark': '252B30',
}.items()}
humanoid = source_meshes(root / 'ArtSource/Characters/SK_PrototypeHumanoid/SK_PrototypeHumanoid.blend')
crate = source_meshes(root / 'ArtSource/Environment/SM_TestCrate/SM_TestCrate.blend')
box('LowNoiseGround', (0, 5, -.15), (28, 24, .3), M['ground'])
for x, team in ((-4, 'A'), (4, 'B')):
    instance(humanoid, 'ExistingHumanoid_' + team, (x, 1, 0), M['stone'])
    # Front and rear heraldry; geometry only, not new armor or team gameplay.
    for y in (.82, 1.18):
        box('TeamCloth_' + team, (x, y, 1.18), (.44, .025, .52), M[team])
        crest(x, y-.025 if y < 1 else y+.025, 1.18, team)
    text('TEAM ' + team, (x, .65, 2.2), .32)
    for dx in (-1.8, 1.8):
        box('GatePier_' + team, (x+dx, 6, 1.8), (1, 1, 3.6), M['stone'])
    box('GateLintel_' + team, (x, 6, 3.7), (4.6, 1, .6), M['stone'])
    box('GateBanner_' + team, (x, 5.45, 3.3), (.8, .04, .9), M[team])
    crest(x, 5.4, 3.3, team)
instance(humanoid, 'NeutralKnight', (0, 4, 0), M['bone'])
text('NEUTRAL', (0, 3.7, 2.2), .26)
instance(humanoid, 'StoneWarden_STANDIN', (0, 11, 0), M['stone'], 1.7)
for dx in (-.35, 0, .35):
    box('WardenCrown', (dx, 11, 3.1), (.18, .35, .4), M['bone'])
text('WARDEN / SCALE STAND-IN', (0, 10.6, 3.6), .3)
instance(crate, 'ExistingTestCrate_1m', (-8, 1, 0), M['wood'])
text('CRATE / 1m', (-8, .6, 1.5), .23)
box('ChestBase_STANDIN', (8, 1, .4), (1.15, .75, .8), M['wood'])
bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, location=(8, 1, .8))
lid = bpy.context.object
lid.name = 'ChestRoundedLid_STANDIN'
lid.scale = (.58, .38, .32)
lid.data.materials.append(M['wood'])
for x in (7.52, 8.48):
    box('ChestBrassBand', (x, .61, .55), (.09, .035, .9), M['brass'])
box('ChestLock', (8, .58, .7), (.22, .08, .25), M['brass'])
text('CHEST / INTERACT', (8, .45, 1.55), .23)
for tier, x in enumerate((-11, -9, -7), 1):
    box('CampCairn', (x, 7, .8), (.85, .75, 1.6), M['stone'])
    for mark in range(tier):
        box('TierNotch', (x, 6.6, .7+mark*.25), (.5, .04, .09), M['dark'])
    text('T' + str(tier), (x, 6.5, 1.9), .26)
for name, x, sweep in (('HOSTILE CIRCLE', -2, 360), ('HOSTILE SECTOR', 2, 70)):
    points = [(x + 1.3*math.sin(math.radians(a)), -1 + 1.3*math.cos(math.radians(a)), .04)
              for a in range(-sweep//2, sweep//2+1, 2)]
    if sweep < 360:
        points = [(x, -1, .04)] + points + [(x, -1, .04)]
    line(name + '_Backing', points, M['dark'], .065)
    line(name + '_Edge', [(a,b,c+.04) for a,b,c in points], M['danger'], .032)
    for a in range(-sweep//2, sweep//2, 20):
        angle = math.radians(a)
        line('DangerTooth', [(x+1.3*math.sin(angle), -1+1.3*math.cos(angle), .09),
                            (x+1.5*math.sin(angle), -1+1.5*math.cos(angle), .09)], M['danger'], .025)
    text('! ' + name, (x, -2.7, .25), .2)
text('M54 / WEATHERED DRAGONLAND / PLACEHOLDER LOOK BOARD', (0, 13, 4.5), .45)
bpy.ops.object.light_add(type='AREA', location=(-6, -4, 12))
key = bpy.context.object
key.name = 'BroadOvercastKey'
key.data.energy = 2400
key.data.shape = 'DISK'
key.data.size = 10
key.rotation_euler = (Vector((0, 4, 0))-key.location).to_track_quat('-Z', 'Y').to_euler()
views = [camera('Overview', (18, -24, 18), (0, 4, 1)),
         camera('Combat', (-4, -4, 2.1), (-4, 4, 1.2)),
         camera('Distance', (-9, -23, 2.1), (-9, 7, 1))]
scene.camera = views[0]
scene['M54_Status'] = 'Offline visual fixture only. Unity MCP scene and gameplay acceptance pending.'
scene['M54_SourceReuse'] = 'Existing humanoid and crate evaluated meshes; source files unchanged.'
bpy.ops.wm.save_as_mainfile(filepath=str(out / 'VisualDirectionBenchmark.blend'))
for view in views:
    scene.camera = view
    scene.render.filepath = str(previews / (view.name + '.png'))
    bpy.ops.render.render(write_still=True)
    # Save a grayscale render without modifying materials or depending on the
    # compositor API (which differs between Blender 4.5 and 5.x).
    scene.render.image_settings.color_mode = 'BW'
    scene.render.filepath = str(previews / (view.name + '_Grayscale.png'))
    bpy.ops.render.render(write_still=True)
    scene.render.image_settings.color_mode = 'RGB'
print('[M54] Saved offline benchmark and six review images; Unity acceptance pending.', flush=True)
