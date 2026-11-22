"""Build the two royal character assets from the project rig and art sources."""

import argparse
import math
from pathlib import Path
from random import Random
import sys

import bpy
from mathutils import Quaternion, Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, str(Path(__file__).resolve().parent))
from character_art import bake_atlas, material, studio_camera
from export_fbx import export_collection
from royal_portrait import brow_line, expression_lines, groom_finish, iris_finish, sculpt_point, skin_finish, surface_bands

ROOT = Path(__file__).resolve().parents[2]
ART_SOURCE = ROOT / "ArtSource/Characters"
GAME_ART = ROOT / "Assets/Art"
TEAMS = {
    "TeamA": ((.015,.048,.148), (.48,.32,.126), (.055,.27,.63), (.089,.083,.076)),
    "TeamB": ((.148,.016,.022), (.42,.225,.078), (.48,.021,.048), (.072,.030,.017)),
}


def select(objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]


def mesh_object(name, vertices, faces):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    return obj


def rigid(obj, name, bone, mat, smooth=True):
    obj.name = name
    obj.data.materials.append(mat)
    for polygon in obj.data.polygons:
        polygon.use_smooth = smooth
    group = obj.vertex_groups.new(name=bone)
    group.add(list(range(len(obj.data.vertices))),1,"REPLACE")
    parts.append(obj)
    return obj


def solid(obj, thickness, bevel=0):
    bpy.context.view_layer.objects.active = obj
    if thickness:
        mod = obj.modifiers.new("Thickness","SOLIDIFY")
        mod.thickness = thickness
        bpy.ops.object.modifier_apply(modifier=mod.name)
    if bevel:
        mod = obj.modifiers.new("Edge bevel","BEVEL")
        mod.width = bevel
        mod.segments = 2
        bpy.ops.object.modifier_apply(modifier=mod.name)


def patch(name,bone,mat,points,thickness=.003,bevel=.001):
    obj = mesh_object(name,points,[tuple(range(len(points)))])
    solid(obj,thickness,bevel)
    return rigid(obj,name,bone,mat,False)


def grid(name,bone,mat,rows,thickness=.003,wrap=False):
    columns = len(rows[0])
    points = [point for row in rows for point in row]
    faces = []
    for row in range(len(rows)-1):
        for column in range(columns if wrap else columns-1):
            a = row*columns+column
            b = row*columns+(column+1)%columns
            faces.append((a,a+columns,b+columns,b))
    obj = mesh_object(name,points,faces)
    solid(obj,thickness)
    return rigid(obj,name,bone,mat)


def turned(name,bone,mat,rings,center=(0,0),sections=56):
    """A set of fitted elliptical bands, expressed as Z, half-width and depth."""
    cx,cy = center
    rows = [[(cx+x*math.sin(a),cy+y*math.cos(a),z)
             for a in (i*math.tau/sections for i in range(sections))] for z,x,y in rings]
    return grid(name,bone,mat,rows,wrap=True)


def bead(name,bone,mat,center,scale,segments=12,rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,location=center)
    obj = bpy.context.object
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return rigid(obj,name,bone,mat)


def tube(name,bone,mat,paths,radius,sides=6,closed=False,taper=0):
    vertices,faces = [],[]
    for path in paths:
        path = tuple(map(Vector,path))
        start = len(vertices)
        for i,point in enumerate(path):
            before = path[i-1] if closed or i else point
            after = path[(i+1)%len(path)] if closed or i+1<len(path) else point
            frame = (after-before).normalized().to_track_quat("Z","Y")
            width = radius*(1-taper*i/(len(path)-1))
            for j in range(sides):
                angle = j*math.tau/sides
                vertices.append(point+frame@Vector((width*math.cos(angle),width*math.sin(angle),0)))
        for i in range(len(path) if closed else len(path)-1):
            a = start+i*sides
            b = start+((i+1)%len(path))*sides
            for j in range(sides):
                k = (j+1)%sides
                faces.append((a+j,a+k,b+k,b+j))
    return rigid(mesh_object(name,vertices,faces),name,bone,mat)


def faceted_gem(bone,center,forward,radius):
    center = Vector(center)
    forward = Vector(forward).normalized()
    up = Vector((0,0,1))
    right = forward.cross(up)
    sides = 10
    vertices = []
    for height,width in ((-.001,radius*.78),(.0014,radius),(.0048,radius*.58)):
        for i in range(sides):
            angle = i*math.tau/sides
            vertices.append(center+forward*height+width*(right*math.cos(angle)+up*math.sin(angle)*1.2))
    faces = []
    for ring in range(2):
        for i in range(sides):
            j = (i+1)%sides
            a = ring*sides
            faces.append((a+i,a+j,a+sides+j,a+sides+i))
    faces.append(tuple(range(sides-1,-1,-1)))
    faces.append(tuple(range(2*sides,3*sides)))
    rigid(mesh_object("Faceted inset gemstone",vertices,faces),"Faceted inset gemstone",bone,jewel,False)


def place_medallion(bone,center,radius):
    x,y,z = center
    bead("Cast clasp",bone,gold,center,(radius,.005,radius),20,8)
    faceted_gem(bone,(x,y+.006,z),(0,1,0),radius*.51)
    ring = [[(x+radius*.83*math.cos(a),y+.004,z+radius*.83*math.sin(a))
             for a in (i*math.tau/24 for i in range(24))]]
    tube("Clasp beading",bone,bright,ring,.0012,5,True)


