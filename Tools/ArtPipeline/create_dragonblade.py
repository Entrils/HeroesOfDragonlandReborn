"""Author Dragonblade, the legendary sword, with the common sword's grip and tip."""

import argparse
import math
from pathlib import Path
from random import Random
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0,str(Path(__file__).resolve().parent))
from character_art import bake_atlas,material,studio_camera
from create_iron_sword import IronSword,TIP,view
from create_neutral_projectiles import bake_emission,emissive
from creature_mesh import select
from export_fbx import export_collection


ROOT=Path(__file__).resolve().parents[2]
STEM="SM_Dragonblade"
SOURCE=ROOT/"ArtSource/Weapons"/STEM
MODEL=ROOT/"Assets/Art/Weapons/Models"/(STEM+".fbx")
TEXTURES=ROOT/"Assets/Art/Textures/Weapons/Dragonblade"


class Dragonblade(IronSword):
    stem=STEM

    def materials(self):
        self.forged=material("Dragonblade_SmokeBlackSteel",(.042,.046,.057),.87,.41,205,.00071,"steel")
        self.edge=material("Dragonblade_HeatTintedEdges",(.158,.134,.107),.88,.32,264,.00032,"steel")
        self.fuller=material("Dragonblade_FireBlackenedFuller",(.018,.017,.021),.72,.47,225,.00058,"steel")
        self.iron=material("Dragonblade_PatinatedBronze",(.077,.040,.017),.73,.55,160,.00087,"steel")
        self.gold=material("Dragonblade_WarmChasedBronze",(.292,.153,.056),.77,.38,217,.00054,"steel")
        self.recess=material("Dragonblade_DeepBronzeRecess",(.029,.017,.011),.65,.60,180,.00067,"steel")
        self.leather=material("Dragonblade_DarkOxbloodGrip",(.055,.014,.018),0,.76,398,.00068,"leather")
        self.thread=material("Dragonblade_WaxDarkCord",(.085,.042,.028),0,.81,339,.00034,"cloth")
        self.heat=emissive("Dragonblade_BankedHeat",(.218,.040,.007),(.75,.095,.007),.62,.44)
        self.hot=emissive("Dragonblade_FireAtTheHeart",(.682,.191,.024),(1,.29,.025),1.7,.33)
        self.ruby=emissive("Dragonblade_DeepGarnet",(.135,.012,.008),(.65,.057,.008),.26,.33)

    def face_plate(self,name,outline,side,level,thickness,mat,bevel=.004):
        verts=[(x,y,side*(z+w)) for w in (level-thickness,level) for x,y,z in outline]
        n=len(outline)
        faces=[tuple(reversed(range(n))),tuple(range(n,n*2))]
        faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
        obj=self.mesh(name,verts,faces,mat)
        self.bevel(obj,bevel)
        return obj

    def blade(self):
        stations=(
            (.162,.053,.023,0,0),(.218,.071,.025,0,0),(.266,.082,.026,0,0),
            (.302,.080,.024,0,0),(.325,.067,.023,0,0),(.369,.077,.022,.001,0),
            (.414,.074,.022,.001,0),(.494,.068,.021,0,0),(.549,.065,.020,-.001,0),
            (.563,.065,.020,-.001,.0023),(.577,.066,.020,-.001,0),(.676,.067,.019,0,0),
            (.779,.064,.018,0,0),(.881,.060,.016,.001,0),(.954,.057,.015,.002,-.0019),
            (1.031,.053,.014,.002,0),(1.094,.048,.013,.003,0),(1.147,.041,.011,.004,0),
            (1.205,.027,.008,.004,0),(1.266,.011,.004,.002,0),(TIP[1],.0005,.00065,0,0),
        )
        upper=((-1,0),(-.81,.68),(-.58,.97),(-.36,1),(-.24,.83),(-.16,.63),
               (0,.53),(.16,.63),(.24,.83),(.36,1),(.58,.97),(.81,.68),(1,0))
        section=upper+tuple((x,-z) for x,z in reversed(upper[1:-1]))
        n=len(section)
        verts=[]
        for y,w,h,curve,chip in stations:
            for u,v in section:
                dent=-math.copysign(abs(chip),u)*abs(u)**8 if u*chip>0 else 0
                shallow=v if y<1.038 or abs(u)>.36 else v*min(1,1+(y-1.038)*.43)
                verts.append((curve+u*w+dent,y,h*shallow))
        faces=[(row*n+i,row*n+(i+1)%n,(row+1)*n+(i+1)%n,(row+1)*n+i)
               for row in range(len(stations)-1) for i in range(n)]
        faces.extend((tuple(reversed(range(n))),tuple(range(len(verts)-n,len(verts)))))
        steel=self.mesh("Heat-treated blade with recurved shoulders",verts,faces,self.forged)
        steel.data.materials.append(self.edge)
        steel.data.materials.append(self.fuller)
        for face in steel.data.polygons:
            row,col=divmod(face.index,n)
            if row<len(stations)-1:
                face.use_smooth=True
                if col in (0,1,10,11,12,13,22,23):
                    face.material_index=1
                elif col in (5,6,17,18) and 1<row<18:
                    face.material_index=2
        for edge in steel.data.edges:
            a,b=edge.vertices
            if a%n==b%n:
                edge.use_edge_sharp=True
        surface=BVHTree.FromPolygons(verts,faces)
        self.veins(surface)

    def fitted_line(self,name,surface,side,coords,radius,mat):
        points=[]
        for x,y in coords:
            p,_,_,_=surface.ray_cast((x,y,side),(0,0,-side))
            if p is None:
                raise ValueError(f"Etching outside blade at {x,y}")
            p.z+=side*radius*.38
            points.append(p)
        self.tube(name,points,radius,mat)

    def veins(self,surface):
        trunk=[(0,.24),(.004,.279),(-.004,.303),(.003,.335),(-.005,.381),
               (.004,.425),(.001,.461),(-.006,.505),(-.001,.526),
               (.005,.561),(-.006,.597),(-.002,.627),(.008,.653),
               (.004,.688),(-.006,.720),(-.002,.746),(.005,.777),
               (.002,.804),(-.006,.833),(-.001,.862),(.003,.896),
               (.001,.918),(-.004,.941),(.000,.970),(.003,1.005),(.002,1.034)]
        branches=[
            [(-.005,.381),(-.020,.406),(-.023,.428),(-.032,.454),(-.033,.482)],
            [(.003,.335),(.028,.355),(.037,.371),(.045,.400)],
            [(.004,.425),(.018,.451),(.016,.485),(.026,.524),(.034,.538)],
            [(-.006,.505),(-.022,.530),(-.020,.557),(-.032,.586)],
            [(.005,.561),(.025,.585),(.025,.614),(.036,.641),(.033,.663)],
            [(.008,.653),(.023,.676),(.020,.693),(.029,.718)],
            [(-.006,.720),(-.024,.736),(-.027,.771),(-.038,.790)],
            [(.005,.777),(.017,.800),(.014,.832),(.023,.852)],
            [(-.004,.941),(-.016,.960),(-.017,.984)],
        ]
        for side in (-1,1):
            self.fitted_line("Cooling fissure at center of forged steel",surface,side,trunk,.0021,self.heat)
            for i,branch in enumerate(branches):
                self.fitted_line("Branch of the cooling fissure",surface,side,branch,.00115 if i<6 else .00085,
                                 self.heat)
            for run in (trunk[2:6],trunk[7:11],trunk[14:17]):
                self.fitted_line("Small exposed ember inside the fissure",surface,side,run,.00115,self.hot)
            for sign in (-1,1):
                checks=[(sign*x,y) for x,y in ((.031,.473),(.043,.487),(.047,.493))]
                self.fitted_line("Wear across the bevel",surface,side,checks,.00064,self.edge)

    def crossguard(self):
        for sign in (-1,1):
            coords=[(.023,.151,.030),(.076,.138,.030),(.114,.123,.026),(.167,.114,.024),
                    (.200,.126,.024),(.237,.115,.021),(.257,.134,.019),(.249,.150,.021),
                    (.219,.159,.023),(.198,.167,.025),(.220,.177,.026),(.265,.164,.023),
                    (.278,.186,.021),(.251,.208,.024),(.217,.208,.029),(.195,.242,.028),
                    (.159,.253,.032),(.127,.276,.022),(.103,.265,.025),(.111,.229,.030),
                    (.083,.237,.033),(.043,.213,.034),(.016,.209,.035)]
            outline=[(sign*x,y,z) for x,y,z in coords]
            verts=outline+[(x,y,-z) for x,y,z in outline]
            n=len(outline)
            faces=[tuple(range(n)),tuple(reversed(range(n,2*n)))]
            faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
            main=self.mesh("Dragon head and folded throat",verts,faces,self.iron)
            self.bevel(main,.009)
            for side in (-1,1):
                ridges=[
                    ((.061,.183,.035),(.104,.168,.034),(.139,.187,.035),(.162,.214,.034),(.195,.210,.037)),
                    ((.190,.213,.039),(.225,.189,.033),(.254,.185,.025)),
                    ((.124,.151,.030),(.158,.136,.029),(.207,.143,.028),(.242,.137,.023)),
                    ((.138,.221,.036),(.128,.243,.033),(.112,.260,.029)),
                ]
                for line in ridges:
                    self.tube("Chased ridge on the dragon casting",
                              [(sign*x,y,side*z) for x,y,z in line],.0035,self.gold)
                jaw=[(sign*x,y,z) for x,y,z in ((.191,.161,.025),(.207,.155,.025),
                     (.232,.148,.021),(.253,.153,.019),(.233,.164,.022),(.209,.171,.026))]
                self.face_plate("Shadow inside the dragon jaw",jaw,side,.004,.011,self.recess,.003)
                nostril=[(sign*x,y,z) for x,y,z in ((.235,.199,.025),(.246,.196,.026),
                         (.249,.187,.025),(.233,.190,.024))]
                self.face_plate("Punched nostril",nostril,side,.004,.009,self.recess,.0018)
                brow=[(sign*x,y,z) for x,y,z in ((.152,.197,.026),(.177,.226,.033),
                      (.204,.212,.035),(.218,.185,.031),(.199,.175,.034),(.167,.182,.031))]
                self.face_plate("Faceted brow socket",brow,side,.006,.016,self.recess)
                eye=[(sign*x,y,z) for x,y,z in ((.168,.199,.034),(.182,.206,.041),
                      (.203,.196,.040),(.181,.191,.038))]
                self.face_plate("Ember in the dragon eye",eye,side,.004,.007,self.heat,.002)
                for offset in (0,.017,.034):
                    tooth=[(sign*x,y+offset*.10,z) for x,y,z in (
                        (.218+offset,.174,.023),(.225+offset,.167,.023),
                        (.228+offset,.179,.024))]
                    self.face_plate("Raised tooth in the casting",tooth,side,.003,.012,self.gold,.0015)
                for i in range(3):
                    x=.056+i*.023
                    y=.170+i*.006
                    scale=[(sign*(x+dx),y+dy,.031) for dx,dy in
                           ((-.005,-.008),(.012,-.004),(.017,.005),(.011,.012),(-.004,.009))]
                    self.face_plate("Hammered neck scale",scale,side,.005,.011,self.gold,.002)
        self.revolved("Bronze blade collar",
                      [(.132,.036,.025),(.144,.048,.035),(.166,.055,.038),
                       (.195,.050,.034),(.224,.048,.027)],self.gold,38,.82)
        for side in (-1,1):
            self.face_plate("Recess behind the red seal",
                            [(-.039,.162,0),(0,.137,0),(.039,.163,0),(.031,.201,0),(0,.224,0),(-.031,.201,0)],
                            side,.038,.015,self.recess,.005)
            self.gem("Guard garnet",0,.181,side,.040,.028,.033)

    def gem(self,name,x,y,side,base,w,h):
        vertices=[(x+w*.33*math.cos(a),y+h*.36*math.sin(a),side*(base+.016))
                  for a in (i*math.tau/8 for i in range(8))]
        vertices.extend((x+w*math.cos(a),y+h*math.sin(a),side*base)
                        for a in (i*math.tau/8 for i in range(8)))
        faces=[tuple(range(8)),tuple(reversed(range(8,16)))]
        faces.extend((i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8))
        gem=self.mesh(name,vertices,faces,self.ruby)
        gem.data.materials.append(self.heat)
        gem.data.materials.append(self.hot)
        for i in (0,3,7):
            gem.data.polygons[i].material_index=1
        gem.data.polygons[4].material_index=2

    def hilt(self):
        self.revolved("Hand-shaped dark oxblood grip",
                      [(-.170,.037,.029),(-.150,.038,.030),(-.107,.036,.029),
                       (-.045,.034,.029),(.033,.036,.029),(.104,.038,.029),
                       (.139,.037,.029),(.155,.034,.027)],self.leather,46,.84)
        for sign in (-1,1):
            path=[]
            for i in range(250):
                t=i/249
                a=sign*math.tau*8.5*t+sign*1.1
                path.append((.0377*math.cos(a),-.142+.272*t,.0304*math.sin(a)))
            self.tube("Stitched leather cross-lacing",path,.0015,self.thread)
        for y in (-.147,.132):
            self.revolved("Dark bronze coil ferrule",
                          [(y-.014,.036,.029),(y-.009,.041,.034),(y+.009,.040,.033),
                           (y+.013,.036,.030)],self.gold,42,.85)
        self.revolved("Coiled dragon-tail pommel",
                      [(-.274,.022,.022),(-.264,.041,.032),(-.243,.060,.044),
                       (-.213,.056,.042),(-.178,.040,.032),(-.156,.033,.027)],
                      self.iron,52,.89,lambda a:1+.05*math.cos(8*a+.3))
        for side in (-1,1):
            for sign in (-1,1):
                line=[(sign*x,y,side*z) for x,y,z in
                      ((.024,-.166,.028),(.034,-.183,.035),(.049,-.214,.043),
                       (.044,-.239,.045),(.028,-.254,.035),(.016,-.244,.038))]
                self.tube("Raised tail coil on pommel",line,.0043,self.gold)
            self.gem("Small pommel garnet",0,-.226,side,.044,.023,.025)

    def export_assets(self,mesh):
        bake_atlas(mesh,2048,TEXTURES,"T_Dragonblade","UV0")
        bake_emission(mesh,2048,TEXTURES/"T_Dragonblade_Emission.png")
        slots=list(mesh.data.materials)
        indices=[p.material_index for p in mesh.data.polygons]
        mesh.data.materials.clear()
        mesh.data.materials.append(material("M_Dragonblade_Atlas",(.10,.047,.032),.78,.44,0,0,""))
        export_collection(MODEL,bake_animation=False)
        mesh.data.materials.clear()
        for mat in slots:
            mesh.data.materials.append(mat)
        for polygon,index in zip(mesh.data.polygons,indices):
            polygon.material_index=index


