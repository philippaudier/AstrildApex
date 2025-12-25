# Prefab System Design

## Concept
Un système de prefabs similaire à Unity permettant de :
1. Créer des prefabs à partir d'entités dans la scène
2. Instancier des prefabs dans la scène
3. Modifier un prefab et mettre à jour toutes ses instances
4. Utiliser des prefabs dans le terrain vegetation generator

## Architecture

### PrefabAsset
```csharp
public class PrefabAsset : Asset
{
    public string Name { get; set; }
    public EntityData RootEntity { get; set; }
    public List<EntityData> ChildEntities { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}

public class EntityData
{
    public string Name { get; set; }
    public Transform Transform { get; set; }
    public List<ComponentData> Components { get; set; }
}
```

### Workflow
1. **Création de Prefab**:
   - Drag & drop un modèle GLTF dans la scène
   - Ajuster position/rotation/scale
   - Ajouter/modifier des composants
   - Right-click → "Create Prefab from Selection"
   - Sauvegarder le prefab (.prefab fichier JSON)

2. **Utilisation dans Vegetation**:
   - Dans VegetationLayer, remplacer `ModelGuid` par `PrefabGuid`
   - Le prefab contient déjà les transformations, matériaux, etc.
   - Instancier le prefab pour chaque position de végétation

3. **Modification de Prefab**:
   - Double-click sur prefab asset → ouvre en mode édition
   - Modifications appliquées à toutes les instances
   - Bouton "Apply Changes to Instances"

## Avantages pour Vegetation
- **Transformations préservées**: Scale/rotation déjà appliqués
- **Matériaux configurés**: Plus besoin de gérer MaterialGuid séparément
- **Multi-submesh automatique**: Le prefab gère la hiérarchie complète
- **Composants custom**: Ajouter scripts, colliders, etc. au prefab

## Implémentation Progressive

### Phase 1 (Minimum Viable)
- [ ] Créer PrefabAsset class
- [ ] Ajouter "Create Prefab" dans le menu contextuel
- [ ] Sérialisation/Désérialisation de prefab
- [ ] Instanciation basique de prefab dans la scène

### Phase 2 (Vegetation Integration)
- [ ] Ajouter PrefabGuid à VegetationLayer
- [ ] Modifier CreateVegetationEntities pour supporter prefabs
- [ ] UI pour drag & drop prefab dans vegetation layer

### Phase 3 (Édition & Mise à jour)
- [ ] Mode édition de prefab
- [ ] Système de tracking des instances
- [ ] "Apply Changes" pour mettre à jour toutes les instances

### Phase 4 (Advanced)
- [ ] Prefab variants (overrides)
- [ ] Nested prefabs
- [ ] Prefab preview dans l'inspector

## Solution Immédiate (Sans Prefab)
Pour l'instant, pour résoudre le problème des arbres:
1. **SubmeshIndex = -1** maintenant fonctionne (crée une entité parent avec enfants pour chaque submesh)
2. **CullingMode.None** par défaut pour la végétation (feuilles alpha)
3. **Density en entier** pour éviter les bugs
4. **Limit à 1000 instances** pour les performances

## Recommandation
Le système de prefab est très utile mais complexe. Pour la végétation, la solution actuelle avec submesh -1 devrait bien fonctionner. Le prefab system peut être ajouté plus tard comme amélioration générale de l'éditeur.