def copy_faces(original, faces, name, transform, bone=None, keep_uv=False):
    """Transfer a fitted region, preserving the source face UVs and skin weights."""
    indices = sorted({v for polygon in faces for v in polygon.vertices})
    remap = {v:i for i,v in enumerate(indices)}
    world = original.matrix_world
    obj = mesh_object(name,[transform(world@original.data.vertices[v].co) for v in indices],
                      [tuple(remap[v] for v in p.vertices) for p in faces])
    for mat in original.data.materials:
        obj.data.materials.append(mat)
    for dst,src in zip(obj.data.polygons,faces):
        dst.material_index = src.material_index
        dst.use_smooth = src.use_smooth
    if keep_uv:
        src_uv = original.data.uv_layers.active
        dst_uv = obj.data.uv_layers.new(name="FaceSource")
        for dst,src in zip(obj.data.polygons,faces):
            for a,b in zip(dst.loop_indices,src.loop_indices):
                dst_uv.data[a].uv = src_uv.data[b].uv
    if bone:
        group = obj.vertex_groups.new(name=bone)
        group.add(list(range(len(indices))),1,"REPLACE")
    else:
        groups = {g.index:obj.vertex_groups.new(name=g.name) for g in original.vertex_groups if g.name in rig.data.bones}
        for index,src_index in enumerate(indices):
            for entry in original.data.vertices[src_index].groups:
                if entry.group in groups and entry.weight > 0:
                    groups[entry.group].add([index],entry.weight,"REPLACE")
    parts.append(obj)
    return obj


def feather(value, center, width):
    t = min(1,max(0,.5+(value-center)/width))
    return t*t*(3-2*t)


def facial_hair(point, team):
    x,y,z = point
    width = 1 if team=="TeamA" else .93
    x = abs(x)*width
    cheek = min(1,x/.062)
    lower = (1.579 if team=="TeamA" else 1.567)+.035*cheek**1.9
    upper = 1.608+.049*cheek**2
    jaw = feather(z,lower,.012)*(1-feather(z,upper,.01))*(1-feather(x,.066,.011))
    front = feather(y,.095-.073*cheek,.025)
    sideburn = feather(x,.052,.011)*(1-feather(x,.071,.008))
    sideburn *= feather(z,1.63,.016)*(1-feather(z,1.673,.011))*feather(y,.05,.016)
    mustache_height = 1.629-.006*(min(x,.034)/.034)**1.55
    mustache = (1-feather(abs(z-mustache_height),.0035,.003))*feather(x,.0045,.006)
    mustache *= (1-feather(x,.032,.009))*feather(y,.119,.013)
    return max(jaw*front,sideburn,mustache),mustache


