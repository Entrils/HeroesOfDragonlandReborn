using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HeroesOfDragonland.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.Art.Editor
{
    /// <summary>Explicit art placement for the existing map; original gameplay collision is retained.</summary>
    public static class BattlefieldModelReplacement
    {
        public const string RootName="Architecture and regional models | reviewed replacement";
        private const string MaterialFolder="Assets/Art/Materials/Environment/Battlefield";
        private static Transform visualRoot;
        private static int instances, hidden;
        private static readonly List<string> changes=new();

        private static string AssetPath(string name)
        {
            string family=name.StartsWith("SM_FR_")?"FortressRuins":name.StartsWith("SM_AF_")?"AncientForest":"Biomes";
            return "Assets/Art/Environment/Prefabs/"+family+"/"+name+".prefab";
        }
        private static Vector3 Abs(Vector3 v)=>new(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z));
        private static Vector3 Size(MeshRenderer renderer)=>Vector3.Scale(renderer.GetComponent<MeshFilter>().sharedMesh.bounds.size,Abs(renderer.transform.lossyScale));
        private static Bounds LocalVisualBounds(GameObject root)
        {
            Bounds bounds=default;bool found=false;
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.EndsWith("_LOD0",StringComparison.Ordinal)))
            {
                var mesh=renderer.GetComponent<MeshFilter>().sharedMesh.bounds;
                var matrix=root.transform.worldToLocalMatrix*renderer.transform.localToWorldMatrix;
                for(int i=0;i<8;i++)
                {
                    var p=matrix.MultiplyPoint3x4(mesh.center+Vector3.Scale(mesh.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    if(!found){bounds=new Bounds(p,Vector3.zero);found=true;}else bounds.Encapsulate(p);
                }
            }
            if(!found)throw new InvalidOperationException("Missing LOD0 art bounds: "+root.name);
            return bounds;
        }
        private static GameObject Fit(string name,Vector3 center,Quaternion rotation,Vector3 size,Transform parent=null)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath(name));
            if(asset==null)throw new InvalidOperationException("Missing existing model prefab "+name);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset,(parent??visualRoot).gameObject.scene);
            instance.transform.SetParent(visualRoot,false);
            foreach(var collider in instance.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
            if(instance.GetComponentsInChildren<MonoBehaviour>(true).Length!=0)throw new InvalidOperationException("Environment art must not add gameplay scripts.");
            var bounds=LocalVisualBounds(instance);
            var scale=new Vector3(size.x/bounds.size.x,size.y/bounds.size.y,size.z/bounds.size.z);
            instance.transform.localScale=scale;
            instance.transform.SetPositionAndRotation(center-rotation*Vector3.Scale(bounds.center,scale),rotation);
            if(parent!=null)instance.transform.SetParent(parent,true);
            instances++;return instance;
        }
        private static void Hide(MeshRenderer renderer)
        {
            Undo.RecordObject(renderer,"Replace blockout renderer with imported art");renderer.enabled=false;hidden++;
            changes.Add(renderer.name+" at "+renderer.transform.position);
        }
        private static void Replace(MeshRenderer renderer,string asset,Vector3? size=null,Quaternion? rotation=null)
        {Fit(asset,renderer.bounds.center,rotation??renderer.transform.rotation,size??Size(renderer));Hide(renderer);}

        private static void Wall(MeshRenderer renderer,bool rocky=false,bool battlements=false)
        {
            var size=Size(renderer);bool alongZ=size.z>size.x;
            float length=alongZ?size.z:size.x,depth=alongZ?size.x:size.z;
            var axis=alongZ?Vector3.forward:Vector3.right;
            var rotation=renderer.transform.rotation*Quaternion.Euler(0,alongZ?90:0,0);
            int count=Mathf.CeilToInt(length/(rocky?3.5f:4));float step=length/count;
            for(int i=0;i<count;i++)
            {
                var center=renderer.bounds.center+renderer.transform.rotation*(axis*(-length*.5f+step*(i+.5f)));
                if(rocky)Fit("SM_FR_WallStraight",center,rotation,new Vector3(step,size.y,depth*.72f));
                Fit(rocky?"SM_BS_StrataOutcrop":"SM_FR_WallStraight",center,rotation,new Vector3(step,size.y,depth));
                if(battlements)Fit("SM_FR_Battlement",center+Vector3.up*(size.y*.5f+.35f),rotation,new Vector3(step,.7f,depth));
            }
            Hide(renderer);
        }
        private static Material TeamMaterial(bool a)
        {
            if(!AssetDatabase.IsValidFolder(MaterialFolder))AssetDatabase.CreateFolder("Assets/Art/Materials/Environment","Battlefield");
            var path=MaterialFolder+"/M_FortressTeam"+(a?"A":"B")+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material!=null){material.SetFloat("_Cull",0);EditorUtility.SetDirty(material);return material;}
            ColorUtility.TryParseHtmlString(a?"#478DD9":"#D96B52",out var color);
            material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.1f);
            material.SetFloat("_Cull",0);
            AssetDatabase.CreateAsset(material,path);return material;
        }
        private static void Standards(Transform fortress)
        {
            bool a=fortress.name.Contains("Team A");var material=TeamMaterial(a);
            foreach(int side in new[]{-1,1})
            {
                var flag=Fit("SM_BS_ShatteredStandard",fortress.TransformPoint(new Vector3(8.5f,6.4f,11.5f*side)),fortress.rotation*Quaternion.Euler(0,90,0),new Vector3(1.1f,2,.45f));
                foreach(var renderer in flag.GetComponentsInChildren<MeshRenderer>(true))
                    if(renderer.name.Contains("_LOD"))renderer.sharedMaterials=BiomeSetBuilder.StandardMaterials(renderer.GetComponent<MeshFilter>().sharedMesh,material);
            }
            GateBadge(fortress);
        }
        private static void GateBadge(Transform fortress)
        {
            string path="Assets/Art/Environment/Meshes/FortressHeraldry.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null)
            {
                mesh=new Mesh{name="Fortress heraldry plate",vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)},triangles=new[]{0,1,2,0,2,3},uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up}};
                mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);
            }
            string crestPath=MaterialFolder+"/M_FortressCrest.mat";
            var crest=AssetDatabase.LoadAssetAtPath<Material>(crestPath);
            if(crest==null)
            {crest=new Material(Shader.Find("Universal Render Pipeline/Unlit"));ColorUtility.TryParseHtmlString("#C8B994",out var color);crest.SetColor("_BaseColor",color);AssetDatabase.CreateAsset(crest,crestPath);}
            bool a=fortress.name.Contains("Team A");
            var badge=new GameObject(a?"Team A gate chevrons":"Team B gate bars",typeof(MeshFilter),typeof(MeshRenderer));badge.transform.SetParent(visualRoot,false);
            badge.transform.SetPositionAndRotation(fortress.TransformPoint(new Vector3(9.7f,5,0)),fortress.rotation*Quaternion.Euler(0,90,0));badge.transform.localScale=new Vector3(1.5f,.85f,1);
            badge.GetComponent<MeshFilter>().sharedMesh=mesh;badge.GetComponent<MeshRenderer>().sharedMaterial=TeamMaterial(a);
            foreach(float y in a?new[]{-.14f,.14f}:new[]{-.23f,0,.23f})
            {
                var line=new GameObject(a?"Split chevron":"Three bar crest").AddComponent<LineRenderer>();line.transform.SetParent(badge.transform,false);line.useWorldSpace=false;line.sharedMaterial=crest;line.startWidth=line.endWidth=.045f;
                var points=a?new[]{new Vector3(-.22f,y,.015f),new Vector3(0,y+.18f,.015f),new Vector3(.22f,y,.015f)}:new[]{new Vector3(-.23f,y,.015f),new Vector3(.23f,y,.015f)};
                line.positionCount=points.Length;line.SetPositions(points);
            }
        }

        public static void RefineRidgesAndHeraldry()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Use Edit Mode.");
            var scene=SceneManager.GetActiveScene();var map=scene.GetRootGameObjects().Single(g=>g.name.StartsWith("Battlefield |"));
            visualRoot=map.transform.Find(RootName);if(visualRoot==null)throw new InvalidOperationException("Apply models first.");
            if(visualRoot.Find("Ridge stone cores")!=null)throw new InvalidOperationException("Refinement already applied.");
            var collision=Collision(map.transform);var gameplay=Gameplay(scene);
            var marker=new GameObject("Ridge stone cores");marker.transform.SetParent(visualRoot,false);
            foreach(var renderer in map.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name=="Boundary"||r.name=="Rotation ridge | solid"))
            {
                var size=Size(renderer);bool alongZ=size.z>size.x;float length=alongZ?size.z:size.x,depth=alongZ?size.x:size.z;
                var rotation=renderer.transform.rotation*Quaternion.Euler(0,alongZ?90:0,0);var axis=alongZ?Vector3.forward:Vector3.right;
                int count=Mathf.CeilToInt(length/3.5f);float step=length/count;
                for(int i=0;i<count;i++)Fit("SM_FR_WallStraight",renderer.bounds.center+renderer.transform.rotation*(axis*(-length*.5f+step*(i+.5f))),rotation,new Vector3(step,size.y,depth*.72f));
            }
            foreach(var flag in visualRoot.Cast<Transform>().Where(t=>t.name=="SM_BS_ShatteredStandard").ToArray())
            {
                var bounds=LocalVisualBounds(flag.gameObject);var center=flag.TransformPoint(bounds.center);var rotation=flag.rotation*Quaternion.Euler(0,90,0);
                flag.SetPositionAndRotation(center-rotation*Vector3.Scale(bounds.center,flag.localScale),rotation);
            }
            foreach(var fortress in map.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Team A Fortress"||t.name=="Team B Fortress"))GateBadge(fortress);
            if(!collision.SequenceEqual(Collision(map.transform))||!gameplay.SequenceEqual(Gameplay(scene)))throw new InvalidOperationException("Refinement changed gameplay collision/configuration.");
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
        }

        public static void RepairStandardMaterials()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Use Edit Mode.");
            string path=AssetPath("SM_BS_ShatteredStandard");var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.Contains("_LOD")))
                    renderer.sharedMaterials=BiomeSetBuilder.StandardMaterials(renderer.GetComponent<MeshFilter>().sharedMesh);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            var scene=SceneManager.GetActiveScene();var map=scene.GetRootGameObjects().Single(g=>g.name.StartsWith("Battlefield |"));
            visualRoot=map.transform.Find(RootName);
            foreach(var renderer in map.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.StartsWith("SM_BS_ShatteredStandard_LOD")))
            {
                Material cloth=renderer.transform.IsChildOf(visualRoot)?TeamMaterial(renderer.transform.position.x<0):null;
                renderer.sharedMaterials=BiomeSetBuilder.StandardMaterials(renderer.GetComponent<MeshFilter>().sharedMesh,cloth);
            }
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
        }
        private static string[] Gameplay(Scene scene)=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true))
            .Where(c=>c is NetworkCampSpawner||c is NetworkFortressSpawner||c is FortressAssaultSpawner||c is TeamSpawnService)
            .Select(c=>c.GetType().Name+EditorJsonUtility.ToJson(c)).ToArray();
        private static string[] Collision(Transform map)=>map.GetComponentsInChildren<Collider>(true)
            .Select(c=>GlobalObjectId.GetGlobalObjectIdSlow(c)+" "+EditorJsonUtility.ToJson(c)+" "+c.transform.localToWorldMatrix.ToString("F6")+" "+c.bounds).ToArray();

        public static void Apply()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.path!="Assets/Scenes/DragonlandBattlefield.unity"||scene.isDirty)
                throw new InvalidOperationException("Use the reviewed saved battlefield in Edit Mode.");
            var map=scene.GetRootGameObjects().Single(g=>g.name.StartsWith("Battlefield |"));
            if(map.transform.Find(RootName)!=null)throw new InvalidOperationException("Model replacement already exists; inspect instead of duplicating.");
            var gameplay=Gameplay(scene);var collision=Collision(map.transform);
            var renderers=map.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled&&r.GetComponent<MeshFilter>()!=null).ToArray();
            changes.Clear();instances=0;hidden=0;
            visualRoot=new GameObject(RootName).transform;visualRoot.SetParent(map.transform,false);Undo.RegisterCreatedObjectUndo(visualRoot.gameObject,"Replace battlefield models");
            foreach(var renderer in renderers)
            {
                string name=renderer.name;
                if(name=="Corner tower")
                {var size=Size(renderer);size.y=5.4f;Fit("SM_FR_Tower",new Vector3(renderer.bounds.center.x,2.7f,renderer.bounds.center.z),renderer.transform.rotation,size);Hide(renderer);}
                else if(name=="Team tower cap"||name.StartsWith("Gate shoulder"))Hide(renderer);
                else if(name=="Open gate lintel")
                {Fit("SM_FR_Gate",new Vector3(renderer.bounds.center.x,2.75f,renderer.bounds.center.z),renderer.transform.parent.rotation*Quaternion.Euler(0,90,0),new Vector3(8,5.5f,1.2f));Hide(renderer);}
                else if(name=="Rear wall"||name=="Front wall beside main gate"||name.StartsWith("Sally port"))Wall(renderer,false,true);
                else if(name=="King chamber rear"||name=="King chamber south"||name=="King chamber front shield"||name=="King chamber north shield")Wall(renderer);
                else if(name=="Chamber doorway lintel"||name=="North doorway lintel")Wall(renderer);
                else if(name.StartsWith("Outer cover"))Replace(renderer,"SM_FR_Crate");
                else if(name=="Rocky outcrop | non-traversable high ground")Replace(renderer,"SM_BS_StrataOutcrop");
                else if(name=="Rock cap")Hide(renderer);
                else if(name=="Broken ruin wall")Replace(renderer,"SM_FR_BrokenWall");
                else if(name=="Ruin pillar")Replace(renderer,"SM_FR_Pillar");
                else if(name=="Corrupted stone placeholder")Replace(renderer,"SM_BS_CorruptedSpire");
                else if(name=="Central ring flank cover")Wall(renderer);
                else if(name=="Boundary"||name=="Rotation ridge | solid")Wall(renderer,true);
                else if(name=="Camp perimeter marker")Replace(renderer,"SM_FR_Rubble01");
                else if(name=="Courtyard"||name=="Inner courtyard"||name=="King chamber floor")
                {
                    Undo.RecordObject(renderer,"Shared fortress paving");
                    renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Environment/Biomes/M_BS_BattlefieldPaving.mat");
                }
                else if(name=="Outer defensive zone")
                {
                    Undo.RecordObject(renderer,"Shared approach surface");
                    renderer.sharedMaterial=AncientForestAssetBuilder.LoadMaterial("M_AF_DirtPath");
                }
            }
            foreach(var fortress in map.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Team A Fortress"||t.name=="Team B Fortress"))Standards(fortress);
            // Public camp landmarks, outside the original perimeter; no new blocker or camp state.
            foreach(var camp in map.transform.Cast<Transform>().Where(t=>t.name.StartsWith("Camp ")))
            {
                var marker=camp.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r=>r.name=="Camp perimeter marker");
                if(marker==null)continue;
                var point=marker.transform.position;point.y=.5f;
                Fit("SM_AF_CampCairn01",point,Quaternion.identity,new Vector3(.75f,1,.65f));
            }
            if(!collision.SequenceEqual(Collision(map.transform))||!gameplay.SequenceEqual(Gameplay(scene)))
                throw new InvalidOperationException("Original collision/gameplay authoring changed during visual replacement.");
            Directory.CreateDirectory("Builds/MapModelReplacement");
            File.WriteAllLines("Builds/MapModelReplacement/replaced-renderers.txt",changes);
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
            Debug.Log("[Map models] Imported instances="+instances+", hidden blockout renderers="+hidden+"; original colliders and camp/fortress/spawn configuration unchanged. Review and save through MCP.");
        }

        public static void UpdateGatePrefabs()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Use Edit Mode.");
            foreach(string team in new[]{"TeamA","TeamB"})
            {
                string path="Assets/Game/Data/FortressAssault/"+team+"Gate.prefab";
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var body=root.transform.Find("Gate body");
                    if(body==null)throw new InvalidOperationException("Missing preserved gate body root.");
                    if(body.Find("Imported wooden gate")!=null)continue;
                    string collision=string.Join("\n",root.GetComponentsInChildren<Collider>(true).Select(c=>EditorJsonUtility.ToJson(c)));
                    visualRoot=root.transform;
                    var gate=Fit("SM_FR_WoodenBarricade",root.transform.TransformPoint(Vector3.up*2),root.transform.rotation*Quaternion.Euler(0,90,0),new Vector3(5.8f,4,1.2f),body);
                    gate.name="Imported wooden gate";body.GetComponent<MeshRenderer>().enabled=false;
                    if(collision!=string.Join("\n",root.GetComponentsInChildren<Collider>(true).Select(c=>EditorJsonUtility.ToJson(c))))throw new InvalidOperationException("Gate collision changed.");
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();Fusion.Editor.NetworkProjectConfigUtilities.RebuildPrefabTable();
            Debug.Log("[Map models] Both gate prefab GUIDs, authoritative components, body ownership and collision retained; imported art follows existing gate opening lifecycle.");
        }
    }
}
