"""Create the brown bear used by Dragonland's neutral camps."""

import argparse
import math
from pathlib import Path
from random import Random
import sys

import bpy
from mathutils import Quaternion, Vector
from mathutils.noise import noise

sys.path.insert(0,str(Path(__file__).resolve().parent))
from character_art import bake_atlas, guard_hair_tile, material, preview_baked_coat, studio_camera
from creature_mesh import CreatureMesh, ramp, select
from export_fbx import export_collection

ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/"ArtSource/Creatures/SK_BrownBear"
GAME=ROOT/"Assets/Art"
STEM="SK_BrownBear"
UP=Vector((0,0,1))


class Bear(CreatureMesh):
    def __init__(self):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        self.scene=bpy.context.scene
        self.scene.unit_settings.system="METRIC"
        self.scene.render.fps=30
        self.collection=bpy.data.collections.new("EXPORT")
        self.scene.collection.children.link(self.collection)
        self.parts=[]
        self.coats=set()
        self.legs={}
        self.make_materials()
        self.rig=self.make_rig()

    def make_materials(self):
        self.fur=material("Bear_BrownCoat",(.14,.081,.045),0,.82,290,.00065,"leather")
        nodes,links=self.fur.node_tree.nodes,self.fur.node_tree.links
        shader=nodes.get("Principled BSDF")
        groom=nodes.new("ShaderNodeAttribute")
        groom.attribute_name="BearGroom"
        pigment=nodes.new("ShaderNodeVertexColor")
        pigment.layer_name="BearCoat"
        strands=nodes.new("ShaderNodeTexImage")
        strands.image=guard_hair_tile("Brown bear banded guard hairs",8291)
        links.new(groom.outputs["Vector"],strands.inputs["Vector"])
        leg_groom=nodes.new("ShaderNodeAttribute")
        leg_groom.attribute_name="BearLegGroom"
        leg_hairs=nodes.new("ShaderNodeTexImage")
        leg_hairs.image=strands.image
        links.new(leg_groom.outputs["Vector"],leg_hairs.inputs["Vector"])
        split=nodes.new("ShaderNodeSeparateXYZ")
        links.new(leg_groom.outputs["Vector"],split.inputs[0])
        strand_mix=nodes.new("ShaderNodeMixRGB")
        links.new(split.outputs["Z"],strand_mix.inputs[0])
        links.new(strands.outputs["Color"],strand_mix.inputs[1])
        links.new(leg_hairs.outputs["Color"],strand_mix.inputs[2])
        fibres=nodes.new("ShaderNodeTexNoise")
        fibres.inputs["Scale"].default_value=16
        fibres.inputs["Detail"].default_value=3
        links.new(nodes.get("Texture Coordinate").outputs["Object"],fibres.inputs["Vector"])
        links.new(strand_mix.outputs[0],nodes.get("Bump").inputs["Height"])
        nodes.get("Bump").inputs["Distance"].default_value=.0018
        broken_color=nodes.new("ShaderNodeValToRGB")
        for stop,position,intensity in zip(broken_color.color_ramp.elements,(.26,.74),(.59,1.20)):
            stop.position=position
            stop.color=(intensity,intensity,intensity,1)
        links.new(fibres.outputs["Fac"],broken_color.inputs["Fac"])
        mix=nodes.new("ShaderNodeMixRGB")
        mix.blend_type="MULTIPLY"
        mix.inputs[0].default_value=.64
        links.new(pigment.outputs["Color"],mix.inputs[1])
        links.new(broken_color.outputs["Color"],mix.inputs[2])
        ticking=nodes.new("ShaderNodeMixRGB")
        ticking.blend_type="OVERLAY"
        ticking.inputs[0].default_value=.94
        links.new(mix.outputs[0],ticking.inputs[1])
        links.new(strand_mix.outputs[0],ticking.inputs[2])
        links.new(ticking.outputs[0],shader.inputs["Base Color"])
        shader.inputs["Sheen Weight"].default_value=.038
        shader.inputs["Specular IOR Level"].default_value=.23
        self.nose=material("Bear_Nose",(.014,.012,.011),0,.43,610,.00072,"leather")
        nodes,links=self.nose.node_tree.nodes,self.nose.node_tree.links
        pores=nodes.new("ShaderNodeTexVoronoi")
        pores.feature="DISTANCE_TO_EDGE"
        pores.inputs["Scale"].default_value=390
        links.new(nodes.get("Texture Coordinate").outputs["Object"],pores.inputs["Vector"])
        links.new(pores.outputs["Distance"],nodes.get("Bump").inputs["Height"])
        self.lip=material("Bear_Lips",(.026,.015,.012),0,.56,410,.00031,"leather")
        self.pad=material("Bear_PawPad",(.026,.019,.016),0,.76,610,.00088,"leather")
        self.dark=material("Bear_EyeDark",(.005,.004,.003),0,.39,0,0,"")
        self.iris=material("Bear_HazelIris",(.19,.071,.016),0,.21,0,0,"")
        iris_tint=self.iris.node_tree.nodes.new("ShaderNodeVertexColor")
        iris_tint.layer_name="BearIris"
        self.iris.node_tree.links.new(iris_tint.outputs["Color"],self.iris.node_tree.nodes.get("Principled BSDF").inputs["Base Color"])
        self.hole=material("Bear_NostrilInterior",(.006,.004,.003),0,.70,0,0,"")
        for mat in (self.dark,self.iris):
            mat.node_tree.nodes.get("Principled BSDF").inputs["Specular IOR Level"].default_value=.25
        self.claw=material("Bear_Claw",(.047,.033,.021),0,.51,297,.00018,"leather")
        horn=self.claw.node_tree.nodes.new("ShaderNodeVertexColor")
        horn.layer_name="HornPigment"
        self.claw.node_tree.links.new(horn.outputs["Color"],self.claw.node_tree.nodes.get("Principled BSDF").inputs["Base Color"])
        self.gum=material("Bear_Gum",(.085,.034,.029),0,.48,208,.00014,"leather")
        self.tooth=material("Bear_Ivory",(.49,.369,.225),0,.43,334,.00009,"leather")
        self.tongue=material("Bear_Tongue",(.151,.056,.047),0,.44,266,.00052,"leather")

    def make_rig(self):
        data=bpy.data.armatures.new("BrownBear_Quadruped")
        rig=bpy.data.objects.new(STEM,data)
        self.collection.objects.link(rig)
        select([rig])
        bpy.ops.object.mode_set(mode="EDIT")

        def bone(name,a,b,parent=None):
            joint=data.edit_bones.new(name)
            joint.head,joint.tail=a,b
            if parent:
                joint.parent=data.edit_bones[parent]

        bone("Root",(0,0,0),(0,.14,0))
        bone("Pelvis",(0,-.64,.98),(0,-.32,1.02),"Root")
        bone("Spine",(0,-.32,1.02),(0,.09,1.045),"Pelvis")
        bone("Chest",(0,.09,1.045),(0,.43,1.14),"Spine")
        bone("Neck",(0,.43,1.14),(0,.69,1.16),"Chest")
        bone("Head",(0,.69,1.16),(0,1.22,1.079),"Neck")
        bone("Jaw",(0,.851,1.037),(0,1.222,.982),"Head")
        bone("Tail",(0,-.846,1.013),(0,-1.02,.873),"Pelvis")
        for side,sign in (("L",-1),("R",1)):
            for limb,rows,parent in (
                ("Fore",((.260,.366,1.11),(.251,.343,.647),(.232,.333,.182),
                         (.231,.452,.064),(.231,.603,.055)),"Chest"),
                ("Hind",((.265,-.613,.934),(.279,-.495,.561),(.258,-.748,.203),
                         (.258,-.608,.065),(.258,-.47,.058)),"Pelvis"),
            ):
                points=[Vector((sign*x,y,z)) for x,y,z in rows]
                names=[]
                for i,suffix in enumerate(("Upper","Lower","Pastern","Toe")):
                    name=f"{limb}{suffix}.{side}"
                    bone(name,points[i],points[i+1],names[-1] if names else parent)
                    names.append(name)
                self.legs[limb,side]=(points,names)
            bone("Ear."+side,(sign*.193,.747,1.305),(sign*.229,.730,1.487),"Head")
        bpy.ops.object.mode_set(mode="OBJECT")
        rig.show_in_front=True
        return rig

    def body_weights(self,point):
        x,y,z=point
        body=self.chain_weights(("Pelvis","Spine","Chest","Neck","Head"),y,
                                ((-.63,-.31),(-.11,.20),(.33,.61),(.625,.85)))
        limb="Fore" if y>-.03 else "Hind"
        points,names=self.legs[limb,"L" if x<0 else "R"]
        top=1.116 if limb=="Fore" else .98
        inside=ramp(abs(x),.09,.214)
        inside+=(1-inside)*(1-ramp(z,.49,.75))
        contribution=(1-ramp(z,top-.29,top))*inside
        contribution*=1-ramp(y,.56,.78) if z>.36 else 1
        if z>.50:
            axis=points[1].y+(points[0].y-points[1].y)*ramp(z,points[1].z,points[0].z)
            contribution*=1-ramp(abs(y-axis),.17,.37)
        if contribution<.00001:
            return body
        z2,z1=points[2].z,points[1].z
        legs=self.chain_weights(tuple(reversed(names)),z,
                               ((.071,.120),(z2-.05,z2+.092),(z1-.13,z1+.117)))
        return {**{n:w*(1-contribution) for n,w in body.items()},
                **{n:w*contribution for n,w in legs.items()}}

    def mass(self):
        components=[
            self.loft("Heavy trunk",[
                (-1.078,0,.981,.025,.033),(-1.051,0,.981,.102,.108),
                (-1.002,0,.982,.187,.191),(-.937,0,.970,.257,.257),
                (-.822,0,.953,.326,.34),(-.603,0,.935,.391,.373),
                (-.356,0,.951,.392,.347),(-.09,0,.956,.412,.389),
                (.132,0,.994,.424,.437),(.322,0,1.048,.393,.488),
                (.474,0,1.060,.336,.459),(.602,0,1.056,.253,.336),
                (.701,0,1.094,.199,.231),(.733,0,1.105,.105,.143)
            ],sections=44),
            self.loft("Broad head",[
                (.398,0,1.09,.232,.252),(.567,0,1.115,.240,.235),
                (.717,0,1.140,.271,.223),(.843,0,1.139,.259,.210),
                (.945,0,1.124,.224,.185),(1.033,0,1.085,.171,.143),
                (1.116,0,1.027,.146,.111),(1.208,0,1.018,.135,.091),
                (1.278,0,1.017,.105,.073),
            ],sections=44),
        ]
        for sign in (-1,1):
            components.extend([
                self.limb_mass("Malar arch",(sign*.189,.781,1.157),(sign*.156,.981,1.120),.083,.067,.038),
                self.limb_mass("Orbital brow",(sign*.180,.908,1.235),(sign*.115,1.024,1.211),.057,.045,.034),
                self.limb_mass("Whisker pad",(sign*.099,1.105,1.034),(sign*.075,1.251,1.017),.060,.051,.023),
                self.limb_mass("Upper cheek",(sign*.158,.934,1.034),(sign*.128,1.101,.986),.051,.058,.036),
            ])
        for (limb,side),(points,_) in self.legs.items():
            d=points[3]
            components.extend([
                self.leg_contour(limb,side),
                self.ellipsoid("Broad paw",(d.x,d.y+.055,.075),(.124,.158,.070)),
            ])
            for i in range(5):
                x=d.x+(i-2)*.039
                y=d.y+.138+.017*(1-abs(i-2)/2)
                components.append(self.ellipsoid("Paw digit",(x,y,.06),(.032,.064,.047),n=14,m=9))
        body=self.union(components,"Connected brown bear sculpture",.018,5)
        self.sculpt_coat(body)
        simplify=body.modifiers.new("Keep shoulder and muzzle contours","DECIMATE")
        simplify.ratio=.61
        bpy.ops.object.modifier_apply(modifier=simplify.name)
        return self.finish(body,self.fur,self.body_weights,True)

    def leg_contour(self,limb,side):
        if limb=="Fore":
            rings=[(.176,.296,1.33,.082,.115),(.224,.345,1.21,.146,.168),
                   (.249,.375,1.05,.168,.188),(.259,.350,.89,.162,.179),
                   (.253,.305,.73,.137,.154),(.248,.295,.638,.122,.147),
                   (.245,.321,.519,.116,.140),(.238,.336,.361,.096,.115),
                   (.232,.338,.215,.084,.096),(.230,.353,.151,.087,.102),
                   (.231,.403,.100,.097,.111),(.231,.464,.068,.103,.105)]
        else:
            rings=[(.169,-.661,1.22,.105,.133),(.221,-.631,1.10,.172,.201),
                   (.256,-.607,.963,.192,.220),(.273,-.563,.804,.181,.210),
                   (.277,-.497,.666,.143,.178),(.274,-.475,.576,.119,.136),
                   (.269,-.542,.490,.113,.152),(.263,-.628,.375,.092,.130),
                   (.259,-.722,.255,.074,.087),(.257,-.741,.199,.071,.074),
                   (.257,-.696,.131,.075,.085),(.258,-.613,.069,.093,.107)]
        sign=-1 if side=="L" else 1
        n=24
        verts=[(sign*x+w*math.cos(i*math.tau/n),y+d*math.sin(i*math.tau/n),z)
               for x,y,z,w,d in rings for i in range(n)]
        faces=[(k+i,k+n+i,k+n+(i+1)%n,k+(i+1)%n)
               for k in range(0,len(verts)-n,n) for i in range(n)]
        faces.extend((tuple(range(n)),tuple(reversed(range(len(verts)-n,len(verts))))))
        leg=self.surface(limb+" muscle contours",verts,faces)
        select([leg])
        smooth=leg.modifiers.new("Plantigrade leg contour","SUBSURF")
        smooth.levels=1
        bpy.ops.object.modifier_apply(modifier=smooth.name)
        return leg

    def sculpt_coat(self,body):
        self.eyes=[]
        surface=self.bvh(body)
        for side in (-1,1):
            aim=Vector((side*.155,.972,1.192))
            outward=Vector((side*.54,.84,.045)).normalized()
            hit,normal,_,_=surface.ray_cast(aim+outward*.32,-outward,.41)
            if hit is None:
                raise ValueError("Bear eye missed the head")
            outward=(normal*.52+outward*.48).normalized()
            vertical=(UP-outward*UP.dot(outward)).normalized()
            cross=vertical.cross(outward).normalized()
            self.eyes.append((hit,outward,vertical,cross))
        for vertex in body.data.vertices:
            point=vertex.co
            x,y,z=point
            normal=vertex.normal.copy()
            shoulder=ramp(z,.76,1.365)*(1-ramp(y,.68,1.015))
            flow=.0042*noise(Vector((x*32,y*10,z*38)))+.0023*noise(Vector((x*76,y*25,z*74)))
            vertex.co+=normal*flow*shoulder
            if y>1.018:
                # Two oblique creases compress the fleshy bridge above the whisker pads.
                flank=ramp(abs(x),.025,.121)
                sweep=y+.043*flank
                relief=.009*math.exp(-((sweep-1.095)/.015)**2)+.007*math.exp(-((sweep-1.148)/.016)**2)
                vertex.co+=normal*relief*ramp(z,1.038,1.104)*(.28+.72*flank)
                lift=.023*math.exp(-((y-1.15)/.074)**2)*ramp(abs(x),.064,.136)*(1-ramp(z,1.02,1.09))
                vertex.co.z+=lift*(1 if x>0 else .73)
            for center,outward,vertical,cross in self.eyes:
                delta=point-center
                radial=(delta.dot(cross)/.068)**2+(delta.dot(vertical)/.044)**2
                if radial<1 and abs(delta.dot(outward))<.049:
                    vertex.co-=outward*(.025*(1-radial)**1.12)
                if abs(delta.dot(outward))<.06:
                    arch=math.exp(-((delta.dot(cross)+.005)/.056)**2-
                                  ((delta.dot(vertical)-.025)/.019)**2)
                    vertex.co+=outward*(.016*arch)

    def ears(self):
        for side,sign in (("L",-1),("R",1)):
            center=Vector((sign*.210,.738,1.327))
            cross=Vector((1,-sign*.18,-sign*.16)).normalized()
            vertical=Vector((sign*.34,-.29,1)).normalized()
            forward=vertical.cross(cross).normalized()
            sections=44
            rows=((.17,-.062),(.55,-.052),(.89,-.035),(1.0,-.004),
                  (.95,.029),(.77,.037),(.59,.014),(.38,-.004),(.12,-.009))
            vertices=[]
            for size,depth in rows:
                for i in range(sections):
                    a=i*math.tau/sections
                    h=math.sin(a)
                    nick=.016*math.exp(-((a-2.50)/.19)**2) if side=="L" else 0
                    vertices.append(center+cross*((.086-nick)*math.cos(a)*size*(.94+.1*h))+
                                    vertical*(.095*h*size)+forward*depth)
            faces=[]
            for r in range(len(rows)-1):
                for i in range(sections):
                    j=(i+1)%sections
                    k=r*sections
                    faces.append((k+i,k+j,k+sections+j,k+sections+i))
            faces.extend((tuple(reversed(range(sections))),
                          tuple(range(len(vertices)-sections,len(vertices)))))
            ear=self.surface("Rounded bear ear "+side,vertices,faces)
            self.finish(ear,self.fur,"Ear."+side,True)
            tree=self.bvh(ear)
            rng=Random(86 if sign<0 else 48)
            fringe,blades=[],[]
            for _ in range(82):
                s=rng.uniform(-.062,.062)
                h=rng.uniform(-.059,.042)
                hit,n,_,_=tree.ray_cast(center+cross*s+vertical*h+forward*.13,-forward,.21)
                if hit is None:
                    continue
                length=rng.uniform(.009,.024)
                base=hit-forward*.0017
                width=rng.uniform(.0008,.0016)
                i=len(fringe)
                fringe.extend((base-cross*width,base+cross*width,
                               base+(vertical-cross*s*7)*length+forward*.004))
                blades.append((i,i+1,i+2))
            self.finish(self.surface("Ear fringe",fringe,blades),self.fur,"Ear."+side,True)

    def set_eyes(self,skull):
        for center,outward,up,cross in self.eyes:
            sign=1 if center.x>0 else -1
            origin=center-outward*.017
            def lens(x,z):
                return origin+cross*x+up*(z-sign*.24*x)+outward*(.0065*(1-(x/.034)**2-(z/.024)**2))

            spokes,rings=48,12
            iris_x=sign*.004
            coords=[(iris_x,0)]
            for row in range(1,rings+1):
                t=row/rings
                for i in range(spokes):
                    a=math.tau*i/spokes
                    u=math.cos(a)
                    s=math.sin(a)
                    coords.append((iris_x+(.030*u-iris_x)*t,
                                   t*s*(.0105 if s>0 else .0125)*(1-.28*abs(u))))
            faces=[(0,1+i,1+(i+1)%spokes) for i in range(spokes)]
            for row in range(rings-1):
                k=1+row*spokes
                faces.extend((k+i,k+spokes+i,k+spokes+(i+1)%spokes,k+(i+1)%spokes) for i in range(spokes))
            eye=self.surface("Recessed hazel eye",[lens(x,z) for x,z in coords],faces)
            tint=eye.data.color_attributes.new(name="BearIris",type="FLOAT_COLOR",domain="POINT")
            for i,(x,z) in enumerate(coords):
                r=math.hypot(x-iris_x,z)
                iris=ramp(r,.0053,.0071)*(1-ramp(r,.0114,.0142))
                iris*=.83+.13*math.cos(19*math.atan2(z,x-iris_x))-.23*ramp(z,.002,.010)
                tint.data[i].color=(.005+.43*iris,.004+.18*iris,.003+.038*iris,1)
            self.finish(eye,self.iris,"Head")
            lid=[lens(x,z) for x,z in coords[-spokes:]]
            self.tube("Wet eyelid",[lid+[lid[0]]],.0019,self.dark,"Head",sides=7,taper=0)
            verts=list(lid)
            for ring in range(1,5):
                t=ring/4
                for i,p in enumerate(lid):
                    a=math.tau*i/spokes
                    s=math.sin(a)
                    target=p+t*(cross*(.029*math.cos(a))+up*((.030 if s>0 else .021)*s))
                    hit,_,_,_=skull.ray_cast(target+outward*.105,-outward,.22)
                    anchor=hit if hit is not None else target
                    thickness=(.004+.009*max(0,s))*math.sin(math.pi*t)-.003*t*t
                    verts.append(p.lerp(anchor,t)+outward*thickness)
            faces=[(k+i,k+spokes+i,k+spokes+(i+1)%spokes,k+(i+1)%spokes)
                   for k in range(0,4*spokes,spokes) for i in range(spokes)]
            self.finish(self.surface("Bear orbital folds",verts,faces),self.fur,"Head",True)

    def nose_pad(self):
        rings,sections=30,68
        verts=[(0,1.259,.984)]
        for row in range(1,rings):
            lat=math.pi*(row/rings-.5)
            for j in range(sections):
                a=j*math.tau/sections
                u=math.cos(lat)*math.sin(a)
                front=math.cos(lat)*math.cos(a)
                v=math.sin(lat)
                side=abs(u)
                slit=.14+.55*(side-.49)-3.10*max(0,side-.66)**2
                aperture=.29-.16*ramp(side,.48,.89)
                recess=math.exp(-((side-.59)/.255)**4-((v-slit)/aperture)**2)
                groove=math.exp(-(u/.072)**2)*max(0,-v)
                verts.append((.122*u*(.95+.30*v),1.259+.064*front-max(0,front)*(.035*recess+.009*groove),
                              1.042+.058*v-.005*side**3))
        verts.append((0,1.259,1.100))
        faces=[]
        for j in range(sections):
            nxt=(j+1)%sections
            faces.append((0,1+nxt,1+j))
            for row in range(rings-2):
                k=1+row*sections
                faces.append((k+j,k+nxt,k+sections+nxt,k+sections+j))
            k=1+(rings-2)*sections
            faces.append((k+j,k+nxt,len(verts)-1))
        nose=self.surface("Nasal leather and alar grooves",verts,faces,self.nose)
        nose.data.materials.append(self.hole)
        for p in nose.data.polygons:
            x,y,z=p.center
            side=abs(x)/.117
            slit=1.049+.032*(side-.49)-.180*max(0,side-.66)**2
            if y>1.263 and ((side-.58)/.24)**4+((z-slit)/.011)**2<1:
                p.material_index=1
        self.finish(nose,self.nose,"Head")

    def eyes_and_muzzle(self):
        skull=self.bvh(self.skin)
        self.set_eyes(skull)
        self.nose_pad()
        self.muzzle_details(skull)

        jaw=self.loft("Mobile lower mandible",[
            (.678,0,.965,.095,.105),(.784,0,.952,.156,.111),(.901,0,.930,.175,.092),
            (1.029,0,.923,.147,.072),(1.146,0,.930,.119,.055),
            (1.240,0,.947,.095,.039),(1.277,0,.96,.070,.024),
        ],sections=38)
        for mat in (self.fur,self.lip,self.gum):
            jaw.data.materials.append(mat)
        for face in jaw.data.polygons:
            if len(face.vertices)==4:
                height=math.cos((face.index%38+.5)*math.tau/38)
                face.material_index=2 if height>.50 else (1 if height>.24 else 0)
        select([jaw])
        sub=jaw.modifiers.new("Jaw contour","SUBSURF")
        sub.levels=1
        bpy.ops.object.modifier_apply(modifier=sub.name)
        self.finish(jaw,self.fur,"Jaw",True)
        self.inner_mouth(skull)
        jaw_surface=self.bvh(jaw)

        for sign in (-1,1):
            self.teeth(skull,jaw_surface,sign)
            self.mouth_corner(skull,jaw_surface,sign)

    def mouth_corner(self,skull,jaw,sign):
        rows,cols=8,20
        verts=[]
        for row in range(rows):
            h=row/(rows-1)
            for col in range(cols):
                t=col/(cols-1)
                front=.957+.055*h-.031*math.sin(math.pi*h)
                y=.820+(front-.820)*t
                low=.928+.011*ramp(y,.83,1.02)
                high=1.045-.030*ramp(y,.85,1.02)
                axis=Vector((-sign,0,0))
                upper,_,_,_=skull.ray_cast(Vector((sign*.36,y,high)),axis,.40)
                lower,_,_,_=jaw.ray_cast(Vector((sign*.36,y,low)),axis,.40)
                p=lower.lerp(upper,h)
                p.x-=sign*(.037+.015*math.sin(math.pi*h)+.037*(1-t)**2)
                verts.append(p)
        def weights(p):
            low=.928+.011*ramp(p.y,.83,1.02)
            high=1.045-.030*ramp(p.y,.85,1.02)
            head=ramp(p.z,low,high)
            return {"Head":head,"Jaw":1-head}
        faces=[(i,i+1,i+cols+1,i+cols) for i in range((rows-1)*cols) if i%cols<cols-1]
        self.finish(self.surface("Buccal lining",verts,faces),self.gum,weights)

    def muzzle_details(self,skull):
        for sign in (-1,1):
            follicles,faces,whiskers=[],[],[]
            for row in range(2):
                for col in range(6-row):
                    y=1.121+.022*col+.007*row
                    z=1.022+.012*row+.005*math.sin(col*.83)
                    hit,n,_,_=skull.ray_cast(Vector((sign*.28,y,z)),Vector((-sign,0,0)),.28)
                    if hit is None:
                        continue
                    up=(UP-n*UP.dot(n)).normalized()
                    across=n.cross(up)
                    center=hit+n*.0009
                    k=len(follicles)
                    follicles.append(center+n*.0008)
                    for j in range(8):
                        a=j*math.tau/8
                        follicles.append(center+across*(.0037*math.cos(a))+up*(.0026*math.sin(a)))
                        faces.append((k,k+1+j,k+1+(j+1)%8))
                    if row==0 and col%2==0:
                        whiskers.append([center,center+Vector((sign*.031,-.006,.004)),
                                         center+Vector((sign*(.063-.004*col),-.026,-.004))])
            self.finish(self.surface("Whisker follicles",follicles,faces),self.lip,"Head")
            self.tube("Tactile hairs",whiskers,.0011,self.lip,"Head",sides=5,taper=.95)
        # One healed gash changes the direction and colour of the short coat on the right cheek.
        verts=[]
        for i in range(29):
            t=i/28
            y=.973+.107*t
            z=1.157-.070*t+.006*math.sin(5*t)
            width=.0003+.0036*math.sin(math.pi*t)**.76
            for edge in (-1,0,1):
                p,n,_,_=skull.ray_cast(Vector((.32,y+width*.62*edge,z+width*edge)),Vector((-1,0,0)),.33)
                if p is None:
                    raise ValueError("Cheek scar missed the skull")
                verts.append(p+n*(.0012 if edge==0 else .0002))
        faces=[(i,i+3,i+4,i+1) for i in range(len(verts)-3) if i%3<2]
        self.finish(self.surface("Healed muzzle scar",verts,faces),self.fur,"Head",True)

    def teeth(self,skull,jaw,sign):
        for upper,tree,bone in ((True,skull,"Head"),(False,jaw,"Jaw")):
            down=-1 if upper else 1
            def seated(x,y):
                start=Vector((sign*x,y,.785 if upper else 1.20))
                hit,_,_,_=tree.ray_cast(start,-down*UP,.46)
                if hit is None:
                    raise ValueError(f"Tooth missed its arcade at {x}, {y}")
                return hit

            def arcade(y):
                canine=.018 if upper else -.007
                return (.110-.022*ramp(y,1.01,1.15)+canine*math.exp(-((y-1.198)/.028)**2)
                        -.037*ramp(y,1.211,1.26))

            gum=[]
            for row in range(40):
                t=row/39
                y=.960+.300*t
                for col in range(7):
                    u=col/3-1
                    width=.018*math.sin(math.pi*t)**.37+.0003
                    p=seated(arcade(y)+width*u,y)
                    p.z+=down*(.0045*(1-u*u)*math.sin(math.pi*t)-.0006)
                    gum.append(p)
            faces=[(i,i+1,i+8,i+7) for i in range(39*7) if i%7<6]
            self.finish(self.surface("Alveolar ridge",gum,faces),self.gum,bone)
            root=seated(.106 if upper else .069,1.196 if upper else 1.228)
            length=.072 if upper else .047
            radius=.018 if upper else .013
            verts=[]
            for depth,width in ((-.14,.84),(.08,1),(.32,.85),(.62,.56),(.83,.30),(1,.045)):
                mid=root+Vector((sign*.003*math.sin(math.pi*depth),-.015*depth*depth,down*length*depth))
                for j in range(14):
                    a=math.tau*j/14
                    verts.append(mid+Vector((radius*width*.83*math.cos(a),radius*width*math.sin(a),0)))
            faces=[(k+j,k+(j+1)%14,k+14+(j+1)%14,k+14+j) for k in range(0,70,14) for j in range(14)]
            faces.append(tuple(range(70,84)))
            self.finish(self.surface("Bear canine",verts,faces),self.tooth,bone)
            for j in range(3):
                root=seated(.012+j*.022,(1.262 if upper else 1.253)-j*.005)
                self.tube("Worn incisor",[[root-down*UP*.003,root+down*UP*.005,root+down*UP*(.011+.001*j)]],
                          .008,self.tooth,bone,sides=9,taper=.49)
            for j,y in enumerate((1.146,1.109,1.064,1.009)):
                y+=0 if upper else .012
                x=arcade(y)
                verts=[]
                # Low, tuberculate crowns broaden toward the molars. The roots follow
                # both slopes of the gum, including at the back of the tilted maxilla.
                for rad,h in ((.83,-.004),(1,.004),(.86,.010),(.59,.013),(.28,.011),(.025,.010)):
                    for n in range(16):
                        a=math.tau*n/16
                        dx=rad*(.008+.0016*j)*math.cos(a)
                        dy=rad*(.014+.002*j)*math.sin(a)
                        p=seated(x+dx,y+dy)
                        cusp=.004*math.cos((2 if j==0 else 4)*a)*rad if h>.009 else 0
                        p.z+=down*(h+cusp)
                        verts.append(p)
                faces=[(k+i,k+(i+1)%16,k+16+(i+1)%16,k+16+i) for k in range(0,80,16) for i in range(16)]
                faces.append(tuple(range(80,96)))
                self.finish(self.surface("Tuberculate cheek tooth",verts,faces),self.tooth,bone)

    def inner_mouth(self,skull):
        verts=[]
        cols=17
        for row in range(35):
            t=row/34
            y=.857+.410*t
            width=(.130-.053*ramp(y,1.052,1.265))*max(.015,math.sin(math.pi*t)**.34)
            for col in range(cols):
                x=width*(2*col/(cols-1)-1)
                hit,_,_,_=skull.ray_cast(Vector((x,y,.777)),UP,.40)
                if hit is None:
                    raise ValueError("Palate missed the underside of the muzzle")
                ridge=.002*max(0,math.cos(169*(y+abs(x)*.31)))
                verts.append(hit-UP*(.0016+ridge))
        faces=[(i,i+1,i+cols+1,i+cols) for i in range(34*cols) if i%cols<cols-1]
        self.finish(self.surface("Ridged upper palate",verts,faces),self.gum,"Head")
        tongue=self.loft("Tongue",[(.839,0,1.004,.035,.007),(.902,0,1.018,.093,.018),
            (.980,0,1.008,.092,.020),(1.083,0,.993,.078,.016),(1.165,0,.985,.063,.014),
            (1.215,0,.983,.041,.011),(1.236,0,.979,.010,.006)])
        for v in tongue.data.vertices:
            x,y,z=v.co
            v.co.z-=.0045*math.exp(-(x/.011)**2)
        select([tongue])
        bevel=tongue.modifiers.new("Tongue margins","SUBSURF")
        bevel.levels=1
        bpy.ops.object.modifier_apply(modifier=bevel.name)
        self.finish(tongue,self.tongue,"Jaw")

    def feet(self):
        for (limb,side),(points,names) in self.legs.items():
            d=points[3]
            self.finish(self.ellipsoid("Plantar pad",(d.x,d.y+.048,.026),(.085,.091,.021)),
                        self.pad,names[-1])
            for i in range(5):
                x=d.x+(i-2)*.039
                y=d.y+.192+.016*(1-abs(i-2)/2)
                length=(.066 if limb=="Fore" else .044)*(1-.15*abs(i-2)/2)
                verts=[]
                for travel,z,width in ((-.27,.061,1),(.04,.065,1),(.40,.060,.78),
                                       (.72,.045,.48),(.91,.027,.23),(1,.017,.045)):
                    for j in range(10):
                        a=j*math.tau/10
                        verts.append((x+.0128*width*math.cos(a),y+length*travel,
                                      z+.0138*width*math.sin(a)))
                faces=[(k+j,k+(j+1)%10,k+10+(j+1)%10,k+10+j) for k in range(0,50,10) for j in range(10)]
                faces.append(tuple(range(50,60)))
                nail=self.surface("Excavating claw",verts,faces)
                pigment=nail.data.color_attributes.new(name="HornPigment",type="FLOAT_COLOR",domain="POINT")
                for v in nail.data.vertices:
                    worn=ramp(v.index//10,0,5)*(.80 if limb=="Fore" else .45)
                    pigment.data[v.index].color=(.032+.24*worn,.022+.165*worn,.014+.083*worn,1)
                self.finish(nail,self.claw,names[-1])
                self.finish(self.ellipsoid("Digital pad",(x,y-.045,.022),(.023,.030,.017),n=12,m=7),
                            self.pad,names[-1])
        self.finish(self.ellipsoid("Short natural tail",(0,-1.043,.922),(.069,.080,.063),
                                  (0,-1,-.64)),self.fur,"Tail",True)

    def guard_coat(self):
        rng=Random(19031)
        data=self.skin.data
        data.calc_loop_triangles()
        triangles=list(data.loop_triangles)
        tree=self.bvh(self.skin)
        verts,faces=[],[]
        for tri in rng.choices(triangles,weights=[tri.area for tri in triangles],k=3800):
            a,b,c=(data.vertices[i].co for i in tri.vertices)
            u,v=rng.random(),rng.random()
            if u+v>1:
                u,v=1-u,1-v
            root=a+(b-a)*u+(c-a)*v
            x,y,z=root
            if y>1.01 or z<.29:
                continue
            mantle=ramp(z,.84,1.21)*ramp(y,-.40,.23)*(1-ramp(y,.70,.95))
            ruff=ramp(y,.52,.77)*(1-ramp(y,.87,1.01))*ramp(abs(x),.10,.20)
            if rng.random()>.25+.63*max(mantle,ruff):
                continue
            normal=tri.normal
            flow=Vector((.9*x,-.85,-.50-.78*ruff-.65*(1-ramp(z,.55,.94))))
            flow-=normal*flow.dot(normal)
            if flow.length<.13:
                continue
            flow.normalize()
            cross=normal.cross(flow).normalized()
            length=(.053+.035*mantle+.038*ruff)*rng.uniform(.61,1.15)
            width=length*rng.uniform(.075,.13)
            bend=rng.uniform(-.10,.10)*length
            k=len(verts)
            for t,w in ((0,.62),(.39,.89),(.72,.51),(1,0)):
                along=root+flow*(length*t)+cross*(bend*t*t)
                surface,n,_,_=tree.find_nearest(along,.06)
                if surface is None or n.dot(normal)<.48:
                    surface,n=along,normal
                mid=surface+n*(-.005*(1-t)+(.003+.006*max(ruff,mantle))*t)
                if t==1:
                    verts.extend((mid-cross*width*.28-flow*(length*.12),mid+cross*width*.17))
                else:
                    verts.extend((mid-cross*width*w,mid+n*(width*.26*w),mid+cross*width*w))
            for j in (0,1,3,4):
                faces.append((k+j,k+j+3,k+j+4,k+j+1))
            faces.extend(((k+6,k+9,k+7),(k+7,k+10,k+8)))
        self.finish(self.surface("Layered guard coat",verts,faces),self.fur,self.body_weights,True)

    def paint_coat(self,obj):
        obj.data.color_attributes.new(name="BearCoat",type="FLOAT_COLOR",domain="POINT")
        obj.data.attributes.new(name="BearGroom",type="FLOAT_VECTOR",domain="CORNER")
        leg_groom=obj.data.attributes.new(name="BearLegGroom",type="FLOAT_VECTOR",domain="CORNER")
        groom=obj.data.attributes["BearGroom"]
        pigment=obj.data.color_attributes["BearCoat"]
        body_uv=[]
        leg_uv=[]
        for vertex in obj.data.vertices:
            point=obj.matrix_world@vertex.co
            x,y,z=point
            shoulder=ramp(z,.96,1.458)*ramp(y,-.41,.48)*(1-ramp(y,.70,1.0))
            cheek=ramp(y,.72,.98)*(1-ramp(z,1.073,1.234))
            muzzle=ramp(y,1.02,1.276)*(1-ramp(z,1.09,1.17))
            back=ramp(z,1.04,1.36)*(.56+.44*(1-ramp(abs(x),.2,.34)))
            legs=(1-ramp(z,.256,.79))*(1-ramp(y,.58,.78))
            eye=max(math.exp(-((point-center).dot(cross)/.062)**2-
                             ((point-center).dot(up)/.044)**2-
                             ((point-center).dot(outward)/.079)**2)
                    for center,outward,up,cross in self.eyes)
            warp=noise(point*9.2)
            axis_z=.96+.19*ramp(y,.47,.78)-.127*ramp(y,.92,1.24)
            angle=math.atan2(x,z-axis_z)
            u=9*angle/math.tau+.041*warp+.055*math.sin(2*angle+y*15)
            v=-2.85*y-2.15*max(0,y-.80)+.12*math.cos(2*angle)+.025*warp
            if obj.name.startswith("Rounded bear ear"):
                u,v=x*7.3+.03*warp,z*5.7
            body_uv.append((u,v,0))
            leg=(1-ramp(z,.47,.82))*(1-ramp(y,.53,.72))
            axis_y=.34 if y>-.04 else -.59
            leg_angle=math.atan2(y-axis_y,x-math.copysign(.25,x))
            leg_uv.append((4*leg_angle/math.tau+.026*warp,z*4.4+.11*math.cos(leg_angle),leg))
            base=(.063,.031,.019)
            warm=(.255,.177,.105)
            deep=(.009,.007,.009)
            h=min(.92,.39*shoulder+.31*cheek+.82*muzzle)
            d=min(.97,.68*back+.82*legs+.95*eye)
            wav=.94+.37*noise(point*6.2)+.21*noise(Vector((x*53,y*14,z*41)))
            color=[((b+(w-b)*h)*(1-d)+k*d)*wav for b,w,k in zip(base,warm,deep)]
            throat=ramp(y,.27,.61)*(1-ramp(y,.79,.99))*(1-ramp(z,.86,1.10))
            color=[c+(s-c)*.66*throat for c,s in zip(color,deep)]
            grizzle=shoulder*ramp(warp+.17*math.sin(y*29+angle*2.8),-.12,.54)
            color=[c+(s-c)*grizzle*.69 for c,s in zip(color,(.239,.193,.148))]
            if y>.74:
                tear=math.exp(-((y-1.025)/.14)**2-((z-(1.177-.60*(y-1.016)))/.034)**2)
                muzzle_band=math.exp(-((y-1.215)/.105)**2)*ramp(z,1.037,1.115)*(1-ramp(abs(x),.034,.127))
                color=[c+(s-c)*min(.90,.80*tear+.69*muzzle_band) for c,s in zip(color,deep)]
                edge=.975-.020*ramp(y,1.025,1.14)+.017*ramp(y,1.15,1.27)
                margin=(1-ramp(z,edge-.008,edge+.024))*ramp(y,.89,1.03)
                color=[c+(s-c)*.84*margin for c,s in zip(color,(.023,.014,.014))]
            if obj.name=="Layered guard coat":
                tip=(vertex.index%11)/10
                color=[c*(.80+.52*tip) for c in color]
            if obj.name.startswith("Rounded bear ear"):
                ring=vertex.index//44
                warmth=ramp(ring,4,6)
                color=[c+(b-c)*warmth for c,b in zip(color,(.057,.031,.025))]
            elif obj.name=="Mobile lower mandible":
                color=[c*.74 for c in color]
            elif obj.name.startswith("Ear fringe"):
                light=.067+.065*(vertex.index%3==2)
                color=(light,light*.77,light*.56)
            elif obj.name=="Healed muzzle scar":
                color=(.16,.095,.068) if vertex.index%3==1 else (.070,.049,.036)
            pigment.data[vertex.index].color=(*color,1)
        # Keep hair direction continuous across the crown. The two charts repeat at
        # integer periods; unwrap individual polygons where they cross an angular seam.
        for poly in obj.data.polygons:
            for chart,attribute,period in ((body_uv,groom,9),(leg_uv,leg_groom,4)):
                values=[chart[i] for i in poly.vertices]
                wrap=max(p[0] for p in values)-min(p[0] for p in values)>period*.5
                for index,(u,v,factor) in zip(poly.loop_indices,values):
                    attribute.data[index].vector=(u+period if wrap and u<0 else u,v,factor)

    def assemble(self):
        self.skin=self.mass()
        self.ears()
        self.eyes_and_muzzle()
        self.feet()
        self.guard_coat()
        for obj in self.coats:
            self.paint_coat(obj)
        select(self.parts)
        bpy.ops.object.join()
        mesh=bpy.context.object
        mesh.name="BrownBear_Surface"
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
        bpy.ops.uv.smart_project(island_margin=.0018,margin_method="FRACTION")
        bpy.ops.object.mode_set(mode="OBJECT")
        mesh.data.uv_layers.active.name="UV0"
        uv=mesh.data.uv_layers.active.data
        for face in mesh.data.polygons:
            if face.center.y>.76 and face.center.z>.82:
                factor=2.4 if mesh.data.materials[face.material_index]==self.iris else 1.50
                for i in face.loop_indices:
                    uv[i].uv*=factor
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.uv.select_all(action="SELECT")
        bpy.ops.uv.pack_islands(rotate=True,scale=True,shape_method="AABB",margin_method="ADD",margin=.0017)
        bpy.ops.object.mode_set(mode="OBJECT")
        triangulate=mesh.modifiers.new("Export triangulation","TRIANGULATE")
        triangulate.keep_custom_normals=True
        skin=mesh.modifiers.new("Quadruped skin","ARMATURE")
        skin.object=self.rig
        assert all(abs(sum(w.weight for w in v.groups)-1)<.00012 for v in mesh.data.vertices)
        return mesh,triangulate

    def poses(self):
        rig=self.rig
        rig.animation_data_create()

        def rotate(name,axis,degrees):
            p=rig.pose.bones[name]
            basis=p.bone.matrix_local.to_quaternion()
            p.rotation_mode="QUATERNION"
            p.rotation_quaternion=basis.inverted()@Quaternion(axis,math.radians(degrees))@basis

        actions={}
        for name,length in (("Idle",54),("Warning",38)):
            action=bpy.data.actions.new("AN_BrownBear_"+name)
            action.use_fake_user=True
            rig.animation_data.action=action
            for i in range(length+1):
                t=i/length
                for p in rig.pose.bones:
                    p.rotation_mode="QUATERNION"
                    p.rotation_quaternion=Quaternion()
                if name=="Idle":
                    rotate("Neck",(1,0,0),1.4*math.sin(t*math.tau))
                    rotate("Head",(0,0,1),2.3*math.sin(t*math.tau))
                else:
                    power=math.sin(math.pi*t)**1.22
                    rotate("Neck",(1,0,0),9*power)
                    rotate("Head",(1,0,0),4*power)
                    rotate("Jaw",(1,0,0),-24*power)
                    rotate("Ear.L",(1,0,0),-9*power)
                    rotate("Ear.R",(1,0,0),-12*power)
                for pose in rig.pose.bones:
                    pose.keyframe_insert(data_path="rotation_quaternion",frame=i+1,group=pose.name)
            actions[name]=action
        rig.animation_data.action=None
        for p in rig.pose.bones:
            p.rotation_quaternion=Quaternion()
        self.scene.frame_set(1)
        return actions

    def review(self,actions,mesh,texture_dir):
        scene=self.scene
        camera=studio_camera(scene)
        if texture_dir:
            preview_baked_coat(mesh,texture_dir,"T_BrownBear")
        scene.render.resolution_x=1160
        scene.render.resolution_y=900
        scene.cycles.samples=32
        key=bpy.data.objects["Soft key"]
        key.data.energy=330
        key.data.color=(1,.81,.63)
        bpy.data.objects["Front fill"].data.energy=95
        rim=bpy.data.objects["Rim"]
        rim.data.color=(.53,.70,1)
        rim.data.energy=430
        for label,position,aim,scale,action,frame in (
            ("",(3.12,4.16,1.58),(0,.055,.80),2.84,actions["Idle"],12),
            ("Profile",(4.8,-.35,1.62),(0,.12,.79),2.83,actions["Idle"],1),
            ("Head",(.68,3.80,1.43),(0,.95,1.157),1.07,actions["Idle"],1),
            ("Warning",(1.66,3.70,1.59),(0,.95,1.173),1.24,actions["Warning"],20),
        ):
            self.rig.animation_data.action=action
            if action:
                self.rig.animation_data.action_slot=action.slots[0]
            scene.frame_set(frame)
            camera.location=position
            camera.rotation_euler=(Vector(aim)-camera.location).to_track_quat("-Z","Y").to_euler()
            camera.data.ortho_scale=scale
            scene.render.filepath=str(SOURCE/("BrownBear"+label+".png"))
            bpy.ops.render.render(write_still=True)


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--bake",type=int,default=2048)
    parser.add_argument("--no-preview",action="store_true")
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    bear=Bear()
    mesh,tri=bear.assemble()
    actions=bear.poses()
    SOURCE.mkdir(parents=True,exist_ok=True)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(STEM+".blend")),compress=True)
    select([mesh])
    bpy.ops.object.modifier_apply(modifier=tri.name)
    texture_dir=GAME/"Textures/Characters/BrownBear"
    if args.bake:
        bake_atlas(mesh,args.bake,texture_dir,"T_BrownBear","UV0")
    export_collection(GAME/"Creatures/Models"/(STEM+".fbx"),bake_animation=False)
    print(f"{STEM}: {len(mesh.data.polygons)} triangles and {len(bear.rig.data.bones)} bones",flush=True)
    if not args.no_preview:
        bear.review(actions,mesh,texture_dir if args.bake else None)


if __name__=="__main__":
    main()
