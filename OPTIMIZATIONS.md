# 🚀 Plan d'Optimisation Végétation GPU Instancing

## 🎯 Objectif: 10 FPS → 200+ FPS

---

## 🔴 BUGS CRITIQUES À CORRIGER IMMÉDIATEMENT

### 1. **Bug de référence dans UpdateBatch()**
**Fichier**: `Engine/Rendering/VegetationRenderer.cs:369`

**Problème**:
```csharp
batch.Transforms = transforms;  // ❌ Copie la RÉFÉRENCE
```

**Solution**:
```csharp
batch.Transforms = new List<Matrix4>(transforms);  // ✅ Clone la liste
```

**Impact**: Empêche l'accumulation infinie d'instances

---

### 2. **UpdateBatch() appelé trop souvent**
**Fichier**: `Editor/Rendering/GameRenderer.cs:177`

**Problème**: `OnTerrainVegetationRegenerated()` est probablement appelé à chaque frame au lieu d'une seule fois.

**Solution**: Ajouter un flag `_vegetationInitialized` et ne register qu'une seule fois:
```csharp
private HashSet<Guid> _initializedTerrains = new();

private void OnTerrainVegetationRegenerated(Terrain terrain)
{
    // Éviter de re-register si déjà fait
    if (!_initializedTerrains.Add(terrain.Entity.Id))
        return;

    // ... reste du code
}
```

---

## 🟠 OPTIMISATIONS MAJEURES

### 3. **Frustum Culling sur CPU trop lent**
**Fichier**: `Engine/Rendering/VegetationRenderer.cs:1284`

**Problème actuel**:
- Frustum culling fait en **C# sur CPU** pour chaque instance
- Pour 500k instances, ça fait 500k tests par frame = LENT

**Solution A: GPU Culling (Optimal)**
```csharp
// Utiliser un Compute Shader pour faire le culling sur GPU
// Passer toutes les instances au GPU
// Le compute shader fait le test et écrit dans un IndirectBuffer
// DrawElementsIndirect() avec le buffer résultant
```

**Solution B: Spatial Hash / Octree (Plus simple)**
```csharp
// Créer une structure spatiale pour ne tester que les instances proches
private SpatialGrid<VegetationInstance> _spatialGrid = new(cellSize: 50f);

// Au lieu de tester TOUTES les instances:
var cellsInFrustum = _spatialGrid.GetCellsInFrustum(frustum);
foreach (var cell in cellsInFrustum)
{
    foreach (var instance in cell.Instances)
    {
        // Test uniquement les instances dans les cellules visibles
    }
}
```

**Impact**: 50-100x plus rapide

---

### 4. **Trop de draw calls pour Grass/Rocks**
**Fichier**: `Engine/Rendering/GrassRenderer.cs`, `RockRenderer.cs`

**Problème**: Un draw call PAR LAYER de grass/rocks

**Solution**: Merger tous les layers d'un même terrain en UN SEUL draw call
```csharp
// Au lieu de:
foreach (var layer in _grassLayers)
    GL.DrawElements(...);  // 10 layers = 10 draw calls

// Faire:
MergeAllLayersIntoSingleBatch();
GL.DrawElements(...);  // 1 draw call pour tout le grass
```

---

### 5. **BufferData appelé trop souvent**
**Fichier**: `Engine/Rendering/VegetationRenderer.cs:1328`

**Problème**: `UpdateInstanceBuffer()` re-upload TOUT le buffer même si seules quelques instances ont changé

**Solution**: Utiliser `BufferSubData` pour updates partiels
```csharp
// Au lieu de tout re-upload:
GL.BufferData(BufferTarget.ArrayBuffer, fullSize, data, BufferUsageHint.DynamicDraw);

// Faire des updates partiels:
GL.BufferSubData(BufferTarget.ArrayBuffer, offset, size, partialData);
```

---

## 🟡 OPTIMISATIONS MINEURES (Gains 5-20%)

### 6. **Distance Culling en squared distance**
**Fichier**: `Engine/Rendering/VegetationRenderer.cs:1302`

Déjà fait ✅ - Bon travail!

---

### 7. **LOD System manquant**
Ajouter 3 niveaux de LOD pour la végétation:
- **LOD 0**: < 50m - Mesh complet
- **LOD 1**: 50-150m - Mesh simplifié (50% triangles)
- **LOD 2**: 150-300m - Billboard (2 triangles)
- **> 300m**: Ne pas rendre

```csharp
float distance = Vector3.Distance(position, cameraPos);
int lodLevel = distance < 50f ? 0 : distance < 150f ? 1 : 2;
batch = GetBatchForLOD(modelGuid, lodLevel);
```

---

### 8. **Occlusion Culling**
Ne pas rendre ce qui est caché par le terrain/autres objets:
- Utiliser Hardware Occlusion Queries
- Ou Hierarchical Z-Buffer

---

## 🔧 CODE À IMPLÉMENTER

### Fix Immédiat #1: Corriger UpdateBatch()
```csharp
// Engine/Rendering/VegetationRenderer.cs:369
public void UpdateBatch(Guid modelGuid, int submeshIndex, List<Matrix4>? transforms, ...)
{
    // ...

    if (needsUpdate)
    {
        // ✅ FIX: Clone la liste au lieu de copier la référence
        batch.Transforms = transforms != null ? new List<Matrix4>(transforms) : new List<Matrix4>();
        batch.LastTransformCount = batch.Transforms.Count;
        batch.NeedsGPUUpdate = true;
    }

    // ...
}
```

---

