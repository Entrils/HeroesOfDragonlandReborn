"""Build the player's greatsword and its two attachment markers."""

import argparse
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0,str(Path(__file__).resolve().parent))
from character_art import material,studio_camera
from create_iron_sword import IronSword,view
from creature_mesh import select


ROOT=Path(__file__).resolve().parents[2]
STEM="SM_Greatsword"
SOURCE=ROOT/"ArtSource/Weapons"/STEM
MODEL=ROOT/"Assets/Art/Weapons/Models"/(STEM+".fbx")
TEXTURES=ROOT/"Assets/Art/Textures/Weapons/Greatsword"
TIP=(0,1.90,0)
OFF_HAND=(0,-.223,0)


class Greatsword(IronSword):
    stem=STEM
    model_path=MODEL
    textures=TEXTURES
    attachment_points=(("Socket_Grip",(0,0,0)),("Socket_OffHand",OFF_HAND),("Socket_Tip",TIP))

    def materials(self):
        self.steel=material("Greatsword_SeasonedSteel",(.111,.129,.139),.90,.42,216,.00055,"steel")
        self.edge=material("Greatsword_BurnishedEdges",(.222,.240,.239),.90,.31,303,.00023,"steel")
        self.fuller=material("Greatsword_ShadowedDol",(.062,.078,.086),.81,.50,232,.00043,"steel")
        self.iron=material("Greatsword_SmokeDarkenedIron",(.062,.071,.075),.82,.54,212,.00062,"steel")
        self.brass=material("Greatsword_TarnishedBrass",(.158,.097,.047),.73,.52,204,.00049,"steel")
        self.leather=material("Greatsword_OldOxbloodLeather",(.055,.018,.014),0,.78,415,.00069,"leather")
        self.thread=material("Greatsword_ShadowedBinding",(.098,.058,.033),0,.82,455,.00038,"cloth")

    def blade(self):
        stations=[
            (.162,.058,.023),(.212,.060,.024),(.285,.062,.023),(.369,.064,.022),
            (.415,.079,.023),(.466,.112,.023),(.538,.107,.022),(.620,.106,.022),
            (.717,.105,.021),(.852,.103,.020),(.946,.102,.020),(.951,.102,.020),
            (.957,.102,.020),(1.048,.100,.019),(1.168,.096,.018),(1.295,.092,.017),
            (1.437,.086,.014),(1.548,.077,.012),(1.658,.060,.010),
            (1.771,.036,.0068),(1.846,.015,.0045),(TIP[1],.0004,.0005),
        ]
        upper=[(-1,0),(-.84,.57),(-.68,.93),(-.54,1),(-.48,.89),(-.41,.63),
               (-.33,.56),(-.25,.63),(-.19,.87),(-.11,.98),(0,1),
               (.11,.98),(.19,.87),(.25,.63),(.33,.56),(.41,.63),(.48,.89),
               (.54,1),(.68,.93),(.84,.57),(1,0)]
        section=upper+[(x,-z) for x,z in reversed(upper[1:-1])]
        width=len(section)
        vertices=[]
        for y,w,d in stations:
            groove=min(1,max(0,(y-.414)/.105),max(0,(1.70-y)/.18))
            for x,z in section:
                variation=.0035*x**9 if y==.951 and x<0 else 0
                if .19<=abs(x)<=.48:
                    z=math.copysign(.98+(abs(z)-.98)*groove,z)
                vertices.append((x*w-variation,y,z*d))
        faces=[(i*width+j,i*width+(j+1)%width,(i+1)*width+(j+1)%width,(i+1)*width+j)
               for i in range(len(stations)-1) for j in range(width)]
        faces.extend((tuple(reversed(range(width))),tuple(range(len(vertices)-width,len(vertices)))))
        blade=self.mesh("Broad steel blade with twin fullers",vertices,faces,self.steel)
        blade.data.materials.append(self.edge)
        blade.data.materials.append(self.fuller)
        for polygon in blade.data.polygons:
            row,col=divmod(polygon.index,width)
            if row>=len(stations)-1:
                continue
            polygon.use_smooth=True
            if col in (0,1,18,19,20,21,38,39) and row>=4:
                polygon.material_index=1
            elif col in (5,6,13,14,25,26,33,34) and 5<=row<=17:
                polygon.material_index=2
        for edge in blade.data.edges:
            a,b=edge.vertices
            if a%width==b%width:
                edge.use_edge_sharp=True
        tree=BVHTree.FromPolygons(vertices,faces)
        for side in (-1,1):
            patterns=[
                ((-.059,.822),(-.071,.841),(-.078,.853)),
                ((.049,1.044),(.075,1.061),(.080,1.073)),
                ((.068,1.279),(.060,1.292),(.043,1.305)),
            ]
            for path in patterns:
                points=[]
                for x,y in path:
                    p,_,_,_=tree.ray_cast((x,y,side),(0,0,-side))
                    p.z+=side*.00023
                    points.append(p)
                self.tube("Scratches caught by the light",points,.00063,self.edge)
            self.tube("Restrained maker's lozenge",
                      [(x,y,side*.025) for x,y in ((-.016,.339),(0,.362),(.016,.339),(0,.321))],
                      .00145,self.brass,True)

    def forging(self,name,points,mat,bevel):
        verts=[(x,y,z*sign) for sign in (-1,1) for x,y,z in points]
        n=len(points)
        faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]
        faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
        obj=self.mesh(name,verts,faces,mat)
        self.bevel(obj,bevel)
        return obj

    def crossguard(self):
        shape=[
            (-.321,.231,.029),(-.309,.260,.029),(-.277,.257,.031),(-.239,.232,.032),
            (-.167,.200,.035),(-.085,.211,.040),(-.045,.226,.038),(.045,.226,.038),
            (.087,.211,.040),(.167,.200,.035),(.239,.232,.032),(.277,.257,.031),
            (.309,.260,.029),(.321,.232,.029),(.317,.200,.028),(.284,.199,.029),
            (.250,.169,.029),(.190,.143,.032),(.109,.139,.038),(.044,.142,.037),
            (-.047,.142,.037),(-.109,.139,.038),(-.190,.143,.032),(-.250,.169,.029),
            (-.284,.199,.029),(-.317,.200,.028),
        ]
        self.forging("Wide forged upswept guard",shape,self.iron,.012)
        for side in (-1,1):
            ring=[
                (-.136,.181,.034),(-.157,.178,.066),(-.129,.166,.104),(-.065,.153,.124),
                (0,.147,.128),(.065,.153,.124),(.129,.166,.104),(.157,.178,.066),
                (.136,.181,.034),
            ]
            self.tube("Forged knuckle ring",[(x,y,side*z) for x,y,z in ring],.018,self.iron)
            self.tube("Burnished ridge on the ring",
                      [(x,y-.011,side*z) for x,y,z in ring[1:-1]],.0031,self.edge)
            for sign in (-1,1):
                line=[(sign*x,y,side*d) for x,y,d in ((.090,.192,.041),(.131,.180,.036),
                      (.184,.184,.037),(.229,.200,.034),(.280,.226,.031),(.303,.230,.030))]
                self.tube("Worn upper line on the guard",line,.0028,self.edge)
                self.nail((sign*.269,.217,side*.034),(0,0,side),.007,self.brass)
        self.revolved("Heavy collar behind guard",[(.122,.037,.030),(.135,.048,.037),(.160,.048,.038),
                      (.180,.045,.034)],self.iron,44,.87)
        self.revolved("Thin embossed collar ring",[(.137,.047,.037),(.144,.051,.040),(.151,.049,.038)],
                      self.brass,44,.87)

    def hilt(self):
        self.revolved("Long oval two-hand grip",[
            (-.365,.038,.031),(-.344,.042,.033),(-.258,.043,.034),(-.185,.044,.034),
            (-.135,.042,.033),(-.040,.038,.032),(.050,.038,.033),(.105,.041,.033),(.132,.039,.032)
        ],self.leather,44,.85)
        for start,end,twists,wrapping in ((-.340,-.125,7,self.thread),(-.120,.110,8,self.leather)):
            line=[]
            for i in range(201):
                t=i/200
                y=start+(end-start)*t
                angle=math.tau*twists*t
                rx=.044 if y<-.13 else .040+.002*abs(y)*8
                line.append((rx*math.cos(angle),y,.0345*math.sin(angle)))
            self.tube("Raised stitching along the grip",line,.0022,wrapping)
        for y,width,mat in ((-.347,.044,self.iron),(-.126,.047,self.iron),(.106,.044,self.brass)):
            self.revolved("Oval binding at the grip",
                          [(y-.011,width-.003,.033),(y-.006,width,.036),
                           (y+.007,width,.036),(y+.012,width-.003,.033)],
                          mat,42,.85)
        self.revolved("Octagonal scent-stopper pommel",[
            (-.454,.034,.030),(-.448,.055,.036),(-.432,.069,.048),(-.394,.068,.046),
            (-.374,.052,.037),(-.351,.037,.031),
        ],self.iron,48,.89,lambda a:1+.056*math.cos(a*8))
        self.revolved("Beaten brass rim on pommel",
                      [(-.436,.058,.042),(-.428,.071,.049),(-.422,.070,.048)],
                      self.brass,48,.89,lambda a:1+.05*math.cos(a*8))
        self.nail((0,-.450,0),(0,-1,0),.025,self.edge)


