"""Milestone 40: a small original weighted humanoid and deliberately simple placeholder actions."""
import math
from pathlib import Path
import sys
import bpy
from mathutils import Vector, Quaternion

sys.path.insert(0, str(Path(__file__).resolve().parent))
from export_fbx import export_collection

root = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
source = root / "ArtSource/Characters/SK_PrototypeHumanoid"
source.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"; scene.unit_settings.scale_length = 1
scene.render.fps = 30
collection = bpy.data.collections.new("EXPORT"); scene.collection.children.link(collection)
armature = bpy.data.armatures.new("PrototypeHumanoidSkeleton")
rig = bpy.data.objects.new("SK_PrototypeHumanoid", armature); collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig; rig.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
bones = {}


def bone(name, head, tail, parent=None):
    value = armature.edit_bones.new(name); value.head = head; value.tail = tail
    if parent: value.parent = bones[parent]
    bones[name] = value


bone("Root", (0,0,0), (0,0,.1))
bone("Hips", (0,0,.93), (0,0,1.07), "Root")
bone("Spine", (0,0,1.07), (0,0,1.22), "Hips")
bone("Chest", (0,0,1.22), (0,0,1.43), "Spine")
bone("Neck", (0,0,1.43), (0,0,1.56), "Chest")
bone("Head", (0,0,1.56), (0,0,1.81), "Neck")
for side, sign in (("Left", -1), ("Right", 1)):
    bone(side+"Shoulder", (.06*sign,0,1.42), (.22*sign,0,1.42), "Chest")
    bone(side+"UpperArm", (.22*sign,0,1.42), (.51*sign,0,1.42), side+"Shoulder")
    bone(side+"LowerArm", (.51*sign,0,1.42), (.78*sign,0,1.42), side+"UpperArm")
    bone(side+"Hand", (.78*sign,0,1.42), (.91*sign,0,1.42), side+"LowerArm")
    bone(side+"UpperLeg", (.11*sign,0,.93), (.11*sign,0,.53), "Hips")
    bone(side+"LowerLeg", (.11*sign,0,.53), (.11*sign,0,.12), side+"UpperLeg")
    bone(side+"Foot", (.11*sign,0,.12), (.11*sign,.16,.06), side+"LowerLeg")
    bone(side+"Toes", (.11*sign,.16,.06), (.11*sign,.25,.06), side+"Foot")
bpy.ops.object.mode_set(mode="OBJECT")
body_material = bpy.data.materials.new("M_PrototypeHumanoid"); body_material.diffuse_color = (.65,.7,.75,1)
dark_material = bpy.data.materials.new("M_PrototypeHumanoidDark"); dark_material.diffuse_color = (.06,.07,.085,1)
parts = []


def finish(obj, name, dark=False):
    obj.name = name+"_Surface"
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(body_material); obj.data.materials.append(dark_material)
    for face in obj.data.polygons: face.material_index = 1 if dark else 0
    group = obj.vertex_groups.new(name=name); group.add(list(range(len(obj.data.vertices))), 1, "REPLACE")
    for old in list(obj.users_collection): old.objects.unlink(obj)
    collection.objects.link(obj); parts.append(obj)


def box(name, center, size, dark=False):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    obj = bpy.context.object; obj.dimensions = size; finish(obj, name, dark)


def limb(name, radius, dark=False):
    data = armature.bones[name]; start, end = data.head_local.copy(), data.tail_local.copy()
    direction = end - start
    bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=radius, depth=direction.length*.95, location=(start+end)*.5)
    obj = bpy.context.object; obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    finish(obj, name, dark)


box("Hips", (0,0,.94), (.30,.21,.21))
box("Spine", (0,0,1.13), (.27,.20,.19))
box("Chest", (0,0,1.33), (.39,.23,.23))
limb("Neck", .055)
bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=1, location=(0,0,1.685))
bpy.context.object.scale = (.115,.105,.135); finish(bpy.context.object, "Head")
box("Head", (0,.107,1.68), (.045,.035,.045))  # +Y face/nose confirms facing.
for sign in (-1,1): box("Head", (.042*sign,.099,1.72), (.024,.018,.014), True)
for side, sign in (("Left", -1), ("Right", 1)):
    for suffix, radius in (("UpperArm", .064), ("LowerArm", .052), ("Hand", .048), ("UpperLeg", .085), ("LowerLeg", .065)):
        limb(side+suffix, radius, suffix == "Hand")
    box(side+"Foot", (.11*sign,.08,.06), (.14,.29,.12), True)
bpy.ops.object.select_all(action="DESELECT")
for obj in parts: obj.select_set(True)
bpy.context.view_layer.objects.active = parts[0]; bpy.ops.object.join()
mesh = bpy.context.object; mesh.name = "SK_PrototypeHumanoid_Surface"
scene.cursor.location = (0,0,0); bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(island_margin=.02); bpy.ops.object.mode_set(mode="OBJECT")
mesh.modifiers.new("ExportTriangulation", "TRIANGULATE")
skin = mesh.modifiers.new("HumanoidSkin", "ARMATURE"); skin.object = rig
mesh.parent = rig
rig.animation_data_create()


def rotation(name, x=0, y=0, z=0):
    pose = rig.pose.bones[name]; pose.rotation_mode = "QUATERNION"
    rest = pose.bone.matrix_local.to_quaternion()
    delta = Quaternion((0,0,1), math.radians(z)) @ Quaternion((1,0,0), math.radians(x)) @ Quaternion((0,1,0), math.radians(y))
    pose.rotation_quaternion = rest.inverted() @ delta @ rest