### Fix Immédiat #2: Éviter les re-registrations
```csharp
// Editor/Rendering/GameRenderer.cs
private HashSet<Guid> _initializedVegetationTerrains = new();

public void SetSourceScene(Scene sourceScene)
{
    _scene = sourceScene;
    _initializedVegetationTerrains.Clear();  // Reset pour nouvelle scène

    // Subscribe to terrain vegetation events (only once per terrain)
    foreach (var entity in sourceScene.Entities)
    {
        var terrain = entity.GetComponent<Engine.Components.Terrain>();
        if (terrain != null && _initializedVegetationTerrains.Add(entity.Id))
        {
            terrain.VegetationRegenerated += () => OnTerrainVegetationRegenerated(terrain);
        }
    }
}
```

---

### Optimisation #1: Spatial Grid pour Frustum Culling
```csharp
// Engine/Rendering/SpatialGrid.cs (nouveau fichier)
public class SpatialGrid<T>
{
    private Dictionary<Vector2i, List<T>> _cells = new();
    private float _cellSize;

    public SpatialGrid(float cellSize)
    {
        _cellSize = cellSize;
    }

    public void Clear()
    {
        foreach (var cell in _cells.Values)
            cell.Clear();
    }

    public void Add(Vector3 position, T item)
    {
        var cellKey = GetCellKey(position);
        if (!_cells.TryGetValue(cellKey, out var cell))
        {
            cell = new List<T>();
            _cells[cellKey] = cell;
        }
        cell.Add(item);
    }

    public IEnumerable<T> GetItemsInRadius(Vector3 center, float radius)
    {
        int cellRadius = (int)Math.Ceiling(radius / _cellSize);
        var centerCell = GetCellKey(center);

        for (int x = -cellRadius; x <= cellRadius; x++)
        {
            for (int z = -cellRadius; z <= cellRadius; z++)
            {
                var cellKey = new Vector2i(centerCell.X + x, centerCell.Y + z);
                if (_cells.TryGetValue(cellKey, out var cell))
                {
                    foreach (var item in cell)
                        yield return item;
                }
            }
        }
    }

    private Vector2i GetCellKey(Vector3 position)
    {
        return new Vector2i(
            (int)Math.Floor(position.X / _cellSize),
            (int)Math.Floor(position.Z / _cellSize)
        );
    }
}

struct Vector2i
{
    public int X, Y;
    public Vector2i(int x, int y) { X = x; Y = y; }
}
```

**Utilisation dans CullBatch()**:
```csharp
// Au startup, construire le grid
private SpatialGrid<int> _spatialGrid = new(cellSize: 100f);

private void RebuildSpatialGrid(VegetationBatch batch)
{
    _spatialGrid.Clear();
    for (int i = 0; i < batch.Transforms.Count; i++)
    {
        var pos = new Vector3(batch.Transforms[i].M41, batch.Transforms[i].M42, batch.Transforms[i].M43);
        _spatialGrid.Add(pos, i);
    }
}

private void CullBatch(VegetationBatch batch, Vector3 cameraPos)
{
    batch.VisibleTransforms.Clear();

    float queryRadius = batch.MaxRenderDistance + _frustumCuller.GetFrustumRadius();

    // Ne teste que les instances PROCHES de la caméra
    foreach (int instanceIndex in _spatialGrid.GetItemsInRadius(cameraPos, queryRadius))
    {
        var transform = batch.Transforms[instanceIndex];
        Vector3 position = new Vector3(transform.M41, transform.M42, transform.M43);

        // Distance culling
        if ((position - cameraPos).LengthSquared > maxDistSqr)
            continue;

        // Frustum culling
        if (!_frustumCuller.TestSphere(position, batch.CullingSphereRadius))
            continue;

        batch.VisibleTransforms.Add(transform);
    }
}
```

---

## 📈 GAINS ATTENDUS

| Optimisation | Gain FPS | Difficulté |
|-------------|----------|-----------|
| Fix #1: Clone liste UpdateBatch | **500%** (10→50 FPS) | Facile ⭐ |
| Fix #2: Éviter re-registration | **200%** (50→100 FPS) | Facile ⭐ |
| Spatial Grid Culling | **100%** (100→200 FPS) | Moyen ⭐⭐ |
| LOD System | **50%** (200→300 FPS) | Difficile ⭐⭐⭐ |
| GPU Compute Culling | **100%** (300→600 FPS) | Très difficile ⭐⭐⭐⭐ |

**Total estimé**: 10 FPS → **200-300 FPS** avec les 3 premiers fixes

---

## 🎬 ORDRE D'IMPLÉMENTATION RECOMMANDÉ

1. ✅ **Fix #1** (5 minutes) - Clone la liste dans UpdateBatch
2. ✅ **Fix #2** (10 minutes) - HashSet pour éviter re-registration
3. ⚙️ **Test** - Vérifier que FPS monte à 50-100
4. ⚙️ **Spatial Grid** (2 heures) - Implémenter le grid pour culling
5. ⚙️ **Test** - Vérifier que FPS monte à 200+
6. 🔮 **LOD** (1 journée) - Si besoin de plus de perf
7. 🔮 **GPU Culling** (3-5 jours) - Pour atteindre 500+ FPS

---

## ✅ CHECKLIST

- [ ] Fix UpdateBatch() clone
- [ ] Fix registration unique terrain
- [ ] Rebuild et test
- [ ] Vérifier profiler: Culled Instances stable
- [ ] Mesurer FPS en playmode
- [ ] Implémenter Spatial Grid si < 200 FPS
- [ ] Implémenter LOD si besoin

---

**Note**: Commence par les 2 premiers fixes. Tu devrais voir une amélioration ÉNORME immédiatement!