def graft_head(team):
    scout = ART_SOURCE/"SK_BorderScout/SK_BorderScout.blend"
    names = {"Body","Eyes","Eyes_Lashes","Eyes_MoistLidMargin",
             "Hair_Cropped","Groom_CombedLocks","Groom_FaceFramingLocks"}
    with bpy.data.libraries.load(str(scout)) as (source,target):
        target.objects = [name for name in source.objects if name in names]
    sculpted = set()

    face = None
    skin = None
    scalp = []
    for original in target.objects:
        faces = [face for face in original.data.polygons
                 if original.name!="Body" or all(
                     (original.matrix_world@original.data.vertices[v].co).z>1.47
                     and abs((original.matrix_world@original.data.vertices[v].co).x)<.131
                     for v in face.vertices)]
        if not faces:
            raise ValueError(f"Scout source has no usable faces for {original.name}")
        feature = original.name
        piece = copy_faces(original,faces,"Royal "+feature,lambda point:sculpt_point(point,team,feature),
                           "Head",True)
        if feature=="Eyes":
            iris_finish(piece,team)
        if feature.startswith(("Hair_","Groom_")):
            if team=="TeamA":
                greys = piece.data.color_attributes.new(name="RoyalTempleGrey",type="FLOAT_COLOR",domain="POINT")
                for vertex in piece.data.vertices:
                    x,y,z = vertex.co
                    amount = .75*feather(abs(x),.053,.032)*(1-feather(z,1.738,.071))*feather(y,.018,.1)
                    greys.data[vertex.index].color = (amount,amount,amount,1)
        if feature in ("Body","Hair_Cropped","Groom_CombedLocks","Groom_FaceFramingLocks"):
            scalp.append(piece)
        if feature=="Body":
            skin = piece
            bpy.context.view_layer.objects.active = piece
            subdivision = piece.modifiers.new("Facial curvature","SUBSURF")
            subdivision.levels = 1
            subdivision.uv_smooth = "PRESERVE_BOUNDARIES"
            bpy.ops.object.modifier_apply(modifier=subdivision.name)
            for vertex in piece.data.vertices:
                vertex.co.y = expression_lines(vertex.co,team)
            face = BVHTree.FromPolygons([v.co for v in piece.data.vertices],
                                        [tuple(p.vertices) for p in piece.data.polygons])
            piece.data.color_attributes.new(name="FacialHairDensity",type="FLOAT_COLOR",domain="POINT")
            piece.data.color_attributes.new(name="RoyalSkinTone",type="FLOAT_COLOR",domain="POINT")
            shade = piece.data.color_attributes["FacialHairDensity"]
            tone = piece.data.color_attributes["RoyalSkinTone"]
            head_weight = piece.vertex_groups["Head"]
            neck_weight = piece.vertex_groups.new(name="Neck")
            for vertex in piece.data.vertices:
                density,_ = facial_hair(vertex.co,team)
                shade.data[vertex.index].color = (density,density,density,1)
                tone.data[vertex.index].color = (*surface_bands(vertex.co,team),1)
                follow_head = feather(vertex.co.z,1.537,.050)
                head_weight.add([vertex.index],follow_head,"REPLACE")
                neck_weight.add([vertex.index],1-follow_head,"REPLACE")
        sculpted.update(original.data.materials)
        bpy.data.objects.remove(original,do_unlink=True)
    for mat in sculpted:
        nodes,links = mat.node_tree.nodes,mat.node_tree.links
        uv = nodes.new("ShaderNodeUVMap")
        uv.uv_map = "FaceSource"
        for node in nodes:
            if node.type=="TEX_IMAGE":
                links.new(uv.outputs["UV"],node.inputs["Vector"])
            if node.type=="NORMAL_MAP":
                node.uv_map = "FaceSource"
        if mat.name=="Scout_Skin":
            skin_finish(mat,team)
        elif mat.name in ("Scout_CroppedHair","Scout_SweptLocks"):
            groom_finish(mat,team)
        elif mat.name in ("Scout_LidMargin","Scout_Lashes"):
            shader = nodes.get("Principled BSDF")
            if mat.name=="Scout_LidMargin":
                shader.inputs["Base Color"].default_value = (.155,.082,.066,1)
                shader.inputs["Roughness"].default_value = .32
            else:
                color = (.047,.036,.029) if team=="TeamA" else (.035,.018,.011)
                shader.inputs["Base Color"].default_value = (*color,1)
    if face is None:
        raise ValueError("Border Scout face mesh was unavailable")
    vertices, faces = [], []
    for piece in scalp:
        offset = len(vertices)
        vertices.extend(v.co.copy() for v in piece.data.vertices)
        faces.extend(tuple(v+offset for v in p.vertices) for p in piece.data.polygons)
    return face,BVHTree.FromPolygons(vertices,faces),skin


def retain_fitted_limbs(original):
    allowed = {bone.name for bone in rig.data.bones
               if bone.name.startswith(("Left","Right")) and
               not bone.name.endswith(("Shoulder","UpperLeg"))}
    names = {g.index:g.name for g in original.vertex_groups}
    kept = {v.index for v in original.data.vertices
            if any(g.weight>.01 and names[g.group] in allowed for g in v.groups)
            and all(g.weight<.01 or names[g.group] in allowed for g in v.groups)}
    faces = [p for p in original.data.polygons if all(v in kept for v in p.vertices)]
    limbs = copy_faces(original,faces,"Royal fitted limb plates",lambda point:point)
    for i,mat in enumerate(limbs.data.materials):
        if mat.name=="Guard_BlackenedSteel":
            limbs.data.materials[i] = plate_steel
        elif mat.name=="Guard_BurnishedSteel":
            limbs.data.materials[i] = royal_edge
        elif mat.name in ("Guard_AntiqueBrass","Guard_SandThread"):
            limbs.data.materials[i] = gold
    bpy.data.objects.remove(original,do_unlink=True)


def beard(team, face):
    """Grow shaped locks from the actual skin surface and leave the lips clear."""
    random = Random(41 if team=="TeamA" else 63)
    locks = [[],[]]
    rows,columns = 60,86
    for row in range(rows):
        for column in range(columns):
            x = ((column+.5+random.uniform(-.42,.42))/columns-.5)*.175
            z = 1.559+(row+.5+random.uniform(-.42,.42))*.128/rows
            root,normal,_,_ = face.ray_cast(Vector((x,.32,z)),Vector((0,-1,0)),.35)
            if root is None:
                continue
            density,mustache = facial_hair(root,team)
            if density<.11 or random.random()>density*.78:
                continue
            sign = -1 if x<0 else 1
            if normal.y<0:
                normal.negate()
            direction = Vector((.72*sign,0,-.59)) if mustache>.55 else Vector((-.16*sign,0,-1))
            tangent = direction-normal*direction.dot(normal)
            tangent.normalize()
            length = random.uniform(.004,.009) if team=="TeamA" else random.uniform(.006,.013)
            if mustache>.55:
                length *= .45
            spread = normal.cross(tangent)*random.uniform(-.0014,.0014)
            curve = []
            for step in range(5):
                t = step/4
                target = root+tangent*length*t+spread*(t*t)
                support,outward,_,_ = face.find_nearest(target,.006)
                if support is None or outward.dot(normal)<.35:
                    support,outward = target,normal
                lift = -.00036 + (.0013 if team=="TeamA" else .0019)*t**.64
                curve.append(support+outward*lift)
            locks[int(random.random()<.19)].append(curve)
    if sum(map(len,locks))<500:
        raise ValueError("Not enough facial hair roots hit the skin")
    for paths,mat in zip(locks,(hair,hair_highlight)):
        tube("Tapered facial hairs","Head",mat,paths,.00064 if team=="TeamA" else .00076,3,taper=.91)


