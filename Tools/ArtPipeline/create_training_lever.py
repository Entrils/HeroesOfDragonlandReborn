"""Build the hinged lever and stone stand for the tutorial chest interaction."""

import argparse
import math
from pathlib import Path
import sys

import bpy
from mathutils import Matrix,Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
from character_art import material,studio_camera
from create_camp_chest import Chest,oak
from create_stone_warden import stone_material


ROOT=Path(__file__).resolve().parents[2]
STEM="SM_TrainingLever"
SOURCE=ROOT/"ArtSource/Environment"/STEM
MODEL=ROOT/"Assets/Art/Environment/Models"/(STEM+".fbx")
TEXTURES=ROOT/"Assets/Art/Textures/Environment/TrainingLever"
PIVOT=(0,0,.847)
READY=-34
PULLED=35


class TrainingLever(Chest):
    mesh_stem="TrainingLever"
    textures=TEXTURES
    hinge=PIVOT
    moving_name="Handle"

    def materials(self):
        self.stone=stone_material("Lever_WeatheredLimestone",(.162,.146,.123),91,.88)
        self.edge_stone=stone_material("Lever_ChamferedLimestone",(.192,.168,.130),88,.85)
        self.slate=stone_material("Lever_DarkerRepairStone",(.122,.133,.132),94,.87)
        self.iron=material("Lever_OiledBlackIron",(.056,.064,.068),.82,.51,187,.00067,"steel")
        self.edge=material("Lever_BurnishedIronEdge",(.17,.182,.180),.86,.37,222,.00031,"steel")
        self.brass=material("Lever_WornPaleBrass",(.31,.172,.060),.76,.45,196,.00061,"steel")
        self.patina=material("Lever_BronzeOxidation",(.074,.095,.070),.53,.68,156,.00058,"leather")
        self.dark=material("Lever_DarkRecess",(.023,.023,.017),0,.90,159,.00039,"leather")
        self.wood=oak("Lever_RubbedOakHandle",(.133,.073,.031),(.31,5.1,6.0))
        self.leather=material("Lever_LeatherGrip",(.086,.033,.019),0,.78,319,.00051,"leather")
        self.red=material("Lever_DullOxideMark",(.144,.034,.018),0,.84,217,.00041,"leather")

    def stone_course(self,name,sections,mat,bevel):
        verts=[]
        for z,w,d,c in sections:
            verts.extend((x,y,z) for x,y in ((-w+c,d),(w-c,d),(w,d-c),(w,-d+c),
                         (w-c,-d),(-w+c,-d),(-w,-d+c),(-w,d-c)))
        faces=[(i*8+j,i*8+(j+1)%8,(i+1)*8+(j+1)%8,(i+1)*8+j)
               for i in range(len(sections)-1) for j in range(8)]
        faces.extend((tuple(range(7,-1,-1)),tuple(range(len(verts)-8,len(verts)))))
        obj=self.mesh(name,verts,faces,mat)
        self.bevel(obj,bevel)

    def axle(self,name,center,sections,mat,n=28):
        x,y,z=center
        verts=[(x+d,y+r*math.cos(j*math.tau/n),z+r*math.sin(j*math.tau/n))
               for d,r in sections for j in range(n)]
        faces=[(i*n+j,i*n+(j+1)%n,(i+1)*n+(j+1)%n,(i+1)*n+j)
               for i in range(len(sections)-1) for j in range(n)]
        faces.extend((tuple(range(n-1,-1,-1)),tuple(range(len(verts)-n,len(verts)))))
        obj=self.mesh(name,verts,faces,mat)
        for face in obj.data.polygons:
            face.use_smooth=face.index<len(faces)-2

    def side_flange(self,sign,outline,mat,x0,x1):
        n=len(outline)
        verts=[(sign*x,y,z) for x in (x0,x1) for y,z in outline]
        faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]
        faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
        self.bevel(self.mesh("Forged mounting cheek",verts,faces,mat),.008)

    def build_body(self):
        self.stone_course("Bevelled stone foot",[
            (.005,.275,.253,.052),(.029,.295,.278,.055),(.098,.296,.279,.056),
            (.124,.271,.256,.058)
        ],self.slate,.013)
        self.stone_course("Single hewn limestone pedestal",[
            (.111,.245,.224,.048),(.172,.247,.228,.045),(.257,.211,.198,.051),
            (.568,.206,.197,.049),(.629,.222,.217,.051),(.680,.228,.221,.050)
        ],self.stone,.014)
        self.stone_course("Beaten stone cap under the mechanism",[
            (.655,.241,.230,.050),(.675,.260,.250,.045),(.717,.259,.251,.044),
            (.748,.236,.228,.045)
        ],self.edge_stone,.013)
        self.box("Black iron plate fixed atop the plinth",(0,0,.755),(.396,.370,.030),self.iron,.010)
        for sx in (-1,1):
            for sy in (-1,1):
                self.nail((sx*.168,sy*.149,.772),(0,0,1),.013,self.brass)
        for sign in (-1,1):
            self.side_flange(sign,[
                (-.158,.754),(.160,.754),(.151,.787),(.090,.812),(.065,.870),
                (.044,.915),(-.041,.917),(-.074,.880),(-.094,.816),(-.148,.789)
            ],self.iron,.090,.139)
            self.axle("Stationary bronze bearing ring",(sign*.142,0,PIVOT[2]),
                      [(-.006,.059),(-.003,.071),(.003,.073),(.006,.067)],self.brass,34)
            self.axle("Fixed keeper of the pin",(sign*.151,0,PIVOT[2]),
                      [(-.006,.027),(-.003,.038),(.016,.037),(.020,.031)],self.iron,22)
            self.nail((sign*.174,0,PIVOT[2]),(sign,0,0),.013,self.edge)
            for y in (-.083,.087):
                self.nail((sign*.144,y,.811),(sign,0,0),.0096,self.edge)
        for direction,mark in ((1,self.red),(-1,self.patina)):
            self.box("Hard travel stop",(0,direction*.146,.785),(.082,.044,.043),self.iron,.007)
            self.box("Inlaid position mark",(0,direction*.161,.807),(.063,.015,.009),mark,.003)
        self.plaque()
        for pts in (
            [(-.177,.199,.309),(-.149,.198,.288),(-.155,.199,.255),(-.123,.205,.223)],
            [(.114,.199,.573),(.148,.199,.556),(.138,.198,.523),(.168,.198,.498)],
        ):
            self.tube("Fine fissure across limestone",pts,.0023,self.dark)

    def plaque(self):
        outline=[(-.087,.431),(-.087,.533),(-.068,.551),(.067,.552),
                 (.086,.530),(.087,.432),(.072,.414),(-.072,.412)]
        self.front_plate("Dark iron field for the chest pictogram",outline,.209,.042,self.iron,.007)
        rim=[(x*.865,.213,.480+(z-.480)*.85) for x,z in outline]
        self.tube("Inset pale brass frame",rim,.0033,self.brass,True)
        icon=[(-.031,.452),(.031,.452),(.031,.486),(-.031,.486)]
        self.front_plate("Cast outline of a chest lock",icon,.216,.009,self.brass,.004)
        self.tube("Lock loop",[(-.021,.216,.483),(-.021,.218,.503),(-.014,.219,.517),
                  (0,.220,.520),(.015,.219,.517),(.022,.218,.502),(.022,.216,.483)],.0043,self.brass)
        self.tube("Tiny key slot",[(0,.222,.475),(0,.222,.463)],.003,self.dark)
        for sign in (-1,1):
            self.nail((sign*.065,.214,.474),(0,1,0),.007,self.patina)

    def build_lid(self):
        self.parts=self.lid_parts
        self.axle("Moving shaft barrel",(0,0,PIVOT[2]),
                  [(-.091,.046),(-.081,.051),(.077,.053),(.091,.047)],self.iron,30)
        outlines=(
            (-.025,.846,.030),(-.031,.902,.029),(-.026,.966,.028),(-.021,1.044,.025),
            (-.020,1.124,.022),(-.023,1.204,.021),(.022,1.204,.022),
            (.019,1.127,.022),(.025,1.044,.026),(.031,.964,.027),(.031,.903,.029),
            (.026,.846,.030),
        )
        verts=[(x,side*depth,z) for side in (-1,1) for x,z,depth in outlines]
        n=len(outlines)
        faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]
        faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
        self.bevel(self.mesh("Slightly tapered forged lever arm",verts,faces,self.iron),.008)
        for sign in (-1,1):
            self.tube("Worked arris along the lever arm",[(sign*x,.027,z) for x,z in
                      ((.027,.919),(.022,.972),(.018,1.055),(.016,1.127),(.016,1.184))],
                      .0023,self.edge)
        self.axle("Oak cross-grip",(0,0,1.218),[
            (-.126,.022),(-.115,.034),(-.084,.040),(-.042,.038),
            (.032,.036),(.084,.040),(.120,.035),(.126,.023)
        ],self.wood,34)
        self.axle("Worked leather on the handle",(0,0,1.218),[
            (-.087,.038),(-.081,.041),(-.022,.041),(.030,.038),(.083,.040),(.088,.037)
        ],self.leather,37)
        for x in (-.108,.108):
            self.axle("Warm brass end cap on grip",(x,0,1.218),[
                (-.015,.031),(-.010,.038),(.010,.037),(.016,.031)
            ],self.brass,36)
        coil=[]
        for i in range(141):
            u=i/140
            a=math.tau*u*7
            coil.append((-.085+.170*u,.0407*math.sin(a),1.218+.0407*math.cos(a)))
        self.tube("Shallow lacing over leather",coil,.0015,self.wood)


