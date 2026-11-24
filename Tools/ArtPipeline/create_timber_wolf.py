"""Build the neutral timber wolf, its quadruped rig and Blender motion studies."""

import argparse
import math
from pathlib import Path
from random import Random
import sys

import bmesh
import bpy
from mathutils import Quaternion, Vector, noise

sys.path.insert(0,str(Path(__file__).resolve().parent))
from character_art import bake_atlas, guard_hair_tile, material, preview_baked_coat, studio_camera
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
        strands=guard_hair_tile("Timber wolf short facial coat",4136)
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
            grain=nodes.new("ShaderNodeAttribute")
            grain.attribute_name="GroomCoordinates"
            fibres=nodes.new("ShaderNodeTexNoise")
            fibres.inputs["Scale"].default_value=1
            fibres.inputs["Detail"].default_value=2.3
            fibres.inputs["Distortion"].default_value=.22
            links.new(grain.outputs["Vector"],fibres.inputs["Vector"])
            bands=nodes.new("ShaderNodeValToRGB")
            bands.label="Dark underfur and silver guard hairs"
            bands.color_ramp.elements[0].position=.30
            bands.color_ramp.elements[0].color=(.22,.20,.20,1)
            bands.color_ramp.elements[1].position=.70
            bands.color_ramp.elements[1].color=(1,1,1,1)
            links.new(fibres.outputs["Fac"],bands.inputs["Fac"])
            strand_color=nodes.new("ShaderNodeMixRGB")
            strand_color.blend_type="MULTIPLY"
            strand_color.inputs[0].default_value=.90
            links.new(mult.outputs[0],strand_color.inputs[1])
            links.new(bands.outputs["Color"],strand_color.inputs[2])
            coords=nodes.new("ShaderNodeAttribute")
            coords.attribute_name="FacialGroom"
            split=nodes.new("ShaderNodeSeparateXYZ")
            links.new(coords.outputs["Vector"],split.inputs[0])
            hairs=nodes.new("ShaderNodeTexImage")
            hairs.image=strands
            links.new(coords.outputs["Vector"],hairs.inputs["Vector"])
            ticking=nodes.new("ShaderNodeMixRGB")
            ticking.blend_type="OVERLAY"
            links.new(split.outputs["Z"],ticking.inputs[0])
            links.new(strand_color.outputs[0],ticking.inputs[1])
            links.new(hairs.outputs["Color"],ticking.inputs[2])
            links.new(ticking.outputs[0],shader.inputs["Base Color"])
            relief=nodes.new("ShaderNodeMixRGB")
            links.new(split.outputs["Z"],relief.inputs[0])
            links.new(fibres.outputs["Fac"],relief.inputs[1])
            links.new(hairs.outputs["Color"],relief.inputs[2])
            links.new(relief.outputs[0],nodes.get("Bump").inputs["Height"])
            nodes.get("Bump").inputs["Distance"].default_value=.0018
            shader.inputs["Sheen Weight"].default_value=.039
            shader.inputs["Sheen Roughness"].default_value=.78
            shader.inputs["Specular IOR Level"].default_value=.22
        self.nose=material("Wolf_Nose",(.015,.014,.013),0,.42,555,.00036,"leather")
        nodes,links=self.nose.node_tree.nodes,self.nose.node_tree.links
        pores=nodes.new("ShaderNodeTexVoronoi")
        pores.feature="DISTANCE_TO_EDGE"
        pores.inputs["Scale"].default_value=390
        links.new(nodes.get("Texture Coordinate").outputs["Object"],pores.inputs["Vector"])
        links.new(pores.outputs["Distance"],nodes.get("Bump").inputs["Height"])
        nodes.get("Bump").inputs["Distance"].default_value=.0007
        self.lip=material("Wolf_Lip",(.025,.016,.013),0,.55,263,.00019,"leather")
        self.mouth=material("Wolf_Mouth",(.067,.024,.028),0,.48,255,.00035,"leather")
        oral_nodes=self.mouth.node_tree.nodes
        tissue=oral_nodes.new("ShaderNodeVertexColor")
        tissue.layer_name="OralTint"
        self.mouth.node_tree.links.new(tissue.outputs["Color"],oral_nodes.get("Principled BSDF").inputs["Base Color"])
        self.keratin=material("Wolf_Claws",(.077,.065,.053),0,.48,205,.00018,"leather")
        self.ivory=material("Wolf_Teeth",(.52,.416,.274),0,.34,210,.000085,"leather")
        self.iris=material("Wolf_AmberIris",(.64,.247,.028),0,.24,0,0,"")
        self.ocular=material("Wolf_OcularDark",(.014,.012,.009),0,.18,0,0,"")
        shader=self.iris.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Coat Weight"].default_value=.29
        iris_tint=self.iris.node_tree.nodes.new("ShaderNodeVertexColor")
        iris_tint.layer_name="IrisPigment"
        self.iris.node_tree.links.new(iris_tint.outputs["Color"],shader.inputs["Base Color"])


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
                (-.32,0,.722,.158,.164),(-.15,0,.744,.143,.151),
                (.01,0,.710,.185,.202),(.19,0,.722,.204,.244),
                (.34,0,.740,.194,.261),(.47,0,.777,.160,.225),(.55,0,.806,.089,.136),
            ]),
            self.loft("Neck and head",[
                (.28,0,.801,.170,.221),(.40,0,.858,.181,.223),(.54,0,.929,.163,.190),
                (.637,0,.978,.158,.161),(.721,0,.992,.159,.143),
                (.788,0,.994,.146,.121),(.841,0,.984,.121,.097),(.878,0,.969,.092,.077),
                (.923,0,.945,.082,.065),(.978,0,.937,.077,.056),
                (1.041,0,.920,.065,.049),(1.089,0,.911,.045,.035),
            ]),
        ]
        # A shallow scapular ridge belongs to the ribcage surface and blends into the withers.
        for v in anatomy[0].data.vertices:
            x,y,z=v.co
            crest=.915-.72*(y-.14)
            relief=.014*math.exp(-((z-crest)/.074)**2-((y-.235)/.126)**2)*ramp(abs(x),.064,.123)
            v.co.x+=math.copysign(relief,x)
        for sign in (-1,1):
            anatomy.extend([
                self.limb_mass("Zygomatic arch",(sign*.137,.699,1.015),(sign*.110,.841,.981),.049,.052,.037),
                self.limb_mass("Masseter",(sign*.118,.715,.982),(sign*.092,.783,.911),.042,.053,.034),
                self.limb_mass("Supraorbital ridge",(sign*.116,.783,1.060),(sign*.081,.873,1.034),.033,.029,.024),
                self.limb_mass("Whisker pad",(sign*.060,.934,.939),(sign*.048,1.055,.910),.039,.035,.020),
            ])
        for (limb,side),(points,_) in self.legs.items():
            d=points[3]
            anatomy.extend([
                self.leg_contour(limb,side),
                self.ellipsoid(limb+" pad",(d.x,d.y+.027,.055),(.051,.063,.045)),
            ])
            for i in range(4):
                x=d.x+(i-1.5)*.027
                y=d.y+.073+.015*(1-abs(i-1.5)/1.5)
                anatomy.append(self.ellipsoid("Digit",(x,y,.041),(.019,.043,.034),n=14,m=9))
        body=self.union(anatomy,"Continuous wolf surface",.013,5)
        # Hollow the orbit below the brow and give the upper lip a shallow unilateral curl.
        for vertex in body.data.vertices:
            x,y,z=vertex.co
            if .76<y<1.08:
                orbit=math.exp(-((y-.845)/.037)**2-((z-1.025)/.020)**2)*ramp(abs(x),.067,.113)
                vertex.co.x-=math.copysign(.012*orbit,x)
                lip=math.exp(-((y-.974)/.051)**2)*ramp(abs(x),.020,.066)*(1-ramp(z,.925,.968))
                vertex.co.z+=(.018 if x>0 else .013)*lip
        simplify=body.modifiers.new("Preserve contours on the unified skin","DECIMATE")
        simplify.ratio=.59
        bpy.ops.object.modifier_apply(modifier=simplify.name)
        self.nose_folds(body)
        self.skin=body
        return self.finish(body,self.coat,self.body_weights,True)

    def leg_contour(self,limb,side):
        # Horizontal sections trace the limb even where the hock doubles back under the stifle.
        if limb=="Fore":
            rings=[
                (.092,.172,.925,.035,.054),(.123,.222,.858,.061,.108),
                (.142,.281,.764,.071,.102),(.144,.269,.667,.070,.088),
                (.140,.238,.551,.055,.074),(.136,.224,.479,.047,.059),
                (.130,.248,.418,.041,.051),(.124,.267,.336,.036,.041),
                (.116,.286,.209,.028,.035),(.114,.286,.151,.031,.038),
                (.111,.308,.109,.028,.035),(.111,.355,.054,.035,.042),
            ]
        else:
            rings=[
                (.078,-.514,.896,.042,.066),(.110,-.512,.846,.076,.121),
                (.131,-.499,.779,.099,.144),(.143,-.473,.699,.107,.143),
                (.152,-.428,.611,.099,.128),(.154,-.377,.536,.078,.101),
                (.151,-.362,.489,.059,.075),(.147,-.401,.451,.056,.082),
                (.141,-.466,.401,.054,.091),(.135,-.550,.329,.043,.074),
                (.129,-.608,.265,.031,.054),(.126,-.656,.219,.032,.044),
                (.124,-.650,.191,.028,.035),(.122,-.623,.136,.024,.033),
                (.120,-.579,.056,.031,.041),
            ]
        sign=-1 if side=="L" else 1
        sides=20
        verts=[(sign*x+width*math.cos(i*math.tau/sides),y+depth*math.sin(i*math.tau/sides),z)
               for x,y,z,width,depth in rings for i in range(sides)]
        faces=[(a+i,a+sides+i,a+sides+(i+1)%sides,a+(i+1)%sides)
               for a in range(0,len(verts)-sides,sides) for i in range(sides)]
        faces.extend((tuple(range(sides)),tuple(reversed(range(len(verts)-sides,len(verts))))))
        leg=self.surface(limb+" muscle and tendon contour",verts,faces)
        select([leg])
        sub=leg.modifiers.new("Round muscle transitions","SUBSURF")
        sub.levels=1
        bpy.ops.object.modifier_apply(modifier=sub.name)
        return leg

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
        # The abdomen lies between the limb chains, even below the shoulder pivots.
        if z>.37:
            center_y=points[1].y+(points[0].y-points[1].y)*ramp(z,points[1].z,points[0].z)
            breadth=.13 if limb=="Fore" else .16
            contribution*=1-ramp(abs(y-center_y),breadth,breadth+.13)
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
        radii=(.078,.112,.123,.103,.054,.010)
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
            widths=[.069,.074,.065,.055,.049,.042,.025,.004]
            heights=[1.047,1.105,1.147,1.173,1.189,1.199,1.234,1.265]
            rows=[]
            for z,w in zip(heights,widths):
                t=(z-heights[0])/(heights[-1]-heights[0])
                x=sign*(.090+.082*t)
                y=.683-(.134 if side=="R" else .124)*t
                row=[]
                for j in range(22):
                    angle=j*math.tau/22
                    lateral=w*math.sin(angle)
                    if side=="R" and abs(z-1.189)<.0001 and lateral>.026:
                        lateral-=.023*ramp(lateral,.026,.047)
                    depth=(.052-.039*t)*math.cos(angle)
                    depth-=.062*max(0,math.cos(angle))**3*math.sin(math.pi*t)
                    row.append((x+lateral,y+depth-sign*lateral*.45,z-.023*t*t))
                rows.append(row)
            points=[p for row in rows for p in row]
            faces=[(r*22+i,r*22+(i+1)%22,(r+1)*22+(i+1)%22,(r+1)*22+i)
                   for r in range(len(rows)-1) for i in range(22)]
            faces.append(tuple(range(len(points)-22,len(points))))
            shell=self.surface("Fitted concave ear",points,faces,self.coat)
            select([shell])
            sub=shell.modifiers.new("Ear bevel","SUBSURF")
            sub.levels=1
            bpy.ops.object.modifier_apply(modifier=sub.name)
            self.finish(shell,self.coat,"Ear."+side,True)
            ear_tree=self.bvh(shell)
            # Pale interior fringe grows from the forward bowl and breaks up its lower rim.
            verts,faces=[],[]
            for _ in range(85):
                height=self.random.uniform(1.106,1.198)
                lateral=self.random.uniform(-.047,.048)
                origin=Vector((sign*(.105+lateral),.79,height))
                root,n,_,_=ear_tree.ray_cast(origin,Vector((0,-1,0)),.24)
                if root is None:
                    continue
                length=self.random.uniform(.008,.020)
                direction=Vector((sign*(-.17-lateral*9),.10,.92))
                if n.y<0:
                    n=-n
                base=root-n*.002
                width=self.random.uniform(.0005,.0012)
                k=len(verts)
                verts.extend((base+Vector((-width,0,0)),base+Vector((width,0,0)),
                              base+direction*length+n*.003))
                faces.append((k,k+2,k+1))
            self.finish(self.surface("Ear fringe",verts,faces),self.guard,"Ear."+side,True)

    def nose_leather(self):
        # Build the nose pad and both recessed nares on one continuous, pebbled surface.
        rings,sections=28,64
        verts=[(0,1.073,.883)]
        for row in range(1,rings):
            lat=math.pi*(row/rings-.5)
            for col in range(sections):
                angle=math.tau*col/sections
                u=math.cos(lat)*math.sin(angle)
                v=math.sin(lat)
                facing=math.cos(lat)*math.cos(angle)
                side=abs(u)
                slit_height=.14+.29*(side-.48)-1.9*max(0,side-.60)**2
                opening=.16-.082*ramp(side,.53,.91)
                naris=math.exp(-((side-.62)/.32)**4-((v-slit_height)/opening)**2)
                center_groove=math.exp(-(u/.084)**2)*max(0,-v)
                verts.append((.058*u*(.98+.22*v),1.073+.041*facing-max(0,facing)*(.019*naris+.006*center_groove),
                              .916+.033*v-.0022*side**3))
        verts.append((0,1.073,.949))
        faces=[]
        for col in range(sections):
            nxt=(col+1)%sections
            faces.append((0,1+nxt,1+col))
            for row in range(rings-2):
                i=1+row*sections
                faces.append((i+col,i+nxt,i+sections+nxt,i+sections+col))
            i=1+(rings-2)*sections
            faces.append((i+col,i+nxt,len(verts)-1))
        pad=self.surface("Rhinarium",verts,faces,self.nose)
        pad.data.materials.append(self.ocular)
        for face in pad.data.polygons:
            x,y,z=face.center
            u=abs(x)/.057
            slit=.9206+.0096*(u-.48)-.060*max(0,u-.60)**2
            if y>1.076 and ((u-.59)/.282)**4+((z-slit)/.0039)**2<1:
                face.material_index=1
        self.finish(pad,self.nose,"Head")

    def cheek_commissure(self,sign,skull,mandible):
        # The buccal lining stays behind the rear teeth as the jaw opens.
        rows,cols=9,25
        verts=[]
        def limits(y):
            return .857+.008*ramp(y,.772,.946),.952-.048*ramp(y,.734,.933)

        def attachments(point):
            low,high=limits(point.y)
            head=ramp((point.z-low)/(high-low),.06,.93)
            return {**{bone:w*head for bone,w in self.body_weights(point).items()},"Jaw":1-head}

        for row in range(rows):
            h=row/(rows-1)
            for col in range(cols):
                t=col/(cols-1)
                front=.811+.049*h-.022*math.sin(math.pi*h)
                y=(.697+.013*h)*(1-t)+front*t
                below,above=limits(y)
                axis=Vector((-sign,0,0))
                a,_,_,_=skull.ray_cast(Vector((sign*.30,y,above)),axis,.32)
                b,_,_,_=mandible.ray_cast(Vector((sign*.30,y,below)),axis,.32)
                if a is None or b is None:
                    raise ValueError("Mouth corner missed an attachment")
                point=b.lerp(a,h)
                point.x-=sign*(.023+.014*math.sin(math.pi*h)+.021*(1-t)**3)
                verts.append(point)
        faces=[(i,i+1,i+cols+1,i+cols) for i in range((rows-1)*cols) if i%cols<cols-1]
        cheek=self.surface("Mobile mouth corner",verts,faces)
        self.finish(cheek,self.mouth,attachments)

    def healed_scar(self,skull):
        # Two tapered marks follow the bony ridge and cheek, leaving the eyelid mobile.
        for y0,z0,y1,z1 in ((.784,1.095,.819,1.058),(.854,1.012,.888,.978)):
            verts=[]
            for i in range(19):
                t=i/18
                y=y0+(y1-y0)*t+.0008*math.sin(11*t)
                z=z0+(z1-z0)*t
                width=.00015+.0019*math.sin(math.pi*t)**.72
                for edge in (-1,0,1):
                    start=Vector((.28,y+width*edge,z+width*.62*edge))
                    hit,n,_,_=skull.ray_cast(start,Vector((-1,0,0)),.29)
                    if hit is None:
                        raise ValueError("Scar missed the brow")
                    verts.append(hit+n*(.0008 if edge==0 else .00024))
            faces=[(i,i+3,i+4,i+1) for i in range(len(verts)-3) if i%3<2]
            self.finish(self.surface("Healed cheek scar",verts,faces),self.coat,"Head",True)

    def nose_folds(self,body):
        # Refine only the nasal skin so the wrinkles share its normals, UVs and bone weights.
        mesh=bmesh.new()
        mesh.from_mesh(body.data)
        edges=[edge for edge in mesh.edges if all(.865<v.co.y<1.053 and v.co.z>.910 for v in edge.verts)]
        bmesh.ops.subdivide_edges(mesh,edges=edges,cuts=1,use_grid_fill=True)
        mesh.normal_update()
        changes=[]
        for v in mesh.verts:
            x,y,z=v.co
            if not .872<y<1.048 or z<.910:
                continue
            center_z=.974-.057*ramp(y,.873,1.045)
            angle=math.atan2(x,z-center_z)
            flank=abs(angle)
            feather=(1-ramp(flank,1.20,1.65))*ramp(y,.872,.893)*(1-ramp(y,1.013,1.048))
            relief=0
            for crest,depth,sweep in ((.915,.006,.032),(.950,.009,.048),(.984,.005,.062)):
                offset=y-crest+sweep*(1-math.cos(angle))+.003*math.sin(angle*2.7)
                relief+=depth*(math.exp(-(offset/.010)**2)-.40*math.exp(-((offset+.014)/.008)**2))
            relief*=feather*(.21+.79*ramp(flank,.10,.92))*(1.07 if x>0 else .86)
            changes.append((v,v.normal*relief))
        for v,offset in changes:
            v.co+=offset
        mesh.to_mesh(body.data)
        mesh.free()
        body.data.update()

    def dentition(self,sign,skull,jaw):
        def half_width(y):
            return .068+.023*(1-ramp(y,.855,.932))-.017*ramp(y,.959,1.051)-.015*ramp(y,1.047,1.081)

        def attachment(x,y,upper):
            direction=UP if upper else -UP
            tree=skull if upper else jaw
            p,_,_,_=tree.ray_cast(Vector((sign*x,y,.785 if upper else 1.032)),direction,.28)
            if p is None:
                raise ValueError(f"Dental arcade missed its attachment at {x}, {y}")
            return p

        gum=[]
        for row in range(44):
            t=row/43
            y=.829+.248*t
            w=half_width(y)
            for f in (.46,.62,.78,.91,.98):
                p=attachment(w*f,y,True)
                scallop=.003+.003*math.exp(-((y-.991)/.016)**2)
                rim=math.sin(math.pi*(f-.46)/.52)*scallop*math.sin(math.pi*t)
                gum.append(p-UP*(.0016+rim))
        faces=[(i,i+1,i+6,i+5) for i in range(len(gum)-5) if i%5<4]
        self.finish(self.surface("Gingival margin",gum,faces),self.mouth,"Head")
        for upper,y,length,radius in ((True,.990,.049,.0100),(False,1.010,.033,.0072)):
            down=-1 if upper else 1
            x=.83*half_width(y) if upper else .037
            root=attachment(x,y,upper)
            verts=[]
            for depth,girth in ((-.10,.85),(.08,1),(.29,.85),(.57,.57),(.80,.34),(1,.045)):
                mid=root+Vector((sign*.002*math.sin(math.pi*depth),-.010*depth**2,down*length*depth))
                for j in range(14):
                    angle=math.tau*j/14
                    verts.append(mid+Vector((radius*girth*.80*math.cos(angle),radius*girth*math.sin(angle),0)))
            faces=[(k+j,k+(j+1)%14,k+14+(j+1)%14,k+14+j) for k in range(0,70,14) for j in range(14)]
            faces.append(tuple(range(70,84)))
            self.finish(self.surface("Curved canine",verts,faces),self.ivory,"Head" if upper else "Jaw")
        for upper in (True,False):
            bone="Head" if upper else "Jaw"
            down=-1 if upper else 1
            for index in range(3):
                x=.0055+index*.010
                y=(1.070 if upper else 1.063)-index*.0035
                root=attachment(x,y,upper)
                self.tube("Incisor",[[root-UP*down*.002,root+UP*down*.004,
                                      root+Vector((0,-.001,down*(.008+index*.001)))]],
                          .0048,self.ivory,bone,sides=8,taper=.55)
            for i,y in enumerate((.950,.911,.864)):
                if not upper:
                    y+=.013
                x=half_width(y)*(.81 if upper else .73)
                depth=.009+.0027*i
                height=.010+.0028*i
                # A mesiodistal cutting edge and two cusps give the cheek teeth their canine profile.
                outline=[(-1,.05),(-.72,.42),(-.45,.32),(-.10,1),(.38,.51),(.67,.56),(1,.04)]
                verts=[]
                for along,h in outline:
                    for width,z in ((-.005,-.002),(-.0034,height*h*.55),(0,height*h+.002),
                                    (.0034,height*h*.55),(.005,-.002)):
                        taper=.13+.87*math.sqrt(max(0,1-along*along))
                        root=attachment(x+width*taper,y+along*depth,upper)
                        verts.append(root+UP*down*z)
                faces=[(j,j+5,j+6,j+1) for j in range(len(verts)-5) if j%5<4]
                self.finish(self.surface("Shearing cheek tooth",verts,faces),self.ivory,bone)

    def build_face(self):
        jaw=self.loft("Lower jaw",[
            (.646,0,.85,.046,.045),(.735,0,.854,.094,.048),(.849,0,.86,.09,.043),
            (.911,0,.865,.077,.035),(.954,0,.868,.067,.028),(1.001,0,.871,.053,.023),
            (1.043,0,.873,.041,.019),(1.069,0,.874,.030,.015),(1.082,0,.874,.017,.010),
        ])
        # Bake the gums, black lip and chin on the same continuous jaw.
        # Material boundaries follow the longitudinal edge loops and flex with the teeth.
        for mat in (self.coat,self.lip,self.mouth):
            jaw.data.materials.append(mat)
        for polygon in jaw.data.polygons:
            if len(polygon.vertices)==4:
                angle=(polygon.index%32+.5)*math.tau/32
                top=math.cos(angle)
                polygon.material_index=2 if top>.34 else (1 if top>.11 else 0)
        select([jaw])
        sub=jaw.modifiers.new("Mandible contour","SUBSURF")
        sub.levels=1
        bpy.ops.object.modifier_apply(modifier=sub.name)
        self.finish(jaw,self.coat,"Jaw",True)
        jaw_tree=self.bvh(jaw)
        surface=self.bvh(self.skin)
        lining=[]
        for row in range(33):
            t=row/32
            y=.757+.320*t
            width=(.066-.038*ramp(y,.875,1.07))*math.sin(math.pi*t)**.33
            for col in range(13):
                x=max(.0003,width)*(col/6-1)
                hit,_,_,_=surface.ray_cast(Vector((x,y,.795)),UP,.25)
                if hit is None:
                    raise ValueError("Palatal lining missed the muzzle")
                ridge=.0015*max(0,math.cos((y+abs(x)*.35)*250))
                lining.append(hit-UP*(.0013+ridge))
        faces=[(i,i+1,i+14,i+13) for i in range(32*13) if i%13<12]
        self.finish(self.surface("Ridged upper palate",lining,faces),self.mouth,"Head")
        self.nose_leather()
        tongue=self.loft("Tongue",[(.755,0,.887,.015,.006),(.785,0,.895,.048,.012),
            (.838,0,.905,.055,.012),(.884,0,.905,.048,.011),(.950,0,.900,.038,.009),
            (.983,0,.895,.031,.008),(1.004,0,.891,.015,.006),(1.009,0,.890,.001,.002)])
        for v in tongue.data.vertices:
            x,y,z=v.co
            if z>.897:
                v.co.z-=.0035*math.exp(-(x/.0085)**2)
        select([tongue])
        sub=tongue.modifiers.new("Rounded tongue margin","SUBSURF")
        sub.levels=1
        bpy.ops.object.modifier_apply(modifier=sub.name)
        self.finish(tongue,self.mouth,"Jaw")
        self.healed_scar(surface)
        for sign in (-1,1):
            self.cheek_commissure(sign,surface,jaw_tree)
            self.dentition(sign,surface,jaw_tree)
            # Follow the lower edge of each muzzle cross-section all the way to the nose.
            lip=[]
            for i in range(27):
                y=.783+i*.0115
                axis=Vector((sign*.47,0,-.88))
                origin=Vector((0,y,1.007-.101*ramp(y,.79,1.09)))
                hit,normal,_,_=surface.ray_cast(origin+axis*.24,-axis,.28)
                if hit is not None:
                    lip.append(hit+normal*.0009)
            self.tube("Pigmented lip margin",[lip],.0041,self.lip,"Head",sides=8,taper=.55)
            # Follicles lie directly on the whisker pads. Their roots follow the animated head.
            for row in range(2):
                for col in range(5-row):
                    y=.948+.018*col+.007*row
                    z=.913+.010*row+.004*math.sin(col*.8)
                    hit,n,_,_=surface.ray_cast(Vector((sign*.19,y,z)),Vector((-sign,0,0)),.18)
                    if hit is None:
                        continue
                    self.finish(self.ellipsoid("Whisker follicle",hit,(.0029,.0021,.0013),n,n=10,m=6),
                                self.lip,"Head")
                    if row==0 and col in (0,2,4):
                        self.tube("Mystacial whisker",[[hit,hit+Vector((sign*.024,-.010,.003)),
                            hit+Vector((sign*(.055-.005*col),-.030,-.001))]],.00075,self.keratin,"Head",sides=4,taper=.90)
        for sign in (-1,1):
            self.build_eye(surface,sign)

    def build_eye(self,skull,sign):
        forward=Vector((sign*.54,.84,.04)).normalized()
        aim=Vector((sign*.098,.848,1.025))
        hit,normal,_,_=skull.ray_cast(aim+forward*.20,-forward,.24)
        if hit is None:
            raise ValueError("Eye missed its socket")
        outward=(forward*.53+normal*.47).normalized()
        up=(UP-outward*UP.dot(outward)).normalized()
        across=up.cross(outward)
        center=hit-outward*.0016

        def lens(x,z):
            bulge=.0064*(1-(x/.027)**2-(z/.022)**2)
            return center+across*x+up*(z-sign*.32*x)+outward*bulge

        # Clip the entire cornea to the lid opening. The upper lid cuts across the
        # iris and casts a shadow, especially at the medial (nose-side) corner.
        spokes=56
        iris_x=sign*.0030
        iris_z=.0010
        coords=[(iris_x,iris_z)]
        lid=[]
        rings=14
        for ring in range(1,rings+1):
            t=ring/rings
            for i in range(spokes):
                a=math.tau*i/spokes
                u=math.cos(a)
                s=math.sin(a)
                x=.024*u
                z=s*(.0078 if s>0 else .0104)*(1-.30*abs(u))
                z-=.0017*sign*u*(1-abs(u))
                coords.append((iris_x+(x-iris_x)*t,iris_z+(z-iris_z)*t))
                if ring==rings:
                    lid.append(lens(x,z))
        faces=[(0,1+i,1+(i+1)%spokes) for i in range(spokes)]
        for ring in range(rings-1):
            k=1+ring*spokes
            faces.extend((k+i,k+spokes+i,k+spokes+(i+1)%spokes,k+(i+1)%spokes) for i in range(spokes))
        eye=self.surface("Recessed amber eye",[lens(x,z) for x,z in coords],faces)
        pigment=eye.data.color_attributes.new(name="IrisPigment",type="FLOAT_COLOR",domain="POINT")
        for i,(x,z) in enumerate(coords):
            dx,dz=x-iris_x,z-iris_z
            r=math.hypot(dx,dz)
            angle=math.atan2(dz,dx)
            fleck=.88+.15*math.sin(19*angle+270*r)+.11*math.cos(31*angle)
            amber=ramp(r,.0048,.0059)*(1-ramp(r,.0098,.0120))
            shadow=1-.49*ramp(z,.002,.009)
            pigment.data[i].color=(.006+.51*amber*fleck*shadow,.005+.192*amber*fleck*shadow,
                                   .003+.028*amber*shadow,1)
        self.finish(eye,self.iris,"Head")
        self.tube("Wet lid margin",[lid+[lid[0]]],.00135,self.lip,"Head",sides=6,taper=0)
        folds=list(lid)
        for ring in range(1,5):
            t=ring/4
            for i,margin in enumerate(lid):
                a=math.tau*i/spokes
                s=math.sin(a)
                hood=max(0,s)
                offset=across*(.016*math.cos(a))+up*((.019 if s>0 else .013)*s)
                sample=margin+offset*t
                skin,_,_,_=skull.ray_cast(sample+outward*.08,-outward,.16)
                skin=skin if skin is not None else sample
                crest=(.0024+.0056*hood)*math.sin(math.pi*t)**1.4
                folds.append(margin.lerp(skin,t)+outward*(crest-.0028*t*t))
        faces=[(k+i,k+spokes+i,k+spokes+(i+1)%spokes,k+(i+1)%spokes)
               for k in range(0,4*spokes,spokes) for i in range(spokes)]
        self.finish(self.surface("Orbital fur folds",folds,faces),self.coat,"Head",True)

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
                if root is None or normal.dot(direction)<.19 or (surface==self.skin and root.z<.535):
                    continue
                # At the cheek and throat the guard coat fans out and down. The brush follows the tail.
                neck=ramp(root.y,.18,.55)
                flow=Vector((.07*math.sin(a),-.85,-.31-.62*neck+.15*random.uniform(-1,1)))
                direction=flow-normal*flow.dot(normal)
                if direction.length<.1:
                    continue
                direction.normalize()
                cross=normal.cross(direction).normalized()
                span=length*random.uniform(.54,1.19)
                w=width*random.uniform(.54,1.15)
                drift=random.uniform(-.65,.65)*w
                start=len(verts)
                lift=span*(.045+.063*neck)
                for t,spread in ((0,.58),(.36,.75),(.67,.58)):
                    center=root+direction*(t*span)+cross*(drift*t*t)
                    center+=normal*(-.0055*(1-t)+lift*t)
                    verts.extend((center-cross*w*spread,center+normal*w*.18*spread,center+cross*w*spread))
                end=root+direction*span+cross*drift+normal*lift
                verts.append(end-cross*w*.48-direction*(span*random.uniform(.04,.16)))
                verts.append(end+cross*w*.27)
                # Winding follows the surface normal (cross = normal x direction).
                for a0 in (0,3):
                    for b in (0,1):
                        i=start+a0+b
                        faces.append((i,i+3,i+4,i+1))
                faces.extend(((start+6,start+9,start+7),(start+7,start+10,start+8)))
        if verts:
            self.finish(self.surface("Overlapping guard hair tufts",verts,faces),self.guard,weights,True)

    def pigment(self,point,variation):
        x,y,z=point
        def blend(base,tint,amount):
            return tuple(a+(b-a)*amount for a,b in zip(base,tint))

        broken=.030*noise.fractal(point*19,1,2,3)
        height=z+broken
        cheek=ramp(y,.49,.68)*(1-ramp(y,.85,1.02))*(1-ramp(height,.97,1.045))
        bib=ramp(y,.29,.53)*(1-ramp(y,.62,.80))*(1-ramp(height,.77,.95))
        belly=ramp(z,.37,.52)*(1-ramp(height,.60,.73))
        sable=(.108,.079,.060)
        ash=(.37,.351,.302)
        soot=(.004,.006,.010)
        coat=blend(sable,ash,min(1,.96*cheek+.97*bib+.63*belly))
        withers=.070*ramp(y,.20,.49)
        saddle=ramp(height,.65+withers,.90+withers)*(1-ramp(y,.57,.81))
        mantle=math.exp(-((height-(.808+withers))/.043)**2)*(1-ramp(y,.48,.68))
        coat=blend(coat,ash,.58*mantle)
        coat=blend(coat,soot,.985*saddle)
        # A dark cap splits above the nasal bridge, leaving pale brows and a charcoal tear line.
        crown=ramp(y,.53,.70)*ramp(height,1.035,1.12)
        bridge=ramp(y,.829,.93)*ramp(height,.915,.970)*(1-ramp(abs(x),.037,.088))
        tear=math.exp(-((y-.860)/.099)**2-((z-(1.022-.44*(y-.85)))/.026)**2)
        brow=math.exp(-((y-.805)/.056)**2-((z-(1.060-.31*(y-.815)))/.012)**2)*ramp(abs(x),.066,.114)
        coat=blend(coat,soot,min(.98,.97*crown+.95*bridge+.98*tear))
        coat=blend(coat,(.235,.208,.165),.72*brow)
        if .69<y<1.05 and z>.88:
            # Two lateral charcoal markings follow the orbit and whisker pads.
            cheekline=math.exp(-((y-.799)/.075)**2-((z-(.959+.26*(y-.80)))/.014)**2)
            noseband=math.exp(-((y-.967)/.065)**2-((z-(.945-.29*(y-.956)))/.021)**2)
            coat=blend(coat,soot,.73*max(cheekline,noseband)*ramp(abs(x),.044,.086))
        muzzle=ramp(y,.902,.983)*(1-ramp(height,.911,.944))
        coat=blend(coat,(.32,.277,.221),muzzle*.90)
        if y<.53:
            coat=blend(coat,soot,.78*(1-ramp(height,.125,.42)))
        if y<-.72:
            tail_dark=max(ramp(-y,1.25,1.47),.66*ramp(z,.49,.67))
            coat=blend(coat,soot,tail_dark*.95)
        return (*[c*variation for c in coat],1)

    def finish_mesh(self):
        for obj in self.parts:
            if self.mouth not in obj.data.materials[:]:
                continue
            tint=obj.data.color_attributes.new(name="OralTint",type="FLOAT_COLOR",domain="POINT")
            tongue=obj.name.startswith("Tongue")
            for v in obj.data.vertices:
                x,y,z=v.co
                grain=1+.10*noise.noise(v.co*240)
                value=(.135,.048,.053) if tongue else (.067,.023,.027)
                light=.65+.35*ramp(y,.78,.97)
                if tongue:
                    light*=1-.28*math.exp(-(x/.006)**2)
                tint.data[v.index].color=(*[c*grain*light for c in value],1)
        for obj in self.coats:
            obj.data.color_attributes.new(name="CoatPigment",type="FLOAT_COLOR",domain="POINT")
            obj.data.attributes.new(name="GroomCoordinates",type="FLOAT_VECTOR",domain="POINT")
            facial=obj.data.attributes.new(name="FacialGroom",type="FLOAT_VECTOR",domain="POINT")
            groom=obj.data.attributes["GroomCoordinates"]
            coat=obj.data.color_attributes["CoatPigment"]
            for vertex in obj.data.vertices:
                point=obj.matrix_world@vertex.co
                x,y,z=point
                warp=noise.noise(point*14)
                # Vary the grain in rest space so it bakes cleanly and travels with the skin.
                slope=.31*y+.24*max(0,y-.2)+.15*abs(x)
                grain=(192*x+.55*warp,15*y,205*(z-slope)+1.3*warp)
                if z<.54 and y>-.72:
                    leg=ramp(.54-z,0,.19)
                    grain=tuple(a+(b-a)*leg for a,b in zip(grain,(156*x,158*y+warp,22*z)))
                if obj.name.startswith("Fitted concave ear"):
                    grain=(172*x+warp,146*y,26*z)
                elif y>.62 and z>.78:
                    # Follow the tapered skull from snout to cheek without a seam at the crown.
                    center_z=.992-.081*ramp(y,.805,1.066)
                    angle=math.atan2(x,z-center_z)
                    radius=27-9*ramp(y,.87,1.09)
                    face_grain=(radius*math.sin(angle)+.80*warp,
                                radius*math.cos(angle)+.78*warp,38*y)
                    amount=ramp(y,.62,.735)
                    grain=tuple(a+(b-a)*amount for a,b in zip(grain,face_grain))
                    # The grain fans from the nose to the cheeks and turns around the eyes.
                    u=abs(angle)*1.27+.033*warp+.12*math.sin(y*17+abs(angle))
                    v=(1.12-y)*5.4+.095*math.cos(2*angle)+.027*warp
                    facial.data[vertex.index].vector=(u,v,amount)
                groom.data[vertex.index].vector=grain
                variation=.98+.17*noise.fractal(point*12,1,2,3)
                if obj.data.materials[0]==self.guard:
                    variation*=.90+.31*(vertex.index%11)/10+.13*math.sin(vertex.index//11*1.94)
                color=self.pigment(point,variation)
                if obj.name.startswith("Lower jaw"):
                    margin=ramp(z,.859,.889)
                    color=tuple(c*(.67-.40*margin) for c in color[:3])+(1,)
                if obj.name.startswith("Healed cheek scar"):
                    value=(.178,.126,.101) if vertex.index%3==1 else (.096,.080,.072)
                    color=(*value,1)
                if obj.name.startswith("Ear fringe"):
                    light=.065+.072*(vertex.index%3==2)
                    color=(light*variation,.86*light*variation,.74*light*variation,1)
                if obj.name.startswith("Fitted concave ear"):
                    x,y,z=point
                    height=ramp(z,1.06,1.242)
                    center_x=.091+.081*height
                    center_y=.682-.129*height
                    within=1-ramp(abs(abs(x)-center_x),.019,.054*(1-.43*height))
                    tint=ramp(y-center_y+(abs(x)-center_x)*.33,-.020,.004)*within*ramp(z,1.10,1.153)
                    tint*=1-ramp(z,1.222,1.262)
                    recess=ramp(y-center_y+(abs(x)-center_x)*.33,-.041,.002)*within
                    color=tuple(c*(1-recess)+i*recess for c,i in zip(color,(.032,.021,.021,1)))
                    color=tuple(c*(1-tint)+i*tint for c,i in zip(color,(.093,.054,.047,1)))
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
        # Reserve more atlas pixels for the brow, lips and eyes at the same 2K budget.
        uv=obj.data.uv_layers.active.data
        for polygon in obj.data.polygons:
            p=polygon.center
            if p.y>.615 and p.z>.78:
                mat=obj.data.materials[polygon.material_index]
                density=2.65 if mat==self.iris else 1.65
                for i in polygon.loop_indices:
                    uv[i].uv*=density
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.uv.select_all(action="SELECT")
        bpy.ops.uv.pack_islands(rotate=True,scale=True,shape_method="AABB",margin_method="ADD",margin=.0017)
        bpy.ops.object.mode_set(mode="OBJECT")
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
            ((-2.24,2.24),(.68,.89),(-.64,.23),220,.069,.011),
            ((-2.83,2.83),(.70,.95),(.22,.63),440,.100,.011),
            ((-1.22,1.22),(.81,.98),(.04,.50),90,.092,.014),
            ((1.08,2.63),(.90,1.06),(.58,.81),155,.076,.008),
            ((-2.63,-1.08),(.90,1.06),(.58,.81),155,.076,.008),
        ],940)
        self.tuft_mesh(tail,self.tail_weights,[((-3,3),(.35,.65),(-1.49,-.71),270,.089,.014)],270)
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

    def review(self,actions,mesh,texture_dir=None):
        scene=self.scene
        camera=studio_camera(scene)
        if texture_dir:
            preview_baked_coat(mesh,texture_dir,"T_TimberWolf")
        key=bpy.data.objects["Soft key"]
        key.location=(1.7,2.0,3.3)
        key.rotation_euler=(Vector((0,.30,.82))-key.location).to_track_quat("-Z","Y").to_euler()
        key.data.energy=285
        key.data.size=2.1
        key.data.color=(1,.81,.62)
        bpy.data.objects["Front fill"].data.energy=80
        rim=bpy.data.objects["Rim"]
        rim.data.color=(.49,.67,1)
        rim.data.energy=380
        bpy.data.objects["Back fill"].data.energy=65
        scene.render.resolution_x=1120
        scene.render.resolution_y=850
        scene.cycles.samples=30
        scene.cycles.adaptive_threshold=.04
        scene.render.image_settings.file_format="PNG"
        for suffix,position,target,scale,action,frame in (
            ("",(3.35,3.48,1.37),(0,-.175,.67),2.59,actions["Idle"],12),
            ("Profile",(4.9,-.31,1.68),(0,-.20,.66),3.02,actions["Idle"],1),
            ("Face",(1.63,3.40,1.41),(0,.77,.978),.98,actions["Threat"],12),
            ("Gaze",(.47,3.62,1.21),(0,.76,1.005),.83,actions["Idle"],1),
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
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(STEM+".blend")),compress=True)
    select([mesh])
    bpy.ops.object.modifier_apply(modifier=tri.name)
    texture_dir=GAME_ART/"Textures/Characters/TimberWolf"
    if args.bake:
        bake_atlas(mesh,args.bake,texture_dir,"T_TimberWolf","UV0")
    export_collection(GAME_ART/"Creatures/Models"/(STEM+".fbx"),bake_animation=False)
    print(f"{STEM}: {len(mesh.data.polygons)} triangles and {len(wolf.rig.data.bones)} bones",flush=True)
    if not args.no_preview:
        wolf.review(actions,mesh,texture_dir if args.bake else None)


if __name__=="__main__":
    main()
