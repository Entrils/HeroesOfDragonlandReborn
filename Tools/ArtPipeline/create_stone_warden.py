"""Author the neutral Stone Warden mesh, rig and attack reads in Blender."""

import argparse
import math
from pathlib import Path
from random import Random
import sys

import bpy
import bmesh
from mathutils import Quaternion, Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
from character_art import bake_atlas, material, preview_baked_coat, studio_camera
from creature_mesh import CreatureMesh, ramp, select
from export_fbx import export_collection

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/"ArtSource/Characters"
GAME=ROOT/"Assets/Art"
STEM="SK_StoneWarden"
OUTPUT=ART/STEM
HEIGHT_SCALE=1.94
UP=Vector((0,0,1))
FORWARD=Vector((0,1,0))
SECTION=((.64,1),(-.65,1),(-1,.62),(-1,-.59),(-.57,-1),
         (.61,-1),(1,-.56),(1,.62))
DRAGON_SEAL=(
    (-.112,-.084),(-.071,-.112),(.003,-.091),(.043,-.05),(.012,-.005),
    (.035,.035),(.097,.049),(.086,.085),(.031,.114),(.060,.149),
    (-.003,.126),(-.018,.158),(-.041,.099),(-.090,.063),
    (-.096,.012),(-.053,-.013),(-.055,-.064),
)


def stone_material(name,base,grain,roughness):
    mat=material(name,base,0,roughness,grain,.0033,"leather")
    nodes,links=mat.node_tree.nodes,mat.node_tree.links
    shader=nodes.get("Principled BSDF")
    shader.inputs["Specular IOR Level"].default_value=.25
    position=nodes.get("Texture Coordinate").outputs["Object"]
    scale=nodes.new("ShaderNodeVectorMath")
    scale.operation="MULTIPLY"
    scale.inputs[1].default_value=(.88,1.13,3.1)
    links.new(position,scale.inputs[0])
    strata=nodes.new("ShaderNodeTexNoise")
    strata.inputs["Scale"].default_value=4.6
    strata.inputs["Detail"].default_value=6
    strata.inputs["Roughness"].default_value=.72
    links.new(scale.outputs["Vector"],strata.inputs["Vector"])
    tint=nodes.new("ShaderNodeValToRGB")
    for stop,stop_at,factor in zip(tint.color_ramp.elements,(.22,.68),(.52,1.34)):
        stop.position=stop_at
        stop.color=(*(c*factor for c in base),1)
    links.new(strata.outputs["Fac"],tint.inputs[0])
    layers=nodes.new("ShaderNodeBump")
    layers.inputs["Strength"].default_value=.34
    layers.inputs["Distance"].default_value=.010
    links.new(strata.outputs["Fac"],layers.inputs["Height"])
    links.new(shader.inputs["Normal"].links[0].from_socket,layers.inputs["Normal"])
    if "Lichen" in name:
        links.new(tint.outputs["Color"],shader.inputs["Base Color"])
        links.new(layers.outputs["Normal"],shader.inputs["Normal"])
        return mat
    shift=nodes.new("ShaderNodeVectorMath")
    shift.operation="SCALE"
    shift.inputs["Scale"].default_value=.104
    links.new(strata.outputs["Color"],shift.inputs[0])
    distort=nodes.new("ShaderNodeVectorMath")
    distort.operation="ADD"
    links.new(position,distort.inputs[0])
    links.new(shift.outputs["Vector"],distort.inputs[1])
    seams=nodes.new("ShaderNodeTexVoronoi")
    seams.feature="DISTANCE_TO_EDGE"
    seams.inputs["Scale"].default_value=7.8
    links.new(distort.outputs["Vector"],seams.inputs["Vector"])
    thin=nodes.new("ShaderNodeValToRGB")
    thin.color_ramp.elements[0].position=.001
    thin.color_ramp.elements[1].position=.033
    thin.color_ramp.elements[0].color=(.42,.43,.45,1)
    links.new(seams.outputs["Distance"],thin.inputs["Fac"])
    tint_with_seam=nodes.new("ShaderNodeMixRGB")
    tint_with_seam.blend_type="MULTIPLY"
    tint_with_seam.inputs[0].default_value=.61
    links.new(tint.outputs["Color"],tint_with_seam.inputs[1])
    links.new(thin.outputs[0],tint_with_seam.inputs[2])
    links.new(tint_with_seam.outputs[0],shader.inputs["Base Color"])
    chipped=nodes.new("ShaderNodeBump")
    chipped.inputs["Strength"].default_value=.32
    chipped.inputs["Distance"].default_value=.008
    links.new(thin.outputs[0],chipped.inputs["Height"])
    links.new(layers.outputs["Normal"],chipped.inputs["Normal"])
    links.new(chipped.outputs["Normal"],shader.inputs["Normal"])
    return mat