def eyebrows(team, face):
    """Anchor each brow fibre over the actual brow ridge."""
    random = Random(274 if team=="TeamA" else 485)
    paths = [[],[]]
    for side in (-1,1):
        for index in range(39):
            t = index/38
            x = .013+.046*t
            rise = .002 if team=="TeamA" else .0027
            for row in range(5):
                root_x = x+random.uniform(-.0006,.0006)
                root_z = brow_line(x,team)+(row-2)*rise*(1-.77*t)*.55
                root_z += random.uniform(-.00033,.00033)+side*.0007
                length = random.uniform(.0031,.0051)*(1-.62*t)
                curve=[]
                for step in range(4):
                    v=step/3
                    px=side*(root_x+length*v)
                    pz=root_z+v*(.0023-.0042*t)
                    hit,normal,_,_=face.ray_cast(Vector((px,.32,pz)),Vector((0,-1,0)),.30)
                    if hit is None:
                        break
                    if normal.y<0:
                        normal.negate()
                    curve.append(hit+normal*(.00030+.00053*math.sin(math.pi*v)))
                if len(curve)==4:
                    paths[int(random.random()<.16)].append(curve)
    for curves,mat in zip(paths,(brow_hair,hair_highlight)):
        tube("Fitted eyebrow fibres","Head",mat,curves,.00028,3,taper=.82)


def seat_head(head_parts, skin):
    """Shorten the exposed neck while moving the face, hair and crown together."""
    bpy.context.view_layer.update()
    for piece in head_parts:
        world = piece.matrix_world
        inverse = world.inverted()
        for vertex in piece.data.vertices:
            point = world@vertex.co
            if piece==skin:
                hidden = 1-feather(point.z,1.494,.032)
                point.x *= 1-.21*hidden
                point.y *= 1-.16*hidden
            point.z -= .034*feather(point.z,1.554,.088)
            point.y -= .005*feather(point.z,1.568,.08)
            vertex.co = inverse@point


def crown(team, scalp):
    count = 96
    step = math.tau/count
    center_y = .027
    raw = []
    for index in range(count):
        angle = index*step
        radial = Vector((math.sin(angle),math.cos(angle),0))
        center = Vector((0,center_y,1.749+.012*math.cos(angle)))
        hit,_,_,distance = scalp.ray_cast(center+radial*.22,-radial,.22)
        if hit is None:
            raise ValueError(f"Missing scalp contact at crown sample {index}")
        raw.append(.22-distance)

    def band_point(angle,height=0,offset=0):
        phase = (angle%math.tau)/step
        i = int(phase)
        blend = phase-i
        radius = raw[i]*(1-blend)+raw[(i+1)%count]*blend+offset
        return Vector((radius*math.sin(angle),center_y+radius*math.cos(angle),
                       1.749+.012*math.cos(angle)+height))

    angles = [i*step for i in range(count)]
    seat = [band_point(a,0,-.0007) for a in angles]
    lining = [seat,[band_point(a,.006,.0012) for a in angles]]
    grid("Fitted velvet crown lining","Head",velvet,lining,.0016,True)
    band = [[band_point(a,z,offset) for a in angles]
            for z,offset in ((.001,.001),(.003,.0033),(.008,.0042),(.026,.0053),(.033,.0060))]
    grid("Hammered crown band","Head",gold,band,.0025,True)
    for z,offset in ((.004,.0041),(.026,.0060),(.032,.0067)):
        tube("Crown beaded wire","Head",bright,[[band_point(a,z,offset) for a in angles]],.00155,7,True)
    filigree = []
    for i in range(24):
        angle = (i+.5)*math.tau/24
        for sign in (-1,1):
            filigree.append([band_point(angle+sign*a,z,.0061) for a,z in
                             ((0,.009),(.031,.012),(.065,.018),(.086,.020),(.063,.022),(.042,.019))])
        filigree.append([band_point(angle+.021*math.sin(a),.017+.005*math.cos(a),.0064)
                         for a in (j*math.tau/12 for j in range(12))])
    tube("Engraved crown scrolls","Head",engraving,filigree,.00055,5)
    if team=="TeamA":
        points = [(-.031,0),(-.034,.026),(-.023,.042),(-.009,.032),
                  (0,.072),(.009,.032),(.023,.042),(.034,.026),(.031,0)]
        count = 6
    else:
        points = [(-.028,0),(-.028,.041),(-.016,.041),(-.016,.030),(-.008,.030),
                  (-.008,.052),(.008,.052),(.008,.030),(.016,.030),(.016,.041),
                  (.028,.041),(.028,0)]
        count = 7
    for i in range(count):
        angle = i*math.tau/count
        scale = 1.18 if i==0 else .87 if team=="TeamA" else .91
        outset = Vector((math.sin(angle),math.cos(angle),0))
        tangent = Vector((math.cos(angle),-math.sin(angle),0))
        start = band_point(angle,.027,.0062)
        points3d = [start+tangent*(x*scale)+outset*(.069*z)+Vector((0,0,z*scale)) for x,z in points]
        patch("Royal crown point","Head",gold,points3d,.004,.0016)
        tube("Chased crown point","Head",bright,[points3d],.0012,5)
        stone = start+outset*.006+Vector((0,0,.012 if i else .019))
        bezel = [[stone+tangent*(.008*math.sin(a))+Vector((0,0,.010*math.cos(a)))
                  for a in (j*math.tau/24 for j in range(24))]]
        tube("Crown gem bezel","Head",bright,bezel,.00115,7,True)
        faceted_gem("Head",stone+outset*.0008,outset,.0065)
    for i in range(32):
        angle = i*math.tau/32
        bead("Crown rivet","Head",bright,band_point(angle,.014,.0049),
             (.0025,.0025,.0025),8,5)


