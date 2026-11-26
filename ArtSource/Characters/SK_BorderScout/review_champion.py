"""Render neutral equipment and base outfit references from the Blender master."""
from pathlib import Path
import sys
import bpy
from mathutils import Vector

scene = bpy.context.scene
camera = scene.camera
rig = bpy.data.objects["SK_PrototypeHumanoid"]
armor = bpy.data.collections["Fitted rigid equipment"]
scene.cycles.samples = 32
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
camera.data.type = "ORTHO"

for name, pos, target, scale, action, equipped in (
    ("Champion", (1.9,3.7,1.60), (0,0,.93), 2.08, "Idle", False),
    ("ChampionPortrait", (.42,1.5,1.78), (0,.02,1.57), .58, "Idle", False),
    ("ChampionEquipped", (-1.35,3.4,1.4), (0,0,.95), 2.08, "Block", True),
    ("ChampionRear", (1.4,-3.4,1.44), (0,0,.93), 2.08, "Run", False),
):
    if "--fbx-only" in sys.argv:
        break
    armor.hide_render = not equipped
    bpy.data.collections["Pattern • Hair"].hide_render = equipped
    rig.animation_data.action = bpy.data.actions["AN_PrototypeHumanoid_"+action]
    scene.frame_set(9)
    camera.location = pos
    camera.rotation_euler = (Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.ortho_scale = scale
    scene.render.resolution_x,scene.render.resolution_y = (840,960)
    scene.render.filepath = str(Path(bpy.data.filepath).parent/"Review"/(name+".png"))
    bpy.ops.render.render(write_still=True)

# Inspect the reduced shipping mesh using the source materials and studio.
# Transfer deformation from rest space so FBX bone-axis conversion is included.
master = Path(bpy.data.filepath)
model = master.parents[3]/"Assets/Art/Characters/Models/SK_BorderScout.fbx"
source_materials = {m.name:m for m in bpy.data.materials}
before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=str(model))
imported = set(bpy.data.objects)-before
game_rig = next(obj for obj in imported if obj.type=="ARMATURE")
for obj in imported:
    if obj.type!="MESH":
        continue
    obj.hide_render = obj.name.endswith("Covered")
    for slot in obj.material_slots:
        slot.material = source_materials[slot.material.name.split(".")[0]]
bpy.data.collections["EXPORT"].hide_render = True
armor.hide_render = True
rig.animation_data.action = bpy.data.actions["AN_PrototypeHumanoid_Idle"]
scene.frame_set(9)
for bone in game_rig.pose.bones:
    src = rig.pose.bones[bone.name]
    delta = rig.matrix_world @ src.matrix @ src.bone.matrix_local.inverted() @ rig.matrix_world.inverted()
    bone.matrix = game_rig.matrix_world.inverted() @ delta @ game_rig.matrix_world @ bone.bone.matrix_local
    bpy.context.view_layer.update()
camera.location = (-1.04,2.6,1.44)
camera.rotation_euler = (Vector((0,.02,1.37))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.ortho_scale = 1.16
scene.render.resolution_x,scene.render.resolution_y = (960,1024)
bpy.data.objects["Fill"].data.energy *= .45
bpy.data.objects["Overcast rim"].data.energy *= 1.35
scene.render.filepath = str(master.parent/"Review/ChampionFBX.png")
bpy.ops.render.render(write_still=True)
