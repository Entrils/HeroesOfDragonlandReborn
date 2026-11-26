"""One-time tailoring pass on the original Border Scout master (230dae1).
Run inside Blender with SK_BorderScout.blend open. Saves the editable master and dyes.
"""
from math import cos, sin, pi, exp
from pathlib import Path

import bpy
import bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

source = Path(__file__).resolve().with_name("SK_BorderScout.blend")
assert Path(bpy.data.filepath).name in (source.name, "Scout-before-tailoring.blend")
scene = bpy.context.scene
rig = bpy.data.objects["SK_PrototypeHumanoid"]
jerkin = bpy.data.collections["Pattern • Jerkin"]
if "Champion • left mantle 1" in bpy.data.objects:
    raise ValueError("The master already contains the champion patterns.")

# Each row runs across a pattern. Insets along its lower selvage form a narrow
# raised metal lip in the same draw slot as the existing metal fasteners.
def panel(name, rows, cols, shape, weights, facing, lining, rim=True, flip=False):
    vertices, faces, uvs = [], [], []
    for j in range(rows + 1):
        for i in range(cols + 1):
            u, v = i / cols, j / rows
            vertices.append(shape(u, v))
            uvs.append((u, v))
    for j in range(rows):
        for i in range(cols):
            k = j * (cols + 1) + i
            quad = (k, k + 1, k + cols + 2, k + cols + 1)
            faces.append(quad[::-1] if flip else quad)
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("Champion • " + name, mesh)
    obj["pattern_columns"] = cols
    jerkin.objects.link(obj)
    obj.parent = rig
    for mat in (facing, lining, "OldBrass"):
        mesh.materials.append(bpy.data.materials["Scout_" + mat])
    uv = mesh.uv_layers.new(name="UV0")
    for face in mesh.polygons:
        face.use_smooth = True
        face.material_index = 2 if rim and face.index // cols == rows - 1 else 0
        for loop in face.loop_indices:
            uv.data[loop].uv = uvs[mesh.loops[loop].vertex_index]
    for i, p in enumerate(vertices):
        for bone, weight in weights(p, uvs[i]).items():
            group = obj.vertex_groups.get(bone) or obj.vertex_groups.new(name=bone)
            group.add([i], weight, 'REPLACE')
    solid = obj.modifiers.new("Turned underside", "SOLIDIFY")
    solid.thickness = .0028
    solid.offset = 0
    solid.material_offset = 0
    solid.material_offset_rim = 1
    bevel = obj.modifiers.new("Hammered bevel", "BEVEL")
    bevel.width = .0016
    bevel.segments = 2
    obj.modifiers.new("Common skeleton", "ARMATURE").object = rig
    return obj


for side, sign in [('Left', -1), ('Right', 1)]:
    # Overlapping articulated lames. The left crown is broader and carries an
    # axial flute; the sword side tapers earlier so the arm reads in profile.
    for layer in range(4 if sign < 0 else 3):
        start = .156 + layer * .044
        radius = (.132 if sign < 0 else .122) - layer * .009
        def mantle(u, v):
            angle = (u - .5) * 4.18
            x = start + v * (.077 + .022 * cos(angle)) + .009 * abs(sin(angle))
            r = radius + .009 * sin(pi*v) + .006 * exp(-(angle/.23)**2)
            if layer == 0:
                r *= .58 + .42*sin(v*pi/2)
            return sign*x, r*sin(angle), 1.423 + r*cos(angle) + .015*(1-v)
        def shoulder(p, uv):
            arm = min(1, max(0, (abs(p[0]) - .165)/.09))
            return {side+'Shoulder': 1-arm, side+'UpperArm': arm}
        panel(f"{side.lower()} mantle {layer+1}", 12, 32, mantle, shoulder,
              "BurnishedEdge", "Bridle", flip=sign>0)

    def vambrace(u, v):
        a = (u-.5)*4.8
        x = .553 + v*.183 + .021*(1-v)*cos(2*a)
        r = .065*(1-v)+.052*v + .005*exp(-(a/.23)**2)
        return sign*x, r*sin(a), 1.418 + r*cos(a)
    panel(side+" vambrace", 16, 24, vambrace, lambda p, uv: {side+"LowerArm":1},
          "BurnishedEdge", "Russet", flip=sign>0)

    # Split riding skirts hung from the side of the belt; front is cut back to
    # expose the thigh plates. Fold amplitude grows toward the loose hem.
    def skirt(u, v):
        foremost = -.62 if sign < 0 else -1.05
        angle = foremost + (2.27-foremost)*u
        x = .11 + (.084 + .029*v)*cos(angle)
        y = -(.131 + .029*v)*sin(angle) + .004
        fold = .012*v*sin(5*pi*u)
        length = .225 + .097*sin(pi*u) + (.04 if sign < 0 else 0)
        return sign*(x + fold*cos(angle)), y-fold*sin(angle), 1.018-v*length
    def tabs(p, uv):
        leg = min(1, uv[1]*1.4)
        return {"Hips":1-leg,side+"UpperLeg":leg}
    panel(side+" riding panel", 22, 28, skirt, tabs, "Russet", "Bridle", flip=sign<0)

    # Lateral splints reinforce the outside of the boot. The detachable greave
    # occupies its anterior face. A narrow heel opening clears the ankle.
    for half in (-1,1):
        def splint(u,v):
            a = half*(.71+.81*u)
            z = .105+v*(.202+.058*sin(pi*u))
            radius = .067+.019*v+.006*sin(pi*v)
            return sign*(.11+radius*cos(a)), .008+radius*sin(a), z
        panel(side+" boot splint "+str(half),12,12,splint,
              lambda p,uv:{side+"LowerLeg":1},"BurnishedEdge","Bridle", flip=half*sign<0)

