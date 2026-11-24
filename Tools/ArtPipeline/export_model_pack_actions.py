"""Export the reviewed Actions from one model-pack source without modifying the blend file."""
from pathlib import Path
import sys

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
from export_fbx import export_collection

source = Path(bpy.data.filepath)
model = source.stem.removeprefix("SK_")
rig, = [obj for obj in bpy.data.collections["EXPORT"].all_objects if obj.type == "ARMATURE"]
track = rig.animation_data_create()
for nla_track in track.nla_tracks:
    nla_track.mute = True
scene = bpy.context.scene
review_actions = {
    "Neutral Archer - review poses": "AN_NeutralArcher_Draw",
    "Neutral Mage - review poses": "AN_NeutralMage_Cast",
    "Sparring guard - tutorial windup": "AN_SparringGuard_Attack",
}
exported = 0
for action in list(bpy.data.actions):
    name = review_actions.get(action.name, action.name)
    if not name.startswith(f"AN_{model}_"):
        continue
    track.action = action
    if len(action.slots) == 1:
        track.action_slot = action.slots[0]
    scene.frame_start, scene.frame_end = (int(frame) for frame in action.frame_range)
    scene.frame_end = max(scene.frame_start + 1, scene.frame_end)
    scene.frame_set(scene.frame_start)
    export_collection(Path("Assets/Art/Animations/ModelPack") / model / (name + ".fbx"),
                      animation_only=True, bake_animation=True)
    exported += 1
if not exported:
    raise ValueError(f"No matching Actions in {source}")
