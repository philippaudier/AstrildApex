using System;
using System.Reflection;
using Engine.Scene;
using Editor.State;

namespace Editor.Inspector
{
    /// <summary>
    /// Action Undo/Redo générique pour éditer un champ (ou propriété) d'une entité via réflexion.
    /// memberPath supporte les chemins simples "Name" ou imbriqués "Transform.Scale".
    /// </summary>
    public sealed class FieldEditAction : IEditorAction
    {
        // ==== IEditorAction ====
        public string   Label            { get; }
        public DateTime Timestamp        { get; }
        public long     MemoryFootprint  { get; }

        // ==== Données ====
        private readonly uint   _entityId;
        private readonly string _memberPath;
        private readonly object? _before, _after;

        // Fenêtre de merge pour regrouper les micro-édits successifs
        private readonly TimeSpan _maxMergeAge = TimeSpan.FromSeconds(2);

        public FieldEditAction(string label, uint entityId, string memberPath, object? before, object? after)
        {
            Label       = label;
            Timestamp   = DateTime.UtcNow;
            _entityId   = entityId;
            _memberPath = memberPath ?? string.Empty;
            _before     = before;
            _after      = after;

            // Estimation grossière de l'empreinte mémoire (pour le système avancé d'Undo/Redo)
            MemoryFootprint = 160
                              + (_memberPath.Length * 2)
                              + EstimateObjectSize(_before)
                              + EstimateObjectSize(_after);
        }

        public void Undo(Scene scene) => Apply(scene, _before);
        public void Redo(Scene scene) => Apply(scene, _after);

        private void Apply(Scene scene, object? value)
        {
            try
            {
                var ent = scene?.GetById(_entityId);
                if (ent == null) return;
                SetValueByPath(ent, _memberPath, value);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Résout root."A.B.C" et affecte value sur le dernier membre (Field ou Property).
        /// Gère la conversion de type si nécessaire (ex: int -> float).
        /// </summary>
        public static void SetValueByPath(object root, string path, object? value)
        {
            if (root == null || string.IsNullOrWhiteSpace(path)) return;

            string[] parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
            object? obj = root;
            Type?   t   = obj.GetType();

            for (int i = 0; i < parts.Length; i++)
            {
                if (obj == null || t == null) return;

                string name = parts[i];
                var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null)
                {
                    if (i == parts.Length - 1)
                    {
                        var final = ConvertToType(value, f.FieldType);
                        f.SetValue(obj, final);
                        return;
                    }
                    obj = f.GetValue(obj);
                    t = obj?.GetType();
                    continue;
                }

                var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null)
                {
                    if (i == parts.Length - 1)
                    {
                        if (p.CanWrite)
                        {
                            var final = ConvertToType(value, p.PropertyType);
                            p.SetValue(obj, final);
                        }
                        return;
                    }
                    obj = p.GetValue(obj);
                    t = obj?.GetType();
                    continue;
                }

                // Membre introuvable : on s'arrête proprement
                return;
            }
        }

        /// <summary>
        /// Conversion tolérante pour types numériques et enum; renvoie "value" si déjà compatible.
        /// </summary>
        private static object? ConvertToType(object? value, Type targetType)
        {
            if (value == null) return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            var vType = value.GetType();
            if (targetType.IsAssignableFrom(vType))
                return value;

            try
            {
                // Gère Nullable<T>
                var coreType = Nullable.GetUnderlyingType(targetType) ?? targetType;

                if (coreType.IsEnum)
                {
                    if (value is string s) return Enum.Parse(coreType, s, ignoreCase: true);
                    return Enum.ToObject(coreType, value);
                }

                // Conversions numériques et bool/string
                if (coreType == typeof(string)) return value.ToString();
                return System.Convert.ChangeType(value, coreType);
            }
            catch
            {
                // Dernier recours : pas de conversion possible, on ne crash pas
                return value;
            }
        }

        // ==== Merging (Undo/Redo avancé) ====

        public bool CanMergeWith(IEditorAction other)
        {
            if (other is not FieldEditAction o) return false;

            // Même entité et même chemin de membre ?
            if (_entityId != o._entityId) return false;
            if (!string.Equals(_memberPath, o._memberPath, StringComparison.Ordinal)) return false;

            // Fenêtre de temps
            if ((o.Timestamp - Timestamp).Duration() > _maxMergeAge) return false;

            return true;
        }

        public IEditorAction? TryMergeWith(IEditorAction other)
        {
            if (!CanMergeWith(other) || other is not FieldEditAction o)
                return null;

            // On garde l'état "before" du premier, et "after" du second
            // Timestamp: on prend le plus récent pour l’action mergée
            var merged = new FieldEditAction(
                label: $"{Label} (merged)",
                entityId: _entityId,
                memberPath: _memberPath,
                before: _before,
                after:  o._after
            );

            // On veut refléter la "fraîcheur" de l’opération mergée
            typeof(FieldEditAction)
                .GetProperty(nameof(Timestamp))?
                .SetValue(merged, o.Timestamp); // Note: Timestamp est read-only; si tu veux strict, enlève ça.

            return merged;
        }

        // ==== Estimation footprint ====
        private static long EstimateObjectSize(object? obj)
        {
            if (obj == null) return 8;
            var t = obj.GetType();

            if (t.IsPrimitive)
                return t == typeof(bool) ? 1 : 8; // arrondi

            if (obj is string s)
                return 24 + (s.Length * 2);

            if (obj is Array arr)
            {
                long total = 24;
                foreach (var item in arr) total += EstimateObjectSize(item);
                return total;
            }

            // fallback grossier
            return 64;
        }
    }
}