def view(camera,location,target,scale):
    camera.location=location
    camera.rotation_euler=(Vector(target)-camera.location).to_track_quat("-Z","Y").to_euler()
    camera.data.ortho_scale=scale


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--quick",action="store_true")
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    SOURCE.mkdir(parents=True,exist_ok=True)
    lever=TrainingLever()
    lever.build_body()
    lever.build_lid()
    body,handle=lever.assemble(not args.quick)
    scene=lever.scene
    camera=studio_camera(scene)
    bpy.data.objects["Studio shadow floor"].location.z=0
    bpy.data.objects["Soft key"].data.energy=296
    bpy.data.objects["Front fill"].data.energy=91
    bpy.data.objects["Rim"].data.energy=221
    scene.render.resolution_x,scene.render.resolution_y=1010,1260
    scene.render.resolution_percentage=52 if args.quick else 100
    scene.cycles.samples=12 if args.quick else 27
    full=((2.20,3.96,1.96),(0,0,.602),1.48)
    view(camera,*full)
    handle.rotation_euler.x=math.radians(READY)
    if not args.quick:
        # Both meshes face the tutorial entrance in the FBX. The review scene faces +Y.
        turn=Matrix.Rotation(math.pi,4,"Z")
        for part in (body,handle):
            part.data.transform(turn)
        handle.rotation_euler.x=math.radians(-READY)
        lever.save_and_export(body,handle,SOURCE/(STEM+".blend"),MODEL,(.162,.146,.123))
        for part in (body,handle):
            part.data.transform(turn.inverted())
        scene.render.fps=30
        scene.frame_start,scene.frame_end=1,13
        for label,frame,angle in (("Ready",1,READY),("Pulled",13,PULLED)):
            handle.rotation_euler.x=math.radians(angle)
            handle.keyframe_insert(data_path="rotation_euler",frame=frame)
            scene.timeline_markers.new(label,frame=frame)
        handle.animation_data.action.name="Training lever - pull"
        scene.frame_set(1)
        bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(STEM+".blend")))
    for suffix,pos,target,scale,angle in (
        ("",*full,READY),
        ("Pulled",*full,PULLED),
        ("Detail",(2.10,2.96,1.69),(0,.036,.965),.735,READY),
    ):
        if handle.animation_data is None:
            handle.rotation_euler.x=math.radians(angle)
        else:
            scene.frame_set(1 if angle==READY else 13)
        view(camera,pos,target,scale)
        scene.render.filepath=str(SOURCE/("TrainingLever"+suffix+".png"))
        bpy.ops.render.render(write_still=True)


if __name__=="__main__":
    main()