def stage(scene):
    camera=studio_camera(scene)
    floor=bpy.data.objects["Studio shadow floor"]
    floor.location.z=-.182
    floor.data.materials.clear()
    floor.data.materials.append(material("Greatsword slate backdrop",(.020,.025,.034),0,1,0,0,""))
    for name,position,power,size in (
        ("Soft key",(2.05,1.28,3.48),398,2.95),("Front fill",(-1.28,-.68,1.55),128,1.88),
        ("Rim",(-2.32,2.25,2.27),224,1.74),("Back fill",(1.73,-1.57,.91),93,1.36),
    ):
        light=bpy.data.objects[name]
        light.location=position
        light.rotation_euler=(Vector((0,.64,0))-light.location).to_track_quat("-Z","Y").to_euler()
        light.data.energy=power
        light.data.size=size
    return camera


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--quick",action="store_true")
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    SOURCE.mkdir(parents=True,exist_ok=True)
    sword=Greatsword()
    mesh=sword.finish_sword()
    if not args.quick:
        sword.save_and_export_sword(mesh)
    scene=sword.scene
    camera=stage(scene)
    scene.render.resolution_x,scene.render.resolution_y=1390,1040
    scene.render.resolution_percentage=51 if args.quick else 100
    scene.cycles.samples=12 if args.quick else 32
    full=((2.66,-1.05,4.61),(0,.72,0),2.70,-17)
    view(camera,*full)
    if not args.quick:
        select([mesh])
        bpy.context.preferences.filepaths.save_version=0
        bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(STEM+".blend")))
    frames=(("",full),("Hilt",((.68,-.48,2.08),(0,.078,0),1.19,-12)))
    for suffix,framing in frames:
        view(camera,*framing)
        scene.render.filepath=str(SOURCE/("Greatsword"+suffix+".png"))
        bpy.ops.render.render(write_still=True)


if __name__=="__main__":
    main()
