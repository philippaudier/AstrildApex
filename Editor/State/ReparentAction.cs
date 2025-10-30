using System;
using Engine.Scene;

namespace Editor.State
{
    /// <summary>
    /// Action Undo/Redo pour changer le parent d'une entité.
    /// - Conserve le transform en monde (keepWorld=true).
    /// - Merge les reparentings successifs (même entité) dans une fenêtre temporelle courte.
    /// </summary>
    public sealed class ReparentAction : IEditorAction
    {
        // ==== IEditorAction ====
        public string   Label           { get; }
        public DateTime Timestamp       { get; }
        public long     MemoryFootprint { get; }

        // ==== Données ====
        private readonly uint  _entityId;
        private readonly uint? _oldParentId;
        private readonly uint? _newParentId;

        // Fenêtre de merge pour "glisser" un objet dans l'arborescence sans spammer l'historique
        private readonly TimeSpan _maxMergeAge = TimeSpan.FromSeconds(2);

        public ReparentAction(string label, uint entityId, uint? oldParentId, uint? newParentId)
        {
            Label        = label;
            Timestamp    = DateTime.UtcNow;
            _entityId    = entityId;
            _oldParentId = oldParentId;
            _newParentId = newParentId;

            // Estimation grossière (pour tri/GC de l'historique si besoin)
            // On reste volontairement simple: quelques champs scalaires.
            MemoryFootprint = 64;
        }

        public void Undo(Scene scene)
        {
            try
            {
                var e = scene?.GetById(_entityId);
                if (e == null) return;

                var p = (_oldParentId.HasValue && scene != null) ? scene.GetById(_oldParentId.Value) : null;
                e.SetParent(p, keepWorld: true);

                UndoRedo.RaiseAfterChange();
            }
            catch (Exception)
            {
            }
        }

        public void Redo(Scene scene)
        {
            try
            {
                var e = scene?.GetById(_entityId);
                if (e == null) return;

                var p = (_newParentId.HasValue && scene != null) ? scene.GetById(_newParentId.Value) : null;
                e.SetParent(p, keepWorld: true);

                UndoRedo.RaiseAfterChange();
            }
            catch (Exception)
            {
            }
        }

        // ==== Merge logique ====

        public bool CanMergeWith(IEditorAction other)
        {
            if (other is not ReparentAction o) return false;

            // On ne merge que si c'est la même entité
            if (_entityId != o._entityId) return false;

            // Fenêtre temporelle
            if ((o.Timestamp - Timestamp).Duration() > _maxMergeAge) return false;

            // Si l'action suivante remet EXACTEMENT le parent d'origine, on accepte le merge
            // (ça permettra de compacter des va-et-vient en un seul bloc).
            return true;
        }

        public IEditorAction? TryMergeWith(IEditorAction other)
        {
            if (!CanMergeWith(other) || other is not ReparentAction o)
                return null;

            // On garde l'ancien parent du premier et le nouveau parent du second
            // Ça compresse une séquence: A->B, B->C, C->D ... en A->D
            var merged = new ReparentAction(
                label: $"{Label} (merged)",
                entityId: _entityId,
                oldParentId: _oldParentId,
                newParentId: o._newParentId
            );

            return merged;
        }
    }
}
