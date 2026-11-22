"""Create the brown bear used by Dragonland's neutral camps."""

import argparse
import math
from pathlib import Path
import sys

import bpy
from mathutils import Quaternion, Vector
from mathutils.noise import noise

sys.path.insert(0,str(Path(__file__).resolve().parent))
from character_art import bake_atlas, material, studio_camera
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
        coords=nodes.get("Texture Coordinate").outputs["Object"]
        pigment=nodes.new("ShaderNodeVertexColor")
        pigment.layer_name="BearCoat"
        stretch=nodes.new("ShaderNodeVectorMath")
        stretch.operation="MULTIPLY"
        stretch.inputs[1].default_value=(3.8,.44,2.8)
        links.new(coords,stretch.inputs[0])
        fibres=nodes.new("ShaderNodeTexNoise")
        fibres.inputs["Scale"].default_value=165
        fibres.inputs["Detail"].default_value=3
        links.new(stretch.outputs["Vector"],fibres.inputs["Vector"])
        links.new(fibres.outputs["Fac"],nodes.get("Bump").inputs["Height"])
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
        links.new(mix.outputs[0],shader.inputs["Base Color"])
        shader.inputs["Sheen Weight"].default_value=.053
        shader.inputs["Specular IOR Level"].default_value=.23
        self.nose=material("Bear_Nose",(.020,.014,.012),0,.42,610,.00072,"leather")
        self.lip=material("Bear_Lips",(.026,.015,.012),0,.56,410,.00031,"leather")
        self.pad=material("Bear_PawPad",(.026,.019,.016),0,.76,610,.00088,"leather")
        self.dark=material("Bear_EyeDark",(.005,.004,.003),0,.39,0,0,"")
        self.iris=material("Bear_HazelIris",(.083,.033,.010),0,.28,260,.00007,"leather")
        self.hole=material("Bear_NostrilInterior",(.006,.004,.003),0,.70,0,0,"")
        for mat in (self.dark,self.iris):
            mat.node_tree.nodes.get("Principled BSDF").inputs["Specular IOR Level"].default_value=.25
        self.claw=material("Bear_Claw",(.047,.033,.021),0,.51,297,.00018,"leather")
        self.gum=material("Bear_Gum",(.085,.034,.029),0,.48,208,.00014,"leather")
        self.tooth=material("Bear_Ivory",(.62,.485,.318),0,.43,334,.00009,"leather")
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
                (-.356,0,.925,.405,.363),(-.09,0,.941,.414,.382),
                (.132,0,.973,.406,.417),(.332,0,1.014,.373,.454),
                (.493,0,1.028,.312,.418),(.602,0,1.053,.246,.318),
                (.701,0,1.094,.199,.231),(.733,0,1.105,.105,.143)
            ],sections=44),
            self.loft("Broad head",[
                (.398,0,1.09,.232,.252),(.567,0,1.115,.240,.235),
                (.717,0,1.147,.270,.226),(.843,0,1.151,.257,.214),
                (.945,0,1.133,.231,.193),(1.033,0,1.091,.186,.158),
                (1.116,0,1.028,.155,.119),(1.208,0,1.014,.132,.091),
                (1.278,0,1.017,.105,.073),
            ],sections=44),
        ]
        for (limb,side),(points,_) in self.legs.items():
            a,b,c,d,e=points
            fore=limb=="Fore"
            components.extend([
                self.limb_mass("Shoulder" if fore else "Haunch",a,b,.164 if fore else .195,
                               .185 if fore else .211,.157),
                self.limb_mass("Forearm" if fore else "Calf",b,c,.113 if fore else .108,
                               .139 if fore else .112,.101),
                self.limb_mass("Wrist and instep",c,d,.085,.096,.057),
                self.ellipsoid("Broad paw",(d.x,d.y+.054,.071),(.116,.153,.067)),
            ])
            for i in range(5):
                x=d.x+(i-2)*.039
                y=d.y+.138+.017*(1-abs(i-2)/2)
                components.append(self.ellipsoid("Paw digit",(x,y,.06),(.032,.064,.047),n=14,m=9))
        body=self.union(components,"Connected brown bear sculpture",.022,4)
        self.sculpt_coat(body)
        return self.finish(body,self.fur,self.body_weights,True)

    def sculpt_coat(self,body):
        self.eyes=[]
        surface=self.bvh(body)
        for side in (-1,1):
            aim=Vector((side*.150,.956,1.192))
            outward=Vector((side*.54,.84,.075)).normalized()
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
            center=Vector((sign*.205,.762,1.339))
            cross=Vector((1,-sign*.18,-sign*.16)).normalized()
            vertical=Vector((sign*.19,.042,1)).normalized()
            forward=vertical.cross(cross).normalized()
            sections=44
            rows=((.17,-.062),(.55,-.052),(.89,-.035),(1.0,-.004),
                  (.95,.029),(.77,.037),(.59,.014),(.38,-.004),(.12,-.009))
            vertices=[]
            for size,depth in rows:
                for i in range(sections):
                    a=i*math.tau/sections
                    h=math.sin(a)
                    vertices.append(center+cross*(.086*math.cos(a)*size*(.94+.1*h))+
                                    vertical*(.095*h*size)+forward*depth)
            faces=[]
            for r in range(len(rows)-1):
                for i in range(sections):
                    j=(i+1)%sections
                    k=r*sections
                    faces.append((k+i,k+j,k+sections+j,k+sections+i))
            faces.extend((tuple(reversed(range(sections))),
                          tuple(range(len(vertices)-sections,len(vertices)))))
            self.finish(self.surface("Rounded bear ear "+side,vertices,faces),self.fur,"Ear."+side,True)

    def eyes_and_muzzle(self):
        for center,outward,up,cross in self.eyes:
            eye=center-outward*.028
            self.finish(self.ellipsoid("Set-in dark eyeball",eye,(.029,.021,.023),outward),self.dark,"Head")
            iris=eye+outward*.0206
            self.finish(self.ellipsoid("Dark amber iris",iris,(.0113,.0113,.0041),outward,n=24,m=12),
                        self.iris,"Head")
            self.finish(self.ellipsoid("Round dark pupil",iris+outward*.00345,(.0071,.0071,.0012),outward),
                        self.dark,"Head")
            upper=[]
            lower=[]
            for i in range(19):
                t=i/18
                horizontal=(t*2-1)*.031
                vertical=math.sin(math.pi*t)
                upper.append(center+cross*horizontal+up*(.003+.012*vertical)-outward*.009)
                lower.append(center+cross*horizontal+up*(.002-.009*vertical)-outward*.010)
            self.tube("Eyelid margins",[upper,lower],.00135,self.hole,"Head",sides=6,taper=.12)

        nose=self.ellipsoid("Broad bear rhinarium",(0,1.267,1.041),(.113,.057,.048),(0,1,.12),n=56,m=28)
        for vertex in nose.data.vertices:
            x,y,z=vertex.co
            recess=math.exp(-((abs(x)-.057)/.025)**2-((z-1.05)/.013)**2)
            vertex.co.y-=.010*recess*ramp(y,1.276,1.3)
        self.finish(nose,self.nose,"Head")
        for sign in (-1,1):
            self.finish(self.ellipsoid("Recessed nostril",(sign*.058,1.289,1.05),(.025,.010,.0095),
                                      (sign*.27,1,.10),n=20,m=11),self.hole,"Head")
        self.tube("Philtrum",[[(0,1.309,1.022),(0,1.301,1.000),(0,1.278,.978)]],
                  .0026,self.lip,"Head",sides=7,taper=.15)

        jaw=self.loft("Mobile lower mandible",[
            (.678,0,.965,.095,.105),(.784,0,.952,.156,.111),(.901,0,.930,.175,.092),
            (1.029,0,.923,.147,.072),(1.146,0,.930,.119,.055),
            (1.240,0,.947,.095,.039),(1.277,0,.96,.070,.024),
        ],sections=38)
        select([jaw])
        sub=jaw.modifiers.new("Jaw contour","SUBSURF")
        sub.levels=1
        bpy.ops.object.modifier_apply(modifier=sub.name)
        self.finish(jaw,self.fur,"Jaw",True)
        self.inner_mouth(jaw)
        jaw_surface=self.bvh(jaw)

        for sign in (-1,1):
            path=[]
            for i in range(22):
                t=i/21
                y=.865+t*.39
                z=.976-.018*math.sin(math.pi*t)
                hit,_,_,_=jaw_surface.ray_cast(Vector((sign*.27,y,z)),Vector((-sign,0,0)),.29)
                if hit is not None:
                    path.append(hit+Vector((sign*.0009,0,0)))
            self.tube("Fitted lower lip",[path],.0022,self.lip,"Jaw",sides=6,taper=.32)
            self.tube("Upper canine",[[(sign*.089,1.147,.988),(sign*.092,1.149,.951),
                     (sign*.083,1.138,.921)]],.015,self.tooth,"Head",sides=9,taper=.92)
            self.tube("Lower canine",[[(sign*.083,1.114,.966),(sign*.089,1.113,.991),
                     (sign*.091,1.113,1.009)]],.010,self.tooth,"Jaw",sides=8,taper=.89)
            for index in range(3):
                x=sign*(.101+.009*index)
                y=1.091-.039*index
                self.tube("Bear premolar",[[(x,y,.982),(x,y+.006,.963),(x,y+.007,.949)]],
                          .010,self.tooth,"Head",sides=7,taper=.85)

    def inner_mouth(self,jaw):
        tree=self.bvh(jaw)
        vertices=[]
        for row in range(24):
            t=row/23
            y=.793+.456*t
            width=.129*(1-.5*ramp(y,.952,1.25))
            for col in range(19):
                x=width*(col/9-1)
                hit,_,_,_=tree.ray_cast(Vector((x,y,1.15)),-UP,.34)
                if hit is None:
                    raise ValueError("Bear jaw lining missed the mandible")
                vertices.append(hit+UP*.0017)
        faces=[(i,i+1,i+20,i+19) for i in range(23*19) if i%19<18]
        self.finish(self.surface("Gums fitted to jaw",vertices,faces),self.gum,"Jaw")
        self.finish(self.ellipsoid("Tongue bed",(0,1.065,.992),(.072,.153,.018),n=24,m=12),
                    self.tongue,"Jaw")

    def feet(self):
        for (_,side),(points,names) in self.legs.items():
            d=points[3]
            self.finish(self.ellipsoid("Plantar pad",(d.x,d.y+.048,.026),(.085,.091,.021)),
                        self.pad,names[-1])
            for i in range(5):
                x=d.x+(i-2)*.039
                y=d.y+.192+.016*(1-abs(i-2)/2)
                self.tube("Short curved paw claw",[[(x,y-.009,.065),(x,y+.012,.050),
                          (x,y+.026,.031),(x,y+.032,.020)]],.009,self.claw,names[-1],
                          sides=7,taper=.9)
                self.finish(self.ellipsoid("Digital pad",(x,y-.045,.022),(.023,.030,.017),n=12,m=7),
                            self.pad,names[-1])
        self.finish(self.ellipsoid("Short natural tail",(0,-1.043,.922),(.069,.080,.063),
                                  (0,-1,-.64)),self.fur,"Tail",True)

    def paint_coat(self,obj):
        pigment=obj.data.color_attributes.new(name="BearCoat",type="FLOAT_COLOR",domain="POINT")
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
            base=(.069,.035,.017)
            warm=(.196,.116,.055)
            deep=(.015,.008,.005)
            h=min(.95,.70*shoulder+.31*cheek+.63*muzzle)
            d=min(.89,.43*back+.74*legs+.59*eye)
            wav=.96+.27*noise(point*6.2)+.15*noise(Vector((x*53,y*14,z*41)))
            color=[((b+(w-b)*h)*(1-d)+k*d)*wav for b,w,k in zip(base,warm,deep)]
            if obj.name.startswith("Rounded bear ear"):
                inner=ramp(y,.764,.798)*ramp(z,1.28,1.342)
                color=[c+(b-c)*inner for c,b in zip(color,(.073,.041,.027))]
            pigment.data[vertex.index].color=(*color,1)

    def assemble(self):
        self.mass()
        self.ears()
        self.eyes_and_muzzle()
        self.feet()
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

    def review(self,actions):
        scene=self.scene
        camera=studio_camera(scene)
        scene.render.resolution_x=1160
        scene.render.resolution_y=900
        scene.cycles.samples=32
        bpy.data.objects["Soft key"].data.energy=406
        bpy.data.objects["Front fill"].data.energy=163
        for label,position,aim,scale,action,frame in (
            ("",(2.85,4.68,1.90),(0,.08,.81),2.89,None,1),
            ("Profile",(4.8,-.35,1.62),(0,.15,.79),2.80,None,1),
            ("Head",(1.42,3.7,1.615),(0,.96,1.175),1.11,None,1),
            ("Warning",(1.75,3.92,1.87),(0,.85,1.137),1.53,actions["Warning"],20),
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
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(STEM+".blend")))
    select([mesh])
    bpy.ops.object.modifier_apply(modifier=tri.name)
    if args.bake:
        bake_atlas(mesh,args.bake,GAME/"Textures/Characters/BrownBear","T_BrownBear","UV0")
    export_collection(GAME/"Creatures/Models"/(STEM+".fbx"),bake_animation=False)
    print(f"{STEM}: {len(mesh.data.polygons)} triangles and {len(bear.rig.data.bones)} bones",flush=True)
    if not args.no_preview:
        bear.review(actions)


if __name__=="__main__":
    main()
