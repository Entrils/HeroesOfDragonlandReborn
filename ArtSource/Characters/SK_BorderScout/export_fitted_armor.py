"""Export rigid fittings in their recipe frames. Run on SK_BorderScout.blend."""
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector

source = Path(bpy.data.filepath).resolve()
if source.name != "SK_BorderScout.blend":
    raise ValueError("Open the Border Scout source before exporting its fittings.")
project = source.parents[3]
sys.path.insert(0, str(project / "Tools/ArtPipeline"))
from export_fbx import export_collection

destination = project / "Assets/Art/Equipment/Models/BorderScout"
batch = bpy.data.collections.new("Rigid export staging")
bpy.context.scene.collection.children.link(batch)

for fitting in bpy.data.collections["Fitted rigid equipment"].objects:
    if "--" in sys.argv and fitting.name not in sys.argv[sys.argv.index("--")+1:]:
        continue
    data = fitting.data.copy()
    pivot = Vector(fitting["rest_pivot_m"])
    data.transform(Matrix.Translation(-pivot))
    static = bpy.data.objects.new("SM_Scout_" + fitting.name, data)
    batch.objects.link(static)
    static.modifiers.new("Game triangles", "TRIANGULATE")
    export_collection(destination / (static.name + ".fbx"), batch.name, bake_animation=False)
    bpy.data.objects.remove(static, do_unlink=True)
    bpy.data.meshes.remove(data)

