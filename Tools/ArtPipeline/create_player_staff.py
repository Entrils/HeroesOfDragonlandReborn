"""Build the player's staff with a smoke-blue focus, grip and casting markers."""

import argparse
import math
from pathlib import Path
from random import Random
import sys

import bpy
from mathutils import Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
from character_art import material,studio_camera
from create_camp_chest import oak
from create_iron_sword import IronSword,view
from create_neutral_projectiles import bake_emission,emissive
from creature_mesh import select


ROOT=Path(__file__).resolve().parents[2]
STEM="SM_PlayerStaff"
SOURCE=ROOT/"ArtSource/Weapons"/STEM
MODEL=ROOT/"Assets/Art/Weapons/Models"/(STEM+".fbx")
TEXTURES=ROOT/"Assets/Art/Textures/Weapons/PlayerStaff"
FOCUS=(0,2.070,0)
TIP=(0,2.190,0)


class PlayerStaff(IronSword):
    stem=STEM
    model_path=MODEL
    textures=TEXTURES
    attachment_points=(("Socket_Grip",(0,0,0)),("Socket_Focus",FOCUS),("Socket_Tip",TIP))

    def materials(self):
        self.wood=oak("Staff_WeatheredHawthorn",(.092,.048,.022),(5.8,.28,5.8))
        self.sap=oak("Staff_ExposedGrain",(.172,.092,.035),(6.4,.34,6.4))
        self.iron=material("Staff_OldBlackenedIron",(.052,.064,.066),.82,.50,221,.00068,"steel")
        self.edge=material("Staff_BurnishedPewter",(.182,.207,.201),.78,.37,231,.00048,"steel")
        self.brass=material("Staff_TarnishedBrass",(.205,.119,.049),.75,.48,223,.00053,"steel")
        self.leather=material("Staff_DarkHide",(.053,.025,.017),0,.76,420,.00064,"leather")
        self.cord=material("Staff_WaxedFlax",(.135,.092,.057),0,.80,480,.00031,"cloth")
        self.stone=emissive("Staff_SmokeBlueFacet",(.040,.135,.173),(.051,.31,.36),.21,.32)
        self.bright=emissive("Staff_LightBlueFacet",(.104,.253,.277),(.16,.62,.70),.37,.29)
        self.deep=emissive("Staff_DarkCrystalFacet",(.025,.051,.075),(.04,.15,.18),.10,.40)
        self.spark=emissive("Staff_TealShardHighlights",(.08,.196,.206),(.22,.66,.65),.51,.36)

    def shaft(self):
        stations=(
            (-.239,-.009,.004,.022),(-.202,-.007,.003,.028),(-.121,-.001,0,.029),
            (.016,0,0,.030),(.162,.006,-.003,.030),(.352,.006,-.010,.028),
            (.503,-.008,-.015,.026),(.675,-.022,-.009,.026),(.772,-.020,.001,.027),
            (.929,-.012,.011,.029),(1.125,.003,.009,.027),(1.303,.012,-.002,.025),
            (1.461,.007,-.011,.025),(1.592,-.006,-.010,.028),(1.741,-.008,-.005,.032),
            (1.828,-.003,.005,.037),(1.924,.007,.007,.033),(1.970,.003,.003,.024),
        )
        rows=[Vector(a).lerp(Vector(b),i/5) for a,b in zip(stations,stations[1:]) for i in range(5)]
        rows.append(Vector(stations[-1]))
        n=28
        verts=[]
        for i,(y,x,z,r) in enumerate(rows):
            for j in range(n):
                t=j*math.tau/n
                irregular=1+.055*math.sin(3*t+.084*i)+.023*math.sin(7*t-.17*i)
                verts.append((x+math.cos(t)*r*irregular,y,z+math.sin(t)*r*irregular*.93))
        faces=[(row*n+j,row*n+(j+1)%n,(row+1)*n+(j+1)%n,(row+1)*n+j)
               for row in range(len(rows)-1) for j in range(n)]
        faces.extend((tuple(reversed(range(n))),tuple(range(len(verts)-n,len(verts)))))
        obj=self.mesh("Bent hawthorn wood with raised grain",verts,faces,self.wood)
        obj.data.materials.append(self.sap)
        for poly in obj.data.polygons:
            row,col=divmod(poly.index,n)
            poly.use_smooth=row<len(rows)-1
            if (21<row<32 and col==4) or (45<row<53 and col==21) or (63<row<70 and col==8):
                poly.material_index=1
        for y,r in ((-.200,.034),(.169,.037),(1.734,.039),(1.805,.046)):
            self.revolved("Rounded iron ferrule",[
                (y-.017,r-.005,r-.005),(y-.011,r,r),(y+.011,r*.98,r*.97),
                (y+.017,r-.006,r-.006),
            ],self.iron,37,.91)
        self.revolved("Rounded iron heel cap",[
            (-.257,.017,.018),(-.250,.025,.026),(-.236,.029,.028),(-.212,.028,.027),
        ],self.iron,40,.90)

    def grip(self):
        self.revolved("Shaped dark leather grip",[
            (-.163,.031,.032),(-.150,.035,.034),(-.098,.036,.034),
            (.019,.035,.034),(.104,.037,.035),(.150,.035,.033),(.164,.031,.031),
        ],self.leather,46,.92)
        for a,b in ((-.142,-.071),(-.070,.067),(.068,.147)):
            line=[]
            for i in range(129):
                t=i/128
                theta=math.tau*(a/.039+5.8*t)
                line.append((.0368*math.cos(theta),a+(b-a)*t,.034*math.sin(theta)))
            self.tube("Waxed flax along the leather seam",line,.00158,self.cord)
        for y in (-.156,.148):
            self.revolved("Narrow grip ferrule",[(y-.010,.034,.033),(y-.006,.039,.038),
                          (y+.006,.038,.036),(y+.010,.034,.033)],self.edge,39,.90)
        self.revolved("Subtle brass band near the head",
                      [(1.762,.035,.034),(1.767,.041,.039),(1.780,.040,.038),(1.786,.034,.033)],
                      self.brass,40,.89)

    def head(self):
        self.revolved("Iron seat below the focus",[
            (1.838,.039,.036),(1.857,.052,.050),(1.890,.057,.053),
            (1.926,.051,.048),(1.952,.045,.041),(1.972,.047,.044),
        ],self.iron,43,.88,lambda a:1+.037*math.cos(3*a-.2))
        self.revolved("Faceted lip of the crystal seat",[
            (1.932,.048,.045),(1.945,.056,.051),(1.953,.056,.051),(1.965,.050,.045),
        ],self.edge,32,.88)
        for i in range(3):
            phase=.41+i*math.tau/3
            marks=((1.866,.042,-.17),(1.916,.062,-.05),(1.964,.082,.02),
                   (2.024,.096,.09),(2.086,.087,.12),(2.133,.064,.09),(2.161,.047,.07))
            path=[Vector((r*math.cos(phase+d),y,r*math.sin(phase+d))) for y,r,d in marks]
            self.forged_claw(path,(.016,.021,.020,.016,.013,.009,.0031))
            self.tube("Bright ridge of the branch",[(x*1.15,y,z*1.15) for x,y,z in path[1:-1]],
                      .0031,self.edge)
            self.nail(path[2],(math.cos(phase),0,math.sin(phase)),.009,self.brass)
            quick=[(r*math.cos(phase+d+.31),y,r*math.sin(phase+d+.31))
                   for y,r,d in marks[1:4]]
            self.tube("Swept secondary leaf",quick,.007,self.iron)
        self.crystal()

    def forged_claw(self,path,widths):
        divisions=12
        points=[]
        for i,(point,width) in enumerate(zip(path,widths)):
            direction=path[min(len(path)-1,i+1)]-path[max(0,i-1)]
            frame=direction.to_track_quat("Z","Y")
            for j in range(divisions):
                t=j*math.tau/divisions
                points.append(point+frame@Vector((width*math.cos(t),width*.74*math.sin(t),0)))
        faces=[(i*divisions+j,i*divisions+(j+1)%divisions,(i+1)*divisions+(j+1)%divisions,
                (i+1)*divisions+j) for i in range(len(path)-1) for j in range(divisions)]
        faces.extend((tuple(reversed(range(divisions))),tuple(range(len(points)-divisions,len(points)))))
        claw=self.mesh("Forged claw tapered towards the crystal",points,faces,self.iron)
        for face in claw.data.polygons:
            face.use_smooth=face.index<len(faces)-2

    def crystal(self):
        rng=Random(40211)
        n=8
        verts=[]
        rows=((1.958,.015),(1.978,.040),(2.026,.060),(2.087,.067),
              (2.141,.044),(2.173,.019),(TIP[1],.0005))
        for index,(y,r) in enumerate(rows):
            for j in range(n):
                t=.18+j*math.tau/n
                local=r*(.91+rng.random()*.16)
                dy=0 if index==len(rows)-1 else rng.uniform(-.008,.008)
                verts.append((math.cos(t)*local,y+dy,math.sin(t)*local*.92))
        faces=[]
        for row in range(len(rows)-1):
            for j in range(n):
                a,b=row*n+j,row*n+(j+1)%n
                c,d=b+n,a+n
                faces.extend(((a,b,c),(a,c,d)) if (row+j)%3==0 else ((a,b,c,d),))
        faces.extend((tuple(reversed(range(n))),tuple(range(len(verts)-n,len(verts)))))
        crystal=self.mesh("Rough smoke-blue crystal with angular cleavage",verts,faces,self.stone)
        for mat in (self.bright,self.deep,self.spark):
            crystal.data.materials.append(mat)
        for face in crystal.data.polygons:
            value=rng.random()
            face.material_index=0 if value<.46 else 1 if value<.76 else 2 if value<.96 else 3

    def build_parts(self):
        self.shaft()
        self.grip()
        self.head()


