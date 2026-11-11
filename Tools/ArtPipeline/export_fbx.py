"""Run in Blender: blender source.blend --background --python export_fbx.py -- OUTPUT.fbx."""
from pathlib import Path
import sys
import bpy


def export_collection(output, collection_name="EXPORT", animation_only=False, bake_animation=None):
    scene = bpy.context.scene
    if scene.unit_settings.system != "METRIC" or abs(scene.unit_settings.scale_length - 1) > 1e-6:
        raise ValueError("Use Metric / Unit Scale 1.0 (one Blender unit = one meter)")
    collection = bpy.data.collections.get(collection_name)
    if collection is None:
        raise ValueError("Missing EXPORT collection")
    objects = list(collection.all_objects)
    if not objects or not any(obj.type == "MESH" for obj in objects):
        raise ValueError("EXPORT must contain a mesh")
    for obj in objects:
        if obj.type not in {"MESH", "ARMATURE", "EMPTY"}:
            raise ValueError(f"Unsupported export object: {obj.name}")
        if any(abs(value - 1) > 1e-5 for value in obj.scale):
            raise ValueError(f"Apply scale before export: {obj.name}")
        if obj.type == "MESH" and (not obj.data.uv_layers or not obj.data.materials):
            raise ValueError(f"Mesh needs UV0 and material slots: {obj.name}")
        if obj.parent is None and (obj.location.length > 1e-5 or any(abs(a) > 1e-5 for a in obj.rotation_euler)):
            raise ValueError(f"Root must have zero location/rotation: {obj.name}")
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        if animation_only and obj.type == "MESH":
            continue
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = next(obj for obj in objects if obj.type == "MESH")
    output = Path(output).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    rigged = any(obj.type == "ARMATURE" for obj in objects)
    if output.suffix.lower() != ".fbx" or not output.stem.startswith(("SM_", "SK_", "AN_")):
        raise ValueError("Use an FBX filename prefixed SM_, SK_, or AN_")
    if rigged and output.stem.startswith("SM_"):
        raise ValueError("A skinned/animated asset cannot use the static SM_ prefix")
    bpy.ops.export_scene.fbx(
        filepath=str(output), use_selection=True, object_types={"MESH", "ARMATURE", "EMPTY"},
        global_scale=1.0, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z", axis_up="Y", use_space_transform=True,
        bake_space_transform=not rigged, use_mesh_modifiers=True, mesh_smooth_type="FACE",
        use_tspace=True, add_leaf_bones=False, use_armature_deform_only=True,
        bake_anim=rigged if bake_animation is None else bake_animation, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
        bake_anim_simplify_factor=0.0, path_mode="STRIP", embed_textures=False,
    )
    print(f"[ArtPipeline] Exported {output}", flush=True)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if len(args) != 1:
        raise SystemExit("Usage: blender source.blend --background --python export_fbx.py -- OUTPUT.fbx")
    export_collection(args[0])
