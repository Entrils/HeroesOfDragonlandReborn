"""Run on the editable Border Scout file. Write the native-pose surface intersection report."""
from itertools import combinations
import json
from pathlib import Path

import bpy
from mathutils import Matrix
from mathutils.bvhtree import BVHTree

source = Path(bpy.data.filepath)
if source.name != "SK_BorderScout.blend":
    raise ValueError("Open the editable Border Scout first.")
scene = bpy.context.scene
export = bpy.data.collections["EXPORT"]
armor = bpy.data.collections["Fitted rigid equipment"]
armor.hide_viewport = False
rig = next(obj for obj in export.all_objects if obj.type == "ARMATURE")
rig.hide_set(False)
hair = set(bpy.data.collections["Pattern • Hair"].objects)
costume = [obj for obj in export.all_objects if obj.type == "MESH" and obj not in hair]
layers = [
    ("Body", "Shirt_Linen"), ("Body", "Breeches"), ("Shirt_Linen", "Jerkin_Russet"),
    ("Breeches", "Boot_L"), ("Breeches", "Boot_R"),
]
failures = []
tested = {}


def surface(obj, graph):
    evaluated = obj.evaluated_get(graph)
    data = evaluated.data
    return BVHTree.FromPolygons([evaluated.matrix_world @ vertex.co for vertex in data.vertices],
                                [list(face.vertices) for face in data.polygons])


def test_pose(label, frame):
    graph = bpy.context.evaluated_depsgraph_get()
    clothing = {obj.name: surface(obj, graph) for obj in costume}
    plates = {obj.name: surface(obj, graph) for obj in armor.objects}
    pairs = [(a, clothing[a], b, clothing[b]) for a, b in layers]
    pairs.extend((a, left, b, right) for (a, left), (b, right) in combinations(plates.items(), 2))
    pairs.extend((a, left, b, right) for a, left in plates.items() for b, right in clothing.items())
    for a, left, b, right in pairs:
        count = len(left.overlap(right))
        if count:
            failures.append(dict(action=label, frame=frame, parts=[a, b], surfacePairs=count))


rig.animation_data_clear()
for bone in rig.pose.bones:
    bone.matrix_basis = Matrix.Identity(4)
bpy.context.view_layer.update()
test_pose("Rest", 1)
for reference in bpy.data.actions:
    if not reference.name.startswith("AN_PrototypeHumanoid_"):
        continue
    label = reference.name.removeprefix("AN_PrototypeHumanoid_")
    action = bpy.data.actions["AN_BorderScout_" + label] if label.startswith("Strafe") else reference
    rig.animation_data_create()
    rig.animation_data.action = action
    first, last = map(round, action.frame_range)
    tested[action.name] = [first, last]
    for frame in range(first, last + 1):
        scene.frame_set(frame)
        test_pose(action.name, frame)

report = {
    "source": source.name, "blender": bpy.app.version_string, "nativeFPS": scene.render.fps,
    "framesIncludingRest": 1 + sum(end - start + 1 for start, end in tested.values()),
    "inclusiveFrameRanges": tested, "rigidParts": sorted(obj.name for obj in armor.objects),
    "garmentPairs": layers, "loadout": "Complete adapted armor, hair hidden, neutral grip shapes",
    "scope": "Evaluated garment pairs, all unordered plate pairs and all plate-to-visible-costume pairs. Integer Blender frames.",
    "detectedSurfaceCrossings": failures,
}
(source.parent / "Review/FitChecks.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print("Fit check:", report["framesIncludingRest"], "poses,", len(failures), "crossing records.")
if failures:
    raise RuntimeError("See Review/FitChecks.json for the intersecting parts and frames.")