def stage(scene):
    camera=studio_camera(scene)
    floor=bpy.data.objects["Studio shadow floor"]
    floor.location.z=-.138
    floor.data.materials.clear()
    floor.data.materials.append(material("Staff charcoal backdrop",(.018,.024,.029),0,1,0,0,""))
    for name,pos,power,size in (
        ("Soft key",(1.96,1.64,3.18),317,2.40),("Front fill",(-1.35,-.19,1.68),110,1.76),
        ("Rim",(-1.98,2.32,2.20),227,1.47),("Back fill",(1.79,-1.16,.92),68,1.17),
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
    staff=PlayerStaff()
    mesh=staff.finish_sword()
    if not args.quick:
        staff.save_and_export_sword(mesh)
        bake_emission(mesh,2048,TEXTURES/"T_PlayerStaff_Emission.png")
    scene=staff.scene
    camera=stage(scene)
    scene.render.resolution_x,scene.render.resolution_y=1400,1000
    scene.render.resolution_percentage=51 if args.quick else 100
    scene.cycles.samples=12 if args.quick else 30
    full=((2.6,-.80,5.10),(0,.960,0),2.76,-20)
    view(camera,*full)
    if not args.quick:
        select([mesh])
        bpy.context.preferences.filepaths.save_version=0
        bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(STEM+".blend")))
    for suffix,framing in (("",full),("Focus",((.64,1.41,1.84),(0,1.942,0),.72,-15)),
                           ("Grip",((.77,-.62,2.08),(0,.028,0),.73,-12))):
        view(camera,*framing)
        scene.render.filepath=str(SOURCE/("PlayerStaff"+suffix+".png"))
        bpy.ops.render.render(write_still=True)


if __name__=="__main__":
    main()
