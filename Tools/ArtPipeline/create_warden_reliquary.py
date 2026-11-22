"""Author the Stone Warden's hinged reward reliquary in Blender."""

import argparse
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0,str(Path(__file__).resolve().parent))
from character_art import material,studio_camera
from create_camp_chest import Chest
from create_stone_warden import DRAGON_SEAL,stone_material


ROOT=Path(__file__).resolve().parents[2]
STEM="SM_WardenReliquary"
SOURCE=ROOT/"ArtSource/Environment"/STEM
MODEL=ROOT/"Assets/Art/Environment/Models"/(STEM+".fbx")


class Reliquary(Chest):
    mesh_stem="WardenReliquary"
    textures=ROOT/"Assets/Art/Textures/Environment/WardenReliquary"

    def materials(self):
        self.stone=stone_material("Reliquary_AshBasalt",(.072,.083,.087),77,.86)
        self.slate=stone_material("Reliquary_BlueSlate",(.097,.112,.114),88,.83)
        self.light=stone_material("Reliquary_CarvedFreestone",(.205,.178,.133),92,.81)
        self.dark=stone_material("Reliquary_RecessedBasalt",(.024,.031,.031),96,.89)
        self.moss=stone_material("Reliquary_Lichen",(.080,.101,.042),129,.91)
        self.bronze=material("Reliquary_AgedBronze",(.225,.116,.047),.74,.51,169,.0011,"steel")
        self.patina=material("Reliquary_OxidizedBronze",(.057,.099,.084),.44,.68,157,.0016,"leather")
        self.mineral=material("Reliquary_SealedMineral",(.086,.214,.155),.13,.47,167,.0008,"steel")
        shader=self.mineral.node_tree.nodes["Principled BSDF"]
        shader.inputs["Emission Color"].default_value=(.10,.31,.20,1)
        shader.inputs["Emission Strength"].default_value=.22

    def stone_loft(self,name,rings,mat,bevel=.012,hollow=False):
        """Join chamfered rectangular courses, continuing over the lip for open vessels."""
        verts=[]
        for z,w,d,c in rings:
            verts.extend((x,y,z) for x,y in (
                (-w+c,d),(w-c,d),(w,d-c),(w,-d+c),
                (w-c,-d),(-w+c,-d),(-w,-d+c),(-w,d-c),
            ))
        faces=[]
        for i in range(len(rings) if hollow else len(rings)-1):
            n=(i+1)%len(rings)
            for j in range(8):
                k=(j+1)%8
                faces.append((8*i+j,8*i+k,8*n+k,8*n+j))
        if not hollow:
            faces.extend((tuple(range(7,-1,-1)),tuple(range(len(verts)-8,len(verts)))))
        obj=self.mesh(name,verts,faces,mat)
        self.bevel(obj,bevel)
        return obj

    def flanking_pier(self,sign):
        # Each cheek is its own fitted course, with irregular corner relief.
        outline=[(.535,.161),(.684,.152),(.770,.225),(.773,.651),
                 (.731,.757),(.567,.773),(.541,.693),(.558,.595),(.518,.477),(.529,.303)]
        self.front_plate("Raised stone cheek",[(sign*x,z) for x,z in outline],
                         .511,.067,self.slate,.013)
        key=[(.590,.232),(.697,.219),(.727,.304),(.676,.414),(.708,.537),
             (.677,.693),(.592,.680),(.578,.606),(.607,.512),(.563,.377)]
        self.front_plate("Chipped carved cheek inlay",[(sign*x,z) for x,z in key],
                         .523,.023,self.light,.009)
        for level in (.258,.649):
            self.front_plate("Bronze brace on cheek",
                             [(sign*x,z) for x,z in ((.552,level-.022),(.714,level-.027),
                              (.737,level),(.714,level+.023),(.556,level+.025))],
                             .535,.025,self.bronze,.005)
            self.nail((sign*.692,.539,level),(0,1,0),.012,self.patina)
        self.tube("Cheek inward engraving",
                  [(sign*.638,.526,.311),(sign*.667,.526,.380),(sign*.624,.526,.466),
                   (sign*.664,.527,.529),(sign*.628,.526,.595)],.004,self.dark)

    def body_seal(self):
        outline=[(0,.745),(.182,.688),(.248,.577),(.232,.423),
                 (.148,.326),(0,.279),(-.151,.326),(-.230,.428),(-.250,.574),(-.176,.692)]
        self.front_plate("Buried basalt seal socket",[(x*1.13,.51+(z-.51)*1.08) for x,z in outline],
                         .524,.051,self.slate,.020)
        self.front_plate("Basalt seal inset",outline,.548,.036,self.dark,.011)
        for i,p in enumerate(outline):
            q=outline[(i+1)%len(outline)]
            a,b=Vector((p[0],.554,p[1])),Vector((q[0],.554,q[1]))
            start,end=(.045,.88) if i in (1,7) else (.021,.978)
            self.tube("Bronze seal border",[a.lerp(b,t) for t in (start,.35,.68,end)],
                      .012,self.bronze)
        self.front_plate("Stone Warden dragon relief",[(x*1.45,.499+z*1.43) for x,z in DRAGON_SEAL],
                         .572,.029,self.light,.006)
        for path in (
            [(-.153,.519),(-.172,.469),(-.129,.439),(-.147,.393),(-.099,.365)],
            [(.155,.410),(.129,.453),(.157,.491),(.140,.529),(.179,.567)],
            [(-.143,.638),(-.120,.605),(-.150,.576)],
        ):
            self.tube("Thin mineral fracture in the seal",[(x,.553,z) for x,z in path],.0039,self.mineral)
        for x in (-.347,.347):
            sign=math.copysign(1,x)
            self.tube("Inlaid angular lettering",
                      [(x-sign*.035,.479,.562),(x,.479,.605),(x+sign*.035,.479,.562),
                       (x,.479,.526),(x,.479,.465),(x-sign*.027,.479,.441)],
                      .0046,self.bronze)

    def worn_surface(self,obj):
        tree=BVHTree.FromPolygons([v.co for v in obj.data.vertices],
                                  [tuple(p.vertices) for p in obj.data.polygons])

        def front(x,z,offset):
            point,_,_,_=tree.ray_cast((x,1,z),(0,-1,0))
            if point is not None:
                point.y+=offset
            return point

        for coords in (
            ((-.467,.672),(-.435,.619),(-.463,.590),(-.411,.542),(-.430,.498),(-.381,.458)),
            ((.475,.400),(.432,.355),(.457,.301),(.419,.258),(.436,.226)),
            ((-.367,.266),(-.345,.244),(-.357,.211),(-.319,.184)),
        ):
            path=[front(x,z,.0009) for x,z in coords]
            self.tube("Hairline break along the basalt",path,.0035,self.dark)
        verts,faces=[],[]
        for x,z,w,h in ((-.53,.23,.083,.072),(-.40,.188,.097,.042),(.53,.224,.101,.078),
                        (.34,.264,.061,.046),(-.70,.335,.051,.109)):
            for _ in range(39):
                dx,dz=(self.rng.uniform(-1,1) for _ in range(2))
                if dx*dx+dz*dz>1:
                    continue
                cx,cz=x+dx*w,z+dz*h
                radius=self.rng.uniform(.0031,.011)
                patch=[front(cx+radius*math.cos(a),cz+radius*.7*math.sin(a),.0016)
                       for a in (i*math.tau/6 for i in range(6))]
                if all(p is not None for p in patch):
                    n=len(verts)
                    verts.extend(patch)
                    faces.append(tuple(range(n,n+6)))
        self.mesh("Irregular lichen along the base",verts,faces,self.moss)

    def build_body(self):
        self.stone_loft("Low burial plinth",[(.008,.77,.470,.092),(.025,.806,.510,.090),
                        (.100,.808,.507,.094),(.148,.770,.474,.087)],self.stone,.016)
        self.stone_loft("Worn ashlar foot band",[(.103,.781,.486,.079),(.141,.780,.487,.079),
                        (.181,.730,.448,.082)],self.light,.012)
        self.stone_loft("Stone floor inside",[(.101,.699,.430,.064),(.171,.695,.415,.075)],
                        self.dark,.010)
        basin=self.stone_loft("Hollow carved basalt vessel",[
            (.142,.719,.440,.100),(.205,.742,.466,.096),(.673,.754,.473,.088),
            (.753,.785,.501,.077),(.787,.785,.498,.076),(.795,.694,.426,.075),
            (.746,.658,.393,.062),(.208,.638,.369,.060),(.142,.647,.367,.064),
        ],self.stone,.014,True)
        self.worn_surface(basin)
        for sign in (-1,1):
            self.flanking_pier(sign)
            self.box("Interior bronze hinge reinforcement",(sign*.53,-.401,.675),(.148,.029,.173),
                     self.patina,.012)
            self.box("Exterior hinge anchor",(sign*.53,-.494,.723),(.146,.048,.127),self.bronze,.012)
            for side in (-1,1):
                center=sign*.53+side*.063
                self.tube("Fixed hinge knuckle",[(center-.023,-.500,.800),(center+.023,-.500,.800)],
                          .025,self.bronze)
            self.tube("Captive bronze hinge pin",[(sign*.53-.095,-.500,.800),(sign*.53+.095,-.500,.800)],
                      .010,self.patina)
            self.box("Bronze end strap",(sign*.748,0,.318),(.032,.70,.061),self.bronze,.012)
            for y in (-.307,.307):
                self.nail((sign*.769,y,.317),(sign,0,0),.013,self.patina)
            self.tube("Cut rhombus on end wall",
                      [(sign*.760,y,z) for y,z in ((-.185,.509),(0,.662),(.185,.509),(0,.368))],
                      .009,self.slate,True)
            self.tube("Bronze center on the end seal",
                      [(sign*.764,y,z) for y,z in ((-.103,.515),(0,.599),(.103,.515),(0,.429))],
                      .0053,self.bronze,True)
        self.body_seal()

    def build_lid(self):
        self.parts=self.lid_parts
        lid=self.stone_loft("Heavy chamfered lid",[
            (.809,.819,.510,.095),(.858,.831,.518,.106),
            (.908,.799,.491,.129),(1.058,.691,.351,.146),
            (1.087,.640,.300,.124),
        ],self.slate,.018)
        self.stone_loft("Dark inset on the underside",[
            (.799,.653,.387,.081),(.821,.671,.402,.086),(.835,.675,.405,.090),
        ],self.dark,.009)
        roof=self.stone_loft("Raised worn roof panel",[
            (1.069,.635,.290,.101),(1.091,.625,.285,.117),(1.108,.601,.262,.131),
        ],self.light,.011)
        surfaces=[BVHTree.FromPolygons([v.co for v in obj.data.vertices],
                     [tuple(p.vertices) for p in obj.data.polygons]) for obj in (lid,roof)]

        def surface(x,y):
            hits=[tree.ray_cast((x,y,1.5),(0,0,-1))[0] for tree in surfaces]
            return max(point.z for point in hits if point is not None)

        for sign in (-1,1):
            x=sign*.530
            for ysign in (-1,1):
                widths=(x-.027,x+.027)
                stations=[ysign*(.214+.285*i/24) for i in range(25)]
                points=[(vx,y,surface(vx,y)+.010) for y in stations for vx in widths]
                faces=[(i,i+1,i+3,i+2) for i in range(0,len(points)-2,2)]
                strap=self.mesh("Flat bronze binding on the lid",points,faces,self.bronze)
                bpy.context.view_layer.objects.active=strap
                solid=strap.modifiers.new("Bronze thickness","SOLIDIFY")
                solid.thickness=.012
                solid.offset=0
                bpy.ops.object.modifier_apply(modifier=solid.name)
                self.bevel(strap,.003)
                self.tube("Bronze strap edge",
                          [(vx,y,z+.004) for vx,y,z in points[::2]],.0034,self.patina)
            for y in (-.437,-.239,.239,.437):
                direction=Vector((0,math.copysign(.70,y),.76)) if abs(y)>.4 else Vector((0,0,1))
                self.nail((x,y,surface(x,y)+.017),direction,.010,self.patina)
            self.tube("Moving hinge knuckle",[(x-.037,-.500,.800),(x+.037,-.500,.800)],
                      .024,self.bronze)
            self.box("Stone lid underside rib",(sign*.449,0,.798),(.044,.702,.018),self.slate,.006)
        self.tube("Incised diamond around the lid seal",
                  [(x,y,1.112) for x,y in ((-.353,0),(0,.201),(.353,0),(0,-.201))],
                   .008,self.dark,True)
        self.tube("Mineral line set into the lid",
                  [(x,y,1.113) for x,y in ((-.284,0),(0,.151),(.284,0),(0,-.151))],
                   .0045,self.mineral,True)
        self.front_plate("Locking stone tongue",[
            (-.07,.780),(-.09,.840),(-.065,.901),(0,.924),(.065,.901),(.09,.840),(.07,.780),
        ],.529,.040,self.dark,.010)
        self.tube("Seal clasp bronze frame",[(x,.537,z) for x,z in
                  ((-.047,.805),(-.058,.849),(0,.889),(.058,.849),(.047,.805))],.009,self.bronze)
        self.nail((0,.540,.846),(0,1,0),.016,self.mineral)

    def review(self,camera,lid,quick):
        self.scene.cycles.samples=12 if quick else 30
        self.scene.render.resolution_percentage=52 if quick else 100
        for suffix,position,target,scale,angle in (
            ("",(2.63,5.04,2.82),(0,0,.56),2.31,0),
            ("Detail",(-1.06,4.56,1.84),(0,.31,.66),1.35,0),
            ("Open",(2.29,4.48,3.12),(0,-.15,.88),3.32,105),
        ):
            lid.rotation_euler.x=math.radians(angle)
            camera.location=position
            camera.rotation_euler=(Vector(target)-camera.location).to_track_quat("-Z","Y").to_euler()
            camera.data.ortho_scale=scale
            self.scene.render.filepath=str(SOURCE/("WardenReliquary"+suffix+".png"))
            bpy.ops.render.render(write_still=True)


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--quick",action="store_true")
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    SOURCE.mkdir(parents=True,exist_ok=True)
    reliquary=Reliquary()
    reliquary.build_body()
    reliquary.build_lid()
    body,lid=reliquary.assemble(not args.quick)
    scene=reliquary.scene
    camera=studio_camera(scene)
    camera.location=(2.63,5.04,2.82)
    camera.rotation_euler=(Vector((0,0,.56))-camera.location).to_track_quat("-Z","Y").to_euler()
    camera.data.ortho_scale=2.31
    scene.render.resolution_x=1200
    scene.render.resolution_y=1000
    bpy.data.objects["Studio shadow floor"].location.z=0
    bpy.data.objects["Soft key"].data.energy=460
    bpy.data.objects["Front fill"].data.energy=147
    if not args.quick:
        reliquary.save_and_export(body,lid,SOURCE/(STEM+".blend"),MODEL,(.072,.083,.087))
    reliquary.review(camera,lid,args.quick)


if __name__=="__main__":
    main()
