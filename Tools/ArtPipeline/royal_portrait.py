"""Facial sculpt, surface detail and iris textures for the fortress kings."""

import math

import bpy


def smoothstep(value, low, high):
    value = min(1, max(0, (value-low)/(high-low)))
    return value*value*(3-2*value)


def region(x, z, center_x, center_z, width, height):
    return math.exp(-((x-center_x)/width)**2-((z-center_z)/height)**2)


def sculpt_point(point, team, feature):
    x,y,z = point
    side = -1 if x<0 else 1
    a = abs(x)
    older = team=="TeamA"
    width = .985 if older else 1.042
    jaw = region(a,z,.053,1.608,.026,.041)
    jaw_width = -.047 if older else .071
    neck = .64+.36*smoothstep(z,1.528,1.598)
    x *= neck*(width+jaw_width*jaw)

    # Eye globes, tear lines, lashes and brows get the same small per-orbit adjustment.
    eye = region(a,z,.032,1.688,.027,.024)
    z += eye*(.0008 if side<0 else -.00055)
    x += side*eye*(.00065 if older else .00032)
    if feature=="Body":
        front = smoothstep(y,.062,.112)
        lower_lid = region(a,z,.035,1.677,.015,.0047)
        tear_trough = region(a,z,.029,1.670,.019,.0047)
        brow = region(a,z,.032,1.707,.021,.009)
        cheek = region(a,z,.048,1.665,.022,.014)
        hollow = region(a,z,.052,1.640,.015,.016)
        bridge = region(a,z,.002,1.667,.009,.025)
        tip = region(a,z,.001,1.647,.014,.009)
        nostril = region(a,z,.013,1.643,.007,.007)
        chin = region(a,z,.018,1.592,.033,.014)
        pout = region(a,z,.006,1.613,.025,.009)
        y += front*(.00135*lower_lid-(.0021 if older else .0011)*tear_trough+
             (.0026 if older else .0041)*brow+.0031*cheek-
             (.0046 if older else .0018)*hollow+(.0029 if older else .0005)*bridge+
             (.001 if older else .0024)*tip+(-.0009 if older else .0018)*nostril+
             (.0019 if older else .0044)*chin-.0028*pout)
        x += front*(.0018*bridge+.0009*tip)*(1 if older else -.7)
        z += front*(-.0015*tip+.0024*chin-side*.0008*pout)
    elif feature.startswith("Groom"):
        # Keep the crown fit local while changing the visible cut at the temples.
        temple = region(a,z,.075,1.689,.015,.06)
        x += side*temple*(.0013 if older else .0029)
        z -= temple*(.0024 if older else .0055)
    return x,y*neck,1.67+(z-1.67)*(1.01 if older else .982)


def surface_bands(point, team):
    """Multiply the source skin texture with soft anatomy and beard-area tones."""
    x,y,z = point
    a = abs(x)
    warm = smoothstep(y,.059,.124)
    ear = region(a,z,.082,1.662,.017,.029)
    cheek = region(a,z,.051,1.666,.019,.013)*warm
    nose = region(a,z,.014,1.645,.018,.012)*warm
    lower_lid = region(a,z,.035,1.675,.02,.007)*warm
    lip = region(a,z,.006,1.615,.023,.006)*warm
    temple = region(a,z,.068,1.713,.024,.026)*warm
    brow = .10*math.exp(-((z-brow_line(a,team))/.0026)**2)*region(a,z,.036,1.704,.025,.023)*warm
    sun = max(0,math.sin(x*73+z*42))*.04
    red = min(.19,.13*ear+.07*cheek+.09*nose+.11*lip)
    shading = .10*lower_lid+.052*temple
    age = .025 if team=="TeamA" else 0
    return (min(1, .86+red-shading*.34-sun-brow),
            .86-red*.65-shading*.66-age-sun-brow,
            .86-red*.8-shading*.45-age-sun-brow)


def brow_line(x, team):
    arch = .009*math.exp(-((x-.036)/.019)**2)
    return (1.699 if team=="TeamA" else 1.696)+arch-.05*(x-.025)


