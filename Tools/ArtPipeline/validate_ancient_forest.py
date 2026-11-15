"""M56 independent exported FBX/material-map checks; run inside Blender."""
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

root=Path(sys.argv[sys.argv.index('--')+1]).resolve()
folder=root/'ArtSource/Environment/AncientForest'
manifest=json.loads((folder/'AncientForest.manifest.json').read_text())
assert len(manifest['assets'])==19
results=[]
for entry in manifest['assets']:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(root/entry['fbx']),use_anim=False)
    objects=list(bpy.context.scene.objects)
    meshes=[o for o in objects if o.type=='MESH']
    assert len(meshes)==len(entry['triangles'])+(entry['collider']=='mesh'),entry['name']
    for index,triangles in enumerate(entry['triangles']):
        obj=next(o for o in meshes if o.name==entry['name']+'_LOD'+str(index))
        mesh=obj.data
        assert len(mesh.polygons)==triangles,(obj.name,'triangle count')
        assert mesh.uv_layers and all(m is not None for m in mesh.materials),(obj.name,'UV/materials')
        assert len(mesh.materials)==len(entry[f'lod{index}Materials']),(obj.name,'slots')
        for p in mesh.polygons:
            assert len(p.vertices)==3 and p.area>1e-10,(obj.name,'degenerate geometry')
            coords=[mesh.uv_layers.active.data[i].uv for i in p.loop_indices]
            area=abs(sum(coords[i].x*coords[(i+1)%3].y-coords[(i+1)%3].x*coords[i].y for i in range(3)))*.5
            assert area>1e-12,(obj.name,'degenerate UV',p.index)
        assert all(math.isfinite(value) for vertex in mesh.vertices for value in vertex.co)
        if index==0:
            vertices=[obj.matrix_world@v.co for v in mesh.vertices]
            low=Vector(tuple(min(v[i] for v in vertices) for i in range(3)))
            high=Vector(tuple(max(v[i] for v in vertices) for i in range(3)))
            assert (low-Vector(entry['minimumBlender'])).length<.001,(obj.name,'minimum')
            assert (high-Vector(entry['maximumBlender'])).length<.001,(obj.name,'maximum')
            if 'TreeAncient' in obj.name:
                assert max(abs(low.x),abs(low.y),abs(high.x),abs(high.y))<1.7
    if entry['collider']=='mesh':
        mesh=next(o.data for o in meshes if o.name.startswith('COL_'))
        bm=bmesh.new()
        bm.from_mesh(mesh)
        assert all(e.is_manifold for e in bm.edges),(entry['name'],'collision has open shell')
        bm.free()
    for suffix,expected in [('Front',(0,1,0)),('Right',(1,0,0)),('Up',(0,0,1))]:
        socket=next(o for o in objects if o.name=='Socket_'+suffix)
        assert (socket.matrix_world.translation-Vector(expected)).length<.001,(entry['name'],suffix)
    results.append(dict(asset=entry['name'],fbx='PASS',triangles=entry['triangles'],collision=entry['collider']))

maps=[]
for path in sorted((root/'Assets/Art/Textures/Environment/AncientForest').glob('*.png')):
    image=bpy.data.images.load(str(path),check_existing=False)
    expected=64 if 'AmbientMote' in path.name else 512
    assert list(image.size)==[expected,expected],path.name
    if path.stem.endswith(('_Normal','_Mask')):
        image.colorspace_settings.name='Non-Color'
    pixels=list(image.pixels)
    assert all(math.isfinite(v) and 0<=v<=1 for v in pixels),path.name
    if path.stem.endswith('_Mask'):
        assert max(pixels[0::4])<.01 and min(pixels[1::4])>.99 and max(pixels[2::4])<.01,path.name
        assert min(pixels[3::4])>.05 and max(pixels[3::4])<.2,path.name
    if path.stem.endswith('_Normal'):
        for i in range(0,len(pixels),4*109):
            normal=Vector(tuple(pixels[i+j]*2-1 for j in range(3)))
            assert abs(normal.length-1)<.03 and normal.z>0,path.name
    maps.append(path.name)
    bpy.data.images.remove(image)
assert len(maps)==13,len(maps)
report=dict(blender=bpy.app.version_string,assets=results,textureMaps=maps,
            checks=['19 actual FBX reimports','LOD counts','nondegenerate triangles and UVs','material slot counts',
                    'meter-scale world bounds','unit basis sockets','closed collision shells','finite 512px PBR maps',
                    'URP metallic/AO/reserved/smoothness mask channels','unit-length +Z tangent normals'],
            limitation='Offline checks only. Unity compile/import, biome application, live lock-on/telegraphs, visual approval and six-player profiling remain pending.')
(folder/'AncientForest.validation.json').write_text(json.dumps(report,indent=2)+'\n')
print(f'[M56 RoundTrip] PASS {len(results)} FBXs and {len(maps)} texture maps',flush=True)
