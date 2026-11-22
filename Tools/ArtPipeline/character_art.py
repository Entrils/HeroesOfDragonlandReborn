"""Shared Blender materials, atlas baking and review lighting for characters."""

import bpy
from mathutils import Vector


def material(name, color, metal, roughness, grain, relief, finish_type):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    shader = nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = mat.diffuse_color
    shader.inputs["Metallic"].default_value = metal
    if not grain:
        shader.inputs["Roughness"].default_value = roughness
        return mat

    position = nodes.new("ShaderNodeTexCoord").outputs["Object"]

    def noise(scale, detail):
        texture = nodes.new("ShaderNodeTexNoise")
        links.new(position, texture.inputs["Vector"])
        texture.inputs["Scale"].default_value = scale
        texture.inputs["Detail"].default_value = detail
        return texture.outputs["Fac"]

    def ramp(source, stops):
        node = nodes.new("ShaderNodeValToRGB")
        links.new(source, node.inputs["Fac"])
        for index, (at, color_at_stop) in enumerate(stops):
            element = node.color_ramp.elements[index] if index < 2 else node.color_ramp.elements.new(at)
            element.position = at
            element.color = (*color_at_stop, 1)
        return node.outputs["Color"]

    mottling = noise(22 if metal else 57, 4)
    contrast = ((.22,.70),(.66,1.02),(.83,1.14)) if metal else ((.22,.82),(.66,1.03),(.83,1.12))
    tint = ramp(mottling, [(at, tuple(min(1, c * factor) for c in color))
                           for at, factor in contrast])
    roughness_variation = ramp(mottling, [(at, (value,) * 3)
        for at, value in ((.26, min(1,roughness+.12)), (.78, max(.1,roughness-.075)))])
    links.new(tint, shader.inputs["Base Color"])
    links.new(roughness_variation, shader.inputs["Roughness"])

    fine = noise(grain, 2)
    if finish_type == "steel":
        stretch = nodes.new("ShaderNodeVectorMath")
        stretch.operation = "MULTIPLY"
        stretch.inputs[1].default_value = (5.1, 3.3, .41)
        links.new(position, stretch.inputs[0])
        texture = nodes.new("ShaderNodeTexVoronoi")
        texture.feature = "DISTANCE_TO_EDGE"
        texture.inputs["Scale"].default_value = grain*.32
        links.new(stretch.outputs["Vector"], texture.inputs["Vector"])
        lines = ramp(texture.outputs["Distance"], [(0,(0,0,0)), (.09,(1,1,1))])
        blend = nodes.new("ShaderNodeMath")
        blend.operation = "MULTIPLY"
        links.new(fine, blend.inputs[0])
        links.new(lines, blend.inputs[1])
        fine = blend.outputs[0]
    elif finish_type == "cloth":
        strands = []
        for direction, frequency in (("X",154),("Z",126)):
            wave = nodes.new("ShaderNodeTexWave")
            wave.bands_direction = direction
            wave.inputs["Scale"].default_value = frequency
            wave.inputs["Distortion"].default_value = 1.15
            wave.inputs["Detail Scale"].default_value = 2
            links.new(position,wave.inputs["Vector"])
            strands.append(wave.outputs["Fac"])
        weave = nodes.new("ShaderNodeMath")
        weave.operation = "MULTIPLY"
        for index,strand in enumerate(strands):
            links.new(strand,weave.inputs[index])
        blend = nodes.new("ShaderNodeMixRGB")
        blend.inputs[0].default_value = .4
        links.new(weave.outputs[0],blend.inputs[1])
        links.new(fine,blend.inputs[2])
        fine = blend.outputs[0]
        shader.inputs["Sheen Weight"].default_value = .075
        shader.inputs["Sheen Roughness"].default_value = .72
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = .36
    bump.inputs["Distance"].default_value = relief
    links.new(fine, bump.inputs["Height"])
    links.new(bump.outputs["Normal"], shader.inputs["Normal"])
    return mat



