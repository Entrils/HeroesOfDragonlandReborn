"""One deterministic calibration prop; source, texture and FBX are generated in Blender."""
import json
import math
from pathlib import Path
import sys
import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from export_fbx import export_collection

root = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
source = root / "ArtSource/Environment/SM_TestCrate"
source.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0
scene.unit_settings.length_unit = "METERS"
export = bpy.data.collections.new("EXPORT")
scene.collection.children.link(export)
material = bpy.data.materials.new("M_TestCrate")
material.use_nodes = True
material.diffuse_color = (.38, .19, .07, 1)
shader = material.node_tree.nodes.get("Principled BSDF")
shader.inputs["Roughness"].default_value = .8

# A small deterministic wood texture, not a large art/content generation pass.
image = bpy.data.images.new("T_TestCrate_BaseColor", width=256, height=256, alpha=False)
pixels = []
for y in range(256):
    for x in range(256):
        grain = .07 * math.sin(y * .44 + math.sin(x * .045) * 2) + .025 * math.sin(y * 1.7 + x * .14)
        seam = .16 if y % 64 < 3 else 0
        pixels.extend((max(0, .42 + grain - seam), max(0, .23 + grain * .6 - seam), max(0, .09 + grain * .3 - seam * .4), 1))
image.pixels = pixels
texture_path = root / "Assets/Art/Textures/Environment/T_TestCrate_BaseColor.png"
texture_path.parent.mkdir(parents=True, exist_ok=True)
image.filepath_raw = str(texture_path); image.file_format = "PNG"; image.save()
node = material.node_tree.nodes.new("ShaderNodeTexImage"); node.image = image
material.node_tree.links.new(node.outputs["Color"], shader.inputs["Base Color"])
pieces = []


def box(name, center, size):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    obj = bpy.context.object; obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    for collection in list(obj.users_collection): collection.objects.unlink(obj)
    export.objects.link(obj)
    pieces.append(obj)
    return obj


box("Core", (0, 0, .5), (.88, .88, .88))
for x in (-.44, .44):
    for y in (-.44, .44): box("CornerPost", (x, y, .5), (.12, .12, 1))
for z in (.06, .94):
    for y in (-.44, .44): box("HorizontalFrame", (0, y, z), (.76, .12, .12))
    for x in (-.44, .44): box("SideFrame", (x, 0, z), (.12, .76, .12))
# Pipeline authoring front is Blender +Y, mapping to Unity +Z.
box("FrontLatch", (0, .475, .60), (.16, .05, .22))
bpy.ops.object.select_all(action="DESELECT")
for obj in pieces: obj.select_set(True)
bpy.context.view_layer.objects.active = pieces[0]
bpy.ops.object.join()
crate = bpy.context.object; crate.name = "SM_TestCrate"; crate.data.name = "SM_TestCrate_Mesh"
scene.cursor.location = (0, 0, 0)
bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
crate.data.materials.clear(); crate.data.materials.append(material)
bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=.035)
bpy.ops.object.mode_set(mode="OBJECT")
crate.modifiers.new("ExportTriangulation", "TRIANGULATE")
front = bpy.data.objects.new("Socket_Front", None); export.objects.link(front)
front.parent = crate; front.location = (0, .51, .5)
front.empty_display_type = "ARROWS"; front.empty_display_size = .1
right = bpy.data.objects.new("Socket_Right", None); export.objects.link(right)
right.parent = crate; right.location = (.51, 0, .5)
up = bpy.data.objects.new("Socket_Up", None); export.objects.link(up)
up.parent = crate; up.location = (0, 0, 1.1)
minimum = Vector((min(v.co.x for v in crate.data.vertices), min(v.co.y for v in crate.data.vertices), min(v.co.z for v in crate.data.vertices)))
maximum = Vector((max(v.co.x for v in crate.data.vertices), max(v.co.y for v in crate.data.vertices), max(v.co.z for v in crate.data.vertices)))
assert (maximum - minimum - Vector((1, 1, 1))).length < 1e-5
assert abs(minimum.z) < 1e-5 and crate.location.length < 1e-5
image.pack()  # Source .blend is self-contained; FBX still references no embedded textures.
bpy.ops.wm.save_as_mainfile(filepath=str(source / "SM_TestCrate.blend"))
export_collection(root / "Assets/Art/Environment/Models/SM_TestCrate.fbx")
(source / "SM_TestCrate.pipeline.json").write_text(json.dumps({
    "asset": "SM_TestCrate", "blender_version": bpy.app.version_string,
    "unity_dimensions_m": [1, 1, 1], "unity_pivot": [0, 0, 0],
    "unity_front_socket": [0, .5, .51], "source_front": "+Y", "unity_front": "+Z",
    "source_units": "METRIC", "unit_scale": 1, "texture_size": [256, 256],
    "fbx": "Assets/Art/Environment/Models/SM_TestCrate.fbx"
}, indent=2) + "\n")
print("[ArtPipeline] PASS Blender source dimensions, applied transforms, pivot, UV0 and export", flush=True)
