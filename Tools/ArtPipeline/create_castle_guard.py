"""Build the original Castle Guard on the project's humanoid skeleton."""

import math
from pathlib import Path
from random import Random
import argparse
import sys

import bpy
from mathutils import Quaternion, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from character_art import bake_atlas, material, studio_camera
from export_fbx import export_collection

root = Path(__file__).resolve().parents[2]
prototype_source = root / "ArtSource/Characters/SK_PrototypeHumanoid/SK_PrototypeHumanoid.blend"
bpy.ops.wm.open_mainfile(filepath=str(prototype_source))
scene = bpy.context.scene
collection = bpy.data.collections["EXPORT"]
rig = next(obj for obj in collection.objects if obj.type == "ARMATURE")
rig.name = "SK_CastleGuard"
rig.data.name = "CastleGuardSkeleton"
rig.animation_data_clear()
for obj in list(collection.objects):
    if obj.type == "MESH":
        bpy.data.objects.remove(obj, do_unlink=True)
for action in list(bpy.data.actions):
    bpy.data.actions.remove(action)


steel = material("Guard_BlackenedSteel", (.094,.105,.114), .92,.43, 205,.00023,"steel")
edge = material("Guard_BurnishedSteel", (.21,.219,.218), .91,.32, 250,.00016,"steel")
brass = material("Guard_AntiqueBrass", (.34,.211,.082), .84,.42, 240,.00025,"steel")
mail = material("Guard_Chainmail", (.11,.122,.127), .88,.47, 260,.0002,"steel")
leather = material("Guard_Leather", (.054,.031,.019), 0,.72, 420,.00057,"leather")
cloth = material("Guard_Oxblood", (.113,.011,.017), 0,.83, 385,.00043,"cloth")
shadow = material("Guard_VisorInterior", (.008,.010,.012), 0,.91, 0,0,"")
thread = material("Guard_SandThread", (.205,.113,.047), 0,.88, 500,.00022,"cloth")
iron = material("Guard_OiledIron", (.039,.047,.051), .74,.53, 180,.0004,"steel")
padded = material("Guard_PaddedLinen", (.021,.018,.015), 0,.86, 260,.00048,"cloth")
parts = []
rng = Random(7734)


def finish(obj, name, bone, mat, smooth=True):
    obj.name = name
    obj.data.materials.append(mat)
    for face in obj.data.polygons:
        face.use_smooth = smooth
    group = obj.vertex_groups.new(name=bone)
    group.add(list(range(len(obj.data.vertices))), 1, "REPLACE")
    parts.append(obj)


def profile(name, bone, mat, origin, rings, axis=(0, 0, 1), count=32,
            arc=(-math.pi, math.pi), ridge=0):
    """Form armor from elliptical cross-sections, with an optional raised keel."""
    center = Vector(origin)
    closed = math.isclose(arc[1] - arc[0], math.tau)
    width = count if closed else count + 1
    vertices = []
    for z, x_radius, y_radius, offset in rings:
        for column in range(width):
            angle = arc[0] + (arc[1] - arc[0]) * column / count
            front = max(0, math.cos(angle))
            point = Vector((x_radius * math.sin(angle),
                            y_radius * math.cos(angle) + offset + ridge * front ** 10, z))
            if abs(axis[0]) == 1:
                vertices.append(center + Vector((axis[0] * point.z, point.y, point.x)))
            else:
                vertices.append(center + point)
    faces = []
    for row in range(len(rings) - 1):
        for column in range(count):
            next_column = (column + 1) % width
            start = row * width
            faces.append((start + column, start + width + column,
                          start + width + next_column, start + next_column))
    if closed:
        faces.extend((tuple(range(width)), tuple(reversed(range(len(vertices) - width, len(vertices))))))
    if axis[0] == 1:
        faces = [tuple(reversed(face)) for face in faces]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    if not closed:
        bpy.context.view_layer.objects.active = obj
        solid = obj.modifiers.new("Folded plate edges", "SOLIDIFY")
        solid.thickness = .006
        bpy.ops.object.modifier_apply(modifier=solid.name)
    finish(obj, name, bone, mat)


def oval(name, bone, mat, center, scale, segments=12, rings=6):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=center)
    obj = bpy.context.object
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    finish(obj, name, bone, mat)


def plate(name, bone, mat, points, thickness=.01, chamfer=.004):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(points, [], [tuple(range(len(points)))])
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    solid = obj.modifiers.new("Plate thickness", "SOLIDIFY")
    solid.thickness = thickness
    bpy.ops.object.modifier_apply(modifier=solid.name)
    bevel = obj.modifiers.new("Chamfer", "BEVEL")
    bevel.width = chamfer
    bevel.segments = 2
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    finish(obj, name, bone, mat, smooth=False)