def collar_head_weight(seated_height):
    unseated = seated_height
    for _ in range(3):
        unseated = seated_height+.034*feather(unseated,1.554,.088)
    return feather(unseated,1.537,.050)


def neck_opening(skin):
    """Cut the visible neckline to the seated neck instead of using a circular hole."""
    surface = BVHTree.FromPolygons([v.co for v in skin.data.vertices],
                                   [tuple(p.vertices) for p in skin.data.polygons])
    angles = [i*math.tau/72 for i in range(72)]
    directions = [Vector((math.sin(a),math.cos(a),0)) for a in angles]

    def fitted_ring(z, clearance):
        center = Vector((0,.005,z))
        sample = Vector((0,.005,max(z,1.508)))
        radii = []
        for direction in directions:
            hit,_,_,dist = surface.ray_cast(sample+direction*.18,-direction,.18)
            if hit is None:
                raise ValueError(f"No seated neck surface at height {z}")
            radii.append(.18-dist)
        return [center+direction*(radius+clearance) for direction,radius in zip(directions,radii)]

    def lower_band(z,width,depth):
        return [(width*math.sin(a),.005+depth*math.cos(a),z) for a in angles]

    grid("Fitted neckline of the gorget","Chest",gold,[
        lower_band(1.446,.115,.106),
        lower_band(1.452,.109,.099),
        lower_band(1.468,.087,.075),
        fitted_ring(1.483,.014),
        fitted_ring(1.491,.010),
    ],.003,True)
    collar = grid("Turned wool collar","Neck",velvet,[
        fitted_ring(1.487,.010),fitted_ring(1.495,.008),fitted_ring(1.505,.006),
        fitted_ring(1.514,.004),fitted_ring(1.518,.0017),fitted_ring(1.517,-.0007),
        fitted_ring(1.508,-.0014),
    ],.0019,True)
    neck = collar.vertex_groups["Neck"]
    head = collar.vertex_groups.new(name="Head")
    for vertex in collar.data.vertices:
        follow_head = collar_head_weight(vertex.co.z)
        neck.add([vertex.index],1-follow_head,"REPLACE")
        head.add([vertex.index],follow_head,"REPLACE")