class Warden(CreatureMesh):
    def __init__(self):
        prototype=ART/"SK_PrototypeHumanoid/SK_PrototypeHumanoid.blend"
        bpy.ops.wm.open_mainfile(filepath=str(prototype))
        self.scene=bpy.context.scene
        self.collection=bpy.data.collections["EXPORT"]
        self.rig,=(o for o in self.collection.objects if o.type=="ARMATURE")
        for obj in list(self.collection.objects):
            if obj.type=="MESH":
                bpy.data.objects.remove(obj,do_unlink=True)
        self.rig.animation_data_clear()
        for action in list(bpy.data.actions):
            bpy.data.actions.remove(action)
        self.parts=[]
        self.random=Random(2866)
        self.scale_rig()
        self.materials()

    def scale_rig(self):
        self.rig.name=STEM
        self.rig.data.name="StoneWardenSkeleton"
        select([self.rig])
        bpy.ops.object.mode_set(mode="EDIT")
        for bone in self.rig.data.edit_bones:
            bone.head*=HEIGHT_SCALE
            bone.tail*=HEIGHT_SCALE
            if bone.name.endswith(("UpperLeg","LowerLeg","Foot","Toes")):
                spacing=.096 if bone.name.startswith("Right") else -.096
                bone.head.x+=spacing
                bone.tail.x+=spacing
        bpy.ops.object.mode_set(mode="OBJECT")
        self.rig.show_in_front=True

    def materials(self):
        self.stone=stone_material("Warden_AshBasalt",(.027,.034,.043),68,.73)
        self.light=stone_material("Warden_WornFreestone",(.116,.103,.084),91,.79)
        self.dark=stone_material("Warden_DarkSeam",(.012,.014,.018),62,.92)
        self.slate=stone_material("Warden_FracturedSlate",(.053,.061,.067),74,.78)
        self.moss=stone_material("Warden_Lichen",(.040,.048,.029),139,.93)
        self.bronze=material("Warden_AgedBronze",(.138,.080,.042),.77,.50,108,.0012,"steel")
        self.patina=material("Warden_BronzePatina",(.046,.058,.052),.52,.73,170,.0011,"leather")
        self.ember=material("Warden_SealedMineral",(.43,.58,.56),.14,.34,202,.00044,"steel")
        shader=self.ember.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Emission Color"].default_value=(.54,.72,.69,1)
        shader.inputs["Emission Strength"].default_value=.34

    def recalc(self,obj):
        bm=bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        bm.to_mesh(obj.data)
        bm.free()

    def beveled(self,obj,width):
        self.recalc(obj)
        select([obj])
        edge=obj.modifiers.new("Irregular worked edges","BEVEL")
        edge.width=width
        edge.segments=2
        edge.affect="EDGES"
        edge.limit_method="ANGLE"
        edge.angle_limit=.08
        bpy.ops.object.modifier_apply(modifier=edge.name)
        return obj

    def block(self,name,bone,mat,origin,rings,axis=(0,0,1),bevel=.021,worn=.04):
        """A chipped octagonal block with independently shaped bearing surfaces."""
        axis=Vector(axis).normalized()
        right=FORWARD.cross(axis).normalized()
        depth=axis.cross(right).normalized()
        center=Vector(origin)
        facets=[]
        for z,rx,ry,x_offset,y_offset in rings:
            for tx,ty in SECTION:
                x=rx*(tx+self.random.uniform(-worn,worn))+x_offset
                y=ry*(ty+self.random.uniform(-worn,worn))+y_offset
                h=z+self.random.uniform(-worn*.065,worn*.065)
                facets.append(center+right*x+depth*y+axis*h)
        faces=[]
        for row in range(len(rings)-1):
            for i in range(8):
                j=(i+1)%8
                a=8*row
                faces.append((a+i,a+j,a+j+8,a+i+8))
        faces.extend((tuple(reversed(range(8))),tuple(range(len(facets)-8,len(facets)))))
        return self.finish(self.beveled(self.surface(name,facets,faces,smooth=False),bevel),mat,bone)

    def plate(self,name,bone,mat,points,thickness,bevel=.008):
        obj=self.surface(name,points,[tuple(range(len(points)))],smooth=False)
        select([obj])
        skin=obj.modifiers.new("Tablet thickness","SOLIDIFY")
        skin.thickness=thickness
        bpy.ops.object.modifier_apply(modifier=skin.name)
        return self.finish(self.beveled(obj,bevel),mat,bone)

    def fitted_plate(self,tree,bone,coordinates):
        """Trace the rock surface inside the whole outline so the armor clears convex areas."""
        outline=[Vector(point) for point in coordinates]
        center=sum(outline,Vector((0,0)))/len(outline)
        vertices=[]
        samples=[center]
        for radius in (.34,.67,1):
            for a,b in zip(outline,outline[1:]+outline[:1]):
                samples.extend(center.lerp(a.lerp(b,i/3),radius) for i in range(3))
        for x,z in samples:
            hit,_,_,_=tree.ray_cast(Vector((x,1.10,z)),-FORWARD,1.5)
            if hit is None:
                raise ValueError("Chest fragment extends past its stone support")
            vertices.append(hit+FORWARD*.026)
        width=len(outline)*3
        faces=[(0,1+i,1+(i+1)%width) for i in range(width)]
        for row in range(2):
            start=1+row*width
            for i in range(width):
                j=(i+1)%width
                faces.append((start+i,start+width+i,start+width+j,start+j))
        a,b,c=(vertices[i] for i in faces[0])
        if (b-a).cross(c-a).dot(FORWARD)<0:
            faces=[tuple(reversed(face)) for face in faces]
        obj=self.surface("Fitted fragments of dressed stone",vertices,faces,smooth=False)
        select([obj])
        thickness=obj.modifiers.new("Stone layer thickness","SOLIDIFY")
        thickness.thickness=.034
        bpy.ops.object.modifier_apply(modifier=thickness.name)
        self.finish(self.beveled(obj,.008),self.light,bone)

    def stone_cracks(self,obj,bone,patterns,width=.007):
        tree=self.bvh(obj)
        chains=[]
        for pattern in patterns:
            chain=[]
            for start,end in zip(pattern,pattern[1:]):
                for i in range(5):
                    t=i/5
                    x,z=(start[j]*(1-t)+end[j]*t for j in (0,1))
                    if i:
                        x+=self.random.uniform(-.0034,.0034)
                    hit,_,_,_=tree.ray_cast(Vector((x,1.10,z)),-FORWARD,1.55)
                    if hit is not None:
                        chain.append(hit+FORWARD*.0011)
            if len(chain)>1:
                chains.append(chain)
        if chains:
            self.tube("Surface fracture",chains,width*.61,self.dark,bone,sides=5,taper=.56)

    def moulding(self, name, bone, mat, stations):
        """A solid pointed moulding with a keel, recessed flutes and turned edges.

        Stations give centre x/y/z, half-width and forward projection in metres.
        """
        profile = ((-1,-.40),(-1,.08),(-.86,.39),(-.72,.23),(-.38,.32),
                   (-.13,.81),(0,1),(.13,.81),(.38,.32),(.72,.23),
                   (.86,.39),(1,.08),(1,-.40))
        vertices = [(x + u*width, y + v*depth, z)
                    for x,y,z,width,depth in stations for u,v in profile]
        count = len(profile)
        faces = []
        for row in range(len(stations)-1):
            for i in range(count):
                a = row*count+i
                b = row*count+(i+1)%count
                faces.append((a,b,b+count,a+count))
        faces += [tuple(range(count-1,-1,-1)),tuple(range(len(vertices)-count,len(vertices)))]
        obj = self.surface(name,vertices,faces,smooth=False)
        return self.finish(self.beveled(obj,.0045),mat,bone)

    def chain(self, bone, points):
        """Closed oval links alternate planes and overlap at the bearing ends."""
        for i,center in enumerate(points):
            axis = (points[min(i+1,len(points)-1)]-points[max(i-1,0)]).normalized()
            across = axis.cross(FORWARD).normalized()
            across = Quaternion(axis,.25 if i%2 else 1.32) @ across
            path = [center + axis*(.045*math.cos(j*math.tau/20))
                    + across*(.026*math.sin(j*math.tau/20)) for j in range(21)]
            self.tube("Forged interlocking shackle",[path],.0085,self.bronze,bone,sides=8,taper=0)

    def funerary_carving(self):
        # A shattered ring of carved merlons, low over the eyes. The tallest
        # surviving stone is off-centre; its neighbour ends in a shear plane.
        for i, height in enumerate((.20,.105,.26,.14,.09,.21,.115,.155,.08)):
            a = i*math.tau/9
            x,y = .198*math.sin(a),.173*math.cos(a)+.018
            self.block("Broken crown voussoir","Head",self.slate,(x,y,3.405),[
                (0,.082,.062,0,0),(.083,.081,.063,.009*math.sin(a),0),
                (height+.03,.045,.044,.044*math.sin(a),.041*math.cos(a)),
                (height+.049,.019,.037,.051*math.sin(a)+.010,.044*math.cos(a))],
                bevel=.008,worn=.089)
        self.moulding("Keystone of the diadem","Head",self.light,[
            (0,.307,3.286,.016,.032),(-.009,.291,3.37,.068,.053),
            (-.018,.252,3.475,.088,.061),(-.027,.211,3.555,.051,.047),
            (-.043,.186,3.622,.020,.032)])
        # A recessed mandible and separate carved bars throw genuine shadows
        # around the mouth. The eye minerals sit in the gap above the cheeks.
        self.block("Hollow of the death mask","Head",self.dark,(0,.215,3.012),[
            (0,.089,.053,0,0),(.054,.145,.063,0,0),(.133,.138,.077,0,0)],
            bevel=.012)
        chest_tree = self.bvh(next(obj for obj in self.parts if obj.name=="Monolithic chest"))
        for side in (-1,1):
            self.moulding("Sundered jaw","Head",self.light,[
                (side*.057,.278,2.980,.017,.033),(side*.104,.287,3.039,.042,.041),
                (side*.155,.281,3.128,.053,.059),(side*.197,.245,3.201,.043,.043),
                (side*.232,.200,3.255,.012,.029)])
            for i in range(2):
                x=side*(.021+.040*i)
                self.moulding("Carved mouth grille","Head",self.slate,[
                    (x*.86,.282,3.026+i*.009,.008,.018),
                    (x,.303,3.087,.014,.025),(x*1.08,.290,3.133,.012,.018)])

            # Paired flying buttresses emerge behind the shoulders, tied into
            # the breast and rear masonry rather than the swinging arm.
            self.moulding("Fractured mantle buttress","Chest",self.stone,[
                (side*.34,-.19,2.50,.136,.115),(side*.45,-.21,2.74,.151,.13),
                (side*.52,-.19,2.98,.104,.115),(side*.57,-.205,3.14,.074,.088),
                (side*.59,-.22,3.27 if side<0 else 3.19,.045,.073)])
            self.moulding("Vault rib","Chest",self.slate,[
                (side*.085,.337,2.245,.023,.055),(side*.223,.354,2.341,.056,.062),
                (side*.377,.347,2.453,.057,.068),(side*.473,.292,2.587,.061,.072),
                (side*.502,.245,2.730,.034,.045)])
            self.moulding("Hip pendant","Hips",self.slate,[
                (side*.258,.214,1.445,.022,.033),(side*.241,.261,1.573,.068,.060),
                (side*.228,.272,1.739,.088,.064),(side*.225,.267,1.831,.071,.044)])
            path=[]
            for i in range(8):
                t=i/7
                x=side*(.19+.33*t)
                z=2.839-.126*math.sin(math.pi*t)+.017*t
                hit,_,_,_=chest_tree.ray_cast((x,1.1,z),-FORWARD)
                y=hit.y if hit is not None else .21
                path.append(Vector((x,y+.038,z)))
            self.chain("Chest",path)
            for link in (path[0],path[-1]):
                self.rivet("Chest",link-FORWARD*.016,.033)
            leg=("Left" if side<0 else "Right")+"LowerLeg"
            self.moulding("Shin reliquary rib",leg,self.slate,[
                (side*.31,.192,.296,.015,.034),(side*.307,.208,.429,.059,.043),
                (side*.31,.221,.689,.085,.056),(side*.31,.195,.887,.062,.044),
                (side*.31,.189,.952,.029,.032)])

    def rivet(self,bone,position,radius=.025):
        obj=self.ellipsoid("Weathered bronze pin",position,(radius,.012,radius),n=10,m=6)
        self.finish(obj,self.bronze,bone)

    def moss_patch(self,obj,bone,center,radius,count,axis=(0,0,1)):
        tree=self.bvh(obj)
        direction=Vector(axis).normalized()
        xaxis=FORWARD.cross(direction).normalized()
        yaxis=direction.cross(xaxis)
        center=Vector(center)
        vertices,faces=[],[]
        for _ in range(count):
            a=self.random.uniform(0,math.tau)
            r=radius*math.sqrt(self.random.random())
            p=center+xaxis*(r*math.cos(a))+yaxis*(r*math.sin(a))
            hit,normal,_,_=tree.ray_cast(p+direction*.41,-direction,.82)
            if hit is None or normal.dot(direction)<.33:
                continue
            size=self.random.uniform(.023,.064)
            contour=[]
            for i in range(9):
                theta=i*math.tau/9
                extent=size*self.random.uniform(.58,1.32)
                q=hit+extent*(xaxis*math.cos(theta)+yaxis*.64*math.sin(theta))
                point,n,_,_=tree.ray_cast(q+direction*.17,-direction,.35)
                if point is not None and n.dot(direction)>.32:
                    contour.append(point+n*.0032)
            if len(contour)>4:
                start=len(vertices)
                vertices.extend([hit+normal*.006,*contour])
                faces.extend((start,start+1+i,start+1+(i+1)%len(contour))
                             for i in range(len(contour)))
        if faces:
            patch=self.surface("Adhered patches of old lichen",vertices,faces)
            # Lock the rock substrate beneath the thin lichen to its sampled
            # surface. Otherwise erosion pulls it away under grazing light.
            obj["lichen_substrate"] = True
            self.finish(patch,self.moss,bone)

    def body(self):
        pelvis=self.block("Keystone pelvis","Hips",self.stone,(0,0,0),[
            (1.61,.24,.19,0,0),(1.70,.38,.246,0,0),(1.89,.407,.27,0,0),
            (2.05,.32,.22,0,0)],bevel=.037)
        self.block("Deep abdominal link","Spine",self.dark,(0,0,0),[
            (1.84,.25,.185,0,0),(1.96,.34,.24,0,0),(2.27,.42,.255,0,0),
            (2.43,.31,.195,0,0)],bevel=.038)
        for i in range(3):
            z=1.91+.127*i
            self.block("Broken front masonry rib","Spine",self.slate,(0,.105,z),[
                (0,.32+.023*i,.153,0,0),(.024,.349+.019*i,.164,0,0),
                (.098,.319+.016*i,.13,0,0)],bevel=.026)
        chest=self.block("Monolithic chest","Chest",self.stone,(0,0,0),[
            (2.20,.35,.245,0,.008),(2.29,.50,.32,0,.008),(2.57,.595,.365,0,.013),
            (2.75,.566,.33,-.017,.014),(2.899,.385,.24,-.034,.014),
            (2.979,.253,.189,-.027,0)],bevel=.047,worn=.058)
        chest_tree=self.bvh(chest)
        for sign in (-1,1):
            shape=[(sign*x,z) for x,z in ((.18,2.763),(.387,2.745),(.508,2.618),
                                        (.449,2.517),(.295,2.443),(.224,2.514))]
            self.fitted_plate(chest_tree,"Chest",shape)
            hit,_,_,_=chest_tree.ray_cast(Vector((sign*.334,1.1,2.533)),-FORWARD,1.5)
            self.rivet("Chest",hit+FORWARD*.033,.024)
        self.stone_cracks(chest,"Chest",[
            [(-.54,2.67),(-.43,2.62),(-.44,2.55),(-.37,2.52),(-.36,2.42),(-.28,2.34)],
            [(.49,2.56),(.42,2.51),(.449,2.49),(.37,2.432),(.38,2.39)],
            [(.20,2.29),(.24,2.36),(.186,2.42)],
        ])
        self.stone_cracks(pelvis,"Hips",[
            [(-.32,1.90),(-.278,1.862),(-.298,1.82),(-.248,1.76),(-.226,1.69)],
            [(.378,1.93),(.314,1.86),(.342,1.824),(.299,1.78)],
        ])
        self.brace()
        self.back()

    def brace(self):
        # A stone seal and bronze binding frame a small, deliberately muted mineral seam.
        z=2.583
        outer=[(0,.429,z+.230),(.176,.421,z+.140),(.216,.410,z-.029),
               (.13,.422,z-.172),(0,.443,z-.231),(-.154,.427,z-.156),
               (-.217,.405,z-.022),(-.142,.426,z+.177)]
        self.plate("Carved inset plaque","Chest",self.dark,outer,.064,.013)
        frame=[]
        for i,p in enumerate(outer):
            q=outer[(i+1)%len(outer)]
            split=.09 if i in (1,5) else .02
            p,q=Vector(p),Vector(q)
            frame.append([p.lerp(q,t)+FORWARD*.018 for t in (.04+split,.28,.65,.966-split)])
        self.tube("Broken bronze seal rim",frame,.015,self.bronze,"Chest",sides=7,taper=.13)
        self.plate("Worn dragon seal","Chest",self.light,[(x,.460,z+h) for x,h in DRAGON_SEAL],.019,.004)
        veins=[
            [(-.123,-.155),(-.104,-.112),(-.07,-.094),(-.077,-.052)],
            [(.086,-.09),(.066,-.059),(.088,-.034),(.074,.024)],
            [(-.122,.064),(-.138,.112),(-.108,.133)],
        ]
        self.tube("Mineral in the seal fractures",[
            [(x,.451,z+h) for x,h in path] for path in veins],.0061,self.ember,"Chest",sides=6,taper=.28)

    def back(self):
        for i in range(5):
            z=2.26+.128*i
            rx=.29-.025*i
            spine=self.block("Rear stacked buttress","Chest",self.slate,(.014,-.205,z),[
                (0,rx,.124,0,0),(.018,rx+.023,.174,0,-.006),
                (.107,rx*.82,.161,0,0),(.138,rx*.67,.10,0,0)],bevel=.025)
            if i in (2,4):
                self.moss_patch(spine,"Chest",(0,-.377,z+.128),.21,13)
        for sign in (-1,1):
            trace=[(sign*x,y,z) for x,y,z in ((.22,-.338,2.81),(.29,-.374,2.66),
                (.255,-.384,2.54),(.273,-.353,2.33),(.201,-.302,2.22))]
            self.tube("Patinated back brace",[trace],.021,self.patina,"Chest",sides=7,taper=.23)

    def head(self):
        self.block("Recessed neck coupling","Neck",self.dark,(0,0,0),[
            (2.805,.176,.154,0,0),(2.969,.19,.174,0,0),(3.115,.147,.132,0,0)],bevel=.026)
        skull=self.block("Weathered head monolith","Head",self.stone,(0,0,0),[
            (2.975,.155,.147,0,.026),(3.045,.238,.212,0,.019),
            (3.19,.248,.221,0,.029),(3.343,.261,.221,-.01,.027),
            (3.472,.213,.174,-.016,.018),(3.558,.144,.145,-.028,0)],
            bevel=.032,worn=.051)
        for sign in (-1,1):
            outline=[(sign*x,y,z) for x,y,z in [
                (.061,.272,3.225),(.158,.299,3.278),(.233,.213,3.290),(.213,.208,3.098),
                (.132,.249,3.029),(.066,.263,3.075),(.108,.299,3.145)]]
            self.plate("Split cheek tablet","Head",self.light,outline,.031,.012)
            brow=[(sign*x,y,z) for x,y,z in [
                (.035,.269,3.318),(.107,.307,3.367),(.220,.245,3.395),
                (.247,.205,3.346),(.166,.304,3.290),(.060,.31,3.271)]]
            self.plate("Oblique chiseled brow","Head",self.slate,brow,.032,.013)
            eye=[(sign*x,y,z) for x,y,z in [
                (.063,.276,3.254),(.139,.291,3.277),(.183,.256,3.285),
                (.150,.268,3.259),(.092,.282,3.238)]]
            self.plate("Deep eye mineral","Head",self.ember,eye,.009,.0035)
        self.plate("Carved nasal bridge","Head",self.stone,[
            (-.043,.311,3.337),(.014,.335,3.333),(.048,.294,3.174),
            (.015,.340,3.124),(-.053,.316,3.162)],.045,.013)
        self.plate("Damaged crown tablet","Head",self.light,[
            (-.241,.188,3.439),(-.166,.204,3.532),(-.027,.240,3.561),
            (.125,.191,3.515),(.182,.177,3.454),(.035,.267,3.383),
            (-.165,.253,3.378)],.031,.014)
        self.stone_cracks(skull,"Head",[
            [(-.09,3.52),(-.10,3.48),(-.065,3.45),(-.093,3.414)],
            [(.21,3.19),(.186,3.154),(.186,3.105),(.151,3.085)],
        ],.004)

    def shoulder(self,side,sign):
        bone=side+"UpperArm"
        axis=(sign,0,0)
        cap=self.block("Split cornerstone pauldron",bone,self.stone,(sign*.39,0,2.78),[
            (-.06,.242,.207,0,0),(.036,.372,.302,.056,0),
            (.261,.39,.302,.076,-.013),(.413,.28,.244,.103,-.024),
            (.51,.204,.157,.078,-.033)],axis=axis,bevel=.037,worn=.071)
        self.block("Shoulder outer layer",bone,self.light,(sign*.409,0,2.78),[
            (.218,.336,.288,.122,0),(.269,.362,.307,.119,0),
            (.331,.316,.273,.128,0),(.397,.245,.207,.09,0)],axis=axis,bevel=.024)
        if sign==-1:
            self.block("Carved half-pillar on the shoulder",bone,self.slate,(-.595,-.075,2.984),[
                (-.168,.232,.174,0,0),(-.05,.226,.196,0,0),
                (.108,.192,.167,-.055,-.044),(.283,.149,.104,-.096,-.055),
                (.346,.058,.077,-.129,-.047)],
                bevel=.026,worn=.093)
        self.moss_patch(cap,bone,(sign*.58,-.055,3.15),.36,25 if sign==-1 else 18)
        self.stone_cracks(cap,bone,[[(sign*.47,3.059),(sign*.53,3.009),
            (sign*.60,2.99),(sign*.58,2.933),(sign*.72,2.876)]],.006)
        for x in (.49,.655,.768):
            self.rivet(bone,(sign*x,.309,2.799),.020)

    def arms(self):
        for side,sign in (("Left",-1),("Right",1)):
            upper,lower,hand=[side+suffix for suffix in ("UpperArm","LowerArm","Hand")]
            self.shoulder(side,sign)
            axis=(sign,0,0)
            self.block("Upper arm bearing",upper,self.slate,(sign*.614,0,2.755),[
                (0,.194,.193,0,0),(.134,.211,.208,-.018,0),
                (.261,.168,.179,-.023,0),(.329,.13,.143,-.036,0)],
                axis,bevel=.032)
            self.finish(self.ellipsoid("Dark rotating elbow",(sign*.996,0,2.73),
                                       (.150,.149,.143),n=18,m=10),self.dark,lower)
            fist=self.block("Heavy fractured forearm",lower,self.stone,(sign*.969,0,2.74),[
                (-.034,.143,.137,0,0),(.051,.21,.198,-.013,0),
                (.233,.253,.235,-.012,.028),(.393,.215,.213,-.018,.023),
                (.55,.145,.161,-.027,.012)],axis,bevel=.043,worn=.08)
            self.block("Bronze wrist fastening",lower,self.bronze,(sign*1.334,.023,2.728),[
                (0,.224,.234,0,0),(.025,.232,.239,0,0),(.078,.226,.229,0,0),
                (.110,.199,.203,0,0)],axis,bevel=.016,worn=.023)
            self.stone_cracks(fist,lower,[[(sign*1.07,2.936),(sign*1.147,2.905),
                 (sign*1.16,2.806),(sign*1.204,2.773),(sign*1.233,2.646)]],.007)
            self.block("Fist palm block",hand,self.stone,(sign*1.443,.007,2.726),[
                (0,.148,.164,0,0),(.08,.193,.195,.015,0),
                (.225,.179,.187,-.008,0),(.287,.14,.168,-.011,0)],axis,bevel=.032)
            for i in range(4):
                z=2.604+.073*i
                dx=.019*abs(i-1.5)
                self.block("Carved stone knuckle",hand,self.slate,(sign*(1.631-dx),.126,z),[
                    (0,.059,.079,0,0),(.071,.061,.079,.008,0),
                    (.115,.046,.053,.011,-.024)],axis=axis,bevel=.012)
            self.block("Hooked thumb fragment",hand,self.light,(sign*1.593,.188,2.819),[
                (-.075,.064,.061,0,0),(-.012,.072,.070,0,0),
                (.056,.046,.047,sign*.021,.022)],bevel=.014)

    def legs(self):
        for side,sign in (("Left",-1),("Right",1)):
            thigh,shin,foot=[side+suffix for suffix in ("UpperLeg","LowerLeg","Foot")]
            x=sign*.3094
            self.block("Deep leg hinge",thigh,self.dark,(x,0,0),[
                (.966,.153,.145,0,0),(1.09,.184,.186,0,0),(1.64,.175,.172,0,0),
                (1.761,.165,.162,0,0)],bevel=.027)
            self.block("Chipped thigh slab",thigh,self.stone,(x,.031,0),[
                (1.116,.169,.155,0,0),(1.223,.217,.204,sign*.022,0),
                (1.578,.23,.233,sign*.022,-.006),(1.739,.184,.177,sign*.035,0)],bevel=.036)
            self.block("Kneecap cut block",shin,self.light,(x,.121,0),[
                (.924,.106,.13,0,0),(.971,.174,.172,0,0),
                (1.094,.172,.17,0,.021),(1.166,.108,.128,0,.03)],
                bevel=.027)
            lower=self.block("Fractured shin column",shin,self.stone,(x,0,0),[
                (.227,.155,.15,0,0),(.321,.188,.207,sign*.009,0),
                (.614,.202,.223,sign*.009,0),(.86,.176,.182,-sign*.01,.007),
                (1.05,.143,.14,-sign*.01,0)],bevel=.040)
            self.stone_cracks(lower,shin,[[(x-sign*.11,.88),(x+sign*.016,.785),
                (x-sign*.026,.721),(x+sign*.073,.644),(x+sign*.049,.587),(x+sign*.103,.515)],
                [(x+sign*.09,.541),(x+sign*.13,.472),(x+sign*.073,.355)]],.0057)
            self.block("Stone foot and ankle",foot,self.slate,(x,.084,0),[
                (.024,.186,.257,0,0),(.063,.215,.273,0,0),(.182,.207,.23,0,-.009),
                (.278,.159,.179,0,-.027),(.341,.144,.147,0,-.067)],bevel=.033)
            for i in range(3):
                offset=(i-1)*.126
                self.block("Inset toe ridge",foot,self.stone,(x+offset,.28,0),[
                    (.033,.056,.099,0,0),(.064,.059,.113,0,.012),
                    (.179,.049,.096,0,-.011)],bevel=.013)

    def finalize(self):
        self.body()
        self.head()
        self.arms()
        self.legs()
        self.funerary_carving()
        for part in self.parts:
            if part.get("lichen_substrate") or part.data.materials[0] not in {self.stone,self.slate,self.light}:
                continue
            group=part.vertex_groups.new(name="Weathered stone only")
            group.add(list(range(len(part.data.vertices))),1,"REPLACE")
        select(self.parts)
        bpy.ops.object.join()
        mesh=bpy.context.object
        mesh.name="StoneWarden_Surface"
        for collection in list(mesh.users_collection):
            if collection!=self.collection:
                collection.objects.unlink(mesh)
        if self.collection not in mesh.users_collection[:]:
            self.collection.objects.link(mesh)
        self.scene.cursor.location=(0,0,0)
        bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
        mesh.parent=self.rig
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.mesh.normals_make_consistent(inside=False)
        bpy.ops.uv.smart_project(island_margin=.002,margin_method="FRACTION")
        bpy.ops.object.mode_set(mode="OBJECT")
        mesh.data.uv_layers.active.name="UV0"
        for layer in list(mesh.data.uv_layers):
            if layer.name!="UV0":
                mesh.data.uv_layers.remove(layer)
        quarry = mesh.vertex_groups["Weathered stone only"]
        assert all(abs(sum(g.weight for g in v.groups if g.group!=quarry.index)-1)<.0001
                   for v in mesh.data.vertices)
        # Subdivide after UV layout so the master and playable mesh share one
        # atlas. Cut stone erodes in place; metal seals and eyes keep sharp edges.
        detail = mesh.modifiers.new("Sculpt density","SUBSURF")
        detail.subdivision_type = "SIMPLE"
        detail.levels = detail.render_levels = 2
        detail.uv_smooth = "NONE"
        for name,size,depth in (("Quarried planes",.129,.048),("Spalled mineral grain",.036,.013)):
            rock_noise = bpy.data.textures.new(name,type="CLOUDS")
            rock_noise.noise_basis = "VORONOI_F2_F1" if size>.1 else "IMPROVED_PERLIN"
            rock_noise.noise_scale = size
            rock_noise.noise_depth = 1
            erosion = mesh.modifiers.new(name,"DISPLACE")
            erosion.texture = rock_noise
            erosion.strength = depth
            erosion.vertex_group = quarry.name
        tri=mesh.modifiers.new("Export triangulation","TRIANGULATE")
        tri.keep_custom_normals=True
        skin=mesh.modifiers.new("Stone fragment binding","ARMATURE")
        skin.object=self.rig
        return mesh

    def actions(self):
        rig=self.rig
        rig.animation_data_create()
        actions={}

        def rotate(name,x=0,y=0,z=0):
            pose=rig.pose.bones[name]
            basis=pose.bone.matrix_local.to_quaternion()
            delta=(Quaternion(UP,math.radians(z))@Quaternion(FORWARD,math.radians(y))@
                   Quaternion((1,0,0),math.radians(x)))
            pose.rotation_mode="QUATERNION"
            pose.rotation_quaternion=basis.inverted()@delta@basis

        for name,windup,finish in (("Idle",0,60),("Cleave",33,75),("Sweep",45,87),("Stomp",54,96)):
            action=bpy.data.actions.new("AN_StoneWarden_"+name)
            action.use_fake_user=True
            rig.animation_data.action=action
            for frame in range(1,finish+2):
                moment=frame-1
                rise=ramp(moment,0,windup) if windup else 0
                strike=ramp(moment,windup,windup+5) if windup else 0
                fade=1-ramp(moment,windup+7,finish) if windup else 0
                prepare=rise*(1-strike)
                hit=strike*fade
                for bone in rig.pose.bones:
                    bone.rotation_mode="QUATERNION"
                    bone.rotation_quaternion=Quaternion()
                    bone.location=(0,0,0)
                rotate("LeftUpperArm",y=-71+18*prepare)
                rotate("RightUpperArm",y=69-18*prepare)
                rotate("LeftLowerArm",x=6)
                rotate("RightLowerArm",x=7)
                if name=="Idle":
                    breathe=math.sin(math.tau*moment/finish)
                    rotate("Chest",x=1.7*breathe)
                    rotate("Head",x=-1.2*breathe,z=2.3*breathe)
                elif name=="Cleave":
                    rotate("Chest",x=-9*prepare+19*hit,z=15*prepare-18*hit)
                    rotate("RightUpperArm",x=-47*prepare+32*hit,y=69-110*prepare-13*hit,z=14*prepare-60*hit)
                    rotate("RightLowerArm",x=30*prepare-12*hit,z=-22*prepare)
                    rotate("LeftUpperArm",y=-71+24*prepare+16*hit,z=19*prepare)
                elif name=="Sweep":
                    rotate("Chest",x=-4*prepare+8*hit,z=34*prepare-41*hit)
                    rotate("RightUpperArm",x=16*prepare,y=69-48*prepare-45*hit,z=36*prepare-64*hit)
                    rotate("RightLowerArm",x=17*prepare,z=-9*hit)
                    rotate("LeftUpperArm",y=-71+25*prepare+13*hit,z=-16*prepare+24*hit)
                else:
                    rotate("Chest",x=-8*prepare+19*hit)
                    rotate("LeftUpperArm",y=-71+26*prepare,z=-17*prepare)
                    rotate("RightUpperArm",y=69-23*prepare,z=19*prepare)
                    rotate("RightUpperLeg",x=35*prepare-7*hit)
                    rotate("RightLowerLeg",x=-54*prepare+9*hit)
                    rotate("RightFoot",x=16*prepare-3*hit)
                for bone in rig.pose.bones:
                    bone.keyframe_insert(data_path="rotation_quaternion",frame=frame,group=bone.name)
                    bone.keyframe_insert(data_path="location",frame=frame,group=bone.name)
            actions[name]=action
        rig.animation_data.action=None
        for pose in rig.pose.bones:
            pose.rotation_quaternion=Quaternion()
        self.scene.frame_set(1)
        return actions

    def review(self,actions):
        scene=self.scene
        camera=studio_camera(scene)
        stage=bpy.data.objects["Studio shadow floor"]
        stage.location.z=.006
        for name,power,position in (
            ("Soft key",780,(3.65,2.7,5.4)),("Front fill",135,(-3.1,4.3,2.75)),
            ("Rim",880,(-2.6,-2.1,4.8)),("Back fill",210,(3.4,-2.8,3.8)),
        ):
            light=bpy.data.objects[name]
            light.location=position
            light.rotation_euler=(Vector((0,0,1.96))-light.location).to_track_quat("-Z","Y").to_euler()
            light.data.energy=power
            light.data.size=2.2
        scene.render.resolution_x=940
        scene.render.resolution_y=1180
        scene.cycles.samples=32
        rigs=[
            ("",actions["Idle"],8,(3.78,7.84,3.52),(0,0,1.84),4.12),
            ("Detail",actions["Idle"],8,(2.41,5.56,3.64),(0,.13,2.83),2.04),
            ("Back",actions["Idle"],8,(-3.67,-8.2,3.72),(0,0,1.87),4.15),
            ("Cleave",actions["Cleave"],30,(3.97,7.8,3.63),(0,0,2.13),4.9),
            ("Sweep",actions["Sweep"],46,(-3.5,7.5,3.4),(0,0,1.95),4.8),
            ("Stomp",actions["Stomp"],50,(3.97,7.1,3.85),(0,0,1.86),4.49),
        ]
        for suffix,action,frame,position,target,scale in rigs:
            self.rig.animation_data.action=action
            self.rig.animation_data.action_slot=action.slots[0]
            scene.frame_set(frame)
            camera.location=position
            camera.rotation_euler=(Vector(target)-camera.location).to_track_quat("-Z","Y").to_euler()
            camera.data.ortho_scale=scale
            scene.render.filepath=str(OUTPUT/("StoneWarden"+suffix+".png"))
            bpy.ops.render.render(write_still=True)


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--bake",type=int,default=2048)
    parser.add_argument("--no-preview",action="store_true")
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    warden=Warden()
    mesh=warden.finalize()
    actions=warden.actions()
    OUTPUT.mkdir(parents=True,exist_ok=True)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT/(STEM+".blend")),compress=True)
    select([mesh])
    print("Master triangles:", len(mesh.evaluated_get(bpy.context.evaluated_depsgraph_get()).data.polygons),flush=True)
    mesh.modifiers["Sculpt density"].levels=1
    for modifier in list(mesh.modifiers):
        if modifier.type=="ARMATURE":
            continue
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    mesh.vertex_groups.remove(mesh.vertex_groups["Weathered stone only"])
    # Preserve dense editable stone in the master while collapsing coplanar
    # triangles in the playable surface before baking its tangent normals.
    reduction=mesh.modifiers.new("Gameplay stone density","DECIMATE")
    reduction.ratio=.52
    reduction.use_collapse_triangulate=True
    bpy.ops.object.modifier_move_to_index(modifier=reduction.name,index=0)
    bpy.ops.object.modifier_apply(modifier=reduction.name)
    if args.bake:
        bake_atlas(mesh,args.bake,GAME/"Textures/Characters/StoneWarden","T_StoneWarden","UV0")
    export_collection(GAME/"Characters/Models"/(STEM+".fbx"),bake_animation=False)
    print(f"{STEM}: {len(mesh.data.polygons)} triangles and {len(warden.rig.data.bones)} bones",flush=True)
    if not args.no_preview:
        if args.bake:
            preview_baked_coat(mesh,GAME/"Textures/Characters/StoneWarden","T_StoneWarden")
            tree=mesh.material_slots[0].material.node_tree
            mask=next(n for n in tree.nodes if n.type=="TEX_IMAGE" and "_Mask" in n.image.name)
            split=tree.nodes.new("ShaderNodeSeparateColor")
            tree.links.new(mask.outputs["Color"],split.inputs[0])
            tree.links.new(split.outputs[0],tree.nodes["Principled BSDF"].inputs["Metallic"])
        warden.review(actions)


if __name__=="__main__":
    main()
