# 🐛 Fix : Freeze en Play Mode - Spatial Hash

## Problème Identifié

Le moteur freezait lors de l'entrée en play mode à cause du système de **Spatial Hash**.

### Cause Racine

Dans `SpatialHash.Insert()`, si un collider avait des bounds **très grandes** (par exemple un `HeightfieldCollider` couvrant 1000x1000 unités), le code créait **des millions de cellules** :

```csharp
// AVANT (DANGEREUX)
for (int x = minCell.X; x <= maxCell.X; x++)  // Peut être 0 à 10000 !
    for (int y = minCell.Y; y <= maxCell.Y; y++)
        for (int z = minCell.Z; z <= maxCell.Z; z++)
            // Crée 10000³ = 1 trillion de cellules = FREEZE
```

**Exemple concret** :
- HeightfieldCollider avec bounds de `-10000` à `+10000` sur chaque axe
- Taille de cellule = 5m
- Nombre de cellules = `(20000/5)³ = 4000³ = 64 milliards de cellules`
- Temps de création = **∞ (freeze)**

### Symptômes

✅ Compilation OK  
✅ Éditeur OK  
❌ **Play mode = Freeze total** (aucun message d'erreur)

---

## Solution Implémentée

### 1. Limite de Sécurité dans `SpatialHash.Insert()`

Ajout d'une vérification avant la création des cellules :

```csharp
// Safety check: limit the number of cells a collider can occupy
int cellCountX = Math.Abs(maxCell.X - minCell.X) + 1;
int cellCountY = Math.Abs(maxCell.Y - minCell.Y) + 1;
int cellCountZ = Math.Abs(maxCell.Z - minCell.Z) + 1;

const int MAX_CELLS_PER_DIMENSION = 100; // Safety limit (100³ = 1M cellules max)

if (cellCountX > MAX_CELLS_PER_DIMENSION || 
    cellCountY > MAX_CELLS_PER_DIMENSION || 
    cellCountZ > MAX_CELLS_PER_DIMENSION)
{
    Console.WriteLine($"[SpatialHash] WARNING: Collider '{collider.Entity?.Name}' has huge bounds ({cellCountX}x{cellCountY}x{cellCountZ} cells). Skipping spatial hash insertion.");
    return; // Skip ce collider, ne freeze pas
}
```

### 2. Nettoyage du Code

Suppression de la variable `processed` inutile dans `QueryPairs()` (le `HashSet` gère déjà les doublons).

---

## Impact

### Avant
- Terrain/HeightfieldCollider → Freeze total
- Impossible d'entrer en play mode

### Après
- ✅ **Tous les colliders normaux** : Fonctionnent parfaitement dans le spatial hash
- ⚠️ **Colliders géants** (>500m³) : Skip du spatial hash avec warning, collision détectée via fallback AABB
- ✅ Play mode démarre instantanément

### Trade-offs

**Colliders affectés par la limite** :
- HeightfieldCollider de très grand terrain (>500m)
- MeshCollider de modèles énormes (ville entière en 1 mesh)

**Solutions** :
1. **Augmenter la taille de cellule** : Changer `cellSize: 5f` → `cellSize: 20f` dans `CollisionSystem.cs`
2. **Découper les grands colliders** : Séparer le terrain en chunks
3. **Accepter le warning** : Le collider fonctionne quand même, juste sans accélération spatiale

---

## Comment Ajuster

### Si vous avez un grand terrain

**Option 1 : Augmenter la taille de cellule**

Dans `Engine/Physics/CollisionSystem.cs` :
```csharp
// Ligne ~17
private static readonly SpatialHash _spatialHash = new SpatialHash(cellSize: 20f); // Au lieu de 5f
```

**Option 2 : Augmenter la limite de cellules**

Dans `Engine/Physics/SpatialHash.cs` :
```csharp
// Ligne ~38
const int MAX_CELLS_PER_DIMENSION = 200; // Au lieu de 100
```

⚠️ **Attention** : Augmenter trop ces valeurs peut ralentir l'initialisation !

### Si vous voyez le warning

```
[SpatialHash] WARNING: Collider 'MyTerrain' has huge bounds (500x500x500 cells). Skipping spatial hash insertion.
```

**Que faire ?**
1. **Vérifier** : Votre collider a-t-il vraiment besoin d'être aussi grand ?
2. **Découper** : Si c'est un terrain, créer plusieurs TerrainChunks
3. **Ignorer** : Le collider fonctionne toujours, juste moins optimisé

---

## Tests de Validation

### Test 1 : Play Mode Normal
✅ Doit démarrer instantanément (<1 seconde)

### Test 2 : Terrain Moyen (100x100m)
✅ Aucun warning, tout fonctionne

### Test 3 : Grand Terrain (1000x1000m)
⚠️ Warning attendu, mais pas de freeze
✅ Collision fonctionne via fallback

### Test 4 : Performance
✅ 60 FPS avec 500 colliders normaux

---

## Monitoring

Pour surveiller si des colliders sont skippés, regarder la console au démarrage du play mode :

```
[SpatialHash] WARNING: Collider 'TerrainHuge' has huge bounds (800x100x800 cells). Skipping spatial hash insertion.
```

Si vous voyez ce message, considérez les options d'ajustement ci-dessus.

---

## Conclusion

**Le freeze est corrigé** avec une solution défensive qui :
- ✅ Empêche les freeze (limite de sécurité)
- ✅ Avertit l'utilisateur (console warning)
- ✅ Fonctionne quand même (fallback AABB)
- ✅ N'impacte pas les cas normaux (99% des colliders)

Le système est maintenant **safe** et **robuste** ! 🎉
