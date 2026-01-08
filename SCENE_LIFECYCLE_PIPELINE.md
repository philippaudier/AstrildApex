# Scene Lifecycle Pipeline - Documentation

## Vue d'ensemble

Ce document décrit le pipeline de gestion du cycle de vie des scènes dans AstrildApex, inspiré du système d'Unreal Engine.

## Architecture inspirée d'Unreal Engine

### Unreal Engine's World Lifecycle

```
World Creation → BeginPlay → Runtime → EndPlay → Cleanup → Garbage Collection
```

**Principes clés d'Unreal :**
1. **Subsystems** : Chaque système (rendering, physics, audio) s'enregistre au World
2. **Lifecycle Hooks** : `OnWorldBeginPlay()`, `OnWorldCleanup()`, etc.
3. **Resource Tracking** : Toutes les ressources sont trackées par World
4. **Deferred Cleanup** : GC asynchrone pour éviter les hitches
5. **World Partitioning** : Support pour mondes streaming (comme notre InfiniteStreaming)

### Notre implémentation

```
Scene Creation → SetScene → Subscribe → Runtime → SetScene (new) → Cleanup → GC
```

## Pipeline détaillé

### 1. Création d'une nouvelle scène (`NewScene()`)

**Fichier :** `Editor/Scene/SceneManager.cs`

```csharp
NewScene()
├── 1. Créer nouvelle Scene instance
├── 2. Appeler SetScene(newScene) sur le renderer
│   ├── → Cleanup automatique de l'ancienne scène
│   └── → Initialisation de la nouvelle scène
├── 3. Clear caches globaux
│   ├── AssetDatabase.ClearAllMaterialCaches()
│   └── TextureCache.ClearCache()
├── 4. Clear framebuffers
├── 5. Reset sélection et état éditeur
└── 6. Update window title
```

### 2. SetScene - Le point central (pattern World Cleanup)

**Fichier :** `Editor/Rendering/ViewportRenderer.cs`

```csharp
SetScene(Scene newScene)
├── PHASE 1: CLEANUP (Old Scene Resources)
│   ├── 1.1. VegetationRenderer.ClearBatches()
│   │   ├── Libère tous les instance VBOs GPU
│   │   ├── Clear la hash table de batches
│   │   └── Note: Mesh VAO/VBO restent (partagés via cache)
│   │
│   ├── 1.2. Clear terrain subscriptions
│   │   ├── _subscribedTerrains.Clear()
│   │   └── _autoInitAttempted.Clear()
│   │
│   └── 1.3. (Future) Clear autres subsystems
│
├── PHASE 2: INITIALIZATION (New Scene Setup)
│   ├── 2.1. Assigner nouvelle scene (_scene = newScene)
│   │
│   ├── 2.2. Invalidate component cache
│   │   └── Force rebuild pour détecter tous les composants
│   │
│   ├── 2.3. Initialize WeatherManager
│   │   └── Cherche WeatherComponent dans la nouvelle scène
│   │
│   ├── 2.4. Subscribe aux événements terrain
│   │   ├── Parcourt tous les terrains
│   │   ├── Subscribe à VegetationRegenerated
│   │   └── Initialize batches végétation si déjà générée
│   │
│   └── 2.5. Subscribe à MaterialSaved (une seule fois)
│
└── PHASE 3: VALIDATION
    └── Vérifie que toutes les ressources sont correctement initialisées
```

### 3. Chargement d'une scène existante (`LoadScene()`)

**Fichier :** `Editor/Scene/SceneManager.cs`

```csharp
LoadSceneFromPath(path)
├── 1. Désérialiser la scène depuis le fichier
│   └── SceneSerializer.Load()
│
├── 2. Appeler SetScene(loadedScene)
│   └── → Même pipeline de cleanup + init que NewScene
│
├── 3. Refresh terrain subscriptions
│   └── renderer.RefreshTerrainSubscriptions()
│       └── Necessary car terrains désérialisés n'étaient pas subscribed
│
├── 4. Update current scene path
├── 5. Mark as unmodified
└── 6. Update window title
```

### 4. Play Mode / Edit Mode Transition

**Pattern :** Création de scène temporaire (comme Unreal PIE - Play In Editor)

```csharp
EnterPlayMode()
├── 1. Clone la scène actuelle (_editScene)
│   └── Scene.Clone() → crée _playScene
│
├── 2. SetScene(_playScene)
│   └── → Cleanup + Init automatique
│
├── 3. Appeler BeginPlay sur tous les composants
└── 4. Activer la simulation physics/scripts

ExitPlayMode()
├── 1. SetScene(_editScene) 
│   └── → Retour à l'état éditeur, cleanup auto de playScene
│
└── 2. Restore editor camera/gizmos
```

## Subsystems et leurs responsabilités

### VegetationRenderer
- **Init :** Subscribe aux terrains lors de SetScene
- **Runtime :** Update batches quand vegetation regenerated
- **Cleanup :** ClearBatches() libère tous les GPU buffers
- **Memory :** Instance VBOs uniquement (mesh VAO/VBO partagés)

