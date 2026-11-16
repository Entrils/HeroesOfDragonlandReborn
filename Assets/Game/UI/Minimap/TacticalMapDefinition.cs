using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfDragonland.UI.Minimap
{
    [CreateAssetMenu(menuName = "Heroes of Dragonland/UI/Tactical Map")]
    public sealed class TacticalMapDefinition : ScriptableObject
    {
        [Serializable] public sealed class Landmark
        {
            public string id, label;
            public MapMarkerKind kind;
            public MapTeam team;
            public Vector3 position;
        }
        [Serializable] public sealed class Region
        {
            public string label;
            public Vector2 center, size;
            public Color color = new Color(.15f, .19f, .16f);
        }
        [SerializeField] private Vector2 center;
        [SerializeField] private Vector2 size = new Vector2(88, 88);
        [SerializeField] private Landmark[] landmarks;
        [SerializeField] private Region[] regions;
        public Vector2 Center => center;
        public Vector2 Size => size;
        public IReadOnlyList<Landmark> Landmarks => landmarks ?? Array.Empty<Landmark>();
        public IReadOnlyList<Region> Regions => regions ?? Array.Empty<Region>();
        public bool Validate(out string error)
        {
            error = null; var ids = new HashSet<string>();
            if (size.x <= 0 || size.y <= 0 || !TacticalMapRules.Finite(size.x) || !TacticalMapRules.Finite(size.y)) { error = "Invalid map bounds."; return false; }
            if (!TacticalMapRules.Finite(center.x) || !TacticalMapRules.Finite(center.y) || Landmarks.Count > 48) { error = "Invalid center/landmark budget."; return false; }
            foreach (var marker in Landmarks)
            {
                if (marker == null || string.IsNullOrWhiteSpace(marker.id) || !ids.Add(marker.id)
                    || (marker.kind != MapMarkerKind.Fortress && marker.kind != MapMarkerKind.Camp && marker.kind != MapMarkerKind.BossCamp && marker.kind != MapMarkerKind.Objective)
                    || !TacticalMapRules.Project(new MapPoint(marker.position.x, marker.position.y, marker.position.z), center.x, center.y, size.x, size.y, out _, out _))
                { error = "Invalid/duplicate/out-of-bounds public landmark."; return false; }
            }
            if (Regions.Count > 128) { error = "Region drawing budget exceeded."; return false; }
            foreach (var region in Regions)
                if (region == null || !TacticalMapRules.Finite(region.center.x) || !TacticalMapRules.Finite(region.center.y)
                    || !TacticalMapRules.Finite(region.size.x) || !TacticalMapRules.Finite(region.size.y) || region.size.x <= 0 || region.size.y <= 0)
                { error = "Invalid public geometry region."; return false; }
            return true;
        }
    }
}