def bake_atlas(mesh, size, texture_dir, stem, uv_layer=""):
    """Bake the procedural finish onto the same UVs and triangles exported to FBX."""
    import numpy as np

    scene = bpy.context.scene
    texture_dir.mkdir(parents=True, exist_ok=True)
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 8
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = mesh

    materials = list(mesh.data.materials)
    bake_target = []
    for mat in materials:
        nodes = mat.node_tree.nodes
        target = nodes.new("ShaderNodeTexImage")
        nodes.active = target
        bake_target.append(target)

    for suffix in ("BaseColor", "Normal", "Mask"):
        name = stem + "_" + suffix
        image = bpy.data.images.new(name, width=size, height=size, alpha=True)
        image.colorspace_settings.name = "sRGB" if suffix == "BaseColor" else "Non-Color"
        temporary = []
        for mat, target in zip(materials, bake_target):
            target.image = image
            if suffix == "Normal":
                continue
            nodes, links = mat.node_tree.nodes, mat.node_tree.links
            shader = nodes.get("Principled BSDF")
            output = nodes.get("Material Output")
            emission = nodes.new("ShaderNodeEmission")
            temporary.append((nodes,links,shader,output,emission))
            if suffix == "BaseColor":
                base = shader.inputs["Base Color"]
                if base.is_linked:
                    links.new(base.links[0].from_socket, emission.inputs["Color"])
                else:
                    emission.inputs["Color"].default_value = base.default_value
            else:
                channels = nodes.new("ShaderNodeCombineColor")
                channels.inputs["Red"].default_value = shader.inputs["Metallic"].default_value
                channels.inputs["Green"].default_value = 1
                roughness = shader.inputs["Roughness"]
                if roughness.is_linked:
                    links.new(roughness.links[0].from_socket, channels.inputs["Blue"])
                else:
                    channels.inputs["Blue"].default_value = roughness.default_value
                links.new(channels.outputs[0],emission.inputs["Color"])
            links.new(emission.outputs[0], output.inputs["Surface"])

        bpy.ops.object.bake(type="NORMAL" if suffix == "Normal" else "EMIT", margin=4, uv_layer=uv_layer)
        for nodes,links,shader,output,emission in temporary:
            links.new(shader.outputs["BSDF"],output.inputs["Surface"])
            nodes.remove(emission)
        if suffix == "Mask":
            pixels = np.empty(size*size*4,dtype=np.float32)
            image.pixels.foreach_get(pixels)
            pixels[3::4] = 1 - pixels[2::4]
            pixels[2::4] = 0
            image.pixels.foreach_set(pixels)

        image.file_format = "PNG"
        image.filepath_raw = str(texture_dir / (name + ".png"))
        image.save()
        print(f"Baked {image.filepath_raw}",flush=True)
    for mat,target in zip(materials,bake_target):
        mat.node_tree.nodes.remove(target)


def studio_camera(scene):
    bpy.ops.object.camera_add(location=(2.33,5.7,1.67))
    camera = bpy.context.object
    camera.rotation_euler = (Vector((0,0,.98))-camera.location).to_track_quat("-Z","Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 2.22
    scene.camera = camera
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 32
    scene.cycles.adaptive_threshold = .038
    scene.cycles.max_bounces = 5
    scene.cycles.diffuse_bounces = 2
    scene.cycles.glossy_bounces = 3
    scene.cycles.use_denoising = True
    scene.world = bpy.data.worlds.new("Preview Studio")
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (.18,.192,.202,1)
    background.inputs["Strength"].default_value = .30
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,.007))
    floor = bpy.context.object
    floor.name = "Studio shadow floor"
    floor.data.materials.append(material("Studio graphite",(.039,.050,.059),0,1,0,0,""))
    for name, position, energy, size, tint in (
        ("Soft key", (2.3,3.3,3.2), 480, 2.3, (1,.91,.78)),
        ("Front fill", (-2.4,2.8,2.2), 225, 3.2, (.82,.89,1)),
        ("Rim", (2.4,-2.1,2.6), 330, 2.3, (.86,.92,1)),
        ("Back fill", (-2,-2.5,2.7), 170, 3, (1,.88,.73)),
    ):
        bpy.ops.object.light_add(type="AREA", location=position)
        light = bpy.context.object
        light.name = name
        light.data.energy = energy
        light.data.size = size
        light.data.color = tint
        light.rotation_euler = (Vector((0,0,1.03))-light.location).to_track_quat("-Z","Y").to_euler()
    scene.render.resolution_x = 1000
    scene.render.resolution_y = 1250
    scene.render.resolution_percentage = 100
    return camera