def stud(bone, point, radius=.008):
    oval("Peened brass rivet", bone, brass, point, (radius, radius * .65, radius))


def surface(name, bone, mat, points, faces, thickness=0, smooth=True):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(points, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    if thickness:
        bpy.context.view_layer.objects.active = obj
        solid = obj.modifiers.new("Material thickness", "SOLIDIFY")
        solid.thickness = thickness
        bpy.ops.object.modifier_apply(modifier=solid.name)
    finish(obj, name, bone, mat, smooth)


def tubes(name, bone, mat, paths, radius, sides=6, closed=False):
    """Build constant-radius piping, stitching or links without curve export objects."""
    vertices, faces = [], []
    for path in paths:
        start = len(vertices)
        path = [Vector(point) for point in path]
        for index, point in enumerate(path):
            prev = path[index-1] if closed or index else point
            after = path[(index+1) % len(path)] if closed or index < len(path)-1 else point
            tangent = (after-prev).normalized()
            rotation = tangent.to_track_quat("Z", "Y")
            for side in range(sides):
                angle = math.tau * side / sides
                vertices.append(point + rotation @ Vector((radius*math.cos(angle),radius*math.sin(angle),0)))
        for index in range(len(path) if closed else len(path)-1):
            first = start+index*sides
            second = start+((index+1) % len(path))*sides
            for side in range(sides):
                next_side = (side+1) % sides
                faces.append((first+side,first+next_side,second+next_side,second+side))
        if not closed:
            faces.append(tuple(start+i for i in reversed(range(sides))))
            last = start+(len(path)-1)*sides
            faces.append(tuple(last+i for i in range(sides)))
    surface(name,bone,mat,vertices,faces)


def panel(name, bone, center_x, rows, facing=1, columns=16):
    """Drape a thick textile over its silhouette, with independently placed hems."""
    points = []
    for width, depth, height, fold, split in rows:
        for index in range(columns+1):
            u = 2*index/columns-1
            phase = (rows[0][2]-height)*5
            drape = fold*(.71*math.cos(3.2*math.pi*u+phase)+.36*math.sin(5.1*math.pi*u-phase*.5))
            points.append((center_x+width*u,facing*(depth+drape),height+split*(1-abs(u))**2))
    faces = []
    for row in range(len(rows)-1):
        for index in range(columns):
            a = row*(columns+1)+index
            faces.append((a,a+1,a+columns+2,a+columns+1))
    surface(name,bone,cloth,points,faces,.006)
    left = [points[row*(columns+1)] for row in range(len(rows))]
    right = [points[row*(columns+1)+columns] for row in range(len(rows))]
    hem = points[-columns-1:]
    tubes("Bound wool edging",bone,thread,[left,right,hem],.00185)
    stitches = []
    for line in (left,right):
        for a,b in zip(line,line[1:]):
            a,b = Vector(a),Vector(b)
            for index in range(max(1,int((b-a).length/.012))):
                fraction = index/max(1,int((b-a).length/.012))
                p = a.lerp(b,fraction)
                p.x += .005 if line is left else -.005
                p.y += facing*.0038
                stitches.append((p,p+Vector((0,0,-.005))))
    tubes("Hand-sewn hem stitches",bone,thread,stitches,.00085,4)


def chain_strip(name, bone, center, columns, rows, radius, facing=1):
    cx,cy,cz = center
    paths = []
    for row in range(rows):
        for col in range(columns):
            x = (col - (columns-1)/2 + .5*(row%2))*radius*1.48
            z = (row - (rows-1)/2)*radius*1.24
            tilt = (1 if row%2 else -1)*.54
            paths.append([(cx+x+radius*math.cos(a),cy+facing*(tilt*radius*math.cos(a)-1.7*x*x),
                           cz+z+radius*math.sin(a))
                          for a in (i*math.tau/8 for i in range(8))])
    tubes(name,bone,mail,paths,radius*.26,3,closed=True)


def bind_joint(obj, near_bone, far_bone, blend):
    near = obj.vertex_groups.get(near_bone) or obj.vertex_groups.new(name=near_bone)
    far = obj.vertex_groups.get(far_bone) or obj.vertex_groups.new(name=far_bone)
    matrix = obj.matrix_world
    for vertex in obj.data.vertices:
        t = min(1,max(0,blend(matrix @ vertex.co)))
        far_weight = t*t*(3-2*t)
        near.add([vertex.index],1-far_weight,"REPLACE")
        far.add([vertex.index],far_weight,"REPLACE")


profile("Quilted waist", "Spine", padded, (0, 0, 0),
        [(.89,.17,.113,0),(1.03,.162,.106,0),(1.17,.145,.101,0),(1.28,.17,.109,0)])
chest_rings = [(1.125,.157,.101,0),(1.144,.165,.111,0),(1.185,.176,.132,0),(1.286,.21,.147,0),
               (1.367,.224,.136,0),(1.417,.21,.112,0),(1.446,.094,.087,0),(1.456,.083,.078,0)]
profile("Breastplate", "Chest", steel, (0, 0, 0), chest_rings,count=56,ridge=.025)


def breastplate_depth(x, z):
    for lower, upper in zip(chest_rings,chest_rings[1:]):
        if lower[0] <= z <= upper[0]:
            t = (z-lower[0])/(upper[0]-lower[0])
            xr = lower[1]+(upper[1]-lower[1])*t
            yr = lower[2]+(upper[2]-lower[2])*t
            front = math.sqrt(max(0,1-(x/xr)**2))
            return yr*front+.025*front**10
    raise ValueError(f"Breastplate query outside profile: {z}")


scuffs = []
for sign in (-1,1):
    for i in range(14):
        x = sign*rng.uniform(.101,.17)
        z = rng.uniform(1.22,1.37)
        dx = rng.uniform(.004,.021)
        dz = rng.uniform(.002,.009)
        scuffs.append([(px,breastplate_depth(px,pz)+.0006,pz)
                       for px,pz in ((x,z),(x+dx*.6,z+dz*.72),(x+dx,z+dz))])
tubes("Breastplate wear marks","Chest",edge,scuffs,.00055,4)
profile("Padded arming collar","Neck",padded,(0,0,0),
        [(1.413,.094,.082,0),(1.466,.08,.076,0),(1.51,.076,.071,0),(1.529,.072,.068,0)])
profile("Turned gorget", "Chest", steel, (0, 0, 0),
        [(1.421,.113,.095,0),(1.429,.114,.097,0),(1.44,.103,.091,0),
         (1.457,.082,.078,0),(1.469,.08,.076,0)])
profile("Gorget rolled collar", "Chest", edge, (0,0,0),
        [(1.459,.083,.079,0),(1.466,.084,.080,0),(1.47,.081,.077,0)],count=40)
for sign in (-1,1):
    chain_strip("Mail at the armhole", "Chest", (sign*.176,.085,1.371), 5, 5, .008)
    plate("Upper harness strap","Chest",leather,
          [(sign*x,y,z) for x,y,z in [(.115,.136,1.322),(.149,.124,1.308),(.196,.074,1.423),
                                     (.182,.02,1.454),(.151,.022,1.456),(.165,.074,1.417)]],.006,.002)
    for z,y in ((1.39,.111),(1.365,.122),(1.338,.129)):
        stud("Chest",(sign*(.13+(z-1.338)*.58),y,z),.004)
for index in range(4):
    z = 1.139 - index * .045
    waist_bone = "Spine" if index < 2 else "Hips"
    xr = .17+index*.012
    yr = .121+index*.006
    profile("Overlapping fauld",waist_bone,steel,(0,0,z),
            [(0,xr,yr,0),(.005,xr+.001,yr+.001,0),(.017,xr-.002,yr-.003,0),
             (.05,xr-.011,yr-.007,0)],count=40,ridge=.005)
    profile("Fauld turned hem",waist_bone,edge,(0,0,z),
            [(0,xr+.0005,yr+.0005,0),(.003,xr+.002,yr+.002,0),
             (.006,xr+.0014,yr+.0014,0)],count=40,ridge=.005)
    for side in (-1,1):
        stud(waist_bone,(side*.102,yr*math.sqrt(1-(.102/xr)**2)+.001,z+.016),.0045)

for facing in (-1,1):
    chain_strip("Hanging riveted mail", "Hips", (0,facing*.112,.934), 19, 7, .0105,facing)
profile("Double-stitched belt", "Hips", leather, (0, 0, 0),
        [(.986,.205,.146,0),(.993,.209,.151,0),(1.034,.199,.146,0),(1.043,.195,.143,0)])
for z in (.995,1.035):
    tubes("Belt stitching","Hips",thread,
          [[(.205*math.sin(a),.151*math.cos(a),z),(.205*math.sin(a+.024),.151*math.cos(a+.024),z)]
           for a in (i*math.tau/88 for i in range(88))],.001,4)
tubes("Cast octagonal buckle","Hips",brass,
      [[(x,.163,z) for x,z in [(-.037,.998),(-.027,.982),(.027,.982),(.037,.998),
                                (.037,1.023),(.027,1.037),(-.027,1.037),(-.037,1.023)]]],
      .0046,8,closed=True)
tubes("Buckle tongue","Hips",edge,[[(-.03,.169,1.01),(0,.175,1.01),(.036,.168,1.012)]],.0026)
for sign in (-1,1):
    for index in range(3):
        x = sign*(.052+.016*index)
        oval("Belt adjustment hole","Hips",shadow,(x,.153-abs(x)*.13,1.01),(.003,.002,.003))
    start = len(parts)
    panel("Forked wool tabard", "Hips", sign*.045,
          [(.039,.149,1.022,.001,0),(.042,.163,.973,.006,0),(.041,.162,.905,.008,0),
           (.037,.174,.84,.01,.009),(.03,.17,.755+sign*.018,.015,.017)])
    thigh = "LeftUpperLeg" if sign < 0 else "RightUpperLeg"
    for part in parts[start:]:
        bind_joint(part,"Hips",thigh,lambda point: .58*(.991-point.z)/.24)
panel("Garrison surcoat", "Chest", 0,
      [(.09,.137,1.42,.003,0),(.097,.16,1.385,.005,0),(.097,.18,1.335,.005,0),
       (.087,.182,1.28,.004,0),(.078,.174,1.216,.004,0),(.071,.163,1.178,.005,-.025)])
crest = [(-.067,1.263),(-.067,1.336),(-.052,1.336),(-.052,1.325),(-.039,1.325),
         (-.039,1.336),(-.025,1.336),(-.025,1.303),(-.018,1.303),(-.018,1.361),
         (-.007,1.361),(-.007,1.349),(.007,1.349),(.007,1.361),(.018,1.361),
         (.018,1.303),(.025,1.303),(.025,1.336),(.039,1.336),(.039,1.325),(.052,1.325),
         (.052,1.336),(.067,1.336),(.067,1.263),(.049,1.242),(0,1.22),(-.049,1.242)]
crest_points = [(x*.93,.190+.042*(z-1.28),1.284+(z-1.284)*.92) for x,z in crest]
plate("Cast three-tower badge", "Chest", brass, crest_points, .0034, .0012)
tubes("Chased badge border","Chest",edge,[crest_points],.0011,5,closed=True)
for x,z in ((-.0437,1.302),(0,1.323),(.0437,1.302)):
    y = .1945
    plate("Arrow slit engraving","Chest",iron,
          [(x-.003,y,z-.006),(x+.003,y,z-.006),(x+.003,y,z+.005),(x,y,z+.009),
           (x-.003,y,z+.005)],.0008,.0004)
plate("Archway engraving","Chest",iron,
      [(-.011,.1938,1.242),(.011,.1938,1.242),(.011,.1938,1.265),(0,.1938,1.274),
       (-.011,.1938,1.265)],.001,.0006)
for sign in (-1, 1):
    plate("Breastplate chased edge", "Chest", edge,
          [(sign*x,y,z) for x,y,z in [(.109,.124,1.404),(.168,.092,1.38),
           (.154,.109,1.296),(.117,.112,1.209),(.107,.12,1.211),(.145,.118,1.30),
           (.155,.103,1.374),(.101,.133,1.40)]], .004, .001)
    stud("Chest", (sign*.107,.13,1.397))
panel("Pleated rear mantle", "Chest", 0,
      [(.14,.107,1.443,.001,0),(.18,.154,1.391,.006,0),(.172,.17,1.31,.01,0),
       (.159,.172,1.224,.014,0),(.149,.179,1.153,.021,-.055)], -1)
for sign in (-1,1):
    stud("Chest",(sign*.133,-.119,1.426),.009)
    plate("Mantle shoulder clasp","Chest",brass,
          [(sign*x,y,z) for x,y,z in [(.103,-.109,1.445),(.163,-.109,1.448),
           (.175,-.126,1.428),(.146,-.145,1.412),(.109,-.13,1.422)]],.005,.002)

profile("Utility pouch","Hips",leather,(.123,-.132,0),
        [(.891,.03,.023,0),(.901,.048,.032,0),(.928,.057,.037,0),
         (.982,.055,.035,0),(1.009,.041,.024,0)],count=24)
pouch_flap = [(.077,-.161,1.006),(.171,-.161,1.006),(.181,-.172,.98),
              (.164,-.175,.957),(.126,-.179,.946),(.091,-.176,.957),(.068,-.17,.98)]
plate("Pouch flap","Hips",leather,pouch_flap,.004,.002)
tubes("Pouch flap seam","Hips",thread,[pouch_flap[1:] + [pouch_flap[0]]],.001,5)
oval("Pouch toggle","Hips",brass,(.126,-.18,.96),(.006,.003,.007))
for x in (.088,.159):
    plate("Pouch belt loop","Hips",leather,
          [(x-.009,-.138,1.046),(x+.009,-.138,1.046),(x+.009,-.16,1.003),
           (x-.009,-.16,1.003)],.004,.001)

profile("Padded helmet interior", "Head", shadow, (0,0,0),
        [(1.5,.073,.068,0),(1.62,.094,.080,0),(1.715,.109,.101,0),(1.839,.042,.053,-.013)])
skull_rings = [(1.676,.119,.114,-.002),(1.698,.127,.124,-.009),(1.745,.124,.124,-.017),
               (1.79,.106,.113,-.022),(1.836,.077,.088,-.026),(1.868,.043,.054,-.029),
               (1.882,.008,.013,-.030)]
profile("Sallet bowl","Head",steel,(0,0,0),skull_rings,count=48,ridge=.013)
profile("Sallet neck defense","Head",steel,(0,0,0),
        [(1.524,.101,.14,-.020),(1.544,.121,.151,-.026),(1.578,.128,.145,-.021),
         (1.638,.129,.128,-.019),(1.704,.12,.118,-.012)],
        count=36, arc=(1.53,4.753))
for index in range(3):
    z = 1.536 + .025*index
    profile("Overlapped neck lame","Head",edge,(0,0,z),
            [(0,.105+.007*index,.146,-.026),(.007,.111+.006*index,.149,-.026),
             (.014,.119+.004*index,.142,-.020)], count=24, arc=(1.74,4.54))

visor_rows = [(1.512,.071,.082,.013),(1.527,.094,.096,.024),(1.549,.113,.108,.037),
              (1.567,.119,.115,.044),(1.575,.121,.118,.048),(1.587,.124,.119,.048),
              (1.605,.125,.120,.044),(1.614,.126,.120,.041),(1.64,.127,.120,.030),
              (1.651,.127,.120,.026),(1.658,.125,.119,.024)]
visor_steps = 56
angles = [-1.82 + 3.64 * i/visor_steps for i in range(visor_steps+1)]
visor_points = []
for z, xr, yr, keel in visor_rows:
    for angle in angles:
        front = max(0,math.cos(angle))
        visor_points.append((xr*math.sin(angle),yr*math.cos(angle)+keel*front**4,
                             z+.010*math.sin(angle)**2))
visor_faces = []
for row in range(len(visor_rows)-1):
    for column in range(visor_steps):
        slits = (16, 19, 22, 33, 36, 39) if row in (3,6) else ()
        if column in slits:
            continue
        p = row*(visor_steps+1)+column
        visor_faces.append((p,p+visor_steps+1,p+visor_steps+2,p+1))
surface("Pierced sallet visor","Head",steel,visor_points,visor_faces,.004)
tubes("Rolled sightline edge","Head",edge,[visor_points[-visor_steps-1:]],.0021)
profile("Raised brow and temple band","Head",edge,(0,0,0),
        [(1.678,.122,.128,.001),(1.682,.13,.130,.001),(1.691,.128,.126,-.004),
         (1.705,.122,.120,-.005)], count=52,arc=(-1.9,1.9),ridge=.033)
crest_seam = [(0,y_radius+offset+.013,z) for z,_,y_radius,offset in skull_rings[2:]]
crest_seam.extend((0,offset-y_radius,z) for z,_,y_radius,offset in reversed(skull_rings[2:]))
tubes("Crest seam","Head",edge,[crest_seam],.0025,8)
for sign in (-1,1):
    oval("Reinforced hinge washer","Head",iron,(sign*.126,.026,1.675),(.006,.017,.019),16,8)
    oval("Visor pivot","Head",brass,(sign*.131,.026,1.675),(.005,.008,.008))
    for row in (2,4,7):
        index = row*(visor_steps+1)+visor_steps//2+sign*18
        x,y,z = visor_points[index]
        stud("Head",(x,y+.001,z),.0028)


def gauntlet(hand, sign):
    oval("Stitched leather palm",hand,leather,(sign*.81,.001,1.421),(.054,.041,.028),16,8)
    plate("Raised metacarpal plate",hand,steel,
          [(sign*x,y,z) for x,y,z in [(.763,-.036,1.444),(.797,-.046,1.455),
           (.848,-.041,1.445),(.86,-.041,1.413),(.826,-.041,1.394),(.77,-.033,1.4)]],.008,.004)
    for index in range(4):
        z = 1.449-index*.016
        x = .867-(.006 if index == 0 else .004*abs(index-1))
        points = [(sign*(x-.028),-.006,z),(sign*x,.000,z),
                  (sign*(x+.012),.012,z-.002),(sign*(x+.007),.025,z-.004)]
        tubes("Formed glove finger",hand,leather,[points],.010,8)
        oval("Hammered knuckle",hand,edge,(sign*x,-.009,z),(.016,.01,.008))
        tubes("Finger lame",hand,steel,
              [[(sign*(x+.01),.007,z+.006),(sign*(x+.017),.012,z),
                (sign*(x+.009),.018,z-.006)]],.0035)
    tubes("Sewn thumb gusset",hand,leather,
          [[(sign*.787,.028,1.445),(sign*.801,.046,1.452),(sign*.831,.048,1.452),
            (sign*.845,.036,1.448)]],.013,8)
    oval("Hinged thumb plate",hand,steel,(sign*.811,.05,1.457),(.024,.008,.012))
    for x,z in ((.78,1.434),(.814,1.448),(.845,1.424)):
        stud(hand,(sign*x,-.046,z),.003)


def pauldron(bone, sign):
    """Fit tapered shoulder lames around a dome and roll the exposed edges."""
    angles = [-.27+3.68*i/32 for i in range(33)]

    def laminate(name, rows, row_rise):
        points = []
        for x, vertical, depth in rows:
            for angle in angles:
                peak = max(0, math.sin(angle))
                points.append((sign*(x+row_rise*peak**2),
                               depth*math.cos(angle),1.433+vertical*math.sin(angle)))
        columns = len(angles)
        faces = [(i,i+columns,i+columns+1,i+1)
                 for i in range((len(rows)-1)*columns) if i%columns<columns-1]
        if sign < 0:
            faces = [tuple(reversed(face)) for face in faces]
        surface(name,bone,steel,points,faces,.004)
        tubes("Pauldron folded edge",bone,edge,[points[-columns:]],.0017)
        for index in (4,28):
            x,y,z = points[-2*columns+index]
            oval("Pauldron strap rivet",bone,brass,(x,y*1.013,z),(.0045,.003,.0045))

    oval("Fitted arming sleeve",bone,padded,(sign*.272,0,1.410),(.089,.076,.08),20,10)
    laminate("Main shoulder plate",
             [(.181,.052,.056),(.195,.081,.084),(.21,.108,.099),
              (.24,.119,.112),(.278,.114,.114),(.305,.103,.106)],.019)
    for index in range(3):
        x = .29+.038*index
        v = .099-.011*index
        d = .102-.008*index
        laminate("Articulated shoulder lame",
                 [(x,v,d),(x+.027,v+.004,d+.004),(x+.035,v,d),
                  (x+.042,v-.005,d-.005)],.016)


for side, sign in (("Left",-1), ("Right",1)):
    upper, lower, hand = (side + suffix for suffix in ("UpperArm","LowerArm","Hand"))
    thigh, shin, foot = (side + suffix for suffix in ("UpperLeg","LowerLeg","Foot"))
    axis = (sign,0,0)
    pauldron(upper,sign)
    profile("Rerebrace", upper, steel, (sign*.318,0,1.42),
            [(0,.086,.077,0),(.019,.091,.083,0),(.13,.072,.063,0),(.163,.069,.06,0)], axis)
    oval("Elbow gusset", lower, padded, (sign*.511,0,1.42), (.074,.059,.063))
    bind_joint(parts[-1],upper,lower,lambda point: (abs(point.x)-.455)/.111)
    chain_strip("Mail elbow articulation",lower,(sign*.51,.063,1.42),7,6,.007)
    bind_joint(parts[-1],upper,lower,lambda point: (abs(point.x)-.455)/.111)
    oval("Elbow cop", lower, steel, (sign*.513,-.048,1.42), (.06,.032,.065),20,10)
    plate("Elbow wing",lower,edge,
          [(sign*x,y,z) for x,y,z in [(.464,-.059,1.426),(.5,-.071,1.493),
           (.56,-.069,1.472),(.592,-.062,1.423),(.552,-.078,1.38),(.501,-.079,1.39)]],.006,.004)
    profile("Vambrace", lower, steel, (sign*.55,0,1.42),
            [(0,.077,.079,0),(.027,.082,.081,0),(.139,.053,.049,0),(.206,.046,.044,0)], axis)
    profile("Gauntlet cuff", lower, edge, (sign*.734,0,1.42),
            [(0,.052,.05,0),(.025,.049,.046,0),(.031,.047,.044,0)], axis)
    for x,radius in ((.587,.076),(.701,.054)):
        profile("Vambrace strap",lower,leather,(sign*x,0,1.42),
                [(0,radius,radius,0),(.013,radius*.98,radius*.98,0)],axis,count=20)
        stud(lower,(sign*(x+.006),.015,1.42+radius*.97),.004)
    gauntlet(hand,sign)
    x = sign*.11
    profile("Quilted chausses", thigh, padded, (x,0,0),
            [(.52,.067,.069,0),(.59,.077,.075,-.004),(.73,.087,.082,-.005),
             (.88,.096,.096,0),(.927,.095,.095,-.002)])
    profile("Forged cuisse",thigh,steel,(x,0,0),
            [(.558,.076,.079,0),(.59,.086,.084,0),(.685,.098,.097,0),(.772,.103,.102,0)],
            arc=(-1.4,1.4),ridge=.013)
    for index in range(4):
        z = .866-.038*index
        width = .098-.003*index
        yr = .102-.003*index
        profile("Tasset lame",thigh,steel,(x,.018,z),
                [(0,width,yr,0),(.006,width+.001,yr+.002,0),
                 (.02,width-.002,yr-.002,0),(.044,width-.006,yr-.008,0)],
                count=36,arc=(-1.24,1.24),ridge=.008)
        for offset in (-.064,.064):
            front = math.sqrt(1-(offset/width)**2)
            stud(thigh,(x+offset,.018+yr*front,z+.014),.0036)
    profile("Pointed poleyn cup",shin,steel,(x,.041,.539),
            [(-.066,.026,.038,0),(-.043,.065,.053,0),(-.002,.074,.071,0),
             (.03,.068,.06,0),(.072,.027,.038,0)],count=28,arc=(-1.53,1.53),ridge=.021)
    plate("Flared knee fan",shin,edge,
          [(x+sign*dx,y,z) for dx,y,z in [(.044,.096,.582),(.071,.077,.586),
           (.095,.053,.563),(.104,.046,.542),(.086,.058,.522),
           (.064,.079,.512),(.043,.096,.523)]],.005,.003)
    stud(shin,(x+sign*.072,.078,.55),.0048)
    profile("Keel greave", shin, steel, (x,0,0),
            [(.125,.049,.053,0),(.157,.059,.066,0),(.31,.073,.078,-.006),
             (.43,.075,.075,-.009),(.48,.069,.063,0)], ridge=.016)
    plate("Chased greave ridge", shin, edge,
          [(x,.071,.17),(x+.008,.091,.37),(x,.081,.463),(x-.008,.091,.37)],
          .005, .001)
    for z,radius in ((.201,.065),(.385,.077)):
        profile("Greave hinge strap",shin,leather,(x,0,z),
                [(0,radius,radius,0),(.015,radius,radius,0)],count=20,arc=(1.3,4.98))
        stud(shin,(x+sign*radius,-.01,z+.008),.0048)
    profile("Leather boot and sole",foot,leather,(x,0,0),
            [(.009,.058,.136,.058),(.018,.065,.145,.058),(.041,.063,.14,.056),
             (.057,.06,.107,.037),(.112,.051,.061,-.007),(.142,.048,.049,-.012)])
    profile("Armored heel and ankle",foot,steel,(x,0,0),
            [(.026,.055,.081,.005),(.042,.06,.095,.016),(.083,.057,.087,.016),
             (.11,.052,.069,-.004),(.129,.049,.053,-.011)],count=24)
    for index in range(5):
        y = .077+.026*index
        width = .067-.004*index
        z = .09-.009*index
        sample_angles = [i*.155 for i in range(-10,11)]
        lame_rows = [(y-.025,z-.004,width*.92),(y-.01,z+.003,width),
                     (y+.005,z+.001,width),(y+.017,z-.004,width*.96)]
        points = [(x+w*math.sin(a),d+.008*math.cos(a),h-.031*abs(math.sin(a))**2)
                  for d,h,w in lame_rows for a in sample_angles]
        columns = len(sample_angles)
        faces = [(i,i+1,i+columns+1,i+columns) for i in range(3*columns) if i%columns<columns-1]
        surface("Overlapping sabaton toe lame",foot,steel,points,faces,.003)
        tubes("Sabaton turned ridge",foot,edge,[points[-columns:]],.0015)

bpy.ops.object.select_all(action="DESELECT")
for obj in parts:
    obj.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
mesh = bpy.context.object
mesh.name = "CastleGuard_Armor"
scene.cursor.location = (0,0,0)
bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
for current in list(mesh.users_collection):
    if current != collection:
        current.objects.unlink(mesh)
if collection not in mesh.users_collection[:]:
    collection.objects.link(mesh)
mesh.parent = rig
skin = mesh.modifiers.new("Humanoid Skin", "ARMATURE")
skin.object = rig
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.mesh.normals_make_consistent(inside=False)
bpy.ops.uv.smart_project(island_margin=.002, margin_method="FRACTION")
bpy.ops.object.mode_set(mode="OBJECT")
triangulate = mesh.modifiers.new("Export Triangulation", "TRIANGULATE")
triangulate.keep_custom_normals = True
bpy.ops.object.modifier_move_up(modifier=triangulate.name)

assert set(group.name for group in mesh.vertex_groups) <= set(rig.data.bones.keys())
assert all(abs(sum(weight.weight for weight in vertex.groups) - 1) < 1e-6
           for vertex in mesh.data.vertices), "Armor has unweighted vertices"
scene.frame_set(1)
bpy.context.preferences.filepaths.save_version = 0
source = root / "ArtSource/Characters/SK_CastleGuard"
source.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(source / "SK_CastleGuard.blend"))
export_collection(root / "Assets/Art/Characters/Models/SK_CastleGuard.fbx", bake_animation=False)
mesh.data.calc_loop_triangles()
print(f"Castle Guard: {len(mesh.data.loop_triangles)} triangles, {len(rig.data.bones)} bones")


