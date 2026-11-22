"""Build the player's ash spear with the existing grip and tip positions."""

import argparse
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0,str(Path(__file__).resolve().parent))
from character_art import material,studio_camera
from create_camp_chest import oak
from create_iron_sword import IronSword,view
from creature_mesh import select


ROOT=Path(__file__).resolve().parents[2]
STEM="SM_Spear"
SOURCE=ROOT/"ArtSource/Weapons"/STEM
MODEL=ROOT/"Assets/Art/Weapons/Models"/(STEM+".fbx")
TEXTURES=ROOT/"Assets/Art/Textures/Weapons/Spear"
TIP=(0,2.295,0)
OFF_HAND=(0,.420,0)


class Spear(IronSword):
    stem=STEM
    model_path=MODEL
    textures=TEXTURES
    attachment_points=(("Socket_Grip",(0,0,0)),("Socket_OffHand",OFF_HAND),("Socket_Tip",TIP))

    def materials(self):
        self.ash=oak("Spear_DarkSeasonedAsh",(.139,.070,.030),(7.2,.28,7.2))
        self.steel=material("Spear_ColdForgedSteel",(.104,.122,.133),.87,.43,229,.00054,"steel")
        self.edge=material("Spear_PolishedFacet",(.228,.247,.249),.90,.32,284,.00027,"steel")
        self.iron=material("Spear_DarkenedIronBands",(.058,.073,.079),.82,.53,195,.00055,"steel")
        self.brass=material("Spear_TarnishedBrass",(.151,.088,.039),.72,.51,212,.00061,"steel")
        self.leather=material("Spear_RussetLeather",(.055,.019,.012),0,.80,402,.00057,"leather")
        self.cord=material("Spear_DarkWaxedCord",(.102,.063,.036),0,.84,480,.00034,"cloth")
        self.recess=material("Spear_ShadowedEngraving",(.031,.036,.039),.54,.58,170,.00046,"steel")

    def head(self):
        stations=(
            (1.848,.025,.013),(1.873,.032,.017),(1.912,.045,.021),(1.951,.057,.022),
            (1.991,.067,.025),(2.024,.070,.025),(2.057,.068,.024),(2.103,.063,.021),
            (2.134,.056,.018),(2.151,.053,.017),(2.155,.052,.017),(2.164,.050,.016),
            (2.194,.043,.013),(2.232,.032,.010),(2.261,.019,.006),(2.285,.008,.003),
            (TIP[1],.0005,.00045),
        )
        section=((-1,0),(-.79,.43),(-.55,.66),(-.29,.82),(0,1),(.29,.82),
                 (.55,.66),(.79,.43),(1,0),(.79,-.43),(.55,-.66),(.29,-.82),
                 (0,-1),(-.29,-.82),(-.55,-.66),(-.79,-.43))
        n=len(section)
        verts=[]
        for y,w,h in stations:
            for x,z in section:
                nick=.0025*x**7 if x>0 and y==2.155 else 0
                verts.append((x*w-nick,y,z*h))
        faces=[(row*n+j,row*n+(j+1)%n,(row+1)*n+(j+1)%n,(row+1)*n+j)
               for row in range(len(stations)-1) for j in range(n)]
        faces.extend((tuple(reversed(range(n))),tuple(range(len(verts)-n,len(verts)))))
        head=self.mesh("Leaf-shaped steel head with raised spine",verts,faces,self.steel)
        head.data.materials.append(self.edge)
        for face in head.data.polygons:
            row,col=divmod(face.index,n)
            if row<len(stations)-1:
                face.use_smooth=True
                if col in (0,7,8,15) and row>1:
                    face.material_index=1
        for edge in head.data.edges:
            a,b=edge.vertices
            if a%n==b%n:
                edge.use_edge_sharp=True
        tree=BVHTree.FromPolygons(verts,faces)
        for side in (-1,1):
            details=(
                ("Incised maker's lozenge",((-.014,1.983),(0,2.007),(.014,1.983),
                  (0,1.963),(-.014,1.983)),.0012,self.recess),
                ("Fine wear on the blade",((.043,2.069),(.035,2.077),(.029,2.095)),.00058,self.edge),
                ("Fine wear on the blade",((-.043,2.018),(-.037,2.036),(-.030,2.043)),.00054,self.edge),
            )
            for name,coords,radius,mat in details:
                points=[]
                for x,y in coords:
                    point,_,_,_=tree.ray_cast((x,y,side),(0,0,-side))
                    point.z+=side*radius*.42
                    points.append(point)
                self.tube(name,points,radius,mat)

    def shaft(self):
        self.revolved("Tapered ash shaft",[
            (-.224,.024,.024),(-.209,.029,.028),(-.075,.031,.029),(.232,.031,.030),
            (.592,.030,.029),(.894,.029,.027),(1.187,.028,.027),(1.464,.026,.026),
            (1.708,.025,.025),(1.847,.024,.024),(1.923,.023,.022),
        ],self.ash,38,.94,lambda a:1+.022*math.cos(3*a)+.013*math.cos(7*a))
        for y in (-.216,.673,1.522):
            self.revolved("Subtle dark iron reinforcing ferrule",[
                (y-.013,.027,.027),(y-.009,.032,.032),(y+.009,.032,.031),(y+.014,.027,.027),
            ],self.iron,32,.96)
        self.revolved("Iron heel cap",[
            (-.241,.017,.017),(-.236,.024,.024),(-.223,.027,.027),(-.206,.028,.028),
        ],self.iron,35,.90)

    def collar(self):
        self.revolved("Long tapering socket behind spearhead",[
            (1.687,.024,.024),(1.701,.033,.032),(1.735,.036,.035),(1.773,.033,.032),
            (1.828,.029,.028),(1.892,.027,.028),(1.925,.024,.024),(1.945,.019,.021),
        ],self.iron,44,.82,lambda a:1+.04*math.cos(4*a))
        for y,r in ((1.725,.037),(1.851,.031)):
            self.revolved("Tarnished line around forged socket",[
                (y-.014,r-.004,r-.004),(y-.009,r,r),(y+.008,r,r),(y+.014,r-.004,r-.004),
            ],self.brass,42,.88)
        for side in (-1,1):
            self.nail((0,1.780,side*.032),(0,0,side),.009,self.brass)
            for sign in (-1,1):
                path=[(sign*x,y,side*z) for x,y,z in
                      ((.020,1.749,.027),(.022,1.774,.025),(.017,1.805,.026),
                       (.011,1.830,.025),(.009,1.847,.026))]
                self.tube("Darkened longitudinal worked line",path,.0018,self.recess)

    def grips(self):
        for a,b,r in ((-.139,.142,.036),(.306,.532,.035)):
            self.revolved("Rolled leather hand grip",[
                (a-.007,r-.004,r-.004),(a+.010,r,r-.001),((a+b)*.5,r-.002,r-.002),
                (b-.013,r,r-.001),(b+.007,r-.004,r-.004),
            ],self.leather,44,.93)
            line=[]
            for i in range(193):
                t=i/192
                theta=math.tau*9*t
                line.append(((r-.0007)*math.cos(theta),a+(b-a)*t,
                             (r-.0013)*math.sin(theta)))
            self.tube("Raised seam in leather binding",line,.00155,self.cord)
        for y in (-.133,.138,.311,.522):
            self.revolved("Wear ring on the edge of the grip",[
                (y-.004,.034,.033),(y,.037,.036),(y+.004,.035,.034),
            ],self.iron,35,.91)

    def build_parts(self):
        self.shaft()
        self.head()
        self.collar()
        self.grips()


