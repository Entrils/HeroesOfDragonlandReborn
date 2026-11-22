"""Build the neutral timber wolf, its quadruped rig and Blender motion studies."""

import argparse
import math
from pathlib import Path
from random import Random
import sys

import bpy
from mathutils import Quaternion, Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
from character_art import bake_atlas, material, studio_camera
from creature_mesh import CreatureMesh, ramp, select
from export_fbx import export_collection

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT/"ArtSource/Creatures/SK_TimberWolf"
GAME_ART = ROOT/"Assets/Art"
STEM = "SK_TimberWolf"
UP = Vector((0,0,1))


class Wolf(CreatureMesh):
    def __init__(self):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        self.scene=bpy.context.scene
        self.scene.unit_settings.system="METRIC"
        self.scene.render.fps=30
        self.collection=bpy.data.collections.new("EXPORT")
        self.scene.collection.children.link(self.collection)
        self.random=Random(571)
        self.parts=[]
        self.legs={}
        self.coats=set()
        self.create_materials()
        self.rig=self.create_rig()

    def create_materials(self):
        self.coat=material("Wolf_AgoutiCoat",(.153,.129,.102),0,.71,205,.00058,"cloth")
        self.guard=material("Wolf_GuardHairs",(.132,.105,.080),0,.68,175,.00031,"leather")
        for mat in (self.coat,self.guard):
            nodes,links=mat.node_tree.nodes,mat.node_tree.links
            shader=nodes.get("Principled BSDF")
            pigment=nodes.new("ShaderNodeVertexColor")
            pigment.layer_name="CoatPigment"
            mult=nodes.new("ShaderNodeMixRGB")
            mult.blend_type="MULTIPLY"
            mult.inputs[0].default_value=.61
            links.new(pigment.outputs["Color"],mult.inputs[1])
            noise_ramp=shader.inputs["Base Color"].links[0].from_node
            for stop,lightness in zip(noise_ramp.color_ramp.elements,(.72,.98,1.13)):
                stop.color=(lightness,lightness,lightness,1)
            links.new(noise_ramp.outputs["Color"],mult.inputs[2])
            links.new(mult.outputs[0],shader.inputs["Base Color"])
            grain=nodes.new("ShaderNodeVectorMath")
            grain.operation="MULTIPLY"
            grain.inputs[1].default_value=(2.8,.51,2.1)
            links.new(nodes.get("Texture Coordinate").outputs["Object"],grain.inputs[0])
            fibres=nodes.new("ShaderNodeTexNoise")
            fibres.inputs["Scale"].default_value=192
            fibres.inputs["Detail"].default_value=2
            links.new(grain.outputs["Vector"],fibres.inputs["Vector"])
            links.new(fibres.outputs["Fac"],nodes.get("Bump").inputs["Height"])
            shader.inputs["Sheen Weight"].default_value=.052
            shader.inputs["Sheen Roughness"].default_value=.78
            shader.inputs["Specular IOR Level"].default_value=.22
        self.nose=material("Wolf_Nose",(.019,.016,.014),0,.34,555,.00036,"leather")
        self.lip=material("Wolf_Lip",(.025,.016,.013),0,.55,263,.00019,"leather")
        self.mouth=material("Wolf_Mouth",(.043,.016,.019),0,.42,137,.00021,"leather")
        self.keratin=material("Wolf_Claws",(.077,.065,.053),0,.48,205,.00018,"leather")
        self.ivory=material("Wolf_Teeth",(.59,.47,.33),0,.31,210,.000085,"leather")
        self.iris=material("Wolf_AmberIris",(.40,.189,.036),0,.27,0,0,"")
        self.ocular=material("Wolf_OcularDark",(.014,.012,.009),0,.18,0,0,"")
        shader=self.iris.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Coat Weight"].default_value=.29

    def create_rig(self):
        data=bpy.data.armatures.new("Timber wolf quadruped")
        rig=bpy.data.objects.new(STEM,data)
        self.collection.objects.link(rig)
        select([rig])
        bpy.ops.object.mode_set(mode="EDIT")

        def bone(name,start,end,parent=None):
            b=data.edit_bones.new(name)
            b.head,b.tail=start,end
            if parent:
                b.parent=data.edit_bones[parent]
            return b

        bone("Root",(0,0,0),(0,.12,0))
        bone("Pelvis",(0,-.53,.76),(0,-.29,.78),"Root")
        bone("Spine",(0,-.29,.78),(0,.11,.82),"Pelvis")
        bone("Chest",(0,.11,.82),(0,.38,.86),"Spine")
        bone("Neck",(0,.38,.86),(0,.63,.96),"Chest")
        bone("Head",(0,.63,.96),(0,.98,.92),"Neck")
        bone("Jaw",(0,.705,.889),(0,1.025,.862),"Head")
        self.tail=[(0,-.637,.762),(0,-.825,.667),(.012,-1.022,.514),
                   (.057,-1.246,.371),(.103,-1.432,.327),(.113,-1.524,.363)]
        for i in range(len(self.tail)-1):
            bone(f"Tail.{i+1}",self.tail[i],self.tail[i+1],f"Tail.{i}" if i else "Pelvis")

        for side,sign in (("L",-1),("R",1)):
            for limb,landmarks,parent in (
                ("Fore",[(.149,.255,.780),(.132,.257,.466),(.113,.288,.143),
                         (.111,.359,.050),(.111,.444,.044)],"Chest"),
                ("Hind",[(.143,-.488,.739),(.154,-.349,.498),(.127,-.650,.207),
                         (.120,-.580,.053),(.120,-.488,.047)],"Pelvis"),
            ):
                points=[Vector((sign*x,y,z)) for x,y,z in landmarks]
                chain=[]
                for i,suffix in enumerate(("Upper","Lower","Pastern","Toe")):
                    name=f"{limb}{suffix}.{side}"
                    bone(name,points[i],points[i+1],chain[-1] if chain else parent)
                    chain.append(name)
                self.legs[limb,side]=points,chain
            bone("Ear."+side,(sign*.103,.673,1.097),(sign*.157,.643,1.334),"Head")

        bpy.ops.object.mode_set(mode="OBJECT")
        rig.show_in_front=True
        return rig

    def build_body(self):
        anatomy=[
            self.loft("Torso",[
                (-.726,0,.73,.032,.065),(-.685,0,.729,.084,.142),
                (-.615,0,.717,.150,.186),(-.49,0,.713,.185,.207),
                (-.32,0,.705,.168,.177),(-.15,0,.734,.147,.153),
                (.01,0,.705,.182,.194),(.19,0,.715,.205,.231),
                (.34,0,.721,.186,.228),(.47,0,.761,.146,.194),(.55,0,.806,.083,.122),
            ]),
            self.loft("Neck and head",[
                (.28,0,.785,.173,.205),(.40,0,.835,.169,.206),(.54,0,.921,.137,.171),
                (.637,0,.973,.125,.157),(.73,0,1.009,.139,.129),
                (.81,0,1.006,.126,.108),(.891,0,.966,.103,.080),
                (.967,0,.924,.080,.053),(1.051,0,.906,.055,.044),(1.094,0,.903,.041,.035),
            ]),
        ]
        for (limb,side),(points,_) in self.legs.items():
            a,b,c,d,e=points
            front=limb=="Fore"
            anatomy.extend([
                self.limb_mass(limb+" proximal",a,b,.087 if front else .117,.099 if front else .132,.101 if front else .105),
                self.limb_mass(limb+" calf",b,c,.047 if front else .059,.051 if front else .064,.057),
                self.limb_mass(limb+" pastern",c,d,.032,.038,.037),
                self.ellipsoid(limb+" pad",(d.x,d.y+.036,.059),(.055,.079,.052)),
            ])
            for i in range(4):
                x=d.x+(i-1.5)*.027
                y=d.y+.073+.015*(1-abs(i-1.5)/1.5)
                anatomy.append(self.ellipsoid("Digit",(x,y,.047),(.020,.046,.037),n=14,m=9))
        body=self.union(anatomy,"Continuous wolf surface",.017,4)
        self.skin=body
        return self.finish(body,self.coat,self.body_weights,True)

    def body_weights(self,point):
        x,y,z=point
        body=self.chain_weights(("Pelvis","Spine","Chest","Neck","Head"),y,
                                ((-.50,-.24),(-.07,.21),(.28,.53),(.565,.76)))
        limb="Fore" if y>-.018 else "Hind"
        side="L" if x<0 else "R"
        points,names=self.legs[limb,side]
        limit=.765 if limb=="Fore" else .79
        side_blend=ramp(abs(x),.038,.111)
        side_blend+=(1-side_blend)*(1-ramp(z,.41,.59))
        contribution=(1-ramp(z,limit-.16,limit))*side_blend
        if z>.40 and (y>.51 or y<-.70):
            contribution*=1-ramp(abs(y-.10 if y>.51 else y+.3),.40,.63)
        if contribution<.0001:
            return body
        # Tall proximal zones follow the shoulder or haunch. The soft spans are centered at the joints.
        hock=points[2].z
        knee=points[1].z
        leg=self.chain_weights(tuple(reversed(names)),z,
                               ((.063,.105),(hock-.033,hock+.069),(knee-.082,knee+.079)))
        return {**{name:weight*(1-contribution) for name,weight in body.items()},
                **{name:weight*contribution for name,weight in leg.items()}}

    def tail_weights(self,point):
        return self.chain_weights(tuple(f"Tail.{i+1}" for i in range(5)),-point.y,
                                  ((.746,.901),(.951,1.112),(1.177,1.318),(1.386,1.473)))

    def build_tail(self):
        radii=(.071,.111,.103,.077,.038,.007)
        rings=[]
        for index,point in enumerate(self.tail):
            before=Vector(self.tail[max(0,index-1)])
            after=Vector(self.tail[min(index+1,len(self.tail)-1)])
            q=(after-before).normalized().to_track_quat("Z","Y")
            rings.append([Vector(point)+q@Vector((radii[index]*math.cos(a),radii[index]*.88*math.sin(a),0))
                          for a in (i*math.tau/24 for i in range(24))])
        verts=[point for ring in rings for point in ring]
        faces=[(r*24+i,r*24+(i+1)%24,(r+1)*24+(i+1)%24,(r+1)*24+i)
               for r in range(len(rings)-1) for i in range(24)]
        faces.append(tuple(range(len(verts)-24,len(verts))))
        tail=self.surface("Tapered brush tail",verts,faces)
        sub=tail.modifiers.new("Tail contour","SUBSURF")
        sub.levels=1
        select([tail])
        bpy.ops.object.modifier_apply(modifier=sub.name)
        return self.finish(tail,self.coat,self.tail_weights,True)

    def build_ears(self):
        for side,sign in (("L",-1),("R",1)):
            # A concave forward ear shell with an independently fitted inner surface.
            widths=[.073,.069,.050,.027,.003]
            heights=[1.061,1.115,1.164,1.218,1.263]
            rows=[]
            for i,(z,w) in enumerate(zip(heights,widths)):
                x=sign*(.094+.011*i)
                y=.649-.008*i
                rows.append([(x+w*math.sin(a),
                              y+(.032-.0033*i)*math.cos(a)-.019*max(0,math.cos(a))**9,
                              z) for a in (j*math.tau/22 for j in range(22))])
            points=[p for row in rows for p in row]
            faces=[(r*22+i,r*22+(i+1)%22,(r+1)*22+(i+1)%22,(r+1)*22+i)
                   for r in range(4) for i in range(22)]
            faces.append(tuple(range(len(points)-22,len(points))))
            shell=self.surface("Fitted concave ear",points,faces,self.coat)
            select([shell])
            sub=shell.modifiers.new("Ear bevel","SUBSURF")
            sub.levels=1
            bpy.ops.object.modifier_apply(modifier=sub.name)
            self.finish(shell,self.coat,"Ear."+side,True)

    def build_face(self):
        jaw=self.loft("Lower jaw",[
            (.646,0,.85,.046,.045),(.735,0,.854,.094,.048),(.849,0,.86,.09,.043),
            (.954,0,.868,.067,.028),(1.035,0,.873,.044,.019),(1.082,0,.874,.026,.013),
        ])
        select([jaw])
        sub=jaw.modifiers.new("Mandible contour","SUBSURF")
        sub.levels=1
        bpy.ops.object.modifier_apply(modifier=sub.name)
        self.finish(jaw,self.coat,"Jaw",True)
        self.finish(self.ellipsoid("Dark palate",(0,.887,.874),(.062,.171,.010)),self.mouth,"Head")
        lining=[]
        jaw_tree=self.bvh(jaw)
        for row in range(23):
            y=.735+row*.014
            width=.061*(1-.57*ramp(y,.88,1.067))
            for col in range(13):
                x=width*(col/6-1)
                point,_,_,_=jaw_tree.ray_cast(Vector((x,y,1.02)), -UP,.24)
                if point is None:
                    raise ValueError("Lower mouth lining missed the jaw")
                lining.append(point+UP*.0012)
        faces=[(i,i+1,i+14,i+13) for i in range(22*13) if i%13<12]
        self.finish(self.surface("Fitted inner mouth",lining,faces),self.mouth,"Jaw")
        self.finish(self.ellipsoid("Nose leather",(0,1.085,.912),(.046,.034,.030),
                                  (0,1,.18),n=26,m=16),self.nose,"Head")
        for sign in (-1,1):
            self.finish(self.ellipsoid("Nostril",(sign*.023,1.111,.921),(.011,.006,.0058),
                                      (sign*.32,1,.10)),self.ocular,"Head")
            lip=[(sign*x,y,z) for x,y,z in ((.105,.756,.886),(.102,.825,.877),
                  (.088,.898,.873),(.071,.97,.868),(.05,1.048,.879),(.034,1.088,.889))]
            self.tube("Mouth line",[lip],.006,self.lip,"Head",sides=7,taper=.21)
            self.tube("Upper canine",[[(sign*.044,.986,.89),(sign*.044,.992,.869),
                (sign*.037,.982,.848)]],.0081,self.ivory,"Head",sides=9,taper=.91)
            self.tube("Lower canine",[[(sign*.044,.954,.866),(sign*.045,.960,.886),
                (sign*.047,.966,.905)]],.0064,self.ivory,"Jaw",sides=8,taper=.91)
            for index in range(3):
                y=.911-index*.046
                x=sign*(.057+index*.009)
                self.tube("Premolar",[[(x,y,.884),(x,y+.007,.870),(x,y+.006,.860)]],
                          .0062,self.ivory,"Head",sides=7,taper=.86)
        surface=self.bvh(self.skin)
        for sign in (-1,1):
            outward=Vector((sign*.66,.75,.08)).normalized()
            aim=Vector((sign*.083,.825,1.025))
            point,normal,_,_=surface.ray_cast(aim+outward*.19,-outward,.21)
            if point is None:
                raise ValueError("Eye location missed the head surface")
            outward=(normal*.60+outward*.40).normalized()
            up=(UP-outward*UP.dot(outward)).normalized()
            across=up.cross(outward).normalized()
            center=point-outward*.0022
            # The rim defines the exposed lens. The back of the globe sits inside the skull.
            lid=[]
            for i in range(37):
                angle=i*math.tau/36
                z=.0125*math.sin(angle)*(.85 if math.sin(angle)>0 else .61)
                lid.append(center+across*(.0195*math.cos(angle))+up*z+outward*.004)
            self.tube("Dark eyelid",[lid],.00215,self.lip,"Head",sides=7,taper=0)
            self.finish(self.ellipsoid("Orbital globe",center,(.018,.0105,.0075),outward),self.ocular,"Head")
            iris_center=center+outward*.0064+across*.0015*sign
            self.finish(self.ellipsoid("Amber iris",iris_center,(.0084,.0084,.0029),outward),
                        self.iris,"Head")
            self.finish(self.ellipsoid("Round pupil",iris_center+outward*.0026,(.0041,.0041,.0012),outward),
                        self.ocular,"Head")

    def claws(self):
        for (_,side),(points,names) in self.legs.items():
            d=points[3]
            for i in range(4):
                dx=(i-1.5)*.027
                y=d.y+.104+.014*(1-abs(i-1.5)/1.5)
                x=d.x+dx
                self.tube("Dark claw",[[(x,y,.050),(x,y+.017,.041),(x,y+.024,.023)]],
                          .007,self.keratin,names[-1],sides=6,taper=.93)

    def tuft_mesh(self,surface,weights,patches,seed):
        """Lay tapered overlapping tufts in the local contour direction."""
        tree=self.bvh(surface)
        random=Random(seed)
        verts,faces=[],[]
        for axis,zrange,yrange,count,length,width in patches:
            for _ in range(count):
                a=random.uniform(*axis)
                z=random.uniform(*zrange)
                y=random.uniform(*yrange)
                direction=Vector((math.sin(a),0,math.cos(a)))
                origin=Vector((0,y,z))+direction*.57
                root,normal,_,_=tree.ray_cast(origin,-direction,.75)
                if root is None or normal.dot(direction)<.19:
                    continue
                flow=Vector((.09*math.sin(a),-.67,-.57 if root.z<.92 else -.19))
                direction=flow-normal*flow.dot(normal)
                if direction.length<.1:
                    continue
                direction.normalize()
                cross=normal.cross(direction).normalized()
                span=length*random.uniform(.54,1.19)
                w=width*random.uniform(.54,1.15)
                start=len(verts)
                root-=normal*.0020
                mid=root+direction*(span*.40)+normal*.004
                aimed=root+direction*span
                hit,tip_normal,_,_=tree.find_nearest(aimed,.024)
                pointed=(hit+tip_normal*.006 if hit is not None and tip_normal.dot(normal)>.14
                          else aimed+normal*.006)
                verts.extend((root-cross*w*.32,root+cross*w*.32,mid-cross*w*.54,
                              mid+normal*w*.54,mid+cross*w*.54,pointed))
                faces.extend(tuple(start+i for i in f)
                             for f in ((0,2,3),(0,3,1),(1,3,4),(2,5,3),(3,5,4),(2,0,1),(2,1,4),(2,4,5)))
        if verts:
            self.finish(self.surface("Overlapping guard hair tufts",verts,faces),self.guard,weights,True)

    def pigment(self,point,variation):
        x,y,z=point
        dorsal=ramp(z,.648,.93)
        flank=1-ramp(abs(x),.125,.239)
        saddle=dorsal*(.67+.32*flank)*(1-ramp(y,.66,.93))
        under=(1-ramp(z,.55,.75))*ramp(y,-.5,.32)
        cheek=ramp(y,.49,.68)*(1-ramp(z,.91,1.044))*(1-ramp(y,.84,1.03))
        brow=math.exp(-((abs(x)-.099)/.041)**2-((y-.796)/.116)**2-((z-1.060)/.029)**2)
        mask=ramp(y,.85,1.09)*(.65+.28*ramp(z,.87,.969))
        paw=(1-ramp(z,.075,.198))*(1-ramp(y,.50,.79))
        tail=ramp(-y,.90,1.47)
        sable=(.102,.081,.058)
        light=(.355,.282,.188)
        dark=(.013,.015,.017)
        value=[a+(b-a)*min(1,.63*under+.95*cheek+.45*brow) for a,b in zip(sable,light)]
        orbit=math.exp(-((abs(x)-.105)/.031)**2-((y-.847)/.079)**2-((z-1.02)/.032)**2)
        dim=min(.96,.93*saddle+.91*mask+.61*paw+.62*tail+.49*orbit)
        return (*[max(.012,(a+(b-a)*dim)*variation) for a,b in zip(value,dark)],1)

    def finish_mesh(self):
        for obj in self.coats:
            coat=obj.data.color_attributes.new(name="CoatPigment",type="FLOAT_COLOR",domain="POINT")
            for vertex in obj.data.vertices:
                point=obj.matrix_world@vertex.co
                variation=.91+.075*math.sin(point.y*49+point.z*57)+.035*math.sin(point.x*79+point.y*97)
                if obj.data.materials[0]==self.guard:
                    variation*=.84+.24*math.sin(vertex.index//6*1.94)
                color=self.pigment(point,variation)
                if obj.name.startswith("Fitted concave ear"):
                    x,y,z=point
                    height=ramp(z,1.073,1.26)
                    center=.646-.029*height
                    tint=ramp(y-center,.0005,.015)*ramp(z,1.09,1.151)
                    tint*=1-ramp(z,1.226,1.264)
                    color=tuple(c*(1-tint)+i*tint for c,i in zip(color,(.158,.102,.085,1)))
                coat.data[vertex.index].color=color
        select(self.parts)
        bpy.ops.object.join()
        obj=bpy.context.object
        obj.name="TimberWolf_Surface"
        for old in list(obj.users_collection):
            if old!=self.collection:
                old.objects.unlink(obj)
        if self.collection not in obj.users_collection[:]:
            self.collection.objects.link(obj)
        self.scene.cursor.location=(0,0,0)
        bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
        obj.parent=self.rig
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.mesh.normals_make_consistent(inside=False)
        bpy.ops.uv.smart_project(island_margin=.0018,margin_method="FRACTION")
        bpy.ops.object.mode_set(mode="OBJECT")
        obj.data.uv_layers.active.name="UV0"
        tri=obj.modifiers.new("Export triangulation","TRIANGULATE")
        tri.keep_custom_normals=True
        skin=obj.modifiers.new("Quadruped skin","ARMATURE")
        skin.object=self.rig
        assert all(abs(sum(g.weight for g in v.groups)-1)<.00012 for v in obj.data.vertices)
        return obj,tri

    def build(self):
        torso=self.build_body()
        tail=self.build_tail()
        self.build_ears()
        self.build_face()
        self.claws()
        self.tuft_mesh(torso,self.body_weights,[
            ((-1.89,1.89),(.64,.89),(-.66,.64),540,.078,.009),
            ((-2.37,2.37),(.71,.95),(.24,.79),365,.096,.011),
            ((-1.83,1.83),(.74,.94),(-.59,.28),316,.062,.007),
            ((-2.34,2.34),(.84,1.023),(.62,.942),138,.029,.005),
        ],940)
        self.tuft_mesh(tail,self.tail_weights,[((-2.8,2.8),(.38,.596),(-1.49,-.71),286,.073,.009)],270)
        return self.finish_mesh()

    def animate(self):
        rig=self.rig
        rig.animation_data_create()
        actions={}

        def turn(name,axis,angle):
            pose=rig.pose.bones[name]
            rest=pose.bone.matrix_local.to_quaternion()
            pose.rotation_mode="QUATERNION"
            pose.rotation_quaternion=rest.inverted()@Quaternion(axis,math.radians(angle))@rest

        for label,length in (("Idle",48),("Trot",24),("Threat",36)):
            action=bpy.data.actions.new("AN_TimberWolf_"+label)
            action.use_fake_user=True
            rig.animation_data.action=action
            for frame in range(1,length+2):
                t=(frame-1)/length
                phase=math.tau*t
                for pose in rig.pose.bones:
                    pose.rotation_mode="QUATERNION"
                    pose.rotation_quaternion=Quaternion()
                    pose.location=(0,0,0)
                if label=="Trot":
                    bounce=.032*abs(math.sin(phase))
                    root=rig.pose.bones["Root"]
                    root.location=root.bone.matrix_local.to_quaternion().inverted()@Vector((0,0,bounce))
                    turn("Chest",(1,0,0),3*math.sin(2*phase))
                    turn("Head",(1,0,0),-5*math.sin(2*phase))
                    for (limb,side),(_,names) in self.legs.items():
                        step=math.cos(phase+(0 if (limb=="Fore")== (side=="L") else math.pi))
                        swing=max(0,math.sin(phase+(0 if (limb=="Fore")== (side=="L") else math.pi)))
                        turn(names[0],(1,0,0),step*(28 if limb=="Fore" else 25))
                        turn(names[1],(1,0,0),-swing*(36 if limb=="Fore" else 31))
                        turn(names[2],(1,0,0),swing*26)
                elif label=="Threat":
                    lift=math.sin(math.pi*t)**1.4
                    turn("Neck",(1,0,0),-11*lift)
                    turn("Head",(1,0,0),3*lift)
                    turn("Jaw",(1,0,0),-27*lift)
                    turn("Ear.L",(0,1,0),-12*lift)
                    turn("Ear.R",(0,1,0),12*lift)
                else:
                    turn("Neck",(0,0,1),3*math.sin(phase))
                    turn("Head",(1,0,0),2*math.sin(phase))
                for i in range(5):
                    turn(f"Tail.{i+1}",(0,0,1),(2.2+i*.95)*math.sin(phase-i*.45))
                for pose in rig.pose.bones:
                    pose.keyframe_insert("rotation_quaternion",frame=frame,group=pose.name)
                    pose.keyframe_insert("location",frame=frame,group=pose.name)
            actions[label]=action
        rig.animation_data.action=None
        for pose in rig.pose.bones:
            pose.rotation_quaternion=Quaternion()
            pose.location=(0,0,0)
        self.scene.frame_set(1)
        return actions

    def review(self,actions):
        scene=self.scene
        camera=studio_camera(scene)
        scene.render.resolution_x=1120
        scene.render.resolution_y=850
        scene.cycles.samples=30
        scene.cycles.adaptive_threshold=.04
        scene.render.image_settings.file_format="PNG"
        for suffix,position,target,scale,action,frame in (
            ("",(3.3,4.8,1.78),(0,-.078,.68),2.76,None,1),
            ("Profile",(4.9,-.31,1.68),(0,-.20,.66),3.02,None,1),
            ("Face",(1.19,3.4,1.43),(0,.84,.992),1.12,actions["Threat"],18),
            ("Trot",(3.4,2.85,2.46),(0,-.12,.66),2.98,actions["Trot"],7),
        ):
            self.rig.animation_data.action=action
            if action:
                self.rig.animation_data.action_slot=action.slots[0]
            scene.frame_set(frame)
            camera.location=position
            camera.rotation_euler=(Vector(target)-camera.location).to_track_quat("-Z","Y").to_euler()
            camera.data.ortho_scale=scale
            scene.render.filepath=str(SOURCE/("TimberWolf"+suffix+".png"))
            bpy.ops.render.render(write_still=True)


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--bake",type=int,default=2048)
    parser.add_argument("--no-preview",action="store_true")
    arguments=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    args=parser.parse_args(arguments)
    wolf=Wolf()
    mesh,tri=wolf.build()
    actions=wolf.animate()
    SOURCE.mkdir(parents=True,exist_ok=True)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(STEM+".blend")))
    select([mesh])
    bpy.ops.object.modifier_apply(modifier=tri.name)
    if args.bake:
        bake_atlas(mesh,args.bake,GAME_ART/"Textures/Characters/TimberWolf","T_TimberWolf","UV0")
    export_collection(GAME_ART/"Creatures/Models"/(STEM+".fbx"),bake_animation=False)
    print(f"{STEM}: {len(mesh.data.polygons)} triangles and {len(wolf.rig.data.bones)} bones",flush=True)
    if not args.no_preview:
        wolf.review(actions)


if __name__=="__main__":
    main()
