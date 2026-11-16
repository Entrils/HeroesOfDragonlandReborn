using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace HeroesOfDragonland.Art.Editor
{
    // Unity exposes no public mixer authoring API. Keep the version-sensitive bridge here,
    // confined to Editor; never emit or rewrite mixer YAML. See AudioSystem.md for manual fallback.
    internal static class AudioMixerFactory
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static Type Controller => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("UnityEditor.Audio.AudioMixerController")).FirstOrDefault(t => t != null)
            ?? throw new InvalidOperationException("Unity mixer authoring API unavailable. Create the documented mixer/groups/exposed parameters through MCP, then rerun.");
        private static object Call(object target, string method, params object[] args)
        {
            var type = target as Type ?? target.GetType();
            var candidates = type.GetMethods(Flags).Where(m => m.Name == method && m.GetParameters().Length == args.Length).ToArray();
            if (candidates.Length != 1) throw new InvalidOperationException("Mixer API changed: " + method + ". Use the documented MCP mixer setup.");
            return candidates[0].Invoke(target is Type ? null : target, args);
        }
        internal static AudioMixer Ensure(string path)
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
            if (mixer != null && new[] { "Master", "Music", "SFX", "UI", "Ambience", "Voice" }.All(n => mixer.FindMatchingGroups("").Any(g => g.name == n))
                && new[] { "MasterVolumeDb", "MusicVolumeDb", "SfxVolumeDb" }.All(n => mixer.GetFloat(n, out _))) return mixer;
            mixer ??= (AudioMixer)Call(Controller, "CreateMixerControllerAtPath", path);
            var master = mixer.GetType().GetProperty("masterGroup", Flags)?.GetValue(mixer)
                ?? throw new InvalidOperationException("Cannot find mixer Master. Use MCP manual setup.");
            foreach (string name in new[] { "Music", "SFX", "UI", "Ambience", "Voice" })
            {
                if (mixer.FindMatchingGroups("").Any(g => g.name == name)) continue;
                var group = Call(mixer, "CreateNewGroup", name, false);
                Call(mixer, "AddChildToParent", group, master);
            }
            Expose(mixer, "Master", "MasterVolumeDb");
            Expose(mixer, "Music", "MusicVolumeDb");
            Expose(mixer, "SFX", "SfxVolumeDb");
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path)) EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets(); return mixer;
        }
        private static void Expose(AudioMixer mixer, string groupName, string parameter)
        {
            var group = mixer.FindMatchingGroups("").Single(g => g.name == groupName);
            object guid = Call(group, "GetGUIDForVolume");
            var property = mixer.GetType().GetProperty("exposedParameters", Flags)
                ?? throw new InvalidOperationException("Mixer exposed-parameter API changed. Use MCP manual setup.");
            var old = (Array)property.GetValue(mixer); var element = old.GetType().GetElementType();
            var nameField = element.GetField("name", Flags); var guidField = element.GetField("guid", Flags);
            if (nameField == null || guidField == null) throw new InvalidOperationException("Mixer parameter fields changed. Use MCP manual setup.");
            foreach (var value in old)
            {
                bool sameName = (string)nameField.GetValue(value) == parameter, sameGuid = guidField.GetValue(value).Equals(guid);
                if (sameName && sameGuid) return;
                if (sameName || sameGuid) throw new InvalidOperationException("Preserved authored mixer parameter conflicts with " + parameter + ". Resolve its binding through MCP.");
            }
            var updated = Array.CreateInstance(element, old.Length + 1); Array.Copy(old, updated, old.Length);
            var added = Activator.CreateInstance(element); nameField.SetValue(added, parameter); guidField.SetValue(added, guid);
            updated.SetValue(added, old.Length); property.SetValue(mixer, updated);
        }
        internal static void ValidateVolumeBindings(AudioMixer mixer)
        {
            var property = mixer.GetType().GetProperty("exposedParameters", Flags)
                ?? throw new InvalidOperationException("Cannot inspect mixer parameter bindings on this Unity version. Inspect through MCP.");
            var parameters = (Array)property.GetValue(mixer);
            var type = parameters.GetType().GetElementType();
            var name = type.GetField("name", Flags); var guid = type.GetField("guid", Flags);
            if (name == null || guid == null) throw new InvalidOperationException("Cannot inspect mixer parameter fields. Inspect through MCP.");
            string[] groups = { "Master", "Music", "SFX" }, names = { "MasterVolumeDb", "MusicVolumeDb", "SfxVolumeDb" };
            for (int i = 0; i < groups.Length; i++)
            {
                var group = mixer.FindMatchingGroups("").Single(g => g.name == groups[i]);
                var expected = Call(group, "GetGUIDForVolume"); bool found = false;
                foreach (var parameter in parameters)
                    if ((string)name.GetValue(parameter) == names[i] && guid.GetValue(parameter).Equals(expected)) found = true;
                if (!found) throw new InvalidOperationException("Expose Volume of " + groups[i] + " as " + names[i] + " through MCP.");
            }
        }
    }
}