# Broad articulated gorget with a pointed keel over the sternum and spine.
for layer in range(2):
    def collar(u, v):
        a = 2*pi*u
        t = (layer + v)/2
        keel = abs(cos(a))**8
        return ((.099+.083*t)*sin(a), .011+(.103+.055*t)*cos(a),
                1.522-.064*t-.046*t*keel+.013*abs(sin(a)))
    panel("gorget "+str(layer+1), 10, 60, collar, lambda p, uv: {"Chest":1},
          "BurnishedEdge", "Bridle")

# Close-fitting plackart lames follow the existing rest garment. The breast
# and back socket armor sit outside these low-relief plates when equipped.
rig.data.pose_position = 'REST'
bpy.context.view_layer.update()
graph = bpy.context.evaluated_depsgraph_get()
hull = bmesh.new()
hull.from_mesh(bpy.data.objects["Jerkin_Russet"].evaluated_get(graph).data)
hull.from_mesh(bpy.data.objects["Jerkin_ShoulderYoke"].evaluated_get(graph).data)
outer = bmesh.ops.convex_hull(hull, input=list(hull.verts))
bmesh.ops.delete(hull, geom=outer["geom_interior"]+outer["geom_unused"],context="VERTS")
garment = BVHTree.FromBMesh(hull)
hull.free()
base = bpy.data.objects["Jerkin_Russet"]
tree = KDTree(len(base.data.vertices))
for vertex in base.data.vertices:
    tree.insert(vertex.co,vertex.index)
tree.balance()
for front in (1, -1):
    # Leave the belly soft below the front three lames. During a tucked roll
    # the removable breastplate travels through this space with the chest.
    for layer in range(1 if front == 1 else 0,4):
        def plackart(u, v):
            span = .139 + .004*layer
            x = (2*u-1)*span
            keel = 1-abs(2*u-1)**1.45
            z = 1.074+layer*.076+.086*(1-v)+.034*(1-keel)
            if layer == 3:
                z += .037*(1-v)*keel
            hit = garment.ray_cast((x,front*.5,z),(0,-front,0))[0]
            if hit is None:
                raise ValueError(f"Pattern beyond garment at {x}, {z}")
            stand_off = .002 if layer == 0 else .006
            return x, hit.y+front*(stand_off+.003*v+.006*keel), z
        def trunk(p, uv):
            weights = {}
            for _,idx,dist in tree.find_n(p,4):
                for group in base.data.vertices[idx].groups:
                    bone = base.vertex_groups[group.group].name
                    if bone in rig.data.bones:
                        weights[bone] = weights.get(bone,0)+group.weight/max(.001,dist)**2
            total = sum(weights.values())
            return {bone:w/total for bone,w in weights.items()}
        panel(("breast" if front>0 else "back")+" lame "+str(layer+1),
              8, 36, plackart, trunk, "BurnishedEdge","Bridle", flip=front<0)
rig.data.pose_position = 'POSE'