def stage(scene):
    camera=studio_camera(scene)
    floor=bpy.data.objects["Studio shadow floor"]
    floor.location.z=-.113
    floor.data.materials.clear()
    floor.data.materials.append(material("Spear muted graphite backdrop",(.022,.028,.036),0,1,0,0,""))
    for name,pos,power,size in (
        ("Soft key",(1.9,1.45,2.81),298,2.31),("Front fill",(-1.09,-.43,1.32),108,1.72),
        ("Rim",(-1.73,2.63,1.90),197,1.61),("Back fill",(1.76,-1.00,.71),75,1.3),
    ):
        light=bpy.data.objects[name]
        light.location=pos
        light.rotation_euler=(Vector((0,1.0,0))-light.location).to_track_quat("-Z","Y").to_euler()
        light.data.energy=power
        light.data.size=size
    return camera


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--quick",action="store_true")
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    SOURCE.mkdir(parents=True,exist_ok=True)
    spear=Spear()
    mesh=spear.finish_sword()
    if not args.quick:
        spear.save_and_export_sword(mesh)
    scene=spear.scene
    camera=stage(scene)
    scene.render.resolution_x,scene.render.resolution_y=1400,1000
    scene.render.resolution_percentage=51 if args.quick else 100
    scene.cycles.samples=12 if args.quick else 30
    full=((2.68,-.80,5.10),(0,1.03,0),2.86,-20)
    view(camera,*full)
    if not args.quick:
        select([mesh])
        bpy.context.preferences.filepaths.save_version=0
        bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(STEM+".blend")))
    for suffix,framing in (("",full),("Head",((.86,1.19,2.40),(0,1.936,0),.84,-15)),
                           ("Grip",((.82,-.60,2.38),(0,.155,0),.95,-14))):
        view(camera,*framing)
        scene.render.filepath=str(SOURCE/("Spear"+suffix+".png"))
        bpy.ops.render.render(write_still=True)


if __name__=="__main__":
    main()