def expression_lines(point, team):
    """Shape soft eyelid and forehead folds after the facial mesh is subdivided."""
    x,y,z = point
    a = abs(x)
    if y < .075:
        return y
    age = 1 if team=="TeamA" else .54
    forehead = region(a,z,0,1.742,.052,.027)
    folds = 0
    for mean,height in ((1.722,.00023),(1.738,.00031),(1.749,.00019)):
        arc = mean+.004*(a/.060)**1.8+.0015*math.sin(x*46)
        folds -= height*math.exp(-((z-arc)/.0017)**2)
    corner = region(a,z,.050,1.68,.013,.011)
    jaw_fold = region(a,z,.034,1.634,.012,.017)
    line = .039-.34*(z-1.619)
    folds = folds*forehead-.00047*jaw_fold*math.exp(-((a-line)/.0032)**2)
    folds -= .00031*corner*math.exp(-((z-(1.681-.38*(a-.048)))/.0018)**2)
    return y+age*smoothstep(y,.075,.112)*folds


def skin_finish(mat, team):
    nodes,links = mat.node_tree.nodes,mat.node_tree.links
    shader = nodes.get("Principled BSDF")
    base = shader.inputs["Base Color"]
    shader.inputs["Subsurface Weight"].default_value = .085
    shader.inputs["Subsurface Radius"].default_value = (1,.46,.31)
    shader.inputs["Subsurface Scale"].default_value = .009
    shader.inputs["IOR"].default_value = 1.43
    shader.inputs["Specular IOR Level"].default_value = .3
    density = nodes.new("ShaderNodeVertexColor")
    density.layer_name = "FacialHairDensity"
    tone = nodes.new("ShaderNodeVertexColor")
    tone.layer_name = "RoyalSkinTone"
    mix = nodes.new("ShaderNodeMixRGB")
    mix.blend_type = "MULTIPLY"
    mix.inputs[0].default_value = 1
    links.new(base.links[0].from_socket,mix.inputs[1])
    links.new(tone.outputs["Color"],mix.inputs[2])
    shade = nodes.new("ShaderNodeMixRGB")
    shade.blend_type = "MULTIPLY"
    shade.inputs[2].default_value = (.46,.44,.41,1) if team=="TeamA" else (.32,.235,.18,1)
    links.new(density.outputs["Color"],shade.inputs[0])
    links.new(mix.outputs[0],shade.inputs[1])
    links.new(shade.outputs[0],base)

    object_coords = nodes.new("ShaderNodeTexCoord").outputs["Object"]
    pores = nodes.new("ShaderNodeTexVoronoi")
    pores.inputs["Scale"].default_value = 1450
    links.new(object_coords,pores.inputs["Vector"])
    pore_profile = nodes.new("ShaderNodeValToRGB")
    pore_profile.color_ramp.elements[0].position = .11
    pore_profile.color_ramp.elements[1].position = .41
    links.new(pores.outputs["Distance"],pore_profile.inputs["Fac"])
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = .32
    bump.inputs["Distance"].default_value = .00019
    links.new(pore_profile.outputs["Color"],bump.inputs["Height"])
    if shader.inputs["Normal"].is_linked:
        links.new(shader.inputs["Normal"].links[0].from_socket,bump.inputs["Normal"])
    links.new(bump.outputs["Normal"],shader.inputs["Normal"])


def groom_finish(mat, team):
    shader = mat.node_tree.nodes.get("Principled BSDF")
    if shader is None or not shader.inputs["Base Color"].is_linked:
        return
    nodes,links = mat.node_tree.nodes,mat.node_tree.links
    source = shader.inputs["Base Color"].links[0].from_socket
    multiplier = nodes.new("ShaderNodeMixRGB")
    multiplier.blend_type = "MULTIPLY"
    multiplier.inputs[0].default_value = .73
    multiplier.inputs[2].default_value = (.49,.56,.64,1) if team=="TeamA" else (.47,.225,.142,1)
    links.new(source,multiplier.inputs[1])
    links.new(multiplier.outputs[0],shader.inputs["Base Color"])
    if team=="TeamA":
        tint = nodes.new("ShaderNodeMixRGB")
        luminance = nodes.new("ShaderNodeRGBToBW")
        links.new(source,luminance.inputs[0])
        links.new(luminance.outputs[0],tint.inputs[2])
        greying = nodes.new("ShaderNodeVertexColor")
        greying.layer_name = "RoyalTempleGrey"
        links.new(greying.outputs["Color"],tint.inputs[0])
        links.new(multiplier.outputs[0],tint.inputs[1])
        links.new(tint.outputs[0],shader.inputs["Base Color"])


