using System;
using System.Linq;
using HeroesOfDragonland.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.Art.Editor
{
    /// <summary>Reviewed rectangles for the existing battlefield. Presentation only, with retained collision.</summary>
    public static class BattlefieldBiomeDetailPass
    {
        private const string RootName = "Biome details | M56 M57";
        private const string MeshFolder = "Assets/Art/Environment/Meshes";
        private static Transform root;
        private static Material Load(string name) => AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Art/Materials/Environment/" + (name.StartsWith("M_AF_") ? "AncientForest/" : "Biomes/") + name + ".mat")
            ?? throw new InvalidOperationException("Missing material " + name);

        // Top surfaces meet exactly. Original floor renderers are retained for public map authoring.
        private static void Surface(string name, float x0, float x1, float z0, float z1, string material, int mirror,
            bool blend = false, bool alongZ = false, bool reverse = false)
        {
            string assetPath = MeshFolder + "/M57_" + name + "_" + mirror + ".asset";
            if(AssetDatabase.LoadAssetAtPath<Mesh>(assetPath)!=null)throw new InvalidOperationException("Inspect existing mesh " + assetPath);
            Vector3 P(float x,float z) => new Vector3(x*mirror,.026f,z*mirror);
            var vertices=new[]{P(x0,z0),P(x0,z1),P(x1,z1),P(x1,z0)};
            var uv=vertices.Select(p=>new Vector2(p.x/4,p.z/4)).ToArray();
            if(blend)
                for(int i=0;i<4;i++)
                {
                    float u=alongZ ? (i==1||i==2?1:0) : (i>=2?1:0);
                    uv[i]=new Vector2(reverse?1-u:u,alongZ?vertices[i].x/4:vertices[i].z/4);
                }
            var mesh=new Mesh { name=name,vertices=vertices,uv=uv,triangles=new[]{0,1,2,0,2,3} };
            mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,assetPath);
            var obj=new GameObject(name+" "+mirror,typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(root,false);
            obj.GetComponent<MeshFilter>().sharedMesh=mesh;obj.GetComponent<MeshRenderer>().sharedMaterial=Load(material);
        }
        private static void Accent(string prefab, Vector3 position, float yaw)
        {
            string family=prefab.StartsWith("SM_AF_")?"AncientForest":prefab.StartsWith("SM_FR_")?"FortressRuins":"Biomes";
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Environment/Prefabs/"+family+"/"+prefab+".prefab");
            if(asset==null)throw new InvalidOperationException("Missing regional prop " + prefab);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset,root.gameObject.scene);
            instance.transform.SetParent(root,false);instance.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
            foreach(var collider in instance.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
        }
        public static void Apply()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.path!="Assets/Scenes/DragonlandBattlefield.unity"||scene.isDirty)
                throw new InvalidOperationException("Use the saved battlefield in Edit Mode.");
            var map=scene.GetRootGameObjects().Single(g=>g.name.StartsWith("Battlefield |"));
            if(map.transform.Find(RootName)!=null)throw new InvalidOperationException("Inspect existing biome details.");
            var camps=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<NetworkCampSpawner>(true)).Single();
            string before=EditorJsonUtility.ToJson(camps);
            var collision=map.GetComponentsInChildren<Collider>(true).Select(c=>(c,c.bounds,c.enabled)).ToArray();
            if(!AssetDatabase.IsValidFolder(MeshFolder))AssetDatabase.CreateFolder("Assets/Art/Environment","Meshes");
            root=new GameObject(RootName).transform;root.SetParent(map.transform,false);Undo.RegisterCreatedObjectUndo(root.gameObject,"M57 split top surfaces");
            foreach(var renderer in map.GetComponentsInChildren<MeshRenderer>(true).Where(r=>new[]{"Forest","Rocky Highlands","Ruined Battlefield","Corrupted reach"}.Contains(r.name)))
            { Undo.RecordObject(renderer,"Retain source floor, replace its rendered top");renderer.enabled=false; }
            foreach(int sign in new[]{1,-1})
            {
                Surface("Forest outer",-42,-23.5f,8,42,"M_AF_ForestFloor",sign);
                Surface("Forest inner",-23.5f,-8,10,42,"M_AF_ForestFloor",sign);
                Surface("Forest corner",-8,-4,10,18,"M_AF_ForestFloor",sign);
                Surface("Highland outer",23.5f,42,8,42,"M_BS_HighlandGravel",sign);
                Surface("Highland inner",8,23.5f,10,42,"M_BS_HighlandGravel",sign);
                Surface("Highland corner",4,8,10,18,"M_BS_HighlandGravel",sign);
                Surface("Corrupted core",-4,4,18,42,"M_BS_CorruptedAsh",sign);
                Surface("Forest to ruins",-23.5f,-4,8,10,"M_BS_TransitionForestRuins",sign,true,true,true);
                Surface("Highlands to ruins",4,23.5f,8,10,"M_BS_TransitionHighlandsRuins",sign,true,true,true);
                Surface("Forest to corrupted",-8,-4,18,42,"M_BS_TransitionForestCorrupted",sign,true);
                Surface("Highlands to corrupted",4,8,18,42,"M_BS_TransitionHighlandsCorrupted",sign,true,false,true);
                Surface("Ruins center edge",-4,4,8,10,"M_BS_BattlefieldPaving",sign);
                // Small rocks stay inside retained trunk cover footprints. No new solid blockers.
                Accent("SM_FR_Rubble01",new Vector3(-36.25f*sign,0,22*sign),sign==1?0:180);
                Accent("SM_FR_Rubble01",new Vector3(-39.25f*sign,0,33*sign),sign==1?0:180);
                // A neutral standard sits on existing non-traversable ruin columns.
                Accent("SM_BS_ShatteredStandard",new Vector3(-19*sign,4,6*sign),sign==1?0:180);
            }
            Surface("Ruins central",-23.5f,23.5f,-8,8,"M_BS_BattlefieldPaving",1);
            foreach(var renderer in map.GetComponentsInChildren<MeshRenderer>(true))
            {
                string material=renderer.name=="Rocky outcrop | non-traversable high ground"||renderer.name=="Rock cap"?"M_BS_StrataStone"
                    :renderer.name=="Corrupted stone placeholder"?"M_BS_CorruptedStone":renderer.name=="Ruin pillar"?"M_BS_StrataStone":null;
                if(material!=null){Undo.RecordObject(renderer,"Regional cover material");renderer.sharedMaterial=Load(material);}
            }
            if(before!=EditorJsonUtility.ToJson(camps)||map.GetComponentsInChildren<Collider>(true).Length!=collision.Length
                ||collision.Any(s=>s.c==null||s.c.bounds!=s.bounds||s.c.enabled!=s.enabled))
                throw new InvalidOperationException("Biome detail pass changed camp/collision invariants.");
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
            Debug.Log("[M57] Fitted four actual neighboring transition pairs, split top surfaces and paired cover accents; camps/collision unchanged. Ruins and corruption are separated by the existing route gap, so their fifth library transition is not placed across that gap.");
        }
    }
}