### TerrainRenderer (Infinite Streaming)
- **Init :** Configure tiles autour de la caméra
- **Runtime :** Load/Unload tiles selon position caméra
- **Cleanup :** Unload tous les tiles actifs
- **Memory :** Mesh data par tile (VAO/VBO/EBO)

### ShadowManager
- **Init :** Crée FBO/texture shadowmap (une fois)
- **Runtime :** Render shadow pass chaque frame
- **Cleanup :** Dispose() libère FBO/texture
- **Memory :** Shadow map texture (2048x2048 par défaut)

### MaterialCache
- **Init :** Vide au démarrage
- **Runtime :** Cache matériaux chargés
- **Cleanup :** ClearAllMaterialCaches()
- **Memory :** Dictionnaires de materials et textures

## Gestion mémoire (GC Pattern)

### Ressources GPU (OpenGL)
```csharp
// Pattern de nettoyage manuel (pas de GC automatique pour GPU)
if (vbo != 0) { GL.DeleteBuffer(vbo); vbo = 0; }
if (vao != 0) { GL.DeleteVertexArray(vao); vao = 0; }
if (tex != 0) { GL.DeleteTexture(tex); tex = 0; }
```

### Ressources CPU (C# GC)
```csharp
// Pattern: Clear references + laisser GC nettoyer
_batches.Clear();  // Clear dictionary
_subscribedTerrains.Clear();  // Clear hashset
// → GC .NET s'occupe du reste automatiquement
```

### Caches partagés
- **MeshCache :** Persiste entre scènes (mesh réutilisables)
- **TextureCache :** Clear lors de NewScene (évite fuite mémoire)
- **MaterialCache :** Clear lors de NewScene (paramètres peuvent changer)
- **ShaderCache :** Persiste toujours (shaders globaux)

## Checklist pour ajouter un nouveau subsystem

1. **Créer la classe du subsystem**
   - Implémenter `IDisposable` si ressources GPU
   - Méthode `Initialize(Scene scene)`
   - Méthode `Cleanup()`

2. **Intégrer dans ViewportRenderer**
   ```csharp
   private MySubsystem _mySubsystem;
   
   // Dans SetScene() - PHASE 1 CLEANUP:
   _mySubsystem?.Cleanup();
   
   // Dans SetScene() - PHASE 2 INIT:
   _mySubsystem?.Initialize(newScene);
   
   // Dans Dispose():
   _mySubsystem?.Dispose();
   ```

3. **Tester les transitions**
   - NewScene → Subsystem doit être clean
   - LoadScene → Subsystem doit initialiser depuis fichier
   - Play/Edit → Subsystem doit gérer deux scènes distinctes

## Améliorations futures

### 1. Event-based Architecture
```csharp
public class SceneLifecycleEvents
{
    public static event Action<Scene> BeforeSceneUnload;
    public static event Action<Scene> AfterSceneLoad;
    public static event Action<Scene> BeforePlayMode;
    public static event Action<Scene> AfterPlayMode;
}
```

### 2. Resource Manager
```csharp
public class SceneResourceManager
{
    private Dictionary<Scene, List<IDisposable>> _resources;
    
    public void TrackResource(Scene scene, IDisposable resource);
    public void CleanupScene(Scene scene);
}
```

### 3. Async Scene Loading
```csharp
public async Task<Scene> LoadSceneAsync(string path, 
    IProgress<float> progress = null,
    CancellationToken token = default)
{
    // 1. Load in background thread
    // 2. Report progress (0-100%)
    // 3. Initialize resources on main thread
}
```

### 4. Scene Streaming (Large Worlds)
```csharp
public class SceneStreamingManager
{
    // Load/Unload scene regions based on camera position
    // Similar to Unreal World Partitioning / Unity World Streaming
}
```

## Debugging

### Vérifier les fuites mémoire GPU
```csharp
// Avant SetScene
int vbosBefore = GL.GetInteger(GetPName.NumVertexBufferObjects);

// Après SetScene + cleanup
int vbosAfter = GL.GetInteger(GetPName.NumVertexBufferObjects);

if (vbosAfter > vbosBefore)
    Console.WriteLine($"⚠️ GPU Memory leak: {vbosAfter - vbosBefore} VBOs not freed!");
```

### Logs de lifecycle
```csharp
[ViewportRenderer] ✓ Cleared vegetation batches from previous scene
[ViewportRenderer] ✓ SetScene: Subscribing to terrain 'MyTerrain', mode=Single
[VegetationRenderer] ✓ UpdateBatch: Trees (906 instances)
```

## Conclusion

Le pipeline actuel est robuste et inspiré des meilleures pratiques d'Unreal Engine :

✅ **Cleanup explicite** lors des transitions de scène  
✅ **Subscription management** pour éviter memory leaks  
✅ **Resource tracking** par scene  
✅ **Separation of concerns** (rendering, physics, etc.)  

Les améliorations futures rendront le système encore plus modulaire et performant pour les grands mondes open-world.
