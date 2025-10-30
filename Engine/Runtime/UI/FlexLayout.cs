using System.Numerics;

namespace Engine.UI
{
    /// <summary>
    /// Propriétés de layout Flexbox pour un UIElement
    /// </summary>
    public class FlexLayout
    {
        // Container properties (pour le parent)
        public FlexDirection Direction { get; set; } = FlexDirection.Row;
        public JustifyContent JustifyContent { get; set; } = JustifyContent.FlexStart;
        public AlignItems AlignItems { get; set; } = AlignItems.FlexStart;
        public FlexWrap Wrap { get; set; } = FlexWrap.NoWrap;
        public float Gap { get; set; } = 0f; // Espace entre les items

        // Item properties (pour l'enfant)
        public float FlexGrow { get; set; } = 0f;   // Facteur de croissance
        public float FlexShrink { get; set; } = 1f; // Facteur de rétrécissement
        public float FlexBasis { get; set; } = -1f; // Taille de base (-1 = auto)
        public AlignSelf AlignSelf { get; set; } = AlignSelf.Auto;
        public int Order { get; set; } = 0; // Ordre d'affichage

        // Padding
        public float PaddingLeft { get; set; } = 0f;
        public float PaddingRight { get; set; } = 0f;
        public float PaddingTop { get; set; } = 0f;
        public float PaddingBottom { get; set; } = 0f;

        public FlexLayout()
        {
        }

        /// <summary>
        /// Calcule le layout flex pour les enfants d'un container
        /// </summary>
        public void ApplyLayout(UIElement container, Vector2 containerSize)
        {
            if (container.Children.Count == 0) return;

            bool isRow = Direction == FlexDirection.Row || Direction == FlexDirection.RowReverse;
            bool isReverse = Direction == FlexDirection.RowReverse || Direction == FlexDirection.ColumnReverse;

            // Trier les enfants par ordre
            var children = container.Children.ToList();
            children.Sort((a, b) => (a.FlexLayout?.Order ?? 0).CompareTo(b.FlexLayout?.Order ?? 0));
            if (isReverse) children.Reverse();

            // Taille disponible
            float availableMainSize = isRow ? containerSize.X : containerSize.Y;
            float availableCrossSize = isRow ? containerSize.Y : containerSize.X;

            // Soustraire le padding
            availableMainSize -= PaddingLeft + PaddingRight;
            availableCrossSize -= PaddingTop + PaddingBottom;

            // Calculer les tailles des enfants
            float totalMainSize = 0f;
            float totalFlexGrow = 0f;
            float totalFlexShrink = 0f;

            foreach (var child in children)
            {
                var childFlex = child.FlexLayout ?? new FlexLayout();
                var childRect = child.Rect;

                // Taille de base
                float mainSize = isRow ? childRect.SizeDelta.X : childRect.SizeDelta.Y;
                if (childFlex.FlexBasis >= 0) mainSize = childFlex.FlexBasis;

                totalMainSize += mainSize;
                totalFlexGrow += childFlex.FlexGrow;
                totalFlexShrink += childFlex.FlexShrink;
            }

            // Ajouter les gaps
            totalMainSize += Gap * (children.Count - 1);

            // Calculer l'espace libre
            float freeSpace = availableMainSize - totalMainSize;

            // Position courante
            float mainPos = PaddingLeft;
            float crossPos = PaddingTop;

            // Ajuster la position de départ selon JustifyContent
            switch (JustifyContent)
            {
                case JustifyContent.FlexEnd:
                    mainPos += freeSpace;
                    break;
                case JustifyContent.Center:
                    mainPos += freeSpace / 2f;
                    break;
                case JustifyContent.SpaceBetween:
                    // L'espace sera distribué entre les items
                    break;
                case JustifyContent.SpaceAround:
                    mainPos += (freeSpace / children.Count) / 2f;
                    break;
                case JustifyContent.SpaceEvenly:
                    mainPos += freeSpace / (children.Count + 1);
                    break;
            }

            // Calculer l'espacement pour SpaceBetween/Around/Evenly
            float itemGap = Gap;
            if (JustifyContent == JustifyContent.SpaceBetween && children.Count > 1)
                itemGap = freeSpace / (children.Count - 1);
            else if (JustifyContent == JustifyContent.SpaceAround && children.Count > 0)
                itemGap = freeSpace / children.Count;
            else if (JustifyContent == JustifyContent.SpaceEvenly && children.Count > 0)
                itemGap = freeSpace / (children.Count + 1);

            // Positionner les enfants
            foreach (var child in children)
            {
                var childFlex = child.FlexLayout ?? new FlexLayout();
                var childRect = child.Rect;

                // Calculer la taille main
                float mainSize = isRow ? childRect.SizeDelta.X : childRect.SizeDelta.Y;
                if (childFlex.FlexBasis >= 0) mainSize = childFlex.FlexBasis;

                // Appliquer flex-grow ou flex-shrink
                if (freeSpace > 0 && childFlex.FlexGrow > 0 && totalFlexGrow > 0)
                {
                    mainSize += (freeSpace * childFlex.FlexGrow) / totalFlexGrow;
                }
                else if (freeSpace < 0 && childFlex.FlexShrink > 0 && totalFlexShrink > 0)
                {
                    mainSize += (freeSpace * childFlex.FlexShrink) / totalFlexShrink;
                }

                // Calculer la taille cross
                float crossSize = isRow ? childRect.SizeDelta.Y : childRect.SizeDelta.X;

                // Calculer la position cross selon AlignItems/AlignSelf
                var align = childFlex.AlignSelf == AlignSelf.Auto ? AlignItems : (AlignItems)(int)childFlex.AlignSelf;
                float childCrossPos = crossPos;

                switch (align)
                {
                    case AlignItems.FlexEnd:
                        childCrossPos = availableCrossSize - crossSize;
                        break;
                    case AlignItems.Center:
                        childCrossPos = (availableCrossSize - crossSize) / 2f;
                        break;
                    case AlignItems.Stretch:
                        crossSize = availableCrossSize;
                        break;
                }

                // Appliquer la position au RectTransform
                if (isRow)
                {
                    childRect.AnchoredPosition = new Vector2(mainPos, childCrossPos);
                    childRect.SizeDelta = new Vector2(mainSize, crossSize);
                }
                else
                {
                    childRect.AnchoredPosition = new Vector2(childCrossPos, mainPos);
                    childRect.SizeDelta = new Vector2(crossSize, mainSize);
                }

                // Avancer sur l'axe principal
                mainPos += mainSize + itemGap;
            }
        }
    }
}