def drapery(team):
    cols = 57
    cape = []
    # The front edges open over the chest, while the weight of the train hangs at the back.
    for z,width,depth,start,fold in [
        (1.466,.119,.084,2.64,.002),(1.42,.213,.134,2.61,.01),
        (1.29,.257,.178,2.35,.02),(1.12,.27,.193,2.1,.024),
        (.92,.282,.205,1.85,.027),(.72,.291,.213,1.66,.03),
        (.49,.318,.229,1.5,.033),(.28,.354,.259,1.38,.035),
        (.132,.382,.285,1.29,.04),
    ]:
        row = []
        for i in range(cols):
            u = 2*i/(cols-1)-1
            angle = start*u
            folds = fold*(.65*math.cos(13*u+.8*(1.45-z))+.39*math.sin(24*u-2*z))
            face_clearance = .055*max(0,-math.cos(angle)) if z>1.26 else 0
            row.append(((width+folds)*math.sin(angle),-(depth+folds)*math.cos(angle)-.012+face_clearance,
                        z+abs(u)**3*.071*(1.45-z)+.012*math.sin(u*8)*(1.45-z)))
        cape.append(row)
    grid("Full-length royal mantle","Chest",velvet,cape,.008)
    hems = [[row[edge] for row in cape] for edge in (0,-1)]+[cape[-1]]
    tube("Mantle braided binding","Chest",thread,hems,.0045,8)
    tube("Mantle gold seam","Chest",bright,[[(x*1.008,y*1.013,z+.004) for x,y,z in path]
                                         for path in hems],.00115,5)

    def mantle_point(row,column,outset):
        a,b = int(row),int(column)
        t,s = row-a,column-b
        start = Vector(cape[a][b]).lerp(Vector(cape[a][b+1]),s)
        end = Vector(cape[a+1][b]).lerp(Vector(cape[a+1][b+1]),s)
        point = start.lerp(end,t)
        radial = Vector((point.x,point.y+.012,0)).normalized()
        return point+radial*outset

    embroidery = []
    for first,last in ((1.15,3.75),(cols-4.75,cols-2.15)):
        mid = (first+last)*.5
        for index in range(18):
            row = 2.26+.30*index
            coordinates = [(row,first),(row+.096,mid),(row,last),(row-.096,mid)]
            embroidery.append([mantle_point(r,c,.0049) for r,c in coordinates])
    tube("Mantle diamond embroidery","Chest",thread,embroidery,.00116,5,True)
    # Work the reverse heraldry into the same fitted surface as the cloth.
    def back_point(u,v):
        return mantle_point(2.28+v*2.04,(cols-1)*.5+u*12,.0045)

    shield = [(0,-.37),(.77,-.19),(.71,.34),(.38,.61),(0,.77),(-.38,.61),(-.71,.34),(-.77,-.19)]
    outline = []
    for start,end in zip(shield,shield[1:]+shield[:1]):
        for i in range(6):
            t=i/6
            outline.append(back_point(start[0]*(1-t)+end[0]*t,start[1]*(1-t)+end[1]*t))
    tube("Reverse embroidered shield","Chest",thread,[outline],.002,6,True)
    if team=="TeamA":
        mark = [(0,-.16),(.095,.09),(.48,.12),(.14,.24),(.15,.53),(0,.33),
                (-.15,.53),(-.14,.24),(-.48,.12),(-.095,.09)]
    else:
        mark = [(-.36,.41),(-.36,-.02),(-.23,-.02),(-.23,.12),(-.09,.12),(-.09,-.13),
                (.09,-.13),(.09,.12),(.23,.12),(.23,-.02),(.36,-.02),(.36,.41),(0,.53)]
    embroidery = []
    for start,end in zip(mark,mark[1:]+mark[:1]):
        for i in range(5):
            t=i/5
            embroidery.append(back_point(start[0]*(1-t)+end[0]*t,start[1]*(1-t)+end[1]*t))
    tube("Reverse embroidered royal charge","Chest",thread,[embroidery],.0028,6,True)

    fur_collar = []
    for index in range(8):
        depth = index/7
        row = []
        for column in range(49):
            u = (column-24)/24
            c = 28+u*(21-1.5*depth)
            r = .04+(1.63+.19*math.cos(math.pi*u))*depth
            r += .02*math.sin(column*2.7)*depth**3
            row.append(mantle_point(r,c,.0085+.0044*math.sin(math.pi*depth)))
        fur_collar.append(row)
    grid("Fitted fur mantle collar","Chest",fur,fur_collar,.005)
    tube("Fur mantle collar stitches","Chest",thread,[fur_collar[-1]],.0013,5)

    robe = []
    for z,w,d,front in ((1.035,.169,.126,3.14159),(.983,.175,.132,3.13),(.95,.205,.153,3.12),(.90,.223,.174,3.08),
                        (.78,.231,.181,3.06),(.62,.239,.191,3.04),
                        (.46,.246,.196,2.96),(.335,.251,.202,2.86)):
        row = []
        for i in range(65):
            angle = -front+2*front*i/64
            amplitude = .005+.014*(1-z)
            fold = amplitude*(math.cos(angle*13+z)+.31*math.sin(angle*24))
            row.append(((w+fold)*math.sin(angle),-(d+fold)*math.cos(angle),z+.018*math.cos(angle*3)))
        robe.append(row)
    grid("Split ceremonial robe","Hips",velvet,robe,.005)
    tube("Woven robe hem","Hips",thread,[robe[-1]],.0038,8)
    tube("Robe opening binding","Hips",gold,[[row[i] for row in robe] for i in (0,-1)],.002,7)
    for i in range(1,64,3):
        a,b = Vector(robe[-1][i]),Vector(robe[-2][i])
        start = a.lerp(b,.12)
        tube("Hem embroidery","Hips",thread,[[start,start.lerp(b,.07)]],.0013,5)


