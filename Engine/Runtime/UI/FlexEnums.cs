namespace Engine.UI
{
    /// <summary>
    /// Direction principale du layout flex (axe principal)
    /// </summary>
    public enum FlexDirection
    {
        Row = 0,           // Horizontal, de gauche à droite
        RowReverse = 1,    // Horizontal, de droite à gauche
        Column = 2,        // Vertical, de haut en bas
        ColumnReverse = 3  // Vertical, de bas en haut
    }

    /// <summary>
    /// Alignement des items sur l'axe principal
    /// </summary>
    public enum JustifyContent
    {
        FlexStart = 0,     // Alignés au début
        FlexEnd = 1,       // Alignés à la fin
        Center = 2,        // Centrés
        SpaceBetween = 3,  // Espace entre les items
        SpaceAround = 4,   // Espace autour des items
        SpaceEvenly = 5    // Espace égal entre tous
    }

    /// <summary>
    /// Alignement des items sur l'axe secondaire
    /// </summary>
    public enum AlignItems
    {
        FlexStart = 0,     // Alignés au début
        FlexEnd = 1,       // Alignés à la fin
        Center = 2,        // Centrés
        Stretch = 3,       // Étirés pour remplir
        Baseline = 4       // Alignés sur la baseline du texte
    }

    /// <summary>
    /// Alignement individuel d'un item (override AlignItems)
    /// </summary>
    public enum AlignSelf
    {
        Auto = 0,          // Utilise AlignItems du parent
        FlexStart = 1,     // Aligné au début
        FlexEnd = 2,       // Aligné à la fin
        Center = 3,        // Centré
        Stretch = 4,       // Étiré pour remplir
        Baseline = 5       // Aligné sur la baseline
    }

    /// <summary>
    /// Comportement du wrap (retour à la ligne)
    /// </summary>
    public enum FlexWrap
    {
        NoWrap = 0,        // Pas de wrap, tous sur une ligne
        Wrap = 1,          // Wrap vers le bas/droite
        WrapReverse = 2    // Wrap vers le haut/gauche
    }
}
