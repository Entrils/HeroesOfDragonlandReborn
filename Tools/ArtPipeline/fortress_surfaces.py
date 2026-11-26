"""Tileable quarried stone, heartwood and forged iron for the fortress modules."""
import math
from pathlib import Path

import bpy
import numpy as np


def make_surfaces(folder):
    folder.mkdir(parents=True, exist_ok=True)
    size = 1024
    y, x = (np.indices((size, size), dtype=np.float32) + .5) / size
    rng = np.random.default_rng(7921)

    def field(cols, rows):
        lattice = rng.uniform(-1, 1, (rows, cols))
        u, v = x*cols, y*rows
        i, j = u.astype(int), v.astype(int)
        a, b = u-i, v-j
        a, b = a*a*(3-2*a), b*b*(3-2*b)
        return ((1-a)*(1-b)*lattice[j,i]+a*(1-b)*lattice[j,(i+1)%cols]
                +(1-a)*b*lattice[(j+1)%rows,i]+a*b*lattice[(j+1)%rows,(i+1)%cols])

    cloud = field(5, 6)+.48*field(13, 11)+.22*field(37, 41)
    grit = field(210, 208)
    for name,base in [('Stone',(.35,.37,.35)),('Wood',(.30,.225,.164)),('Iron',(.27,.286,.29))]:
        if name == 'Stone':
            seam = np.exp(-abs(np.sin(math.tau*(5*y+2*x+.31*cloud)))*16)
            height = .21*cloud+.095*grit-.22*seam
            tone = 1+.21*cloud+.11*grit-.27*seam
            polish = .30+.11*cloud-.12*seam
        elif name == 'Wood':
            fibres = np.sin(math.tau*(25*x+.9*field(5,3)+.13*field(17,6)))
            grain = np.maximum(0,fibres)**6
            tone = .99-.32*grain+.20*field(65,5)+.10*grit
            height = -.22*grain+.095*field(95,9)+.04*grit
            polish = .26-.10*grain+.05*cloud
        else:
            pits = np.maximum(0,-cloud-.38)
            tone = 1+.12*grit-.32*pits
            height = .10*field(73,65)-.23*pits+.045*grit
            polish = .52-.22*pits+.05*cloud
        dx = np.roll(height,-1,1)-np.roll(height,1,1)
        dy = np.roll(height,-1,0)-np.roll(height,1,0)
        normal = np.stack((-1.9*dx,-1.9*dy,np.ones_like(x)),axis=2)
        normal /= np.linalg.norm(normal,axis=2,keepdims=True)
        maps={'BaseColor':np.array(base)*tone[:,:,None],'Normal':normal*.5+.5,
              'Mask':np.stack((np.clip(.92-.40*np.maximum(0,-cloud),0,1) if name=='Iron' else x*0,
                                1+0*x,0*x,np.clip(polish,.09,.70)),axis=2)}
        for suffix,values in maps.items():
            stem='T_FR_'+name+'_'+suffix
            image=bpy.data.images.new(stem,size,size,alpha=True)
            image.colorspace_settings.name='sRGB' if suffix=='BaseColor' else 'Non-Color'
            image.alpha_mode='CHANNEL_PACKED'
            rgba=np.ones((size,size,4),dtype=np.float32)
            rgba[:,:,:values.shape[2]]=np.clip(values,0,1)
            image.pixels.foreach_set(rgba.ravel())
            image.filepath_raw=str(folder/(stem+'.png'))
            image.file_format='PNG'
            image.save()
            bpy.data.images.remove(image)


def bind_surface(material, folder):
    nodes,links=material.node_tree.nodes,material.node_tree.links
    shader=nodes.get('Principled BSDF')
    for suffix in ('BaseColor','Normal','Mask'):
        node=nodes.new('ShaderNodeTexImage')
        node.image=bpy.data.images.load(str(folder/(material.name.replace('M_FR_','T_FR_')+'_'+suffix+'.png')),check_existing=True)
        if suffix=='BaseColor':
            links.new(node.outputs['Color'],shader.inputs['Base Color'])
        elif suffix=='Normal':
            node.image.colorspace_settings.name='Non-Color'
            normal=nodes.new('ShaderNodeNormalMap')
            links.new(node.outputs[0],normal.inputs['Color'])
            links.new(normal.outputs[0],shader.inputs['Normal'])
        else:
            node.image.colorspace_settings.name='Non-Color'
            node.image.alpha_mode='CHANNEL_PACKED'
            split=nodes.new('ShaderNodeSeparateColor')
            links.new(node.outputs[0],split.inputs[0])
            links.new(split.outputs[0],shader.inputs['Metallic'])
            rough=nodes.new('ShaderNodeMath')
            rough.operation='SUBTRACT'
            rough.inputs[0].default_value=1
            links.new(node.outputs['Alpha'],rough.inputs[1])
            links.new(rough.outputs[0],shader.inputs['Roughness'])
