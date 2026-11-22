"""Build the neutral camp knight in Blender using the shared humanoid rig."""

import argparse
import math
from pathlib import Path
import sys

import bpy
from mathutils import Quaternion, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from character_art import bake_atlas, material, studio_camera
from creature_mesh import CreatureMesh, ramp, select
from export_fbx import export_collection


ROOT = Path(__file__).resolve().parents[2]
STEM = "SK_NeutralKnight"
SOURCE = ROOT / "ArtSource/Characters" / STEM
TEXTURES = ROOT / "Assets/Art/Textures/Characters/NeutralKnight"
MODEL = ROOT / "Assets/Art/Characters/Models" / (STEM + ".fbx")
ATLAS = "T_NeutralKnight"


class Knight(CreatureMesh):
    rig_stem = STEM

    def __init__(self):
        prototype = ROOT / "ArtSource/Characters/SK_PrototypeHumanoid/SK_PrototypeHumanoid.blend"
        bpy.ops.wm.open_mainfile(filepath=str(prototype))
        self.scene = bpy.context.scene
        self.collection = bpy.data.collections["EXPORT"]
        self.rig = next(obj for obj in self.collection.objects if obj.type == "ARMATURE")
        self.rig.name = self.rig_stem
        self.rig.data.name = self.rig_stem.removeprefix("SK_")+"Skeleton"
        self.rig.animation_data_clear()
        for obj in list(self.collection.objects):
            if obj.type == "MESH":
                bpy.data.objects.remove(obj, do_unlink=True)
        self.parts = []
        self.steel = self.blackened_steel()
        self.edge = material("Knight_BurnishedEdges", (.208,.223,.233), .91,.34, 290,.00022,"steel")
        self.iron = material("Knight_DarkIron", (.042,.051,.058), .78,.54, 240,.00043,"steel")
        self.mail = material("Knight_RivetedMail", (.103,.112,.118), .84,.47, 260,.00020,"steel")
        self.brass = material("Knight_AgedBrass", (.225,.125,.056), .71,.49, 280,.00036,"steel")
        self.leather = material("Knight_Leather", (.057,.032,.020), 0,.73, 440,.00032,"leather")
        self.linen = material("Knight_QuiltedLinen", (.04,.047,.044), 0,.85, 320,.00046,"cloth")
        self.cloth = material("Knight_OxbloodWool", (.077,.011,.014), 0,.88, 348,.00062,"cloth")
        self.thread = material("Knight_FlaxStitching", (.136,.094,.059), 0,.87, 490,.00036,"cloth")
        self.bone = material("Knight_OldIvoryPaint", (.343,.278,.177), .12,.68, 340,.00046,"cloth")
        self.shadow = material("Knight_VisorShadow", (.008,.010,.011), 0,.93, 0,0,"")
        self.shield_wood = material("Knight_EbonizedOak", (.045,.027,.021), 0,.69, 360,.00060,"leather")
        wood_nodes, wood_links = self.shield_wood.node_tree.nodes,self.shield_wood.node_tree.links
        coord = next(node for node in wood_nodes if node.type=="TEX_COORD")
        grain = wood_nodes.new("ShaderNodeVectorMath")
        grain.operation = "MULTIPLY"
        grain.inputs[1].default_value = (.12,.8,5.3)
        wood_links.new(coord.outputs["Object"],grain.inputs[0])
        for node in list(wood_nodes):
            if node.type=="TEX_NOISE":
                wood_links.new(grain.outputs["Vector"],node.inputs["Vector"])

    @staticmethod
    def blackened_steel():
        mat = material("Knight_BlackenedSteel", (.073,.084,.095), .88,.44, 270,.00055,"steel")
        nodes,links = mat.node_tree.nodes,mat.node_tree.links
        shader = nodes["Principled BSDF"]
        coord = nodes.new("ShaderNodeTexCoord").outputs["Object"]
        noise = nodes.new("ShaderNodeTexNoise")
        links.new(coord,noise.inputs["Vector"])
        noise.inputs["Scale"].default_value = 33
        noise.inputs["Detail"].default_value = 5
        noise.inputs["Roughness"].default_value = .67
        tarnish = nodes.new("ShaderNodeValToRGB")
        links.new(noise.outputs["Fac"],tarnish.inputs["Fac"])
        for i,(pos,col) in enumerate((
            (.21,(.022,.027,.031,1)),(.40,(.049,.057,.065,1)),
            (.60,(.083,.094,.104,1)),(.74,(.112,.083,.066,1)),
            (.84,(.150,.098,.065,1)),
        )):
            stop = tarnish.color_ramp.elements[i] if i<2 else tarnish.color_ramp.elements.new(pos)
            stop.position,stop.color = pos,col
        links.new(tarnish.outputs["Color"],shader.inputs["Base Color"])
        hammer = nodes.new("ShaderNodeBump")
        links.new(shader.inputs["Normal"].links[0].from_socket,hammer.inputs["Normal"])
        links.new(noise.outputs["Fac"],hammer.inputs["Height"])
        hammer.inputs["Strength"].default_value = .22
        hammer.inputs["Distance"].default_value = .0010
        links.new(hammer.outputs["Normal"],shader.inputs["Normal"])
        return mat

    def profile(self, name, bone, mat, origin, rings, *, axis=0, arc=None, keel=0, steps=42, slope=0):
        """Loft steel from elliptical cross-sections and turn the edges of open pieces."""
        closed = arc is None
        low, high = arc or (-math.pi, math.pi)
        columns = steps if closed else steps+1
        center = Vector(origin)
        vertices = []
        for height, width, depth, offset in rings:
            for i in range(columns):
                angle = low+(high-low)*i/steps
                u = width*math.sin(angle)
                v = depth*math.cos(angle)+offset+keel*max(0,math.cos(angle))**14
                h = height+slope*math.sin(angle)**2
                vertices.append(center+Vector((axis*h,v,u) if axis else (u,v,h)))
        faces = []
        for row in range(len(rings)-1):
            for i in range(steps):
                j = (i+1)%columns
                start = row*columns
                faces.append((start+i,start+j,start+j+columns,start+i+columns))
        if closed:
            faces.extend((tuple(reversed(range(columns))),
                          tuple(range(len(vertices)-columns,len(vertices)))))
        obj = self.surface(name, vertices, faces)
        if not closed:
            select([obj])
            mod = obj.modifiers.new("Rolled edge thickness","SOLIDIFY")
            mod.thickness = .004
            bpy.ops.object.modifier_apply(modifier=mod.name)
        return self.finish(obj,mat,bone)

    def plate(self, name, bone, mat, points, thickness=.009, bevel=.003):
        obj = self.surface(name, points, [tuple(range(len(points)))], smooth=False)
        select([obj])
        solid = obj.modifiers.new("Plate thickness","SOLIDIFY")
        solid.thickness = thickness
        bpy.ops.object.modifier_apply(modifier=solid.name)
        edge = obj.modifiers.new("Forged bevel","BEVEL")
        edge.width = bevel
        edge.segments = 2
        bpy.ops.object.modifier_apply(modifier=edge.name)
        return self.finish(obj,mat,bone)

    def oval(self,name,bone,mat,point,radii,steps=16,rings=8):
        return self.finish(self.ellipsoid(name,point,radii,n=steps,m=rings),mat,bone)

    def rivet(self,bone,point,r=.006,mat=None):
        self.oval("Peened rivet",bone,mat or self.brass,point,(r,r*.61,r),10,6)

    def chainmail(self,name,bone,origin,radius,rows,columns,*,axis=0,arc=None,ring=.0062):
        """Weave alternating interlocking rings around a curved articulation."""
        verts,faces = [],[]
        start,end = arc or (0,math.tau)
        long_segments,tube_segments = 12,5
        center = Vector(origin)
        for row in range(rows):
            for column in range(columns):
                angle = start+(end-start)*(column+.5*(row%2))/columns
                angle_vector = Vector((math.sin(angle),math.cos(angle),0))
                tangent = Vector((math.cos(angle),-math.sin(angle),0))
                slant = .57 if (row+column)%2==0 else -.57
                vertical = Vector((0,0,1))
                tilted = vertical*math.cos(slant)+angle_vector*math.sin(slant)
                normal = tangent.cross(tilted)
                ring_center = angle_vector*radius+vertical*(row*ring*1.12)
                base = len(verts)
                for i in range(long_segments):
                    a = i*math.tau/long_segments
                    spoke = tangent*math.cos(a)+tilted*math.sin(a)
                    for j in range(tube_segments):
                        b = j*math.tau/tube_segments
                        p = ring_center+spoke*(ring+.00124*math.cos(b))+normal*(.00124*math.sin(b))
                        verts.append(center+Vector((axis*p.z,p.y,p.x)) if axis else center+p)
                for i in range(long_segments):
                    a,b = base+i*tube_segments,base+(i+1)%long_segments*tube_segments
                    for j in range(tube_segments):
                        k = (j+1)%tube_segments
                        faces.append((a+j,a+k,b+k,b+j))
        self.finish(self.surface(name,verts,faces),self.mail,bone)

    def garment(self,sign):
        def weights(point):
            value = 1-ramp(point.z,.65,.94)
            return {"Hips":1-value, ("Left" if sign<0 else "Right")+"UpperLeg":value}

        for facing in (-1,1):
            hem_height = .613 if facing==1 else .576
            rows = ((.158,.120,1.022),(.157,.123,.947),(.149,.134,.863),
                    (.140,.134,.782),(.125,.135,.704),(.115,.141,hem_height))
            columns = 33
            verts = []
            for row,(width,depth,z) in enumerate(rows):
                for i in range(columns):
                    u = i/(columns-1)
                    fold = .013*math.sin(4*math.pi*u+row*.31)+.0065*math.sin(7.2*math.pi*u-row*.20)
                    tear = .056*max(0,1-abs(u-(.36 if sign>0 else .68))/.092)
                    hem = (u-.4)*.028+math.sin(u*math.pi*11)*.007+tear if row==len(rows)-1 else 0
                    verts.append((sign*(.012+u*width+row*.003*math.sin(6*u)),facing*(depth+fold),z+hem))
            faces = [(i,i+1,i+columns+1,i+columns) for i in range((len(rows)-1)*columns) if i%columns!=columns-1]
            obj = self.surface("Split wool skirt",verts,faces)
            select([obj])
            mod = obj.modifiers.new("Doubled wool","SOLIDIFY")
            mod.thickness = .005
            bpy.ops.object.modifier_apply(modifier=mod.name)
            self.finish(obj,self.cloth,weights)
            hem = [(x,y+facing*.003,z+.006) for x,y,z in verts[-columns:]]
            self.tube("Remnants of the bound hem",[hem[:9],hem[19:28]],.0017,self.thread,weights,sides=6,taper=.7)
            fibers = [[(x,y,z+.006),(x+.0018*sign,y+facing*.002,z-.005-(i%4)*.0016)]
                      for i,(x,y,z) in enumerate(hem) if i%3!=1]
            self.tube("Frayed wool strands",fibers,.00065,self.cloth,weights,sides=5,taper=.8)

    def torso(self):
        self.profile("Arming tunic","Hips",self.linen,(0,0,0),
                     [(.847,.123,.091,0),(.91,.163,.107,0),(1.02,.15,.093,0),(1.14,.13,.102,0)])
        self.profile("Padded shoulder lining","Chest",self.linen,(0,0,0),
                     [(1.095,.142,.11,0),(1.26,.191,.115,0),(1.413,.209,.102,0),
                      (1.469,.141,.077,0),(1.501,.064,.058,0)])
        for i in range(4):
            z = .994+i*.048
            w = .155+i*.005
            self.profile("Articulated fauld","Spine" if i>=2 else "Hips",self.steel,(0,0,z),
                         [(0,w,.123,.006),(.008,w+.005,.123,.006),(.036,w+.004,.116,.003),
                          (.059,w-.006,.108,0)],keel=.011)
        chest = self.profile("Raised keel breastplate","Chest",self.steel,(0,0,0),
                     [(1.109,.147,.114,.014),(1.142,.159,.129,.015),(1.234,.189,.139,.012),
                      (1.318,.197,.127,.009),(1.402,.175,.100,.004),(1.467,.105,.080,0)],
                     arc=(-1.8,1.8),keel=.034,steps=64,slope=.012)
        self.profile("Backplate","Chest",self.iron,(0,0,0),
                     [(1.10,.149,.112,0),(1.16,.168,.124,0),(1.286,.188,.127,0),
                      (1.411,.176,.111,0),(1.455,.09,.082,0)],arc=(1.30,4.98))
        self.tube("Polished breastplate keel",
                  [[(0,.150,1.113),(0,.173,1.154),(0,.179,1.234),(0,.163,1.318),(0,.132,1.399)]],
                  .0025,self.edge,"Chest",taper=.3)
        chest_surface = self.bvh(chest)
        for sign in (-1,1):
            for i, coords in enumerate((
                ((.015,1.218),(.067,1.242),(.108,1.280),(.146,1.327),(.162,1.362)),
                ((.018,1.177),(.071,1.202),(.121,1.241),(.157,1.279)),
            )):
                line = []
                for p,q in zip(coords,coords[1:]):
                    line.extend(Vector(p).lerp(Vector(q),step/5) for step in range(5))
                line.append(Vector(coords[-1]))
                detail = [self.front_point(chest_surface,sign*x,z,.0014) for x,z in line]
                self.tube("Raised fluting on the cuirass",[detail],.0028 if i==0 else .0021,
                          self.steel,"Chest",sides=8,taper=.55)
            for x,z in ((.15,1.401),(.145,1.366),(.156,1.307)):
                self.rivet("Chest",self.front_point(chest_surface,sign*x,z,.0014),.0048)
        for index,coords in enumerate((
            ((.106,1.315),(.085,1.302),(.068,1.287),(.055,1.284)),
            ((.136,1.303),(.116,1.289),(.106,1.286)),
            ((-.085,1.184),(-.071,1.197),(-.060,1.201)),
        )):
            scar = [self.front_point(chest_surface,x,z,.0006) for x,z in coords]
            self.tube("Fine breastplate scuff",[scar],.0014 if index==0 else .0008,
                      self.iron,"Chest",sides=5,taper=.75)
        self.profile("Hinged collar","Neck",self.iron,(0,0,0),
                     [(1.443,.087,.077,0),(1.454,.098,.083,0),(1.486,.084,.076,0),
                      (1.522,.065,.061,0)])
        self.profile("Rolled collar lip","Neck",self.edge,(0,0,0),
                     [(1.486,.086,.077,0),(1.500,.081,.074,0),(1.504,.076,.071,0)])
        self.profile("Scalloped gorget skirt","Chest",self.steel,(0,0,0),
                     [(1.397,.155,.119,.007),(1.423,.155,.119,.002),(1.461,.111,.092,0),
                      (1.486,.089,.080,0)],arc=(-1.89,1.89),keel=.013,steps=56,slope=.023)
        self.profile("Gorget rolled lower lip","Chest",self.edge,(0,0,0),
                     [(1.397,.155,.120,.008),(1.400,.156,.121,.008),(1.406,.154,.120,.008)],
                     arc=(-1.87,1.87),keel=.013,steps=54,slope=.023)
        self.chainmail("Mail standard around the throat","Neck",(0,0,1.505),.068,5,27,ring=.0066)
        self.profile("Sword belt","Hips",self.leather,(0,0,0),
                     [(1.018,.173,.129,.006),(1.054,.170,.129,.007)])
        self.plate("Belt buckle","Hips",self.brass,[(-.028,.151,1.02),(.028,.151,1.02),
                   (.028,.150,1.057),(-.028,.150,1.057)],.008,.005)
        self.plate("Buckle inset","Hips",self.leather,[(-.018,.157,1.029),(.018,.157,1.029),
                   (.018,.157,1.049),(-.018,.157,1.049)],.003,.002)
        for sign in (-1,1):
            self.garment(sign)
            strap = [(sign*x,y,z) for x,y,z in [(.089,.093,1.461),(.127,.113,1.427),
                     (.151,.117,1.397),(.168,.111,1.373),(.143,.113,1.364),
                     (.123,.124,1.391),(.10,.115,1.423),(.068,.098,1.454)]]
            self.plate("Leather suspension tab","Chest",self.leather,strap,.005,.002)
            self.rivet("Chest",(sign*.149,.12,1.383))
            self.rivet("Hips",(sign*.062,.131,1.038))
            self.plate("Rear shoulder suspension strap","Chest",self.leather,
                       [(sign*x,y,z) for x,y,z in [(.073,-.066,1.467),(.101,-.094,1.415),
                        (.121,-.107,1.36),(.145,-.097,1.374),(.124,-.088,1.433),
                        (.092,-.069,1.474)]],.006,.002)
            self.rivet("Chest",(sign*.129,-.109,1.371),.005)
        self.plate("Backplate hinge","Chest",self.iron,
                   [(-.023,-.112,1.132),(.023,-.112,1.132),(.019,-.126,1.210),
                    (-.019,-.126,1.210)],.004,.002)
        for z in (1.147,1.193):
            self.rivet("Chest",(0,-.128,z),.004,self.edge)

    def helmet(self):
        self.profile("Padded bevor lining","Head",self.shadow,(0,0,0),
                     [(1.507,.049,.056,0),(1.545,.076,.082,.009),(1.637,.084,.094,.003),
                      (1.745,.079,.084,-.004)])
        self.profile("Forged helmet skull","Head",self.steel,(0,0,0),
                     [(1.568,.069,.080,-.015),(1.61,.093,.093,-.014),(1.679,.101,.105,-.015),
                      (1.734,.098,.106,-.016),(1.769,.084,.092,-.020),
                      (1.795,.067,.077,-.021),(1.820,.043,.056,-.025),
                      (1.849,.022,.034,-.031),(1.870,.005,.012,-.041)],keel=.013,steps=68)
        visor = self.profile("Visor lower shell","Head",self.steel,(0,0,0),
                     [(1.524,.042,.063,.016),(1.548,.066,.091,.018),(1.575,.077,.106,.019),
                      (1.607,.086,.119,.016),(1.631,.091,.116,.008),
                      (1.647,.093,.108,.004)],arc=(-1.78,1.78),keel=.040,steps=60,slope=.020)
        self.profile("Visor black sight line","Head",self.shadow,(0,0,0),
                     [(1.646,.091,.102,.004),(1.666,.091,.105,.004)],arc=(-1.40,1.40),keel=.023,slope=.027)
        self.profile("Angular brow","Head",self.steel,(0,0,0),
                     [(1.663,.094,.112,.004),(1.672,.103,.118,.004),(1.689,.104,.113,0),
                      (1.713,.101,.105,-.001),(1.730,.095,.097,-.004)],
                     arc=(-1.74,1.74),keel=.035,steps=60,slope=.024)
        self.plate("Forged nose ridge","Head",self.steel,[(0,.146,1.710),(.007,.147,1.675),
                   (.011,.149,1.636),(0,.165,1.608),(-.011,.149,1.636),(-.007,.147,1.675)],.009,.003)
        self.plate("Hammer-welded crest of helmet","Head",self.iron,
                   [(0,.092,1.741),(0,.087,1.780),(0,.046,1.819),(0,-.002,1.856),
                    (0,-.042,1.891),(0,-.091,1.841),(0,-.116,1.773),
                    (0,-.117,1.718),(0,-.090,1.776),(0,-.035,1.866)],.018,.006)
        for i in range(3):
            z=1.549+.028*i
            self.profile("Lapped sallet neck guard","Head",self.steel,(0,-.020,z),
                         [(0,.079+.006*i,.094+.004*i,0),(.007,.082+.006*i,.093+.004*i,0),
                          (.033,.076+.006*i,.084+.004*i,0)],arc=(1.35,4.94),steps=45)
        visor_surface = self.bvh(visor)
        for sign in (-1,1):
            self.oval("Visor pivot","Head",self.iron,(sign*.097,.019,1.673),(.009,.022,.022))
            self.oval("Pivot pin","Head",self.brass,(sign*.105,.019,1.673),(.004,.009,.009))
            for x,z in ((.031,1.620),(.045,1.619),(.060,1.620),(.034,1.602),(.050,1.602)):
                x *= sign
                center = self.front_point(visor_surface,x,z,.0007)
                rim = [self.front_point(visor_surface,x+.0029*math.cos(a),z+.0036*math.sin(a),.0007)
                       for a in (math.tau*i/10 for i in range(10))]
                self.finish(self.surface("Visor breath perforation",[center]+rim,
                            [(0,1+i,1+(i+1)%10) for i in range(10)],smooth=False),self.shadow,"Head")
            abrasion = [self.front_point(visor_surface,sign*x,z,.0007) for x,z in
                        ((.077,1.624),(.067,1.610),(.060,1.592),(.056,1.586))]
            self.tube("Visor score",[abrasion],.0012,self.edge,"Head",sides=6,taper=.8)
        self.tube("Old visor edge repair",[[(.013,.137,1.597),(.006,.137,1.584),
                  (.008,.130,1.58),(-.004,.125,1.570)]],.0012,self.edge,"Head",sides=5,taper=.5)

    def arms(self,side,sign):
        upper,fore,hand = [side+suffix for suffix in ("UpperArm","LowerArm","Hand")]
        axis = {"axis":sign}
        for name,bone,mat,origin,rings,options in (
            ("Upper arm padding",upper,self.linen,.23,[(0,.079,.077,0),(.11,.081,.077,0),(.28,.057,.054,0)],{}),
            ("Hand-forged pauldron",upper,self.steel,.193,[(0,.068,.069,0),(.046,.124,.128,0),
                (.101,.115,.125,0),(.157,.092,.092,0),(.195,.071,.076,0)],{"arc":(-1.65,2.05),"keel":.012}),
            ("Rerebrace",upper,self.steel,.344,[(0,.075,.077,0),(.064,.069,.071,0),(.135,.055,.053,0)],{}),
            ("Elbow leather joint",fore,self.iron,.466,[(0,.051,.050,0),(.044,.065,.064,0),(.092,.058,.058,0)],{}),
            ("Tapered vambrace",fore,self.steel,.557,[(0,.070,.067,0),(.066,.065,.064,0),(.173,.046,.043,0),
                (.213,.045,.042,0)],{"keel":.009}),
            ("Gauntlet cuff",fore,self.edge,.731,[(0,.054,.057,0),(.035,.049,.048,0),(.048,.046,.044,0)],{}),
            ("Gauntlet palm",hand,self.leather,.768,[(0,.046,.046,0),(.048,.053,.048,0),(.098,.046,.038,.008),
                (.121,.038,.029,.014)],{}),
        ):
            self.profile(name,bone,mat,(sign*origin,0,1.42),rings,**axis,**options)
        for i in range(3):
            start = .297+.035*i
            radius = .108-.013*i
            self.profile("Overlapping shoulder lame",upper,self.steel,(sign*start,0,1.42),
                         [(0,radius,radius,0),(.013,radius+.004,radius+.005,0),
                          (.051,radius-.016,radius-.016,0)],**axis,arc=(-1.63,2.0),steps=35,slope=.012)
            self.rivet(upper,(sign*(start+.022),radius*.91,1.45),.005)
        fork = [
            (.222,-.067,1.475),(.222,.047,1.479),(.245,.097,1.500),
            (.286,.114,1.507),(.304,.098,1.537),(.258,.059,1.574),
            (.201,.004,1.598 if sign>0 else 1.581),(.223,-.070,1.551),
            (.286,-.101,1.507),(.269,-.092,1.492),
        ]
        verts = [(sign*x,y,z) for x,y,z in fork]
        self.plate("Swept pauldron guard",upper,self.steel,verts,.015,.008)
        self.tube("Pauldron ridge edge",[verts[3:9]],.0042,self.edge,upper,sides=9,taper=.72)
        self.chainmail("Riveted elbow gusset",fore,(sign*.482,0,1.42),.063,8,19,
                       axis=sign,arc=(-1.5,1.5),ring=.0058)
        self.oval("Pointed elbow cop",fore,self.steel,(sign*.516,-.047,1.419),(.059,.032,.067),20,10)
        self.plate("Elbow fan",fore,self.edge,[(sign*x,y,z) for x,y,z in
                   [(.46,-.057,1.405),(.485,-.066,1.474),(.522,-.073,1.498),
                    (.574,-.060,1.460),(.555,-.074,1.42),(.507,-.077,1.385)]],.006,.003)
        for i in range(3):
            start = .786+i*.032
            radius = .054-.005*i
            self.profile("Gauntlet metacarpal lame",hand,self.steel,(sign*start,0,1.42),
                         [(0,radius,.052-i*.003,0),(.014,radius+.003,.054-i*.003,0),
                          (.036,radius-.004,.049-i*.003,0)],**axis,arc=(-1.64,1.64),steps=22)
        self.oval("Glove thumb",hand,self.leather,(sign*.826,.034,1.386),(.031,.027,.019))

    def legs(self,side,sign):
        thigh,shin,foot = [side+suffix for suffix in ("UpperLeg","LowerLeg","Foot")]
        x = sign*.11
        self.profile("Quilted trousers",thigh,self.linen,(x,0,0),
                     [(.501,.064,.069,0),(.62,.077,.074,0),(.795,.097,.085,0),(.938,.092,.09,0)])
        self.profile("Forged thigh plate",thigh,self.steel,(x,0,0),
                     [(.562,.067,.082,0),(.60,.082,.088,0),(.72,.096,.105,0),
                      (.79,.096,.105,0)],arc=(-1.26,1.46),keel=.014)
        self.profile("Knee articulation",shin,self.iron,(x,0,0),
                     [(.46,.057,.059,0),(.524,.067,.068,0),(.575,.059,.061,0)])
        self.chainmail("Mail behind the knee",shin,(x,0,.490),.068,6,14,arc=(1.42,4.86))
        self.profile("Knee cup",shin,self.steel,(x,.035,.53),
                     [(-.067,.028,.034,0),(-.042,.057,.060,0),(.008,.071,.068,0),
                      (.049,.054,.055,0),(.072,.025,.035,0)],arc=(-1.60,1.60),keel=.020)
        self.plate("Knee fan",shin,self.edge,[(x+sign*dx,y,z) for dx,y,z in
                   [(.041,.087,.572),(.089,.057,.573),(.103,.045,.526),(.068,.065,.498),
                    (.037,.094,.509)]],.006,.003)
        self.rivet(shin,(x+sign*.068,.072,.540))
        self.profile("Keel greave",shin,self.steel,(x,0,0),
                     [(.115,.048,.054,0),(.163,.057,.061,0),(.32,.069,.077,-.004),
                      (.428,.075,.076,-.008),(.487,.067,.063,0)],keel=.018)
        self.tube("Greave bright ridge",[[(x,.075,.154),(x,.090,.305),(x,.087,.429),(x,.080,.481)]],
                  .002,self.edge,shin,sides=5,taper=.4)
        self.profile("Leather boot and sole",foot,self.leather,(x,0,0),
                     [(.009,.054,.131,.060),(.017,.067,.145,.062),(.037,.067,.144,.062),
                      (.060,.060,.094,.037),(.107,.054,.057,0),(.136,.047,.048,0)])
        for i in range(5):
            center = .066+.029*i
            width = .066-.0032*i
            z = .091-.009*i
            self.profile("Sabaton toe lame",foot,self.steel,(x,center,z),
                         [(-.026,width*.75,.030,0),(-.014,width,.033,0),
                          (0,width*.92,.029,0),(.012,width*.66,.015,-.004)],steps=18)

    def shield(self):
        bone = "LeftLowerArm"
        # The rest shield runs along the forearm. It becomes vertical when the arm drops.
        outline = [(-.157,.227),(.137,.213),(.158,.158),(.150,.026),(.106,-.121),
                   (.033,-.224),(-.019,-.264),(-.079,-.211),(-.143,-.103),(-.168,.037)]

        def point(u,v,depth):
            y = .132+depth-.099*u*u-.21*v*v
            return (-.616+v,y,1.42+u)

        def shell(name,mat,scales,rounded=False):
            verts = [point(u*scale,v*scale,depth) for scale,depth in scales for u,v in outline]
            n = len(outline)
            faces = [(row*n+i,row*n+(i+1)%n,(row+1)*n+(i+1)%n,(row+1)*n+i)
                     for row in range(len(scales)-1) for i in range(n)]
            faces.append(tuple(reversed(range(n))))
            if rounded:
                last = len(verts)-n
                verts.append(point(0,0,scales[-1][1]+.001))
                faces.extend((last+i,last+(i+1)%n,last+n) for i in range(n))
            else:
                faces.append(tuple(range(len(verts)-n,len(verts))))
            return self.finish(self.surface(name,verts,faces,smooth=False),mat,bone)

        shell("Laminated heater shield",self.leather,[(.94,-.022),(1,-.012),(1,.001),(.98,.005)])
        shell("Steel shield edge",self.iron,[(.969,.005),(1,.009),(1,.017),(.927,.022)])
        face = shell("Blackened oak of the shield",self.shield_wood,
                     [(.92,.020),(.92,.023),(.64,.032),(.3,.037)],True)
        rim = [point(u*.943,v*.943,.024) for u,v in outline]
        self.tube("Shield edge burnishing",[rim+[rim[0]]],.0024,self.edge,bone,sides=6,taper=0)
        diamond = [(0,.101),(.076,-.004),(0,-.116),(-.076,-.004)]
        inner = [(u*.72,v*.72) for u,v in diamond]
        face_surface = self.bvh(face)
        for u,start,end in ((-.102,-.094,.176),(-.052,-.166,.197),
                            (.006,-.216,.199),(.057,-.135,.184),(.104,-.073,.178)):
            seam = []
            for i in range(29):
                v = start+(end-start)*i/28
                x,_,z = point(u+.0018*math.sin(i*.11),v,0)
                seam.append(self.front_point(face_surface,x,z,.0008))
            self.tube("Oak plank seams",[seam],.00145,self.shadow,bone,sides=5,taper=.35)
        verts, faces = [], []
        for i in range(4):
            start = len(verts)
            j = (i+1)%4
            for step in range(7):
                t = step/6
                for points in (diamond,inner):
                    u,v = Vector(points[i]).lerp(Vector(points[j]),t)
                    x,_,z = point(u,v,0)
                    verts.append(self.front_point(face_surface,x,z,.0013))
            faces.extend((start+k,start+k+2,start+k+3,start+k+1) for k in range(0,12,2))
        self.finish(self.surface("Neutral diamond painted on shield",verts,
                    faces),self.bone,bone)
        for coords in (
            ((-.10,.17),(-.087,.157),(-.076,.137),(-.077,.126)),
            ((.061,-.13),(.073,-.117),(.079,-.103),(.097,-.090)),
            ((.101,.094),(.084,.077),(.075,.071)),
        ):
            mark = []
            for u,v in coords:
                x,_,z = point(u,v,0)
                mark.append(self.front_point(face_surface,x,z,.0007))
            self.tube("Shield paint wear",[mark],.0019,self.thread,bone,sides=5,taper=.8)
        for u,v in outline:
            self.rivet(bone,point(u*.967,v*.967,.023),.0064)
        for v in (-.082,.078):
            self.profile("Shield backing strap",bone,self.leather,(-.616+v,.055,1.42),
                         [(0,.078,.063,0),(.027,.078,.064,0)],axis=-1,steps=20)

    @staticmethod
    def front_point(surface,x,z,offset):
        point,_,_,_ = surface.ray_cast((x,1,z),(0,-1,0))
        if point is None:
            raise ValueError(f"Detail outside its armor plate at {(x,z)}")
        point.y += offset
        return point

    def sword(self):
        bone = "RightHand"
        center = .85
        z = 1.417
        stations = [(.067,.036,.010),(.100,.036,.010),(.156,.033,.0095),(.411,.029,.008),
                    (.549,.024,.007),(.646,.016,.0055),(.704,.007,.003),(.744,.0007,.0007)]
        verts = [(center+u*width,y,z+v*thick) for y,width,thick in stations
                 for u,v in [(-1,0),(-.55,1),(0,.63),(.55,1),(1,0),(.55,-1),(0,-.63),(-.55,-1)]]
        faces = [(i,i//8*8+(i+1)%8,i//8*8+(i+1)%8+8,i+8) for i in range((len(stations)-1)*8)]
        faces.extend((tuple(range(7,-1,-1)),tuple(range(len(verts)-8,len(verts)))))
        blade = self.finish(self.surface("Fullered arming sword blade",verts,faces,smooth=False),self.iron,bone)
        blade.data.materials.append(self.edge)
        for face in blade.data.polygons:
            if face.index<len(faces)-2 and face.index%8 in (0,3,4,7):
                face.material_index = 1
        self.plate("Swept iron crossguard",bone,self.iron,[(center+x,y,z+.011) for x,y in
                   [(-.112,.086),(-.093,.060),(-.038,.048),(.039,.048),(.093,.072),
                    (.109,.089),(.116,.068),(.098,.045),(.035,.028),(-.036,.028),(-.099,.046)]],
                   .034,.005)
        self.oval("Leather sword grip",bone,self.leather,(center,-.018,z),(.018,.065,.018))
        path = [(center+.019*math.cos(t),-.070+.107*i/108,z+.019*math.sin(t))
                for i in range(109) for t in (i*math.tau/18,)]
        self.tube("Spiral grip cord",[path],.0019,self.thread,bone,sides=5,taper=0)
        self.oval("Brass wheel pommel",bone,self.brass,(center,-.085,z),(.027,.023,.018),20,10)

    def geometry(self):
        self.torso()
        self.helmet()
        for side,sign in (("Left",-1),("Right",1)):
            self.arms(side,sign)
            self.legs(side,sign)
        self.shield()
        self.sword()

    def finish_model(self):
        self.geometry()
        return self.join_geometry()

    def join_geometry(self):
        select(self.parts)
        bpy.ops.object.join()
        mesh = bpy.context.object
        mesh.name = self.rig_stem.removeprefix("SK_")+"_Surface"
        self.scene.cursor.location = (0,0,0)
        bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
        mesh.parent = self.rig
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.mesh.normals_make_consistent(inside=False)
        bpy.ops.uv.smart_project(island_margin=.0009,margin_method="FRACTION")
        bpy.ops.object.mode_set(mode="OBJECT")
        mesh.data.uv_layers.active.name = "UV0"
        mod = mesh.modifiers.new("Triangulation","TRIANGULATE")
        mod.keep_custom_normals = True
        bpy.ops.object.modifier_apply(modifier=mod.name)
        skin = mesh.modifiers.new("Humanoid skin","ARMATURE")
        skin.object = self.rig
        assert len(self.rig.data.bones)==22
        assert all(abs(sum(g.weight for g in v.groups)-1)<.0001 for v in mesh.data.vertices)
        return mesh

    def set_rotation(self,name,world_delta):
        bone=self.rig.pose.bones[name]
        matrix=(world_delta@bone.bone.matrix_local.to_quaternion()).to_matrix().to_4x4()
        matrix.translation=bone.head
        bone.matrix=matrix
        bpy.context.view_layer.update()

    def arm_to(self,side,grip,rotation,anchor,pole):
        upper,lower,hand=[side+s for s in ("UpperArm","LowerArm","Hand")]
        arms=self.rig.pose.bones
        start=arms[upper].head.copy()
        rest=arms[hand].bone.head_local
        wrist=grip-rotation@(anchor-rest)
        difference=wrist-start
        length=difference.length
        a,b=arms[upper].bone.length,arms[lower].bone.length
        assert abs(a-b)<length<a+b,(side,length)
        direction=difference.normalized()
        bend=(pole-direction*pole.dot(direction)).normalized()
        distance=(a*a-b*b+length*length)/(2*length)
        elbow=start+direction*distance+bend*math.sqrt(a*a-distance*distance)
        rest_axis=Vector((-1,0,0) if side=="Left" else (1,0,0))
        self.set_rotation(upper,rest_axis.rotation_difference(elbow-start))
        self.set_rotation(lower,rest_axis.rotation_difference(wrist-elbow))
        self.set_rotation(hand,rotation)
        point=self.skin_point(hand,anchor)
        assert (point-grip).length<.003,(side,point,grip)

    def skin_point(self,name,point):
        bone=self.rig.pose.bones[name]
        return bone.matrix@bone.bone.matrix_local.inverted()@point

    def pose(self,guard=False):
        angles = {
            "LeftUpperArm": (0,-77,0), "RightUpperArm": (0,77,0),
            "LeftLowerArm": (0,2,-16), "RightLowerArm": (0,-5,14),
            "RightHand": (46,-8,-35), "Head": (-2,0,3),
        }
        if guard:
            angles.update({"Chest":(-3,0,2),"Head":(3,0,-7),
                           "LeftUpperArm":(0,-17,-69),
                           "RightUpperArm":(0,58,10),"RightLowerArm":(0,-10,19),
                           "RightHand":(40,-8,-28)})

        def rotation(degrees):
            x,y,z = map(math.radians,degrees)
            return Quaternion((0,0,1),z)@Quaternion((0,1,0),y)@Quaternion((1,0,0),x)

        rotations = {name:rotation(degrees) for name,degrees in angles.items()}
        if guard:
            rotations["LeftLowerArm"] = rotations["LeftUpperArm"].inverted()@rotation((0,-81,11))
        drop = .052 if guard else .026
        for side,sign in (("Left",-1),("Right",1)):
            # Two-bone construction with ankle targets on the floor and knees pointing forward.
            upper,lower,foot = [side+name for name in ("UpperLeg","LowerLeg","Foot")]
            start = self.rig.data.bones[upper].head_local-Vector((0,0,drop))
            ankle = Vector((sign*(.177 if guard else .139),-sign*(.135 if guard else .075),.12))
            span = ankle-start
            direction = span.normalized()
            forward = Vector((0,1,0))
            bend = (forward-direction*forward.dot(direction)).normalized()
            a = self.rig.data.bones[upper].length
            b = self.rig.data.bones[lower].length
            projection = (a*a-b*b+span.length_squared)/(2*span.length)
            knee = start+projection*direction+math.sqrt(a*a-projection*projection)*bend
            down = Vector((0,0,-1))
            up_leg = down.rotation_difference(knee-start)
            lo_leg = down.rotation_difference(ankle-knee)
            rotations[upper] = up_leg
            rotations[lower] = up_leg.inverted()@lo_leg
            rotations[foot] = lo_leg.inverted()@rotation((0,0,-sign*(10 if guard else 5)))
        root = self.rig.pose.bones["Root"]
        root.location = root.bone.matrix_local.to_3x3().inverted()@Vector((0,0,-drop))
        for bone in self.rig.pose.bones:
            basis = bone.bone.matrix_local.to_quaternion()
            delta = rotations.get(bone.name,Quaternion())
            bone.rotation_mode = "QUATERNION"
            bone.rotation_quaternion = basis.inverted()@delta@basis
        self.rig.update_tag()
        bpy.context.view_layer.update()

    def review(self,quick):
        camera = self.scene.camera
        self.scene.cycles.samples = 14 if quick else 30
        self.scene.render.resolution_x = 1080
        self.scene.render.resolution_y = 1350
        self.scene.render.resolution_percentage = 52 if quick else 100
        for suffix,position,target,scale,guard in (
            ("",(2.15,6.4,1.91),(.08,.08,.94),2.16,False),
            ("Detail",(-1.42,5.1,1.77),(0,.08,1.50),.86,False),
            ("Guard",(.64,6.1,1.92),(.08,.10,.94),2.17,True),
            ("Back",(-1.75,-5.8,1.9),(.17,.04,.92),2.29,False),
        ):
            camera.location = position
            camera.rotation_euler = (Vector(target)-camera.location).to_track_quat("-Z","Y").to_euler()
            camera.data.ortho_scale = scale
            self.pose(guard)
            self.scene.render.filepath = str(SOURCE/("NeutralKnight"+suffix+".png"))
            bpy.ops.render.render(write_still=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--quick",action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    SOURCE.mkdir(parents=True,exist_ok=True)
    knight = Knight()
    mesh = knight.finish_model()
    assert len(mesh.data.polygons)>85000
    if not args.quick:
        bake_atlas(mesh,4096,TEXTURES,ATLAS,"UV0")
        slots = list(mesh.data.materials)
        indices = [polygon.material_index for polygon in mesh.data.polygons]
        mesh.data.materials.clear()
        mesh.data.materials.append(material("M_NeutralKnight_Atlas",(.073,.084,.095),.88,.44,0,0,""))
        export_collection(MODEL,bake_animation=False)
        mesh.data.materials.clear()
        for mat in slots:
            mesh.data.materials.append(mat)
        for polygon,index in zip(mesh.data.polygons,indices):
            polygon.material_index = index
        print(f"{STEM}: {len(mesh.data.polygons)} triangles, {len(knight.rig.data.bones)} bones",flush=True)
    camera = studio_camera(knight.scene)
    bpy.data.objects["Soft key"].data.energy = 529
    bpy.data.objects["Front fill"].data.energy = 163
    bpy.data.objects["Rim"].data.energy = 347
    knight.scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = .20
    bpy.data.objects["Studio shadow floor"].location.z = 0
    knight.pose()
    camera.location = (2.15,6.4,1.91)
    camera.rotation_euler = (Vector((.08,.08,.94))-camera.location).to_track_quat("-Z","Y").to_euler()
    camera.data.ortho_scale = 2.16
    if not args.quick:
        select([mesh])
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(STEM+".blend")))
    knight.review(args.quick)


if __name__ == "__main__":
    main()
