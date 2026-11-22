"""Reusable Blender mesh primitives for skinned creatures."""

import math

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def ramp(value,low,high):
    t=min(1,max(0,(value-low)/(high-low)))
    return t*t*(3-2*t)


def select(objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]


class CreatureMesh:
    """Build, skin and collect parts belonging to one export collection."""

    def surface(self,name,vertices,faces,mat=None,smooth=True):
        mesh=bpy.data.meshes.new(name)
        mesh.from_pydata(vertices,[],faces)
        mesh.update()
        obj=bpy.data.objects.new(name,mesh)
        self.collection.objects.link(obj)
        if mat:
            mesh.materials.append(mat)
        for face in mesh.polygons:
            face.use_smooth=smooth
        return obj

    def finish(self,obj,mat,weights,pigment=False):
        if not obj.data.materials:
            obj.data.materials.append(mat)
        if pigment:
            self.coats.add(obj)
        weights=weights if callable(weights) else (lambda _,bone=weights: {bone:1.0})
        groups={}
        for vertex in obj.data.vertices:
            for name,value in weights(obj.matrix_world@vertex.co).items():
                if value>0.00001:
                    if name not in groups:
                        groups[name]=obj.vertex_groups.new(name=name)
                    groups[name].add([vertex.index],value,"REPLACE")
        self.parts.append(obj)
        return obj

    def loft(self,name,rings,sections=32,cap=True):
        """Build closed Y-facing cross sections with independent width and height."""
        verts=[(x+w*math.sin(a),y,z+h*math.cos(a)) for y,x,z,w,h in rings
               for a in (i*math.tau/sections for i in range(sections))]
        faces=[]
        for r in range(len(rings)-1):
            for i in range(sections):
                j=(i+1)%sections
                k=r*sections
                faces.append((k+i,k+j,k+sections+j,k+sections+i))
        if cap:
            faces.extend((tuple(reversed(range(sections))),
                          tuple(range(len(verts)-sections,len(verts)))))
        return self.surface(name,verts,faces)

    def ellipsoid(self,name,center,radii,axis=None,n=20,m=12):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=n,ring_count=m,location=center)
        obj=bpy.context.object
        obj.name=name
        obj.scale=radii
        if axis is not None:
            obj.rotation_euler=Vector(axis).to_track_quat("Z","Y").to_euler()
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        for polygon in obj.data.polygons:
            polygon.use_smooth=True
        return obj

    def limb_mass(self,name,a,b,width,depth,extension):
        a,b=Vector(a),Vector(b)
        span=b-a
        return self.ellipsoid(name,(a+b)*.5,(width,depth,.5*span.length+extension),span)

    def union(self,objects,name,voxel,relax=3):
        select(objects)
        bpy.ops.object.join()
        obj=bpy.context.object
        obj.name=name
        obj.data.remesh_voxel_size=voxel
        bpy.ops.object.voxel_remesh()
        mod=obj.modifiers.new("Relax voxel junctions","SMOOTH")
        mod.factor=.55
        mod.iterations=relax
        bpy.ops.object.modifier_apply(modifier=mod.name)
        for p in obj.data.polygons:
            p.use_smooth=True
        return obj

    @staticmethod
    def chain_weights(names,value,transitions):
        weights={}
        for index,name in enumerate(names):
            left=1 if index==0 else ramp(value,*transitions[index-1])
            right=0 if index==len(names)-1 else ramp(value,*transitions[index])
            if left-right>0:
                weights[name]=left-right
        return weights

    @staticmethod
    def bvh(obj):
        return BVHTree.FromPolygons([v.co for v in obj.data.vertices],
                                    [tuple(p.vertices) for p in obj.data.polygons])

    def tube(self,name,paths,radius,mat,weights,sides=7,pigment=False,taper=.75):
        vertices,faces=[],[]
        for path in paths:
            path=list(map(Vector,path))
            start=len(vertices)
            for i,p in enumerate(path):
                axis=path[min(i+1,len(path)-1)]-path[max(0,i-1)]
                q=axis.normalized().to_track_quat("Z","Y")
                r=radius*(1-taper*i/(len(path)-1))
                for j in range(sides):
                    a=j*math.tau/sides
                    vertices.append(p+q@Vector((r*math.cos(a),r*math.sin(a),0)))
            for i in range(len(path)-1):
                for j in range(sides):
                    k=(j+1)%sides
                    a=start+i*sides
                    faces.append((a+j,a+k,a+sides+k,a+sides+j))
        return self.finish(self.surface(name,vertices,faces),mat,weights,pigment)
