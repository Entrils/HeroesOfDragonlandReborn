"""Independent FBX round-trip checks in Blender, not a Unity acceptance substitute."""
import json
import sys
from pathlib import Path

import bmesh
import bpy
import numpy as np
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

for surface in ('Stone','Wood','Iron'):
    for suffix in ('BaseColor','Normal','Mask'):
        path=root/f'Assets/Art/Textures/Environment/FortressRuins/T_FR_{surface}_{suffix}.png'
        image=bpy.data.images.load(str(path),check_existing=False)
        image.colorspace_settings.name='Non-Color'
        image.alpha_mode='CHANNEL_PACKED'
        assert tuple(image.size)==(1024,1024), path
        pixels=np.empty(1024*1024*4,dtype=np.float32)
        image.pixels.foreach_get(pixels)
        pixels=pixels.reshape(-1,4)
        if suffix=='Normal':
            normal=pixels[:,:3]*2-1
            assert np.max(abs(np.linalg.norm(normal,axis=1)-1))<.025 and normal[:,2].min()>0, path
        elif suffix=='Mask':
            metal,occlusion,unused,smooth=pixels.T
            assert occlusion.min()>.99 and unused.max()<.01 and np.ptp(smooth)>.015, path
            assert metal.max()>.85 if surface=='Iron' else metal.max()<.01, path
        else:
            assert np.ptp(pixels[:,0])>.04, path
        bpy.data.images.remove(image)
report = dict(blender=bpy.app.version_string, assets=results, textureMaps=9,
              checks=['21 FBX reimports','LOD triangle counts','nonzero triangle area','UV0',
                      'closed manifold component shells','material slots','world bounds in meters','three basis markers',
                      '1024 square surfaces','tangent normals','metallic and smoothness packing'],
              limitation='Blender round-trip only; Unity import, prefabs, static MeshCollider cooking and Play Mode pending.')
(root/'ArtSource/Environment/FortressRuins/FortressRuins.validation.json').write_text(json.dumps(report,indent=2)+'\n')
print(f'[M55 RoundTrip] PASS {len(results)} FBXs and nine surface maps',flush=True)
