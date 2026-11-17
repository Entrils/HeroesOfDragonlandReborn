using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.Art.Editor
{
    /// <summary>Static M54 review fixture; never changes gameplay scenes or source prefabs.</summary>
    public static class VisualDirectionBenchmarkSetup
    {
        public const string ScenePath = "Assets/Art/Environment/Scenes/VisualDirectionBenchmark.unity";
        private const string Materials = "Assets/Art/Materials/Benchmarks";
        private static Material stone, wood, bone, danger, dark, brass, crestMaterial;

        private static Material Material(string name, string hex)
        {
            var path = Materials + "/M54_" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            material = new Material(Shader.Find(name == "Danger" || name == "Dark" || name == "Crest" ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness", .15f);
            AssetDatabase.CreateAsset(material, path); return material;
        }
        private static GameObject Box(string name, Vector3 position, Vector3 size, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name;
            obj.transform.position = position; obj.transform.localScale = size;
            UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            obj.GetComponent<Renderer>().sharedMaterial = material; return obj;
        }
        private static void Line(string name, Vector3[] points, Material material, float width = .035f)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.positionCount = points.Length; line.SetPositions(points);
            line.startWidth = line.endWidth = width; line.useWorldSpace = true;
        }
        private static void Label(string label, Vector3 position)
        {
            var text = new GameObject(label).AddComponent<TextMesh>(); text.text = label;
            text.transform.position = position; text.characterSize = label.StartsWith("TEAM ") ? .05f : label.StartsWith("STATIC WARNING") ? .04f : .07f; text.fontSize = 48;
            text.anchor = TextAnchor.MiddleCenter; text.color = new Color(.9f,.87f,.76f);
            text.transform.rotation = Quaternion.identity;
        }
        private static void Crest(Vector3 center, bool teamA)
        {
            if (teamA)
                foreach (float offset in new[] { -.14f,.14f })
                    Line("A split chevron", new[] { center+new Vector3(-.18f,offset,0),center+new Vector3(0,.2f+offset,0),center+new Vector3(.18f,offset,0) },crestMaterial,.04f);
            else foreach(float offset in new[] { -.17f,0,.17f })
                Box("B three bars", center+new Vector3(0,offset,0),new Vector3(.38f,.065f,.025f),crestMaterial);
        }
        private static GameObject Visual(string path, string name, Vector3 position, Material material)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab == null) throw new InvalidOperationException("Missing existing visual: "+path);
            var root = UnityEngine.Object.Instantiate(prefab); root.name = name; root.SetActive(false);
            foreach(var component in root.GetComponentsInChildren<Component>(true))
                if(component != null && !(component is Transform) && !(component is Renderer) && !(component is MeshFilter))
                    UnityEngine.Object.DestroyImmediate(component);
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_=>material).ToArray();
            root.transform.position=position; root.SetActive(true); return root;
        }
        private static Camera Camera(string name, Vector3 position, Vector3 target, bool active)
        {
            var camera = new GameObject(name).AddComponent<Camera>(); camera.fieldOfView=60;
            camera.transform.position=position; camera.transform.LookAt(target); camera.farClipPlane=200;
            camera.enabled=active; camera.backgroundColor=new Color(.38f,.43f,.48f); camera.clearFlags=CameraClearFlags.SolidColor;
            return camera;
        }
        public static void Build()
        {
            if(Application.isPlaying || EditorApplication.isCompiling) throw new InvalidOperationException("Use compiled Edit Mode.");
            if(File.Exists(ScenePath)) throw new InvalidOperationException("Inspect existing benchmark instead of overwriting it.");
            if(!AssetDatabase.IsValidFolder(Materials)) AssetDatabase.CreateFolder("Assets/Art/Materials","Benchmarks");
            stone=Material("Stone","97988B");wood=Material("Wood","65594B");bone=Material("Bone","C8B994");
            danger=Material("Danger","FF913D");dark=Material("Dark","252B30");brass=Material("Brass","D6B66F");
            crestMaterial=Material("Crest","C8B994");
            var ground=Material("Ground","626B70"); var a=Material("TeamA","478DD9");var b=Material("TeamB","D96B52");
            var previous=SceneManager.GetActiveScene(); var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                Box("Low noise ground",new Vector3(0,-.15f,5),new Vector3(28,.3f,24),ground);
                foreach(int sign in new[]{-1,1})
                {
                    float x=sign*4; bool teamA=sign<0; var team=teamA?a:b;
                    Visual("Assets/Art/Characters/Prefabs/PV_PrototypeHumanoid.prefab","Existing humanoid team "+(teamA?"A":"B"),new Vector3(x,0,1),stone);
                    foreach(float z in new[]{.8f,1.2f})
                    { Box("Team field",new Vector3(x,1.18f,z),new Vector3(.44f,.52f,.025f),team);Crest(new Vector3(x,1.18f,z+(z<1?-.025f:.025f)),teamA); }
                    Label("TEAM "+(teamA?"A":"B"),new Vector3(x,2.2f,.65f));
                    foreach(float dx in new[]{-1.8f,1.8f})Box("Matched gate pier",new Vector3(x+dx,1.8f,6),new Vector3(1,3.6f,1),stone);
                    Box("Matched gate lintel",new Vector3(x,3.7f,6),new Vector3(4.6f,.6f,1),stone);
                    Box("Team banner",new Vector3(x,3.3f,5.45f),new Vector3(.8f,.9f,.04f),team);Crest(new Vector3(x,3.3f,5.4f),teamA);
                    Visual("Assets/Art/Models/Weapons/Sword.prefab","Existing sword",new Vector3(x+.6f,.8f,1),brass);
                }
                Visual("Assets/Art/Characters/Prefabs/PV_PrototypeHumanoid.prefab","Neutral sample",new Vector3(0,0,4),bone);
                var boss=Visual("Assets/Art/Characters/Prefabs/PV_PrototypeHumanoid.prefab","Warden silhouette stand-in",new Vector3(0,0,11),stone);
                boss.transform.localScale=Vector3.one*1.7f;
                foreach(float dx in new[]{-.35f,0,.35f})Box("Stand-in crown",new Vector3(dx,3.1f,11),new Vector3(.18f,.4f,.35f),bone);
                Visual("Assets/Art/Environment/Prefabs/SM_TestCrate.prefab","Existing 1m crate",new Vector3(-8,0,1),wood);
                Box("Chest stand-in",new Vector3(8,.4f,1),new Vector3(1.15f,.8f,.75f),wood);
                var lid=GameObject.CreatePrimitive(PrimitiveType.Sphere);lid.name="Rounded chest lid";lid.transform.position=new Vector3(8,.8f,1);lid.transform.localScale=new Vector3(1.16f,.64f,.76f);
                UnityEngine.Object.DestroyImmediate(lid.GetComponent<Collider>());lid.GetComponent<Renderer>().sharedMaterial=wood;
                Box("Chest lock",new Vector3(8,.65f,.6f),new Vector3(.18f,.25f,.08f),brass);
                for(int tier=1;tier<=4;tier++)
                {
                    float x=5+tier*2; Box("Camp cairn",new Vector3(x,.75f,8),new Vector3(.8f,1.5f,.6f),stone);
                    for(int notch=0;notch<(tier==4?3:tier);notch++)Box("Tier notch",new Vector3(x,.4f+notch*.3f,7.68f),new Vector3(.6f,.12f,.02f),dark);
                    if(tier==4)Box("Boss cairn crown",new Vector3(x,1.65f,8),new Vector3(.5f,.3f,.4f),bone);
                    Label(tier==4?"BOSS":"T"+tier,new Vector3(x,2,8));
                }
                foreach(float x in new[]{-2f,2f})
                {
                    var points=Enumerable.Range(0,65).Select(i=>new Vector3(x+Mathf.Cos(i*Mathf.PI/32)*1.5f,.04f,4+Mathf.Sin(i*Mathf.PI/32)*1.5f)).ToArray();
                    if(x>0)
                    {
                        points=Enumerable.Range(0,65).Select(i=>new Vector3(x,.04f,4)+Quaternion.AngleAxis(-60+120f*i/64,Vector3.up)*Vector3.forward*1.5f).ToArray();
                        points=points.Concat(new[]{new Vector3(x,.04f,4),points[0]}).ToArray();
                    }
                    Line("Warning dark backing",points,dark,.12f);Line("Warning true boundary",points.Select(p=>p+Vector3.up*.01f).ToArray(),danger,.06f);
                    Label("STATIC WARNING",new Vector3(x,.1f,1));
                }
                var sun=new GameObject("Overcast key").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.1f;sun.transform.rotation=Quaternion.Euler(50,-30,0);
                RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.59f,.63f);
                Camera("Overview",new Vector3(19,16,-19),new Vector3(0,1,5),true);
                Camera("Combat 5m",new Vector3(0,2,-4),new Vector3(0,1,1),false);
                Camera("Distance 30m",new Vector3(10,2,-22),new Vector3(10,1,8),false);
                if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new InvalidOperationException("Benchmark save failed.");
                AssetDatabase.SaveAssets();Debug.Log("[M54] Static benchmark saved; perceptual and moving gameplay acceptance remains pending.");
            }
            finally { SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true); }
        }
    }
}