# Raise a keel and paired swept flutes directly in the plate stock. Both sides
# of the shell move together, keeping its rolled edge and equipment pivot.
gear = bpy.data.collections["Fitted rigid equipment"]
for obj, direction in ((gear.objects["Breastplate"],1),(gear.objects["Backplate"],-1)):
    for vert in obj.data.vertices:
        x,y,z = vert.co
        t = min(1,max(0,(z-1.156)/.218))
        envelope = sin(pi*t)**.8
        wing = abs(x)-(.018+.116*t**1.4)
        flute = exp(-(wing/.015)**2)+.56*exp(-((wing+.032)/.010)**2)
        vert.co.y += direction*envelope*(.014*exp(-(x/.028)**2)+.009*flute)
for vert in gear.objects["Skullcap"].data.vertices:
    x,y,z = vert.co
    arch = max(0,min(1,(z-1.685)/.10))
    vert.co.z += .022*arch*exp(-(x/.023)**2)

# Lower cheek and mandibular planes. Apply the same field to every shape key
# so grip deltas stay local to the hands. Leave eye rims and lips in place.
def jaw(point):
    x,y,z = point
    strength = exp(-((z-1.615)/.030)**2) * min(1,abs(x)/.043)
    point.x += x*.13*strength
    brow = exp(-((z-1.701)/.009)**2)*min(1,max(0,(y-.086)/.025))
    point.z += .0045*brow*max(-1,min(1,(abs(x)-.043)/.024))
    point.y += .0025*brow
body = bpy.data.objects["Body"].data
for vertex in body.vertices:
    jaw(vertex.co)
for key in body.shape_keys.key_blocks:
    for point in key.data:
        jaw(point.co)
for vertex in bpy.data.objects["Brows_IndividualHairs"].data.vertices:
    jaw(vertex.co)

for obj in bpy.data.collections["Pattern • Hair"].objects:
    for vertex in obj.data.vertices:
        p = vertex.co
        lift = max(0,min(1,(p.z-1.716)/.1))
        fringe = max(0,min(1,(p.y-.045)/.09))
        p.x += .023*lift*fringe
        p.y -= .024*lift
        p.z += lift*(.019+.013*fringe)
        nape = max(0,min(1,(-p.y+.018)/.088))*max(0,min(1,(1.795-p.z)/.095))
        p.y -= .022*nape
        p.z -= .050*nape

# Tint linear albedo from the packed source, retaining UV wear, weave and
# opacity. Normal and metallic/smoothness maps keep their original bindings.
dyes = {"Russet":(.61,.22,.26), "Sleeve":(.115,.19,.265), "Breeches":(.53,.56,.60),
        "Flax":(.17,.23,.30), "SweptLocks":(.46,.44,.50),
        "HairRoots":(.46,.44,.50)}
textures = source.parents[3]/"Assets/Art/Textures/Characters/BorderScout"
def save_texture(img, stem):
    target = textures/(stem+".png")
    img.filepath_raw = str(target)
    img.file_format = 'PNG'
    img.save()
    encoded = target.read_bytes()
    img.pack(data=encoded,data_len=len(encoded))
    img.filepath = bpy.path.relpath(str(target),start=str(source.parent))

for name, tint in dyes.items():
    stem = "T_Scout_"+name+"_BaseColor"
    img = next(i for i in bpy.data.images if Path(i.name).stem == stem)
    pixels = np.empty(len(img.pixels), dtype=np.float32)
    img.pixels.foreach_get(pixels)
    pixels.reshape(-1,4)[:,:3] *= tint
    img.pixels.foreach_set(pixels)
    save_texture(img,stem)