def stage(scene):
    camera=studio_camera(scene)
    floor=bpy.data.objects["Studio shadow floor"]
    floor.location.z=-.119
    floor.data.materials.clear()
    floor.data.materials.append(material("Dragonblade graphite backdrop",(.016,.022,.031),0,1,0,0,""))
    for name,pos,power,size in (
        ("Soft key",(1.44,.89,2.97),257,2.4),("Front fill",(-1.09,-.53,1.20),103,1.4),
        ("Rim",(-1.58,1.63,1.89),154,1.2),("Back fill",(1.56,-1.16,.68),70,1.2),
    ):
        obj=bpy.data.objects[name]
        obj.location=pos
        obj.rotation_euler=(Vector((0,.46,0))-obj.location).to_track_quat("-Z","Y").to_euler()
        obj.data.energy=power
        obj.data.size=size
    scene.render.compositor_device="CPU"
    tree=scene.compositing_node_group=bpy.data.node_groups.new("Quiet ember glow","CompositorNodeTree")
    tree.interface.new_socket(name="Image",in_out="OUTPUT",socket_type="NodeSocketColor")
    image=tree.nodes.new("CompositorNodeRLayers")
    glare=tree.nodes.new("CompositorNodeGlare")
    glare.inputs["Type"].default_value="Fog Glow"
    glare.inputs["Threshold"].default_value=1.06
    glare.inputs["Strength"].default_value=.29
    output=tree.nodes.new("NodeGroupOutput")
    tree.links.new(image.outputs["Image"],glare.inputs["Image"])
    tree.links.new(glare.outputs["Image"],output.inputs["Image"])
    return camera


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--quick",action="store_true")
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    SOURCE.mkdir(parents=True,exist_ok=True)
    sword=Dragonblade()
    mesh=sword.finish_sword()
    if not args.quick:
        sword.export_assets(mesh)
    scene=sword.scene
    camera=stage(scene)
    scene.render.resolution_x,scene.render.resolution_y=1340,1040
    scene.render.resolution_percentage=51 if args.quick else 100
    scene.cycles.samples=12 if args.quick else 32
    full=((2.62,-1.02,4.32),(0,.510,0),1.86,-18)
    view(camera,*full)
    if not args.quick:
        select([mesh])
        bpy.context.preferences.filepaths.save_version=0
        bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(STEM+".blend")))
    frames=(("",full),("Detail",((.49,-.31,2.52),(0,.159,0),.91,-9)))
    for suffix,framing in frames:
        view(camera,*framing)
        scene.render.filepath=str(SOURCE/("Dragonblade"+suffix+".png"))
        bpy.ops.render.render(write_still=True)


if __name__=="__main__":
    main()
