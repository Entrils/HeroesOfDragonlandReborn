"""Run in Blender on SK_BorderScout.blend, from any directory.

Consolidates the editable garment patterns in memory. The .blend is never saved.
"""
import sys
from collections import defaultdict
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix

source = Path(bpy.data.filepath).resolve()
if source.name != "SK_BorderScout.blend":
    raise RuntimeError("Open SK_BorderScout.blend before running this exporter.")
project = source.parents[3]
sys.path.insert(0, str(project / "Tools/ArtPipeline"))
from export_fbx import export_collection

collection = bpy.data.collections["EXPORT"]
rig = next(obj for obj in collection.all_objects if obj.type == "ARMATURE")
rig.hide_set(False)
rig.animation_data_clear()
for bone in rig.pose.bones:
    bone.matrix_basis = Matrix.Identity(4)
bpy.context.view_layer.update()


def select(objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]


def evaluated_grips(body):
    """Sample the two authored targets after the same subdivision as the exported body."""
    for modifier in list(body.modifiers):
        if modifier.type in {"ARMATURE", "MASK"}:
            body.modifiers.remove(modifier)
    keys = body.data.shape_keys
    if not keys:
        return {}
    targets = {}
    for key in keys.key_blocks[1:]:
        key.value = 0
    for name in ("Grip_Left", "Grip_Right"):
        key = keys.key_blocks.get(name)
        if key is None:
            continue
        key.value = 1
        bpy.context.view_layer.update()
        evaluated = body.evaluated_get(bpy.context.evaluated_depsgraph_get())
        points = np.empty(len(evaluated.data.vertices) * 3, dtype=np.float32)
        evaluated.data.vertices.foreach_get("co", points)
        targets[name] = points.reshape(-1, 3)
        key.value = 0
    body.shape_key_clear()
    return targets


def flatten_pattern(pattern):
    pieces = list(pattern.objects)
    for obj in pieces:
        select([obj])
        for modifier in list(obj.modifiers):
            if modifier.type in {"MASK", "ARMATURE"}:
                obj.modifiers.remove(modifier)
            else:
                bpy.ops.object.modifier_apply(modifier=modifier.name)
    select(pieces)
    if len(pieces) > 1:
        bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = "Scout_" + pattern.name.split(" • ")[1]
    return obj


def zone_for(point):
    if point.z >= 1.475 and abs(point.x) < 0.143:
        return "HeadNeck"
    if abs(point.x) >= 0.739 and point.z > 1.16:
        return "Hands"
    if point.z < 0.915:
        return "LegsCovered"
    if abs(point.x) > 0.27 and point.z > 1.16:
        return "ArmsCovered"
    return "TrunkCovered"


def split_body(body, targets):
    surface = body.data
    zones = defaultdict(list)
    for face in surface.polygons:
        zones[zone_for(face.center)].append(face)
    for label, faces in zones.items():
        indices = sorted({vertex for face in faces for vertex in face.vertices})
        remap = {index: local for local, index in enumerate(indices)}
        data = bpy.data.meshes.new(label)
        data.from_pydata(
            [surface.vertices[i].co for i in indices], [],
            [[remap[i] for i in face.vertices] for face in faces],
        )
        data.update()
        part = bpy.data.objects.new("Scout_" + label, data)
        collection.objects.link(part)
        part.parent = rig
        if label == "Hands":
            for name, points in targets.items():
                if len(points) != len(surface.vertices):
                    raise ValueError("Grip target topology differs from the body")
                hand_shapes[name] = points[indices].copy()
        data.materials.append(surface.materials[0])
        uvs = data.uv_layers.new(name="UV0")
        for new, original in zip(data.polygons, faces):
            new.use_smooth = True
            for dst, src in zip(new.loop_indices, original.loop_indices):
                uvs.data[dst].uv = surface.uv_layers.active.data[src].uv
        data.normals_split_custom_set_from_vertices([surface.vertices[i].normal for i in indices])
        groups = {}
        for local, index in enumerate(indices):
            for weight in surface.vertices[index].groups:
                name = body.vertex_groups[weight.group].name
                if name not in rig.data.bones or weight.weight <= 0:
                    continue
                if name not in groups:
                    groups[name] = part.vertex_groups.new(name=name)
                groups[name].add([local], weight.weight, "REPLACE")
    bpy.data.objects.remove(body, do_unlink=True)


hand_shapes = {}
for pattern in collection.children:
    targets = evaluated_grips(pattern.objects["Body"]) if pattern.name.endswith(" • Body") else {}
    flattened = flatten_pattern(pattern)
    if pattern.name.endswith(" • Body"):
        split_body(flattened, targets)

for obj in collection.all_objects:
    if obj.type != "MESH":
        continue
    select([obj])
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    triangles = obj.modifiers.new("Game triangles", "TRIANGULATE")
    bpy.ops.object.modifier_apply(modifier=triangles.name)
    if obj.name == "Scout_Hands" and hand_shapes:
        obj.shape_key_add(name="Basis")
        for name, points in hand_shapes.items():
            shape = obj.shape_key_add(name=name, from_mix=False)
            shape.data.foreach_set("co", points.ravel())
            shape.value = 0.0
    obj.modifiers.new("Common skeleton", "ARMATURE").object = rig

export_collection(project / "Assets/Art/Characters/Models/SK_BorderScout.fbx", bake_animation=False)

for direction in ("StrafeLeft", "StrafeRight"):
    name = "AN_BorderScout_" + direction
    action = bpy.data.actions[name]
    rig.animation_data_create()
    rig.animation_data.action = action
    scene = bpy.context.scene
    scene.frame_start, scene.frame_end = map(round, action.frame_range)
    scene.frame_set(scene.frame_start)
    export_collection(project / "Assets/Art/Animations/BorderScout" / (name + ".fbx"),
                      animation_only=True, bake_animation=True)

