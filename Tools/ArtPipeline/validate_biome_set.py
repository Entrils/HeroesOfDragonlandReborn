"""Independent M57 export, PBR packing and transition endpoint validation in Blender."""
import json
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

root=Path(sys.argv[sys.argv.index('--')+1]).resolve()
source=root/'ArtSource/Environment/Biomes'
manifest=json.loads((source/'Biomes.manifest.json').read_text())
assert len(manifest['assets'])==5 and len(manifest['transitions'])==5
results=[]
for entry in manifest['assets']:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(root/entry['fbx']),use_anim=False)
    objects=list(bpy.context.scene.objects)
    meshes=[o for o in objects if o.type=='MESH']
    assert len(meshes)==len(entry['triangles']),entry['name']
    for i,count in enumerate(entry['triangles']):
        obj=next(o for o in meshes if o.name==entry['name']+'_LOD'+str(i))
        mesh=obj.data
        assert len(mesh.polygons)==count and mesh.uv_layers,obj.name
        assert len(mesh.materials)==len(entry[f'lod{i}Materials']),obj.name
        for p in mesh.polygons:
            assert len(p.vertices)==3 and p.area>1e-10,obj.name
            uv=[mesh.uv_layers.active.data[j].uv for j in p.loop_indices]
            area=abs(sum(uv[j].x*uv[(j+1)%3].y-uv[(j+1)%3].x*uv[j].y for j in range(3)))*.5
            assert area>1e-12,(obj.name,'UV',p.index)
        if i==0:
            points=[obj.matrix_world@v.co for v in mesh.vertices]
            lo=Vector(tuple(min(v[k] for v in points) for k in range(3)))
            hi=Vector(tuple(max(v[k] for v in points) for k in range(3)))
            assert (lo-Vector(entry['minimumBlender'])).length<.001,obj.name
            assert (hi-Vector(entry['maximumBlender'])).length<.001,obj.name
        if entry['name'].endswith(('StrataOutcrop','CorruptedSpire','TwistedRoot')) and i==len(entry['triangles'])-1:
            bm=bmesh.new()
            bm.from_mesh(mesh)
            assert all(e.is_manifold for e in bm.edges),(obj.name,'static collision shell')
            bm.free()
    for suffix,expected in [('Front',(0,1,0)),('Right',(1,0,0)),('Up',(0,0,1))]:
        socket=next(o for o in objects if o.name=='Socket_'+suffix)
        assert (socket.matrix_world.translation-Vector(expected)).length<.001,(entry['name'],suffix)
    results.append(dict(asset=entry['name'],fbx='PASS',triangles=entry['triangles']))


def pixels(stem,suffix):
    folder='AncientForest' if stem.startswith('AF_') else 'Biomes'
    image=bpy.data.images.load(str(root/f'Assets/Art/Textures/Environment/{folder}/T_{stem}_{suffix}.png'),check_existing=False)
    image.colorspace_settings.name='Non-Color'
    assert list(image.size)==[512,512]
    values=list(image.pixels)
    bpy.data.images.remove(image)
    return values


stems=['BS_HighlandGravel','BS_BattlefieldPaving','BS_CorruptedAsh']+[t['stem'] for t in manifest['transitions']]
for stem in stems:
    normal=pixels(stem,'Normal')
    mask=pixels(stem,'Mask')
    assert max(mask[0::4])<.01 and min(mask[1::4])>.99 and max(mask[2::4])<.01,stem
    assert min(mask[3::4])>.04 and max(mask[3::4])<.2,stem
    for index in range(0,len(normal),4*107):
        n=Vector(tuple(normal[index+k]*2-1 for k in range(3)))
        assert abs(n.length-1)<.03 and n.z>0,stem
identities={'Forest':'AF_ForestFloor','Highlands':'BS_HighlandGravel',
            'Ruins':'BS_BattlefieldPaving','Corrupted':'BS_CorruptedAsh'}
for item in manifest['transitions']:
    for suffix in ('BaseColor','Normal','Mask'):
        blend=pixels(item['stem'],suffix)
        first=pixels(identities[item['first']],suffix)
        second=pixels(identities[item['second']],suffix)
        for y in range(0,512,17):
            left=y*512*4
            right=(y*512+511)*4
            assert max(abs(blend[left+k]-first[left+k]) for k in range(4))<.012,(item['name'],'left',suffix)
            assert max(abs(blend[right+k]-second[right+k]) for k in range(4))<.012,(item['name'],'right',suffix)
report=dict(blender=bpy.app.version_string,assets=results,textureMaps=len(stems)*3,
            transitionPairs=[t['first']+' > '+t['second'] for t in manifest['transitions']],
            checks=['actual FBX reimports','LOD counts','UV triangle area','meter bounds','basis sockets',
                    'closed lowest-LOD static collision shells','URP mask packing','normal validity','transition endpoint matching'],
            limitation='Unity import/compile, material-light matching, map boundary placement, camp/lock-on readability and six-player performance remain pending.')
(source/'Biomes.validation.json').write_text(json.dumps(report,indent=2)+'\n')
print('[M57 RoundTrip] PASS five FBXs, 24 maps and five PBR transition pairs',flush=True)