# Rasterize rest-space grooming against the authored head UVs. Jaw shadow
# follows the cheek and chin on both sides of the seam; lips stay uncovered.
skin = bpy.data.images["T_Scout_Skin_BaseColor.png"]
w,h = skin.size
texels = np.empty(w*h*4,dtype=np.float32)
skin.pixels.foreach_get(texels)
rgba = texels.reshape(h,w,4)
body.calc_loop_triangles()
uvs = body.uv_layers.active.data
for tri in body.loop_triangles:
    points = np.array([tuple(body.vertices[i].co) for i in tri.vertices])
    if points[:,2].min()<1.50 or points[:,2].min()>1.73 or points[:,1].max()<.02:
        continue
    tex = np.array([tuple(uvs[i].uv) for i in tri.loops])*[w,h]
    lo = np.maximum(0,np.floor(tex.min(axis=0)).astype(int))
    hi = np.minimum([w,h],np.ceil(tex.max(axis=0)).astype(int))
    if np.any(hi<=lo):
        continue
    a,b = tex[1:]-tex[0]
    det = a[0]*b[1]-a[1]*b[0]
    if abs(det)<1e-6:
        continue
    yy,xx = np.mgrid[lo[1]:hi[1],lo[0]:hi[0]]
    dx,dy = xx+.5-tex[0,0],yy+.5-tex[0,1]
    s,t = (dx*b[1]-dy*b[0])/det,(a[0]*dy-a[1]*dx)/det
    inside = (s>=0)&(t>=0)&(s+t<1)
    x,y,z = (points[0]+s[:,:,None]*(points[1]-points[0])+t[:,:,None]*(points[2]-points[0])).T
    x,y,z = x.T,y.T,z.T
    jawline = 1.614+.043*(np.minimum(abs(x)/.082,1)**1.6)
    beard = np.clip((jawline-z)/.016,0,1)*np.clip((z-1.55)/.023,0,1)*np.clip((y-.026)/.036,0,1)
    beard *= 1-np.exp(-(x/.032)**4-((z-1.625)/.012)**4)
    whiskers = np.mod(np.sin(xx*12.9898+yy*78.233)*43758.5453,1)
    scar = np.exp(-((x+.046+(z-1.696)*.41)/.0013)**2-((z-1.696)/.024)**4)*np.clip((y-.09)/.02,0,1)
    region = rgba[lo[1]:hi[1],lo[0]:hi[0],:3]
    region *= 1-(inside*beard*(.19+.37*whiskers**4))[:,:,None]*np.array([1,.88,.70])
    region += (inside*scar)[:,:,None]*np.array([.045,.008,.009])
skin.pixels.foreach_set(texels)
save_texture(skin,"T_Scout_Skin_BaseColor")

bpy.context.preferences.filepaths.save_version = 0
size = 1024
v,u = (np.indices((size,size),dtype=np.float32)+.5)/size
rng = np.random.default_rng(4109)
# A chased border with interleaved pointed leaves. Broad steel in the middle
# carries the reflection. Height, alloy and polish follow the same tooling.
grain = rng.normal(0,1,u.shape)
wave = .5+.5*np.cos(2*pi*u*8)
leaf = np.minimum(abs(v-(.12+.11*wave)),abs(v-(.12+.11*(1-wave))))
inlay = np.maximum(np.exp(-(leaf/.005)**2),np.exp(-((v-.085)/.004)**2))
brush = .5*np.sin(2*pi*(v*122+1.3*np.sin(2*pi*u)))+.5*grain
patina = np.cos(2*pi*(2*u+v))*.5+np.sin(2*pi*(u-3*v))*.5
height = .70*inlay+.018*brush
dy,dx = np.gradient(height)
normal = np.stack((-dx*1.8,-dy*1.8,np.ones_like(u)),axis=2)
normal /= np.linalg.norm(normal,axis=2,keepdims=True)
alloy = np.array([.122,.162,.182])*(1-inlay[:,:,None])+np.array([.46,.29,.105])*inlay[:,:,None]
alloy *= (1+.08*patina+.024*brush)[:,:,None]
mask = np.stack((.90+.06*inlay, .97+.03*inlay, 0*u,
                  .53+.15*inlay+.045*patina+.018*brush),axis=2)
shader = bpy.data.materials["Scout_BurnishedEdge"].node_tree
for suffix, rgb, node_name in (("BaseColor",alloy,"Image Texture"),
                               ("Normal",normal*.5+.5,"Image Texture.001"),
                               ("Mask",mask,"Image Texture.002")):
    stem = "T_Scout_BurnishedEdge_"+suffix
    node = shader.nodes[node_name]
    img = node.image if suffix=="BaseColor" else bpy.data.images.new(stem,size,size,alpha=True)
    img.colorspace_settings.name = "sRGB" if suffix=="BaseColor" else "Non-Color"
    if suffix=="Mask":
        img.alpha_mode = 'CHANNEL_PACKED'
    rgba = np.ones((size,size,4),dtype=np.float32)
    rgba[:,:,:rgb.shape[2]] = np.clip(rgb,0,1)
    img.pixels.foreach_set(rgba.ravel())
    save_texture(img,stem)
    node.image = img
split = shader.nodes.new("ShaderNodeSeparateColor")
shader.links.new(shader.nodes["Image Texture.002"].outputs["Color"],split.inputs[0])
shader.links.new(split.outputs[0],shader.nodes["Principled BSDF"].inputs["Metallic"])
bpy.ops.wm.save_as_mainfile(filepath=str(source),compress=True,relative_remap=False)