def iris_finish(obj, team):
    """Generate an eye atlas texture with a limbal ring, radial fibres and softly veined sclera."""
    import numpy as np

    size = 512
    coords = (np.arange(size,dtype=np.float32)+.5)/size
    x,z = np.meshgrid(coords-.5,coords-.5)
    angle = np.arctan2(z,x)
    radius = np.sqrt(x*x+z*z)
    ring = .178
    normalized = radius/ring
    noise = .50+.15*np.sin(angle*123+.52*np.sin(angle*47)+normalized*4)
    noise += .11*np.sin(angle*209+normalized*22)+.095*np.sin(angle*59+normalized*33)
    noise += .09*np.sin(angle*15+np.sin(normalized*33))
    pupil = np.clip((normalized-.408)/.065,0,1)
    limbal = np.clip((1.01-normalized)/.13,0,1)
    gold_ring = np.exp(-((normalized-.44)/.15)**2)*(.35+.65*noise)
    iris_hue = np.array((.058,.139,.152) if team=="TeamA" else (.169,.091,.038))
    gold = np.array((.21,.12,.044))
    iris = iris_hue[None,None,:]*(.52+.93*noise[:,:,None])
    iris += gold[None,None,:]*gold_ring[:,:,None]*(.2 if team=="TeamA" else .45)
    iris = (iris*limbal[:,:,None]+np.array((.019,.023,.024))*(1-limbal[:,:,None]))
    iris *= pupil[:,:,None]
    iris += (1-pupil[:,:,None])*np.array((.005,.006,.007))
    red = np.clip((abs(x)-.234)/.26,0,1)
    vessel = np.exp(-np.abs(np.sin(z*138+np.sin(x*82)+np.sin(z*35)))*17)*red**2
    sclera = np.empty((size,size,3),dtype=np.float32)
    sclera[:,:,0] = .57-.065*red+.041*vessel
    sclera[:,:,1] = .517-.13*red-.089*vessel
    sclera[:,:,2] = .475-.137*red-.075*vessel
    mask = np.clip((ring+.0018-radius)/.0036,0,1)[:,:,None]
    colors = sclera*(1-mask)+iris*mask
    pixels = np.ones((size,size,4),dtype=np.float32)
    pixels[:,:,:3] = np.clip(colors,0,1)
    image = bpy.data.images.new("T_RoyalIris_"+team,size,size,alpha=True)
    image.pixels.foreach_set(pixels.ravel())
    image.pack()
    uv = obj.data.uv_layers["FaceSource"]
    centers = {}
    for side in (-1,1):
        vertices = [v.co for v in obj.data.vertices if v.co.x*side>0]
        # The local texture axis follows the forward-most eye dome.
        center = max(vertices,key=lambda v:v.y)
        centers[side] = (center.x,center.z)
    for index,loop in enumerate(obj.data.loops):
        x,_,z = obj.data.vertices[loop.vertex_index].co
        cx,cz = centers[-1 if x<0 else 1]
        uv.data[index].uv = (min(.999,max(.001,.5+(x-cx)/.033)),
                             min(.999,max(.001,.5+(z-cz)/.032)))
    mat = obj.data.materials[0]
    nodes,links = mat.node_tree.nodes,mat.node_tree.links
    shader = nodes.get("Principled BSDF")
    tex = next(n for n in nodes if n.type=="TEX_IMAGE")
    tex.image = image
    shader.inputs["IOR"].default_value = 1.376
    shader.inputs["Roughness"].default_value = .19
    shader.inputs["Coat Weight"].default_value = .22
    shader.inputs["Coat Roughness"].default_value = .09
    links.new(tex.outputs["Color"],shader.inputs["Base Color"])
