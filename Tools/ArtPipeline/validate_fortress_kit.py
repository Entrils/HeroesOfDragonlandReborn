"""Independent FBX round-trip checks in Blender, not a Unity acceptance substitute."""
import json
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

root = Path(sys.argv[sys.argv.index('--')+1]).resolve()
manifest = json.loads((root/'ArtSource/Environment/FortressRuins/FortressRuins.manifest.json').read_text())
results = []
for entry in manifest['assets']:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(root/entry['fbx']), use_anim=False)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    assert len(meshes) == 3, (entry['name'], len(meshes))
    for suffix, expected_count in zip(('_LOD0','_LOD1','COL_'), entry['triangles']):
        obj = next(o for o in meshes if o.name.endswith(suffix) or (suffix=='COL_' and o.name.startswith(suffix)))
        mesh = obj.data
        assert len(mesh.polygons) == expected_count, (obj.name,'triangle count')
        assert all(len(p.vertices)==3 and p.area > 1e-9 for p in mesh.polygons), obj.name
        assert mesh.uv_layers and len(mesh.uv_layers.active.data) == len(mesh.loops), obj.name
        bm = bmesh.new()
        bm.from_mesh(mesh)
        assert all(edge.is_manifold for edge in bm.edges), (obj.name,'open/nonmanifold shell')
        bm.free()
        assert all(slot is not None for slot in mesh.materials), (obj.name,'material')
        if suffix == '_LOD0':
            vertices = [obj.matrix_world@v.co for v in mesh.vertices]
            low = Vector(tuple(min(v[i] for v in vertices) for i in range(3)))
            high = Vector(tuple(max(v[i] for v in vertices) for i in range(3)))
            assert (low-Vector(entry['minimumBlender'])).length < .001, (obj.name,'minimum',list(low))
            assert (high-Vector(entry['maximumBlender'])).length < .001, (obj.name,'maximum',list(high))
    for suffix, expected in [('Front',(0,1,0)),('Right',(1,0,0)),('Up',(0,0,1))]:
        socket = next(o for o in bpy.context.scene.objects if o.name=='Socket_'+suffix)
        assert (socket.matrix_world.translation-Vector(expected)).length < .001, (entry['name'],suffix)
    results.append({'asset':entry['name'],'fbxRoundTrip':'PASS','triangles':entry['triangles']})
report = dict(blender=bpy.app.version_string, assets=results,
              checks=['21 FBX reimports','LOD triangle counts','nonzero triangle area','UV0',
                      'closed manifold component shells','material slots','world bounds in meters','three basis markers'],
              limitation='Blender round-trip only; Unity import, prefabs, static MeshCollider cooking and Play Mode pending.')
(root/'ArtSource/Environment/FortressRuins/FortressRuins.validation.json').write_text(json.dumps(report,indent=2)+'\n')
print(f'[M55 RoundTrip] PASS {len(results)} FBXs',flush=True)