actions = {}
for kind, end in (("Idle",30),("Walk",30),("Run",20),("Backward",30),("StrafeLeft",30),("StrafeRight",30),
                  ("Jump",18),("Fall",30),("Land",12),("Attack",20),("LightAttack",20),("HeavyAttack",32),
                  ("Block",30),("HitReaction",12),("Stagger",24),("Dodge",20),("Death",30)):
    action = bpy.data.actions.new("AN_PrototypeHumanoid_"+kind); action.use_fake_user = True
    rig.animation_data.action = action
    for frame in range(1, end+2):
        t = (frame-1)/end; phase = math.sin(t*math.tau)
        for pose in rig.pose.bones:
            pose.rotation_mode = "QUATERNION"; pose.rotation_quaternion = Quaternion(); pose.location = (0,0,0)
        rotation("LeftUpperArm", y=-75); rotation("RightUpperArm", y=75)
        rotation("LeftLowerArm", x=10); rotation("RightLowerArm", x=10)
        if kind in ("Walk", "Run", "Backward"):
            if kind=="Backward":phase=-phase
            amplitude = 25 if kind == "Walk" else 42
            rotation("LeftUpperLeg", x=phase*amplitude); rotation("RightUpperLeg", x=-phase*amplitude)
            rotation("LeftLowerLeg", x=-max(0,-phase)*amplitude); rotation("RightLowerLeg", x=-max(0,phase)*amplitude)
            rotation("LeftUpperArm", x=-phase*amplitude*.6,y=-75); rotation("RightUpperArm",x=phase*amplitude*.6,y=75)
        elif kind == "Idle": rotation("Chest", x=math.sin(t*math.tau)*1.5)
        elif kind in ("Attack","LightAttack","HeavyAttack"):
            swing = math.sin(t*math.pi)
            rotation("Chest", z=20-40*t); rotation("RightUpperArm", x=45*swing,y=75-65*swing,z=35-100*t)
            rotation("RightLowerArm", x=35*swing)
            if kind=="HeavyAttack":
                rotation("Chest",x=-15*swing,z=35-70*t)
                rotation("RightUpperArm",x=85*swing,y=75-85*swing,z=55-130*t)
        elif kind in ("StrafeLeft","StrafeRight"):
            sign=-1 if kind=="StrafeLeft" else 1
            rotation("LeftUpperLeg",y=phase*18*sign);rotation("RightUpperLeg",y=-phase*18*sign)
            rotation("LeftLowerLeg",x=-max(0,-phase)*20);rotation("RightLowerLeg",x=-max(0,phase)*20)
            rotation("Chest",z=phase*3)
        elif kind in ("Jump","Fall","Land"):
            crouch=(math.sin(t*math.pi) if kind!="Fall" else .7)
            rotation("LeftUpperLeg",x=crouch*25);rotation("RightUpperLeg",x=crouch*25)
            rotation("LeftLowerLeg",x=-crouch*45);rotation("RightLowerLeg",x=-crouch*45)
            rotation("LeftUpperArm",x=-25*crouch,y=-55);rotation("RightUpperArm",x=-25*crouch,y=55)
            if kind=="Land":rig.pose.bones["Hips"].location=rig.pose.bones["Hips"].bone.matrix_local.to_quaternion().inverted()@Vector((0,0,-.12*crouch))
        elif kind=="Block":
            rotation("LeftUpperArm",x=45,y=-40);rotation("RightUpperArm",x=45,y=40)
            rotation("LeftLowerArm",x=75);rotation("RightLowerArm",x=75)
        elif kind in ("HitReaction","Stagger"):
            recoil=math.sin(min(1,t*2)*math.pi) if kind=="HitReaction" else .7+math.sin(t*math.tau)*.1
            rotation("Chest",x=20*recoil);rotation("Neck",x=10*recoil)
            rotation("LeftUpperArm",x=-15*recoil,y=-60);rotation("RightUpperArm",x=-15*recoil,y=60)
        elif kind == "Dodge": rotation("Chest",x=-35*math.sin(t*math.pi))
        elif kind == "Death":
            ease = min(1,t*1.5); rotation("Hips",x=-85*ease)
            rig.pose.bones["Hips"].location = rig.pose.bones["Hips"].bone.matrix_local.to_quaternion().inverted() @ Vector((0,.15*ease,-.72*ease))
        for pose in rig.pose.bones:
            pose.keyframe_insert("rotation_quaternion", frame=frame, group=pose.name)
            pose.keyframe_insert("location", frame=frame, group=pose.name)
    actions[kind] = (action,end)
rig.animation_data.action = None; scene.frame_start=1; scene.frame_end=31; scene.frame_set(1)
for pose in rig.pose.bones: pose.rotation_quaternion = Quaternion(); pose.location = (0,0,0)
bpy.ops.wm.save_as_mainfile(filepath=str(source/"SK_PrototypeHumanoid.blend"))
export_collection(root/"Assets/Art/Characters/Models/SK_PrototypeHumanoid.fbx", bake_animation=False)
for kind,(action,end) in actions.items():
    rig.animation_data.action = action; scene.frame_start=1; scene.frame_end=end+1; scene.frame_set(1)
    export_collection(root/("Assets/Art/Animations/PrototypeHumanoid/FBX/AN_PrototypeHumanoid_"+kind+".fbx"), animation_only=True, bake_animation=True)
print(f"[Humanoid] Exported weighted mannequin, {len(armature.bones)}-bone rig and {len(actions)} in-place placeholder actions.", flush=True)
