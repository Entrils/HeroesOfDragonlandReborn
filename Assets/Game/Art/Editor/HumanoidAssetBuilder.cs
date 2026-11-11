using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HeroesOfDragonland.Art.Editor
{
    public static class HumanoidAssetBuilder
    {
        public const string ModelPath="Assets/Art/Characters/Models/SK_PrototypeHumanoid.fbx";
        public const string VisualPath="Assets/Art/Characters/Prefabs/PV_PrototypeHumanoid.prefab";
        public static readonly string[] Clips={"Idle","Walk","Run","Backward","StrafeLeft","StrafeRight","Jump","Fall","Land","Attack","LightAttack","HeavyAttack","Block","HitReaction","Stagger","Dodge","Death"};
        public static void Folder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            Folder(System.IO.Path.GetDirectoryName(path).Replace('\\','/'));
            AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(path).Replace('\\','/'),System.IO.Path.GetFileName(path));
        }
        public static Avatar BuildRigAndClips(out AnimationClip[] clips)
        {
            foreach(string path in new[]{"Assets/Art/Characters/Prefabs","Assets/Art/Materials/Characters","Assets/Art/Animations/PrototypeHumanoid"})Folder(path);
            AssetDatabase.Refresh();
            var importer=AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if(importer==null)throw new InvalidOperationException("Generate the Blender humanoid first.");
            ArtAssetPostprocessor.ConfigureModel(importer,ModelPath);
            importer.animationType=ModelImporterAnimationType.Human; importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation=false; importer.optimizeGameObjects=false; importer.preserveHierarchy=true; importer.SaveAndReimport();
            var description=importer.humanDescription;
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var names=new HashSet<string>(model.GetComponentsInChildren<Transform>().Select(t=>t.name));
            description.human=HumanTrait.BoneName.Where(name=>names.Contains(name.Replace(" ",""))).Select(name=>new HumanBone {
                humanName=name, boneName=name.Replace(" ",""), limit=new HumanLimit {useDefaultValues=true}
            }).ToArray();
            description.armStretch=.05f; description.legStretch=.05f; description.upperArmTwist=.5f; description.lowerArmTwist=.5f;
            description.upperLegTwist=.5f; description.lowerLegTwist=.5f;
            importer.humanDescription=description; importer.SaveAndReimport();
            var avatar=AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            if(avatar==null || !avatar.isValid || !avatar.isHuman)throw new InvalidOperationException("Humanoid Avatar validation failed.");
            var imported=new List<AnimationClip>();
            foreach(string kind in Clips)
            {
                string path="Assets/Art/Animations/PrototypeHumanoid/FBX/AN_PrototypeHumanoid_"+kind+".fbx";
                var animation=AssetImporter.GetAtPath(path) as ModelImporter;
                if(animation==null)throw new InvalidOperationException("Missing "+path);
                animation.animationType=ModelImporterAnimationType.Human; animation.avatarSetup=ModelImporterAvatarSetup.CopyFromOther;
                animation.sourceAvatar=avatar; animation.importAnimation=true; animation.optimizeGameObjects=false;
                animation.preserveHierarchy=true;
                animation.globalScale=1; animation.useFileScale=true; animation.bakeAxisConversion=true;
                animation.materialImportMode=ModelImporterMaterialImportMode.None; animation.importCameras=false; animation.importLights=false;
                animation.SaveAndReimport();
                var ranges=animation.defaultClipAnimations;
                if(ranges.Length==0)throw new InvalidOperationException("No animation take: "+kind);
                var range=ranges[0]; range.name="AN_PrototypeHumanoid_"+kind;
                range.loopTime=new[]{"Idle","Walk","Run","Backward","StrafeLeft","StrafeRight","Fall","Block","Stagger"}.Contains(kind);
                range.lockRootPositionXZ=true; range.lockRootHeightY=true; range.lockRootRotation=true;
                range.keepOriginalOrientation=true; range.keepOriginalPositionY=true; range.keepOriginalPositionXZ=true;
                if(kind=="Walk"||kind=="Run"||kind=="Backward"||kind.StartsWith("Strafe"))
                    range.events=new[]{new AnimationEvent{functionName="Footstep",time=.25f},new AnimationEvent{functionName="Footstep",time=.75f}};
                else if(kind=="LightAttack"||kind=="HeavyAttack")
                    range.events=new[]{new AnimationEvent{functionName="WeaponTrailOn",time=.15f},new AnimationEvent{functionName="Impact",time=.4f},new AnimationEvent{functionName="WeaponTrailOff",time=.7f}};
                else if(kind=="Dodge")range.events=new[]{new AnimationEvent{functionName="DodgeWhoosh",time=.1f}};
                animation.clipAnimations=new[]{range}; animation.SaveAndReimport();
                var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
                if(!clip.humanMotion)throw new InvalidOperationException("Clip is not Humanoid: "+kind);
                imported.Add(clip);
            }
            clips=imported.ToArray(); return avatar;
        }
        public static Material Material(string name,Color color)
        {
            string path="Assets/Art/Materials/Characters/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material!=null)return material;
            material=new Material(Shader.Find("Universal Render Pipeline/Lit")); material.SetColor("_BaseColor",color); material.SetFloat("_Smoothness",.25f);
            AssetDatabase.CreateAsset(material,path); return material;
        }
    }
}
