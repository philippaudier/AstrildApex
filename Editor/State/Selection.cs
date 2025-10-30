using System;
using System.Collections.Generic;
using System.Numerics;
using Engine.Scene;

namespace Editor.State
{
    public static class Selection
    {
        // ===== ENTITIES (existant) =====
        public static readonly HashSet<uint> Selected = new();
        public static uint ActiveEntityId = 0;

        public static void SetSingle(uint id)
        {
            Selected.Clear();
            if (id != 0) Selected.Add(id);
            ActiveEntityId = id;

            // Si on choisit une entité, on sort du mode "asset"
            ClearAsset();
        }

        public static void AddMany(IEnumerable<uint> ids)
        {
            foreach (var id in ids) if (id != 0) Selected.Add(id);
            // Si on n'avait pas d'entité active, prendre la première ajoutée
            if (ActiveEntityId == 0 && Selected.Count > 0)
                ActiveEntityId = Selected.First();
        }

        public static void ReplaceMany(IEnumerable<uint> ids)
        {
            Selected.Clear();
            AddMany(ids);
            // Définir le premier ID comme actif pour afficher le gizmo
            ActiveEntityId = Selected.Count > 0 ? Selected.First() : 0;
            ClearAsset();
        }

        public static void Toggle(uint id)
        {
            if (id == 0) return;
            if (Selected.Contains(id)) 
            {
                Selected.Remove(id);
                // Si on supprime l'entité active, prendre une autre
                if (ActiveEntityId == id)
                    ActiveEntityId = Selected.Count > 0 ? Selected.First() : 0;
            }
            else 
            {
                Selected.Add(id);
                // Si on n'avait pas d'entité active, prendre celle-ci
                if (ActiveEntityId == 0)
                    ActiveEntityId = id;
            }
        }

        public static void Clear()
        {
            Selected.Clear();
            ActiveEntityId = 0;
            // NOTE: Clear() ne touche PAS aux assets — comme Unity,
            // un clic dans la hiérarchie n’efface pas la sélection Project.
        }

        public static Vector3 ComputeCenter(Scene scene)
        {
            if (Selected.Count == 0) return Vector3.Zero;
            var sum = Vector3.Zero; int n = 0;
            foreach (var id in Selected)
            {
                var e = scene.GetById(id);
                if (e == null) continue;
                e.GetWorldTRS(out var p, out _, out _);
                sum += new Vector3(p.X, p.Y, p.Z); n++;
            }
            return (n > 0) ? (sum / n) : Vector3.Zero;
        }

        // ===== ASSETS (nouveau) =====
        public static Guid ActiveAssetGuid { get; private set; } = Guid.Empty;
        public static string? ActiveAssetType { get; private set; }

        public static bool HasAsset => ActiveAssetGuid != Guid.Empty;

        public static void SetActiveAsset(Guid guid, string? type)
        {
            ActiveAssetGuid = guid;
            ActiveAssetType = type;
            // En mode asset, on vide la sélection d'entités pour un Inspector clair
            Clear();
        }

        public static void ClearAsset()
        {
            ActiveAssetGuid = Guid.Empty;
            ActiveAssetType = null;
        }

        public static void ClearAll()
        {
            Clear();
            ClearAsset();
        }

        public static bool Contains(uint id)
        {
            return Selected.Contains(id);
        }
    }
}