def bake_textures(size):
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.modifier_apply(modifier=triangulate.name)
    bake_atlas(mesh, size, root / "Assets/Art/Textures/Characters/CastleGuard", "T_CastleGuard")


def preview_pose(name, axis, degrees):
    pose = rig.pose.bones[name]
    rest = pose.bone.matrix_local.to_quaternion()
    pose.rotation_mode = "QUATERNION"
    pose.rotation_quaternion = rest.inverted() @ Quaternion(axis, math.radians(degrees)) @ rest


def render_previews(preview):
    for name, degrees in (("LeftUpperArm", -75), ("RightUpperArm", 73)):
        preview_pose(name,(0,1,0),degrees)
    preview_pose("LeftLowerArm",(1,0,0),-9)
    preview_pose("RightLowerArm",(1,0,0),-6)
    preview_pose("Head",(0,0,1),4)
    camera = studio_camera(scene)
    scene.render.filepath = str(preview)
    bpy.ops.render.render(write_still=True)

    for suffix, position, target, framing in (
        ("Back",(-2.3,-5.9,1.79),(0,0,.98),2.22),
        ("Detail",(1.5,4.8,1.71),(0,0,1.49),.99),
    ):
        camera.location = position
        camera.rotation_euler = (Vector(target)-camera.location).to_track_quat("-Z","Y").to_euler()
        camera.data.ortho_scale = framing
        scene.render.filepath = str(preview.with_name(preview.stem+suffix+preview.suffix))
        bpy.ops.render.render(write_still=True)

    action_names = {"AN_PrototypeHumanoid_Attack":("Attack",12),
                    "AN_PrototypeHumanoid_Run":("Run",6)}
    with bpy.data.libraries.load(str(prototype_source)) as (data_from,data_to):
        data_to.actions = [name for name in data_from.actions if name in action_names]
    rig.animation_data_create()
    camera.location = (2.8,5.9,2.19)
    camera.rotation_euler = (Vector((0,0,.98))-camera.location).to_track_quat("-Z","Y").to_euler()
    camera.data.ortho_scale = 2.44
    for action in data_to.actions:
        suffix, frame = action_names[action.name]
        rig.animation_data.action = action
        rig.animation_data.action_slot = action.slots[0]
        scene.frame_set(frame)
        scene.render.filepath = str(preview.with_name(preview.stem+suffix+preview.suffix))
        bpy.ops.render.render(write_still=True)


if "--" in sys.argv:
    parser = argparse.ArgumentParser()
    parser.add_argument("preview", type=Path, nargs="?")
    parser.add_argument("--bake", type=int, metavar="SIZE")
    options = parser.parse_args(sys.argv[sys.argv.index("--")+1:])
    if options.bake:
        bake_textures(options.bake)
    if options.preview:
        render_previews(options.preview.resolve())
