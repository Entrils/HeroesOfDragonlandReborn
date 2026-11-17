using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeroesOfDragonland.Art.Editor
{
    public static class EnvironmentKitPlayValidation
    {
        public static string Status { get; private set; }="Not run";
        private static GameObject Spawn(Scene scene,string name,Vector3 position)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Environment/Prefabs/FortressRuins/SM_FR_"+name+".prefab");
            var obj=UnityEngine.Object.Instantiate(prefab);SceneManager.MoveGameObjectToScene(obj,scene);obj.transform.position=position;return obj;
        }
        private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException("M55 physics: "+reason);}
        public static void Run()
        {
            Check(Application.isPlaying,"Use Play Mode");Status="RUNNING";
            var scene=SceneManager.CreateScene("Temporary M55 native physics",new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            try
            {
                var physics=scene.GetPhysicsScene();
                Spawn(scene,"Gate",Vector3.zero);Spawn(scene,"Arch",new Vector3(12,0,0));
                var wall=Spawn(scene,"WallStraight",new Vector3(24,0,0));
                Spawn(scene,"FloorSection",new Vector3(36,0,0));Spawn(scene,"Stairs",new Vector3(48,0,0));
                physics.Simulate(.02f);
                Check(!physics.Raycast(new Vector3(0,1,-4),Vector3.forward,out _,8),"Gate opening blocked");
                Check(!physics.Raycast(new Vector3(12,1,-4),Vector3.forward,out _,8),"Arch opening blocked");
                Check(physics.Raycast(new Vector3(24,1,-4),Vector3.forward,out _,8),"Wall collision missing");
                wall.GetComponent<LODGroup>().ForceLOD(1);
                Check(physics.Raycast(new Vector3(24,1,-4),Vector3.forward,out _,8),"LOD change lost wall collision");
                float previous=-1;
                foreach(float z in new[]{-1.5f,-.5f,.5f,1.5f})
                {
                    Check(physics.Raycast(new Vector3(48,5,z),Vector3.down,out var hit,6),"Stair collision missing");
                    Check(hit.point.y>previous,"Stair collision must rise toward +Z");previous=hit.point.y;
                }
                var ball=GameObject.CreatePrimitive(PrimitiveType.Sphere);SceneManager.MoveGameObjectToScene(ball,scene);
                ball.transform.position=new Vector3(36,3,0);ball.transform.localScale=Vector3.one*.5f;
                var body=ball.AddComponent<Rigidbody>();body.sleepThreshold=0;
                for(int i=0;i<150;i++)physics.Simulate(.02f);
                Check(Mathf.Abs(ball.transform.position.y-.25f)<.08f,"Native rigidbody did not settle on floor top");
                Status="PASS gate/arch opening rays, wall/LOD collision, ascending stair geometry and native sphere/floor settling in isolated physics scene.";
                Debug.Log("[M55] "+Status);
            }
            finally{SceneManager.UnloadSceneAsync(scene);if(Status=="RUNNING")Status="FAILED; inspect Console";}
        }
    }
}