def regalia(team):
    turned("Tailored arming doublet","Spine",velvet,
           [(.965,.166,.123),(1.063,.158,.117),(1.195,.163,.117),(1.33,.179,.129),
            (1.409,.175,.104),(1.468,.077,.071)])
    chest_rings = [(1.083,.164,.124),(1.106,.169,.133),(1.241,.198,.157),
                   (1.342,.216,.151),(1.408,.194,.126),(1.449,.096,.094)]
    turned("Fitted royal cuirass","Chest",plate_steel,chest_rings)

    def chest_point(x,z):
        for low,high in zip(chest_rings,chest_rings[1:]):
            if low[0]<=z<=high[0]:
                t=(z-low[0])/(high[0]-low[0])
                width=low[1]+(high[1]-low[1])*t
                depth=low[2]+(high[2]-low[2])*t
                return (x,depth*math.sqrt(max(0,1-(x/width)**2))+.002,z)
        raise ValueError(f"Cuirass engraving is outside the plate at {z}")

    scrollwork = []
    for sign in (-1,1):
        scrollwork.append([chest_point(x*sign,z) for x,z in
             ((.039,1.107),(.104,1.118),(.144,1.144),(.158,1.181),
              (.152,1.231),(.135,1.265),(.115,1.276),(.108,1.251),(.122,1.232))])
        for rise in (0,.027,.054):
            scrollwork.append([chest_point(x*sign,z+rise) for x,z in
                 ((.143,1.146),(.111,1.169),(.085,1.163),(.083,1.15),(.099,1.148))])
    tube("Royal cuirass filigree","Chest",engraving,scrollwork,.00115,6)
    turned("Royal waist belt","Hips",strap,[(.984,.185,.147),(.99,.19,.149),
                                         (1.035,.183,.148),(1.042,.181,.144)],sections=48)
    place_medallion("Hips",(0,.157,1.013),.028)
    if team=="TeamA":
        crest = [(0,.102),(.016,.029),(.074,.051),(.035,.007),(.091,-.013),(.04,-.028),
                 (.055,-.066),(.013,-.05),(0,-.104),(-.013,-.05),(-.055,-.066),
                 (-.04,-.028),(-.091,-.013),(-.035,.007),(-.074,.051),(-.016,.029)]
    else:
        crest = [(-.079,-.042),(-.074,.064),(-.05,.064),(-.05,.049),(-.034,.049),
                 (-.034,.067),(-.022,.067),(-.022,.033),(-.012,.033),(-.012,.092),
                 (.012,.092),(.012,.033),(.022,.033),(.022,.067),(.034,.067),(.034,.049),
                 (.05,.049),(.05,.064),(.074,.064),(.079,-.042),(0,-.094)]
    shield = [(0,.105),(.086,.079),(.096,.021),(.069,-.056),(0,-.113),(-.069,-.056),(-.096,.021),(-.086,.079)]
    patch("Heraldic enamel shield","Chest",velvet,[(x,.160,1.258+z) for x,z in shield],.007,.002)
    tube("Gilded shield boundary","Chest",royal_edge,[[(x,.166,1.258+z) for x,z in shield]],.003,7,True)
    patch("Royal charge","Chest",royal_edge,[(x*.72,.168,1.261+z*.72) for x,z in crest],.004,.0013)
    for sign in (-1,1):
        place_medallion("Chest",(.161*sign,.148,1.369),.024)
    links = []
    for index in range(19):
        u = (index-9)/9
        x = .156*u
        y = .176-.028*abs(u)**2.3
        z = 1.313+.057*u*u
        links.append([(x+.012*math.cos(a),y+.004*math.cos(a)*(1 if index%2 else -1),
                       z+.008*math.sin(a)) for a in (i*math.tau/10 for i in range(10))])
    tube("Chain of office","Chest",gold,links,.0025,6,True)
    place_medallion("Chest",(0,.181,1.293),.016)
    for side, sign in (("Left",-1),("Right",1)):
        turned("Royal wool chausses",side+"UpperLeg",strap,
               [(.503,.066,.069),(.55,.074,.073),(.73,.085,.077),(.92,.087,.089)],
               center=(sign*.11,0),sections=32)


def assemble(team):
    global rig, collection, parts, gold, bright, jewel, plate_steel, royal_edge, velvet, strap, thread, fur, hair, hair_highlight, engraving, brow_hair
    bpy.ops.wm.open_mainfile(filepath=str(ART_SOURCE/"SK_CastleGuard/SK_CastleGuard.blend"))
    collection = bpy.data.collections["EXPORT"]
    rig, = (obj for obj in collection.objects if obj.type=="ARMATURE")
    original, = (obj for obj in collection.objects if obj.type=="MESH")
    stem = "SK_FortressKing_"+team
    rig.name = stem
    rig.data.name = stem+"Skeleton"
    parts = []
    colors,gold_color,gem_color,hair_color = TEAMS[team]
    gold = material("King_Gold",gold_color,.91,.34,230,.00018,"steel")
    bright = material("King_PolishedGold",(.57,.382,.157),.93,.28,260,.00016,"steel")
    jewel = material("King_RoyalGem",gem_color,.24,.17,0,0,"")
    jewel.node_tree.nodes.get("Principled BSDF").inputs["Coat Weight"].default_value = .28
    engraving = material("King_ChasedInlay",tuple(c*.63 for c in gold_color),.84,.41,265,.00016,"steel")
    plate_steel = material("King_TemperedSteel",(.094,.109,.134) if team=="TeamA" else (.107,.078,.072),
                           .9,.38,225,.0002,"steel")
    royal_edge = material("King_Silver" if team=="TeamA" else "King_EngravedBronze",
                          (.39,.429,.454) if team=="TeamA" else (.411,.255,.077),.9,.32,250,.00015,"steel")
    velvet = material("King_RoyalWool",colors,0,.84,320,.00042,"cloth")
    strap = material("King_WaxedLeather",(.046,.027,.016),0,.68,285,.00056,"leather")
    thread = material("King_GoldThread",(.288,.175,.058),.1,.76,330,.00023,"cloth")
    fur = material("King_MantleLining",(.49,.397,.276) if team=="TeamA" else (.188,.096,.056),
                   0,.87,295,.0012,"cloth")
    hair = material("King_Beard",hair_color,0,.68,460,.00036,"leather")
    hair_highlight = material("King_Beard_Highlight",tuple(value*1.5 for value in hair_color),
                              0,.72,460,.00022,"leather")
    brow_hair = material("King_BrowHair",(.048,.035,.026) if team=="TeamA" else (.042,.020,.011),0,.71,0,0,"")
    retain_fitted_limbs(original)
    head_start = len(parts)
    face,scalp,skin = graft_head(team)
    eyebrows(team,face)
    beard(team,face)
    crown(team,scalp)
    seat_head(parts[head_start:],skin)
    regalia(team)
    neck_opening(skin)
    drapery(team)
    select(parts)
    bpy.ops.object.join()
    mesh = bpy.context.object
    mesh.name = stem+"_Surface"
    for current in list(mesh.users_collection):
        if current!=collection:
            current.objects.unlink(mesh)
    if collection not in mesh.users_collection[:]:
        collection.objects.link(mesh)
    bpy.context.scene.cursor.location = (0,0,0)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    mesh.parent = rig
    atlas = mesh.data.uv_layers.new(name="UV0",do_init=False)
    mesh.data.uv_layers.active = atlas
    atlas.active_render = True
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.uv.smart_project(island_margin=.0016,margin_method="FRACTION")
    bpy.ops.object.mode_set(mode="OBJECT")
    triangulate = mesh.modifiers.new("Export triangulation","TRIANGULATE")
    triangulate.keep_custom_normals = True
    skin = mesh.modifiers.new("Common humanoid skin","ARMATURE")
    skin.object = rig
    weights = {g.index:g.name for g in mesh.vertex_groups}
    assert all(abs(sum(g.weight for g in v.groups)-1)<.0001 for v in mesh.data.vertices)
    assert all(weights[g.group] in rig.data.bones for v in mesh.data.vertices for g in v.groups)
    scene = bpy.context.scene
    scene.frame_set(1)
    return stem,mesh,triangulate


def review(path, profile_only=False):
    def rotate(name,axis,angle):
        pose = rig.pose.bones[name]
        rest = pose.bone.matrix_local.to_quaternion()
        pose.rotation_mode = "QUATERNION"
        pose.rotation_quaternion = rest.inverted()@Quaternion(axis,math.radians(angle))@rest

    for name,angle in (("LeftUpperArm",-76),("RightUpperArm",75)):
        rotate(name,(0,1,0),angle)
    rotate("LeftLowerArm",(1,0,0),-10)
    rotate("RightLowerArm",(1,0,0),-7)
    scene = bpy.context.scene
    camera = studio_camera(scene)
    scene.cycles.samples = 24
    scene.render.resolution_x = 900
    scene.render.resolution_y = 1125
    camera.data.ortho_scale = 2.27
    if not profile_only:
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        camera.location = (-2.1,-5.2,1.83)
        camera.rotation_euler = (Vector((0,0,1.03))-camera.location).to_track_quat("-Z","Y").to_euler()
        scene.render.filepath = str(path.with_name(path.stem+"Back.png"))
        bpy.ops.render.render(write_still=True)
        camera.location = (1.28,5.1,1.69)
        camera.rotation_euler = (Vector((0,.034,1.558))-camera.location).to_track_quat("-Z","Y").to_euler()
        camera.data.ortho_scale = .715
        key = bpy.data.objects["Soft key"]
        key.location = (1.38,2.45,2.4)
        key.rotation_euler = (Vector((0,.06,1.606))-key.location).to_track_quat("-Z","Y").to_euler()
        key.data.energy = 196
        key.data.size = 1.17
        bpy.data.objects["Front fill"].data.energy = 123
        scene.render.resolution_x = 1080
        scene.render.resolution_y = 1350
        scene.cycles.samples = 40
        scene.render.filepath = str(path.with_name(path.stem+"Portrait.png"))
        bpy.ops.render.render(write_still=True)
    rotate("Head",(0,0,1),-19)
    bpy.data.objects["Studio shadow floor"].hide_render = True
    camera.location = (4.6,.42,1.81)
    camera.rotation_euler = (Vector((0,.033,1.67))-camera.location).to_track_quat("-Z","Y").to_euler()
    camera.data.ortho_scale = .61
    scene.render.resolution_x = 760
    scene.render.resolution_y = 920
    scene.cycles.samples = 26
    scene.render.filepath = str(path.with_name(path.stem+"Profile.png"))
    bpy.ops.render.render(write_still=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--team",choices=TEAMS)
    parser.add_argument("--bake",type=int,default=4096)
    parser.add_argument("--no-preview",action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    for team in (args.team,) if args.team else TEAMS:
        stem,mesh,triangulate = assemble(team)
        folder = ART_SOURCE/"FortressKings"/team
        folder.mkdir(parents=True,exist_ok=True)
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=str(folder/(stem+".blend")))
        bpy.context.view_layer.objects.active = mesh
        bpy.ops.object.modifier_apply(modifier=triangulate.name)
        if args.bake:
            bake_atlas(mesh,args.bake,GAME_ART/"Textures/Characters/FortressKings"/team,"T_FortressKing_"+team,"UV0")
        # The editable source keeps the scout face UVs. FBX uses the baked atlas as its first channel.
        for uv in list(mesh.data.uv_layers):
            if uv.name!="UV0":
                mesh.data.uv_layers.remove(uv)
        export_collection(GAME_ART/"Characters/Models"/(stem+".fbx"),bake_animation=False)
        print(f"{stem}: {len(mesh.data.polygons)} triangles and {len(rig.data.bones)} bones",flush=True)
        if not args.no_preview:
            bpy.ops.wm.open_mainfile(filepath=str(folder/(stem+".blend")))
            refresh_rig()
            review(folder/("FortressKing_"+team+".png"))


def refresh_rig():
    global rig
    rig, = (obj for obj in bpy.data.collections["EXPORT"].objects if obj.type=="ARMATURE")


if __name__=="__main__":
    main()
