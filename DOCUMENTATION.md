# Documentation AstrildApex Engine

**Version**: 0.1.0
**Langage**: C# (.NET 8.0)
**API Graphique**: OpenGL 4.6 (via OpenTK 4.9.4)
**Interface**: ImGui.NET 1.91.6.1

---

## Table des Matières

1. [Vue d'ensemble](#1-vue-densemble)
2. [Architecture Globale](#2-architecture-globale)
3. [Système ECS](#3-système-ecs)
4. [Système de Rendu](#4-système-de-rendu)
5. [Système de Physique](#5-système-de-physique)
6. [Système de Terrain](#6-système-de-terrain)
7. [Système de Scripting](#7-système-de-scripting)
8. [Système d'Entrées](#8-système-dentrées)
9. [Système UI - AstrildUI](#9-système-ui---astrildui)
10. [Système de Post-Processing](#10-système-de-post-processing)
11. [Système de Sérialisation](#11-système-de-sérialisation)
12. [L'Éditeur](#12-léditeur)
13. [Play Mode](#13-play-mode)
14. [Système de Lumières](#14-système-de-lumières)
15. [Système de Caméra](#15-système-de-caméra)
16. [Asset Management](#16-asset-management)
17. [Mathématiques](#17-mathématiques)
18. [Dépendances](#18-dépendances)
19. [Workflow de Développement](#19-workflow-de-développement)
20. [Conventions de Code](#20-conventions-de-code)
21. [Appendix - Guides consolidés](#appendix--guides-consolidés)

---

## 1. Vue d'ensemble

**AstrildApex** est un moteur de jeu 3D développé en C# utilisant OpenGL pour le rendu. Il adopte une architecture Entity-Component avec un éditeur intégré de type Unity-like, permettant le développement de jeux 3D avec des outils visuels complets.

### Caractéristiques principales

- **Architecture ECS** : Entity-Component-System pour une organisation claire du code
- **Rendu PBR** : Physically Based Rendering avec support des matériaux réalistes
- **Éditeur visuel** : Hiérarchie, inspecteur, assets, console, viewport 3D
- **Play Mode** : Test direct dans l'éditeur avec clonage de scène
- **Hot-Reload** : Rechargement à chaud des shaders et scripts
- **Système de terrain** : Génération depuis heightmap avec layers multiples
- **Physique** : Raycasting, collision detection, triggers
- **UI moderne** : AstrildUI (système déclaratif basé sur ImGui.NET)
- **Post-processing** : Bloom, tone mapping, SSAO, aberration chromatique

### Structure du projet

```
AstrildApex/
├── Engine/              # Runtime du moteur (bibliothèque)
│   ├── Components/      # Composants (Transform, MeshRenderer, Light, etc.)
│   ├── ECS/            # Système Entity-Component-System
│   ├── Input/          # Gestion des entrées
│   ├── Physics/        # Système de physique
│   ├── Rendering/      # Rendu, shaders, matériaux
│   ├── Scene/          # Gestion de scènes
│   ├── Scripting/      # MonoBehaviour et compilation de scripts
│   ├── Serialization/  # Sérialisation de scènes et composants
│   ├── UI/             # AstrildUI - Système UI natif
│   └── Mathx/          # Utilitaires mathématiques et noise
├── Editor/             # Éditeur visuel (application standalone)
│   ├── Icons/          # Icônes de l'éditeur
│   ├── ImGui/          # Intégration ImGui
│   ├── Inspector/      # Inspecteurs de composants
│   ├── Logging/        # Système de logs
│   ├── Panels/         # Panels de l'éditeur
│   ├── Rendering/      # ViewportRenderer, GridRenderer
│   └── State/          # Undo/Redo, sélection, settings
├── Sandbox/            # Projet de test
├── Assets/             # Assets du projet (textures, models, scenes)
├── Scenes/             # Scènes sauvegardées (.scene)
└── Materials/          # Matériaux (.material)
```

---

## 2. Architecture Globale

### Philosophie de Conception

- **Entity-Component System (ECS)** : Architecture orientée composants
- **Data-Oriented** : Séparation claire entre données et comportement
- **Hot-Reload** : Support du rechargement de shaders et scripts
- **Play Mode** : Simulation en éditeur avec clonage de scène
- **Extensibilité** : Nouveaux composants, shaders et effets facilement ajoutables

### Séparation Engine / Editor

**Engine/** : Code runtime pur
- Aucune dépendance à l'éditeur
- Peut être utilisé sans l'éditeur
- Portable et réutilisable

**Editor/** : Outils de développement
- Dépend de Engine
- Interface graphique (ImGui)
- Outils d'édition et de debugging

---

## 3. Système ECS

### Fichiers principaux

- `Engine/ECS/EcsCore.cs`
- `Engine/Components/Component.cs`
- `Engine/Scene/Scene.cs`

### Entity

Conteneur pour composants avec gestion de hiérarchie parent-enfant.

```csharp
public sealed class Entity
{
    public uint Id { get; }
    public string Name { get; set; }
    public Guid Guid { get; }
    public Transform Transform { get; }
    public Entity? Parent { get; set; }
    public List<Entity> Children { get; }
    public bool Active { get; set; }

    // Gestion de composants
    public T? GetComponent<T>() where T : Component;
    public T AddComponent<T>() where T : Component, new();
    public void RemoveComponent<T>() where T : Component;
    public bool HasComponent<T>() where T : Component;
}
```

**Responsabilités** :
- Conteneur pour composants
- Gestion de hiérarchie
- Transformations locales/monde (position, rotation, scale)
- Activation/désactivation

### Component

Classe de base pour tous les composants.

```csharp
public abstract class Component
{
    public Entity? Entity { get; }
    public bool Enabled { get; set; }

    // Lifecycle hooks
    protected virtual void OnAttached() { }
    protected virtual void OnDetached() { }
    protected virtual void OnEnable() { }
    protected virtual void OnDisable() { }
    protected virtual void Start() { }
    protected virtual void Update(float deltaTime) { }
    protected virtual void LateUpdate(float deltaTime) { }
    protected virtual void FixedUpdate(float deltaTime) { }

    // Collision callbacks
    protected virtual void OnCollisionEnter(Collision collision) { }
    protected virtual void OnCollisionStay(Collision collision) { }
    protected virtual void OnCollisionExit(Collision collision) { }
    protected virtual void OnTriggerEnter(Collision collision) { }
    protected virtual void OnTriggerStay(Collision collision) { }
    protected virtual void OnTriggerExit(Collision collision) { }
}
```

**Cycle de Vie** :
1. `OnAttached()` : Attachement à une entité
2. `OnEnable()` : Activation en Play Mode
3. `Start()` : Initialisation au démarrage
4. `Update()` : Frame par frame
5. `LateUpdate()` : Après tous les updates
6. `FixedUpdate()` : Physique à intervalle fixe (50 FPS)
7. `OnDisable()` : Désactivation
8. `OnDetached()` : Détachement

### Scene

Conteneur principal pour entités.

```csharp
public sealed class Scene
{
    public List<Entity> Entities { get; }

    // Factory methods
    public Entity CreateCube(string name, Vector3 position);
    public Entity CreateSphere(string name, Vector3 position);
    public Entity CreateCapsule(string name, Vector3 position);
    public Entity CreatePlane(string name, Vector3 position);
    public Entity CreateQuad(string name, Vector3 position);
    public Entity CreateDirectionalLight(string name);
    public Entity CreatePointLight(string name, Vector3 position);
    public Entity CreateSpotLight(string name, Vector3 position);

    // Cloning for Play Mode
    public Scene Clone(object? scriptHost);
    public CameraComponent? GetMainCamera();
}
```

### Composants Principaux

| Composant | Description | Fichier |
|-----------|-------------|---------|
| **TransformComponent** | Position, rotation, scale | `Components/TransformComponent.cs` |
| **MeshRendererComponent** | Rendu de mesh 3D | `Components/MeshRendererComponent.cs` |
| **LightComponent** | Lumière (directional, point, spot) | `Components/LightComponent.cs` |
| **CameraComponent** | Caméra (perspective, orthographic) | `Components/CameraComponent.cs` |
| **Collider** | Collision (box, sphere, capsule) | `Components/Collider.cs` |
| **Terrain** | Terrain avec heightmap | `Components/Terrain.cs` |
| **EnvironmentSettings** | Paramètres d'environnement | `Components/EnvironmentSettings.cs` |

---

## 4. Système de Rendu

### Architecture

**Fichiers principaux** :
- `Engine/Rendering/ShaderLibrary.cs`
- `Engine/Rendering/ShaderProgram.cs`
- `Engine/Rendering/MaterialRuntime.cs`
- `Editor/Rendering/ViewportRenderer.cs`

### Pipelines de Rendu

**ViewportRenderer** (Éditeur) :
- Rendu avancé avec PBR (Physically Based Rendering)
- Support SSAO (Screen Space Ambient Occlusion)
- Shadow mapping avec CSM (Cascaded Shadow Maps)
- Post-processing (Bloom, Tone Mapping, Chromatic Aberration)
- Skybox avec HDR
- Gizmos et wireframe

**GameRenderer** (Play Mode) :
- Rendu simplifié pour le game panel
- Support terrain et meshes basiques

### Shaders Modulaires

Architecture modulaire pour réutilisabilité et maintenabilité.

**Structure** :
```
Engine/Rendering/Shaders/
├── Includes/              # Modules réutilisables
│   ├── Common.glsl       # Constantes, uniforms, structures
│   ├── Lighting.glsl     # Fonctions PBR et éclairage
│   ├── Fog.glsl          # Effets de brouillard
│   ├── Shadows.glsl      # Shadow mapping
│   ├── IBL.glsl          # Image-Based Lighting
│   └── SSAO.glsl         # Ambient occlusion
├── Forward/              # Shaders forward rendering
│   ├── ForwardBase.vert  # Vertex shader modulaire
│   └── ForwardBase.frag  # Fragment shader modulaire
├── PostProcess/          # Effets post-processing
│   ├── tonemap.frag
│   ├── bloom_*.frag
│   └── chromatic_aberration.frag
├── SSAO/                 # SSAO pipeline
│   ├── SSAOGeometry.*
│   ├── SSAOCalc.*
│   └── SSAOBlur.*
└── pbr.* (legacy)        # Anciens shaders (compatibilité)
```

**Utilisation** :
```glsl
#version 330 core
#include "../Includes/Common.glsl"
#include "../Includes/Lighting.glsl"
#include "../Includes/Fog.glsl"
```

**ShaderLibrary** :
- Découverte automatique des shaders
- Cache des programmes compilés
- Hot-reload support
- Préprocesseur avec gestion des `#include`

### Matériaux

**MaterialAsset** :
```csharp
public class MaterialAsset
{
    public Guid Guid { get; set; }
    public string Name { get; set; }
    public string Shader { get; set; }
    public float[] AlbedoColor { get; set; }
    public Guid? AlbedoTexture { get; set; }
    public Guid? NormalTexture { get; set; }
    public Guid? MetallicTexture { get; set; }
    public Guid? RoughnessTexture { get; set; }
    public float Metallic { get; set; }
    public float Roughness { get; set; }
    public float NormalStrength { get; set; }
    public int TransparencyMode { get; set; } // 0=Opaque, 1=Transparent
}
```

**AssetDatabase** :
- Gestion centralisée des assets (textures, matériaux, modèles)
- Index GUID → Asset
- Fichiers .meta pour persistance des GUIDs
- Hot-reload support

### Render Queue

Ordre de rendu pour optimisation et transparence.

```csharp
public static class RenderQueue
{
    public const int Background = 1000;    // Skybox
    public const int SSAOGeometry = 1900;  // SSAO pass
    public const int Geometry = 2000;      // Opaque
    public const int SSAOProcess = 2600;   // SSAO calc
    public const int Transparent = 3000;   // Alpha blended
    public const int Overlay = 4000;       // UI
}
```

### Skybox

**Types supportés** :
- **Procedural** : Génération procédurale (couleurs ciel/sol)
- **Cubemap** : 6 faces de texture
- **Six-Sided** : Chargement de 6 images séparées
- **Panoramic** : Image HDR panoramique

**Fonctionnalités** :
- Intégration avec système d'éclairage (direction soleil)
- Exposure et tint configurables
- Rendu derrière tous les objets (depth testing)

---

## 5. Système de Physique

### Fichiers principaux

- `Engine/Physics/Physics.cs`
- `Engine/Physics/CollisionSystem.cs`
- `Engine/Components/Collider.cs`

### API

API Unity-like pour familiarité.

```csharp
public static class Physics
{
    // Raycasting
    public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance = Mathf.Infinity, int layerMask = -1, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal);
    public static RaycastHit[] RaycastAll(Vector3 origin, Vector3 direction, float maxDistance = Mathf.Infinity, int layerMask = -1, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal);
    public static int RaycastNonAlloc(Vector3 origin, Vector3 direction, RaycastHit[] results, float maxDistance = Mathf.Infinity, int layerMask = -1, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal);

    // Overlap queries
    public static bool OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, int layerMask = -1, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal);
    public static bool OverlapSphere(Vector3 center, float radius, int layerMask = -1, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal);

    // Shape casting
    public static bool SphereCast(Vector3 origin, float radius, Vector3 direction, out RaycastHit hit, float maxDistance = Mathf.Infinity, int layerMask = -1, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal);
    public static bool CapsuleCast(Vector3 point1, Vector3 point2, float radius, Vector3 direction, out RaycastHit hit, float maxDistance = Mathf.Infinity, int layerMask = -1, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal);
}
```

### CollisionSystem

**Fonctionnalités** :
- Broadphase simple AABB
- Collision detection (pas de résolution physique)
- Callbacks : OnCollisionEnter/Stay/Exit, OnTriggerEnter/Stay/Exit
- Layer mask support
- Query trigger interaction

### Colliders

**Types** :
- **BoxCollider** : Boîte alignée aux axes
- **SphereCollider** : Sphère
- **CapsuleCollider** : Capsule
- **HeightfieldCollider** : Pour terrains

**Propriétés** :
- `IsTrigger` : Volume de détection (pas de collision physique)
- `Center` : Décalage local
- `Size/Radius/Height` : Dimensions

---

## 6. Système de Terrain

### Fichiers principaux

- `Engine/Components/Terrain.cs`
- `Engine/Rendering/Terrain/TerrainRenderer.cs`
- `Engine/Rendering/HeightmapLoader.cs`

### Terrain Component

```csharp
public class Terrain : Component
{
    // Dimensions
    public float TerrainWidth { get; set; } = 1000f;
    public float TerrainLength { get; set; } = 1000f;
    public float TerrainHeight { get; set; } = 100f;
    public int MeshResolution { get; set; } = 256;

    // Heightmap
    public Guid? HeightmapTextureGuid { get; set; }

    // Material
    public Guid? TerrainMaterialGuid { get; set; }

    // Water
    public bool EnableWater { get; set; }
    public Guid? WaterMaterialGuid { get; set; }
    public float WaterHeight { get; set; } = 10f;

    // Méthodes
    public void GenerateTerrain();
    public float SampleHeight(float worldX, float worldZ);
}
```

### Fonctionnalités

- **Génération de mesh depuis heightmap** : PNG 16-bit pour haute précision
- **Interpolation bilinéaire** : Terrain smooth
- **Calcul de normales automatique** : Éclairage correct
- **Support de layers multiples** : Jusqu'à 8 layers
- **Blending automatique** : Basé sur hauteur et pente
- **Water plane intégré** : Plan d'eau avec matériau séparé
- **Collision heightfield** : Raycasting sur terrain

### Terrain Layers

```csharp
public class TerrainLayer
{
    // Textures
    public Guid? AlbedoTexture { get; set; }
    public Guid? NormalTexture { get; set; }

    // Tiling
    public float[] Tiling { get; set; } = { 1f, 1f };
    public float[] Offset { get; set; } = { 0f, 0f };

    // Blending par hauteur
    public float HeightMin { get; set; } = 0f;
    public float HeightMax { get; set; } = 1f;

    // Blending par pente
    public float SlopeMinDeg { get; set; } = 0f;
    public float SlopeMaxDeg { get; set; } = 90f;

    // Force du layer
    public float Strength { get; set; } = 1f;

    // Underwater (optionnel)
    public bool IsUnderwater { get; set; }
    public float UnderwaterHeightMax { get; set; }
    public float UnderwaterBlendDistance { get; set; }
}
```

---

## 7. Système de Scripting

### Fichiers principaux

- `Engine/Scripting/MonoBehaviour.cs`
- `Engine/Scripting/ScriptHost.cs`
- `Engine/Scripting/ScriptCompiler.cs`

### MonoBehaviour

API Unity-like pour familiarité.

```csharp
public abstract class MonoBehaviour : Component
{
    // Lifecycle
    protected virtual void Awake() { }
    // Hérite de Component: Start, Update, FixedUpdate, etc.

    // Helpers
    protected T? GetComponent<T>() where T : Component;
    protected T AddComponent<T>() where T : Component, new();
    protected void Destroy(Entity entity);
}
```

### ScriptCompiler

**Fonctionnalités** :
- Compilation automatique des scripts C# dans `Editor/Assets/Scripts/`
- File watcher pour hot-reload
- Génération d'assembly dynamique
- Événement `OnReloaded` pour recharger les types

### Workflow

1. Écrire scripts dans `Editor/Assets/Scripts/`
2. Compilation automatique au changement
3. Types découverts via réflexion
4. Attachement aux entités via inspecteur
5. Exécution en Play Mode

**Exemple de script** :

```csharp
using Engine.Scripting;
using OpenTK.Mathematics;

public class PlayerController : MonoBehaviour
{
    public float MoveSpeed = 5f;
    public float JumpForce = 10f;

    protected override void Start()
    {
        Console.WriteLine("Player initialized!");
    }

    protected override void Update(float deltaTime)
    {
        // Input handling
        if (Input.GetKey(Keys.W))
        {
            Entity.Transform.Position += Vector3.UnitZ * MoveSpeed * deltaTime;
        }
    }
}
```

---

## 8. Système d'Entrées

### Fichiers principaux

- `Engine/Input/InputManager_Clean.cs`
- `Engine/Input/InputAction.cs`
- `Engine/Input/InputBinding.cs`
- `Engine/Input/InputActionMap.cs`

### Architecture

Système d'actions et bindings (Unity New Input System style).

**InputAction** :
```csharp
public class InputAction
{
    public string Name { get; }
    public List<InputBinding> Bindings { get; }

    public bool IsPressed();
    public bool WasJustPressed();
    public bool WasJustReleased();
    public float GetValue();
    public Vector2 GetVector2();
}
```

**InputActionMap** :
```csharp
public class InputActionMap
{
    public string Name { get; }
    public Dictionary<string, InputAction> Actions { get; }

    public void Enable();
    public void Disable();
    public InputAction GetAction(string actionName);
}
```

### Action Maps

**Action Maps prédéfinis** :
- **Player** : Mouvement (WASD), Jump (Space), Camera (Mouse)
- **Vehicle** : Accelerate/Brake (Flèches), Steer, Exit (F)
- **Menu** : Navigate (Flèches), Confirm (Enter), Cancel (Escape)

**Utilisation** :
```csharp
// Activer un action map
InputManager.Instance.EnableActionMap("Player");

// Désactiver un action map
InputManager.Instance.DisableActionMap("Vehicle");

// Mode exclusif (désactive tous les autres)
InputManager.Instance.SetActiveActionMap("Menu");
```

### Contextes

**EditorInputContext** : Actif en mode Edit
- Ctrl+S (sauvegarder)
- W/E/R (outils de transformation)
- Delete (supprimer entité)

**PlayModeInputContext** : Actif en mode Play
- WASD (mouvement)
- Space (saut)
- ESC (pause)

### Intégration ImGui

**Capture automatique** :
```csharp
InputManager.Instance.SetImGuiCapture(
    ImGui.GetIO().WantCaptureKeyboard,
    ImGui.GetIO().WantCaptureMouse
);
```

Évite les conflits entre UI et gameplay.

---

## 9. Système UI - AstrildUI

### Fichiers principaux

- `Engine/UI/AstrildUI/UIBuilder.cs`
- `Engine/UI/AstrildUI/UIStyleSheet.cs`
- `Engine/UI/AstrildUI/UILayout.cs`
- `Engine/UI/AstrildUI/UIComponents.cs`

### Vue d'ensemble

AstrildUI est un système de UI déclaratif et intuitif construit au-dessus d'ImGui.NET avec une API fluide et des composants high-level.

**Philosophie** :
- **Déclaratif** : Décrivez ce que vous voulez, pas comment le construire
- **Fluent API** : Chaînage de méthodes pour un code lisible
- **Thématique** : 4 thèmes prédéfinis + customisation facile
- **Composable** : Assemblez des composants pour créer des UIs complexes

### UIBuilder

API fluide pour construction d'interfaces.

```csharp
using Engine.UI.AstrildUI;

var ui = new UIBuilder(UIStyleSheet.Default);

ui.Window("Inventory", () =>
{
    ui.Text("Welcome to your inventory!", UITextStyle.Colored);
    ui.Separator();

    if (ui.Button("Open Chest", style: UIButtonStyle.Primary))
    {
        Console.WriteLine("Chest opened!");
    }
});
```

### UIStyleSheet

Système de thèmes avec 4 thèmes prédéfinis.

**Thèmes disponibles** :
- **RPG Theme** : Rouge #E94560, dark fantasy
- **SciFi Theme** : Cyan neon #00B3FF, angles vifs
- **Minimal Theme** : Clair, blue accents
- **Fantasy Theme** : Or #B39619, tons chauds

```csharp
var theme = UIStyleSheet.CreateRPGTheme();
var ui = new UIBuilder(theme);
```

### UILayout

Helpers de layout (grilles, stacks, splits).

```csharp
// Grille 4 colonnes
UILayout.Grid(4, () =>
{
    ui.Button("Item 1");
    ui.Button("Item 2");
    ui.Button("Item 3");
    ui.Button("Item 4");
});

// Split 60/40
UILayout.Split(0.6f,
    () => { /* Left: 60% */ },
    () => { /* Right: 40% */ }
);

// Tabs
UILayout.Tabs("main_tabs", new[]
{
    ("Tab 1", () => ui.Text("Content 1")),
    ("Tab 2", () => ui.Text("Content 2"))
});
```

### UIComponents

Composants réutilisables (cards, bars, toasts).

```csharp
// Card cliquable
if (UIComponents.Card("Settings", "Configure options", "⚙️"))
{
    Console.WriteLine("Settings clicked!");
}

// Barre de stat
UIComponents.StatBar("Health", 85, 100, new Vector4(0.8f, 0.2f, 0.2f, 1));

// Progress ring
UIComponents.ProgressRing(0.65f, 50, new Vector4(0.91f, 0.27f, 0.38f, 1));

// Toast notification
UIComponents.Toast("Item received!", ToastType.Success, duration: 3f);
```

### Migration WebView2 → AstrildUI

**Raisons de la migration** :
- ❌ WebView2 : 30 FPS, 100-200ms latence, 50-70% timeouts, complexité élevée
- ✅ AstrildUI : 60+ FPS, ~0.5ms latence, 100% fiabilité, simplicité

**Avantages AstrildUI** :
- **400x plus rapide** en rendu
- **200x moins de latence**
- **100% fiable** (vs 30-50% avec WebView2)
- **Plus simple** : C# uniquement, pas de JavaScript/HTML
- **Aucune dépendance externe** (seulement ImGui.NET)

---

## 10. Système de Post-Processing

### Fichiers principaux

- `Engine/Rendering/PostProcessManager.cs`
- `Engine/Rendering/PostProcessEffects.cs`
- `Engine/Components/GlobalEffects.cs`

### GlobalEffects Component

Composant attaché à une entité "Environment" pour gérer les effets post-processing de la scène.

```csharp
public class GlobalEffects : Component
{
    public List<PostProcessEffect> Effects { get; }
}

public abstract class PostProcessEffect
{
    public bool Enabled { get; set; }
    public int Priority { get; set; }
}
```

### Effets Disponibles

**Bloom** :
- Glow sur zones lumineuses
- Threshold, intensity, radius configurables

**ToneMapping** :
- HDR → LDR mapping
- Modes : ACES, Reinhard, Uncharted2

**ChromaticAberration** :
- Aberration chromatique
- Intensité configurable

**SSAO** :
- Screen Space Ambient Occlusion
- Intégré dans forward pass
- Radius, bias, samples configurables

### Pipeline

1. Rendu de la scène → FBO
2. Application des effets par ordre de priorité
3. Chaque effet lit/écrit dans des textures
4. Composition finale

---

## 11. Système de Sérialisation

### Fichiers principaux

- `Engine/Serialization/ComponentSerializer.cs`
- `Editor/Serialization/SceneSerializer.cs`

### Attribut Serializable

Marquer les champs/propriétés à sérialiser.

```csharp
[Serializable("myField")]
public float MyField;
```

### ComponentSerializer

**Fonctionnalités** :
- Sérialisation automatique par réflexion
- Types supportés : primitifs, Vector3/4, Quaternion, Matrix4, Enum, Guid
- Sérialiseurs custom pour types complexes
- Résolution de références Entity/Component

### SceneSerializer

**Format** : JSON

**Structure** :
```json
{
  "entities": [
    {
      "id": 1,
      "name": "Cube",
      "guid": "...",
      "transform": {
        "position": [0, 0, 0],
        "rotation": [0, 0, 0, 1],
        "scale": [1, 1, 1]
      },
      "parent": null,
      "components": [
        {
          "type": "MeshRendererComponent",
          "data": { ... }
        }
      ]
    }
  ],
  "sceneSettings": { ... }
}
```

**Fonctionnalités** :
- Sauvegarde : entités, composants, hiérarchie, assets
- Chargement : reconstruction complète de scène
- Résolution de GUIDs pour assets

---

## 12. L'Éditeur

### Architecture

**Program.cs** : Point d'entrée
- Initialisation OpenTK/ImGui
- Boucle principale Update/Render
- Gestion du Play Mode

**EditorUI** : Hub central
- DockSpace pour panels
- Main menu bar
- Gestion des fenêtres
- Raccourcis clavier

### Panels Principaux

**ViewportPanel** :
- Rendu 3D de la scène
- Gizmos de transformation
- Camera controls (orbit, pan, zoom)
- Entity picking (raycasting)

**HierarchyPanel** :
- Arbre des entités
- Drag & drop pour reparenting
- Création/suppression d'entités
- Sélection

**InspectorPanel** :
- Affichage/édition de composants
- Widgets custom par type
- Material editor
- Component add/remove

**AssetsPanel** :
- Vue du dossier Assets/
- Import de fichiers (drag & drop OS)
- Création de matériaux
- Thumbnail preview

**ConsolePanel** :
- Logs (Info, Warning, Error)
- Filtres par type
- Clear/Copy

**GamePanel** :
- Vue du jeu en Play Mode
- Input capture
- Cursor lock

**EnvironmentPanel** :
- Lighting settings
- Skybox configuration
- Shadow settings

**RenderingSettingsPanel** :
- SSAO configuration
- Post-processing toggles

### Inspecteurs Custom

Architecture modulaire pour édition de composants.

**ComponentInspector** : Base class
- `LightInspector`
- `MaterialInspector`
- `TerrainInspector`
- `CameraInspector`
- `BoxColliderInspector`
- `SphereColliderInspector`
- `CapsuleColliderInspector`

**FieldWidgets** : Widgets réutilisables
- FloatField, Vector3Field, ColorPicker
- TextureSelector, EntityRef, ComponentRef
- Enum dropdown

---

## 13. Play Mode

### Fichiers principaux

- `Editor/PlayMode.cs`

### États

- **Edit** : Mode édition
- **Playing** : Simulation en cours
- **Paused** : Simulation en pause

### Workflow

1. **Play** : Clone de la scène originale
2. **Simulation** : Update des composants (Update, FixedUpdate, LateUpdate)
3. **Stop** : Retour à la scène originale

### Clonage de Scène

**Processus** :
- Deep copy de toutes les entités
- Copie des composants avec leurs données
- Résolution des références internes
- Création de nouveaux GUIDs
- Conservation de la hiérarchie

### Physics Integration

- FixedUpdate à 50 FPS
- CollisionSystem.Step() avant FixedUpdate
- Callbacks de collision

---

## 14. Système de Lumières

### Types

```csharp
public enum LightType
{
    Directional, // Soleil
    Point,       // Ampoule
    Spot         // Projecteur
}
```

### LightComponent

```csharp
public class LightComponent : Component
{
    public LightType Type { get; set; }
    public Vector3 Color { get; set; }
    public float Intensity { get; set; }
    public float Range { get; set; }          // Point/Spot
    public float SpotAngle { get; set; }      // Spot
    public float SpotInnerAngle { get; set; } // Spot
    public bool CastShadows { get; set; }
}
```

### Shadow Mapping

**ShadowManager** :
- Single shadow map (1024×1024 par défaut)
- Support CSM (Cascaded Shadow Maps) - 4 cascades max
- PCF (Percentage Closer Filtering)
- Bias configurable
- Light-space matrix per cascade

---

## 15. Système de Caméra

### CameraComponent

```csharp
public class CameraComponent : Component
{
    public enum ProjectionMode
    {
        Perspective,
        Orthographic,
        TwoD
    }

    public ProjectionMode Projection { get; set; }
    public float FieldOfView { get; set; } = 60f;  // Perspective
    public float OrthoSize { get; set; } = 10f;    // Orthographic
    public float Near { get; set; } = 0.1f;
    public float Far { get; set; } = 1000f;
    public bool IsMain { get; set; }

    public Matrix4 ViewMatrix { get; }
    public Matrix4 ProjectionMatrix(float aspect);
}
```

### Camera Behaviors

**Note** : CameraComponent est data-only. Les behaviors sont implémentés dans des scripts MonoBehaviour.

**FPS Camera** :
- WASD movement
- Mouse look
- Sprint
- Smoothing

**Orbit Camera** :
- Follow target entity
- Mouse orbit
- Zoom avec scroll
- Collision detection
- Pitch clamping

---

## 16. Asset Management

### AssetDatabase

**Fonctionnalités** :
- Index automatique des assets dans `Editor/Assets/`
- GUID persistant via fichiers .meta
- Types supportés :
  - Textures : PNG, JPG, TGA, HDR
  - Modèles : GLTF, GLB, FBX
  - Matériaux : .material
  - Skybox : .skymat
  - Fonts : TTF, OTF, .fontasset

**API** :
```csharp
AssetDatabase.Initialize(assetsDir);
AssetDatabase.Refresh();
AssetDatabase.TryGet(guid, out record);
AssetDatabase.CreateMaterial(name);
AssetDatabase.SaveMaterial(material);
AssetDatabase.LoadMaterial(guid);
```

### TextureCache

**Fonctionnalités** :
- Cache GPU des textures chargées
- Lazy loading
- GUID → OpenGL texture ID
- Support sRGB/Linear

---

## 17. Mathématiques

### Fichiers

- `Engine/Mathx/Mathf.cs`
- `Engine/Mathx/QuatUtil.cs`
- `Engine/Mathx/LH.cs`
- `Engine/Mathx/Noise/*.cs`

### Utilitaires

**Mathf** : Helpers mathématiques
- Clamp, Lerp, InverseLerp
- SmoothStep, Remap
- Approximately (float comparison)

**QuatUtil** : Quaternion helpers
- ToEuler, FromEuler
- Slerp, LookRotation

**LH** : Left-Handed coordinate system
- LookAtLH : View matrix left-handed

**Noise** :
- **PerlinNoise** : Bruit de Perlin classique
- **SimplexNoise** : Bruit de simplex (plus rapide)
- **VoronoiNoise** : Bruit de Voronoi (cellulaire)

---

## 18. Dépendances

### Packages NuGet

| Package | Version | Usage |
|---------|---------|-------|
| **OpenTK** | 4.9.4 | Graphics, Windowing, Mathematics |
| **ImGui.NET** | 1.91.6.1 | UI de l'éditeur |
| **Serilog** | 4.3.0 | Logging structuré |
| **Serilog.Sinks.Console** | 6.0.0 | Sink pour console panel |
| **StbImageSharp** | 2.30.15 | Chargement d'images |
| **SixLabors.ImageSharp** | 3.1.11 | Traitement d'images |
| **SixLabors.ImageSharp.Drawing** | 2.1.7 | Dessin d'images |
| **SixLabors.Fonts** | 2.1.3 | Rendu de fonts |
| **SharpGLTF.Core** | 1.0.5 | Import de modèles GLTF |
| **Microsoft.CodeAnalysis.CSharp** | 4.11.0 | Compilation de scripts |
| **System.Drawing.Common** | 8.0.8 | Interop avec System.Drawing |

### Requirements

- **.NET 8.0** SDK
- **Windows 10/11 x64**
- **OpenGL 4.6** compatible GPU
- **Visual Studio 2022** ou **JetBrains Rider** (recommandé)

---

## 19. Workflow de Développement

### Développement de Jeu

1. **Créer scène** dans l'éditeur
2. **Ajouter entités** : Hierarchy → Create
3. **Configurer composants** : Inspector
4. **Écrire scripts** : `Editor/Assets/Scripts/` (MonoBehaviour)
5. **Attacher scripts** : Inspector → Add Component
6. **Tester** : Play Mode (Ctrl+P ou bouton Play ▶️)
7. **Sauvegarder** : File → Save Scene (Ctrl+S)

### Création de Matériau

1. Assets → Create Material
2. Inspector : Configurer shader, couleurs, textures
3. Assigner à entité : MeshRenderer → Material
4. Live preview dans viewport

### Création de Terrain

1. Hierarchy → Create → Terrain
2. Inspector : Configurer dimensions, résolution
3. Importer heightmap (PNG 16-bit)
4. Configurer layers (textures, height/slope ranges)
5. Optionnel : Ajouter water plane

### Développement UI

1. Créer système de menu avec AstrildUI
2. Utiliser UIBuilder pour construire l'interface
3. Tester en Play Mode (ESC pour toggle menu)
4. Itérer rapidement (hot reload C#)

---

## 20. Conventions de Code

### Naming

- **Classes** : PascalCase (ex: `MeshRendererComponent`)
- **Methods** : PascalCase (ex: `GetWorldTRS()`)
- **Private fields** : _camelCase (ex: `_vao`, `_meshCache`)
- **Public fields/props** : PascalCase (ex: `Entity`, `Enabled`)

### Organisation

- **Engine/** : Code runtime pur (pas de dépendance Editor)
- **Editor/** : Dépend de Engine, contient outils
- Séparation claire Data (Engine) vs Tools (Editor)

### Serialization

- Attribut `[Serializable("name")]` sur fields/properties
- Types supportés : primitifs, Vector, Quaternion, Enum, Guid
- References Entity/Component via GUID

---

## Points d'Extension

### Nouveaux Composants

1. Créer classe héritant de `Component`
2. Marquer propriétés avec `[Serializable]`
3. Implémenter lifecycle hooks (Update, etc.)
4. Créer inspecteur custom dans Editor/

### Nouveaux Shaders

1. Ajouter .vert/.frag dans `Engine/Rendering/Shaders/`
2. Utiliser `#include` pour modules réutilisables
3. ShaderLibrary découvre automatiquement
4. Référencer par nom dans matériau

### Nouveaux Effets Post-Processing

1. Hériter de `PostProcessEffect`
2. Implémenter `IPostProcessRenderer`
3. Enregistrer dans `PostProcessManager`
4. Ajouter à `GlobalEffects` component

### Nouveaux Asset Types

1. Définir classe asset avec GUID
2. Ajouter extension dans `AssetDatabase.GuessTypeFromExtension()`
3. Implémenter Load/Save
4. Créer inspecteur custom

---

## Limitations Actuelles

### Rendering

- Pas de deferred rendering
- Forward rendering uniquement
- CSM avec 4 cascades max
- Pas de LOD automatique

### Physics

- Broadphase simple N² (pas de spatial partitioning)
- Pas de rigidbody dynamics
- Collisions détection only (pas de résolution physique)
- Pas de joints/constraints

### Animation

- Pas de système d'animation squelettique
- Pas de blend trees
- Pas de state machines

### Audio

- Pas de système audio intégré

### Networking

- Pas de support multijoueur

---

## 21. Fonctionnalités à Venir

Cette section présente les fonctionnalités prévues pour les prochaines versions du moteur, organisées par ordre de priorité.

### Priorité Critique (Version 0.2.0)

#### 1. Système d'Animation Squelettique
**Priorité** : ★★★★★
**Complexité** : Élevée
**Description** : Système complet d'animation pour personnages et objets riggés.

**Fonctionnalités** :
- Import d'animations depuis GLTF/FBX
- Skinned mesh rendering avec bone transforms
- Animation blending et transitions
- Animator component avec state machine
- Support d'animations procédurales (IK)

**Bénéfices** :
- Personnages animés (marche, course, saut)
- Objets animés (portes, coffres)
- Cutscenes et cinématiques

#### 2. Système Audio
**Priorité** : ★★★★★
**Complexité** : Moyenne
**Description** : Système audio spatial pour effets sonores et musique.

**Fonctionnalités** :
- AudioSource component (3D et 2D)
- AudioListener component (attaché à la caméra)
- Support formats : WAV, MP3, OGG
- Spatial audio avec atténuation
- Audio mixer avec canaux (SFX, Music, Voice)
- Effets audio : reverb, echo, filters

**Bénéfices** :
- Ambiance sonore immersive
- Feedback sonore des actions
- Musique dynamique

#### 3. Prefab System
**Priorité** : ★★★★☆
**Complexité** : Moyenne
**Description** : Système de préfabriqués réutilisables pour entités.

**Fonctionnalités** :
- Création de prefabs depuis entités
- Instanciation de prefabs dans la scène
- Modification de prefabs (propagation aux instances)
- Override de propriétés par instance
- Nested prefabs (prefabs dans prefabs)

**Bénéfices** :
- Réutilisabilité des entités complexes
- Itération rapide sur objets répétés
- Workflow plus productif

### Priorité Haute (Version 0.3.0)

#### 4. Système de Particules
**Priorité** : ★★★★☆
**Complexité** : Élevée
**Description** : Système de particules pour effets visuels (feu, fumée, magie, etc.).

**Fonctionnalités** :
- ParticleSystem component
- Emission shapes (sphère, cône, box)
- Color over lifetime, size over lifetime
- Velocity, gravity, collision
- Texture sheet animation (flipbook)
- Sub-emitters (explosion en chaîne)

**Bénéfices** :
- Effets visuels riches (explosions, feu, fumée)
- Feedback visuel des actions
- Ambiance environnementale (pluie, neige)

#### 5. Navmesh et Pathfinding
**Priorité** : ★★★★☆
**Complexité** : Élevée
**Description** : Système de navigation pour IA.

**Fonctionnalités** :
- Génération de navmesh depuis géométrie
- NavMeshAgent component pour déplacement
- A* pathfinding optimisé
- Dynamic obstacles
- Off-mesh links (sauts, téléportations)
- Multi-agent crowd simulation

**Bénéfices** :
- IA navigant intelligemment
- Ennemis qui poursuivent le joueur
- NPCs se déplaçant naturellement

#### 6. Deferred Rendering Pipeline
**Priorité** : ★★★☆☆
**Complexité** : Élevée
**Description** : Pipeline de rendu différé pour supporter plus de lumières.

**Fonctionnalités** :
- G-Buffer (albedo, normal, metallic/roughness, depth)
- Lighting pass séparé
- Support de centaines de lumières
- Screen-space reflections (SSR)
- Decals support

**Bénéfices** :
- Performance accrue avec nombreuses lumières
- Effets visuels avancés (SSR, decals)
- Meilleure scalabilité

### Priorité Moyenne (Version 0.4.0)

#### 7. Visual Scripting
**Priorité** : ★★★☆☆
**Complexité** : Très Élevée
**Description** : Système de scripting visuel par nodes pour designers.

**Fonctionnalités** :
- Graph editor avec nodes
- Variables, flow control (if/else, loops)
- Event system (OnTrigger, OnClick, etc.)
- Mathématiques et logique
- Intégration C# (appel de méthodes)

**Bénéfices** :
- Designers peuvent scripter sans code
- Prototypage rapide de gameplay
- Logique de jeu accessible

#### 8. Physically-Based Rigidbody Dynamics
**Priorité** : ★★★☆☆
**Complexité** : Élevée
**Description** : Simulation physique complète avec dynamique des corps rigides.

**Fonctionnalités** :
- Rigidbody component (masse, vélocité, force)
- Résolution de collisions physiques
- Joints et constraints (hinge, spring, fixed)
- Continuous collision detection (CCD)
- Sleeping et optimisations

**Bénéfices** :
- Objets physiques réalistes
- Puzzles physiques
- Destruction et ragdolls

#### 9. LOD (Level of Detail) System
**Priorité** : ★★★☆☆
**Complexité** : Moyenne
**Description** : Système de niveaux de détail pour optimisation.

**Fonctionnalités** :
- LOD groups avec plusieurs niveaux de mesh
- Transition automatique basée sur distance
- Culling agressif pour objets lointains
- Billboard imposters pour très grande distance

**Bénéfices** :
- Performance accrue en scènes complexes
- Mondes ouverts optimisés
- Plus d'objets visibles simultanément

#### 10. Scene Streaming et Occlusion Culling
**Priorité** : ★★★☆☆
**Complexité** : Élevée
**Description** : Chargement progressif et culling avancé pour grands mondes.

**Fonctionnalités** :
- Scene streaming (load/unload par zones)
- Async loading avec loading screens
- Occlusion culling (pas de rendu caché)
- Portal-based culling
- Distance-based streaming

**Bénéfices** :
- Mondes ouverts immenses
- Chargement seamless sans écrans
- Performance optimale

### Priorité Basse (Version 0.5.0+)

#### 11. Réseau Multijoueur
**Priorité** : ★★☆☆☆
**Complexité** : Très Élevée
**Description** : Support multijoueur networked.

**Fonctionnalités** :
- Client-server architecture
- Entity synchronization
- RPC (Remote Procedure Calls)
- Lag compensation et interpolation
- Authoritative server

**Bénéfices** :
- Jeux multijoueur en ligne
- Co-op local et online
- Matchmaking et lobbies

#### 12. Mobile Platform Support
**Priorité** : ★★☆☆☆
**Complexité** : Très Élevée
**Description** : Support des plateformes mobiles (Android, iOS).

**Fonctionnalités** :
- OpenGL ES rendering
- Touch input support
- Gyroscope et accéléromètre
- Performance profiling mobile
- App lifecycle management

**Bénéfices** :
- Déploiement sur mobiles
- Élargissement de l'audience
- Jeux cross-platform

#### 13. VR/XR Support
**Priorité** : ★☆☆☆☆
**Complexité** : Très Élevée
**Description** : Support de la réalité virtuelle et augmentée.

**Fonctionnalités** :
- Stereo rendering
- Head tracking
- Controller input (6DOF)
- OpenXR integration
- Performance optimizations (foveated rendering)

**Bénéfices** :
- Expériences immersives VR
- Support Quest, PSVR, etc.
- Jeux en réalité augmentée

#### 14. Advanced Weather System
**Priorité** : ★☆☆☆☆
**Complexité** : Élevée
**Description** : Système météorologique dynamique.

**Fonctionnalités** :
- Rain, snow, fog dynamiques
- Wind zones affectant particules et végétation
- Day/night cycle
- Volumetric clouds
- Lightning et effets atmosphériques

**Bénéfices** :
- Mondes vivants et dynamiques
- Ambiance immersive
- Gameplay basé sur météo

#### 15. Vegetation System
**Priorité** : ★☆☆☆☆
**Complexité** : Élevée
**Description** : Système de végétation optimisé pour grands environnements.

**Fonctionnalités** :
- Grass painting et instancing
- Tree placement et batching
- Wind animation
- LOD et culling agressif
- Detail distance configurable

**Bénéfices** :
- Forêts et prairies denses
- Performance optimale
- Outils de level design

### Améliorations Continues

Ces améliorations seront implémentées progressivement au fil des versions :

**Performance** :
- Multithreading (job system)
- GPU compute shaders
- Frustum culling optimisé
- Spatial partitioning (octree, BVH)

**Éditeur** :
- Terrain sculpting tools
- Material graph editor
- Timeline pour cutscenes
- Profiler intégré
- Scene view gizmos customisables

**Rendering** :
- Global Illumination (GI)
- Volumetric fog et lighting
- Ray tracing (optionnel)
- Advanced water (waves, foam, caustics)

**UI/UX** :
- AstrildUI animations
- Data binding bidirectionnel
- Responsive layouts
- Hot reload UI

**Quality of Life** :
- Auto-save et crash recovery
- Version control integration
- Asset bundles pour builds
- Build pipeline customisable
- Plugin system

---

## 22. Comment Contribuer

### Proposer une Fonctionnalité

Si vous souhaitez proposer une nouvelle fonctionnalité :

1. **Vérifier** qu'elle n'existe pas déjà dans la roadmap ci-dessus
2. **Créer une issue** sur le repository GitHub avec le tag `feature-request`
3. **Décrire** :
   - Le problème que ça résout
   - Les cas d'usage
   - La complexité estimée
   - Les alternatives considérées

### Implémenter une Fonctionnalité

Pour implémenter une fonctionnalité de la roadmap :

1. **Commenter** l'issue correspondante pour indiquer que vous travaillez dessus
2. **Créer une branche** : `feature/nom-de-la-feature`
3. **Développer** en suivant les conventions de code
4. **Tester** exhaustivement
5. **Documenter** dans le code et dans DOCUMENTATION.md
6. **Pull Request** avec description détaillée

---

**AstrildApex** - Moteur de jeu 3D en C# avec éditeur intégré
Version 0.1.0 - Octobre 2025

---

## Consolidated Guides (merged content)

The following sections were merged from individual guide files across the repository. Each subsection preserves the original document title and relative path for provenance.


### UI_SYSTEM_COMPLETE.md — /UI_SYSTEM_COMPLETE.md

````markdown
# 🎉 Mission Complete: RPG HUD & UIBuilder API Extensions

## ✅ Objectifs Accomplis

### 1. **Curseur Mode Locked - FIXÉ** 🎯
- ✅ Curseur **invisible** en gameplay (mode Locked FPS)
- ✅ Curseur **visible et libre** dans le menu (ESC)
- ✅ Curseur **visible** après sortie du Play Mode
- ✅ Fix du compteur ShowCursor Win32 (boucle de reset)
- ✅ Ordre correct : `CursorState.Normal` → puis `visible=true`

... (content truncated in documentation for brevity; full content retained in repo before deletion)
````


### THEME_READABILITY_TEST_GUIDE.md — /THEME_READABILITY_TEST_GUIDE.md

````markdown
# Guide de Test - Améliorations de Lisibilité 🎯

## Comment Tester les Améliorations

... (omitted)
````


### THEME_SYSTEM_GUIDE.md — /THEME_SYSTEM_GUIDE.md

````markdown
# 🎨 Theme System Guide - AstrildApex Editor

## Vue d'ensemble

... (omitted)
````

### THEME_READABILITY_IMPROVEMENTS.md — /THEME_READABILITY_IMPROVEMENTS.md

````markdown
(empty file)
````

### THEME_COLLECTION.md — /THEME_COLLECTION.md

````markdown
# 🎨 AstrildApex Theme Collection

... (omitted)
````

### TERRAIN_SHADER_FIX.md — /TERRAIN_SHADER_FIX.md

````markdown
# 🔧 Fix: Terrain Shader Loading Issue

... (omitted)
````

### TERRAIN_WORKFLOW_GUIDE.md — /TERRAIN_WORKFLOW_GUIDE.md

````markdown
# Terrain System - Complete Workflow Guide

... (omitted)
````

### TERRAIN_PLAYMODE_FIX_FINAL.md — /TERRAIN_PLAYMODE_FIX_FINAL.md

````markdown
(original file merged)
````

### TERRAIN_MATERIAL_SYSTEM.md — /TERRAIN_MATERIAL_SYSTEM.md

````markdown
(original file merged)
````

### SSAO_IMPLEMENTATION_NOTES.md — /SSAO_IMPLEMENTATION_NOTES.md

````markdown
# SSAO Implementation Notes

## 🎯 Question : Pourquoi le bruit SSAO suit-il l'écran ?

... (omitted)
````

### SHADOWMAP_TEST_GUIDE.md — /SHADOWMAP_TEST_GUIDE.md

````markdown
# Guide de Test - Corrections Shadow Mapping

... (omitted)
````

### SHADOWMAP_FIX_SUMMARY.md — /SHADOWMAP_FIX_SUMMARY.md

````markdown
# Correction du système Shadow Mapping - Résumé

... (omitted)
````

### REFACTORING_GUIDE.md — /REFACTORING_GUIDE.md

````markdown
# Panel & Overlay Refactoring Guide

... (omitted)
````

### MONOBEHAVIOUR_LIFECYCLE.md — /MONOBEHAVIOUR_LIFECYCLE.md

````markdown
# MonoBehaviour Lifecycle Methods

... (omitted)
````

### MODERN_UI_TODO.md — /MODERN_UI_TODO.md

````markdown
# ✨ Refactorisation UI Moderne - ViewportPanel & GamePanel

... (omitted)
````

### MODERN_UI_REFACTORING.md — /MODERN_UI_REFACTORING.md

````markdown
# 🎨 Modern UI Refactoring - ViewportPanel & GamePanel

... (omitted)
````

### MENU_ANALYSIS.md — /MENU_ANALYSIS.md

````markdown
# Analyse des menus contextuels - Hierarchy & Assets Panel

... (omitted)
````

### MAXIMIZE_ON_PLAY_IMPLEMENTATION.md — /MAXIMIZE_ON_PLAY_IMPLEMENTATION.md

````markdown
# 🎮 Maximize on Play - Implementation Plan

... (omitted)
````

### MAXIMIZE_ON_PLAY_COMPLETE.md — /MAXIMIZE_ON_PLAY_COMPLETE.md

````markdown
# ✅ Maximize on Play - Implémentation Complète

... (omitted)
````

### MATERIAL_HOTRELOAD_SYSTEM.md — /MATERIAL_HOTRELOAD_SYSTEM.md

````markdown
# Material Hot-Reload System

... (omitted)
````

### INSPECTOR_WIDGETS_GUIDE.md — /INSPECTOR_WIDGETS_GUIDE.md

````markdown
# 🎨 Inspector UX System - Guide Complet

... (omitted)
````

### INSPECTOR_UX_AUDIT.md — /INSPECTOR_UX_AUDIT.md

````markdown
(original file merged)
````

### INSPECTOR_PHASE2_UI_COMPLETE.md — /INSPECTOR_PHASE2_UI_COMPLETE.md

````markdown
(original file merged)
````

### INSPECTOR_PHASE1_COMPLETE.md — /INSPECTOR_PHASE1_COMPLETE.md

````markdown
(original file merged)
````

### INSPECTOR_AUDIT_REPORT.md — /INSPECTOR_AUDIT_REPORT.md

````markdown
(original file merged)
````

### INPUT_SYSTEM_COMPLETE.md — /INPUT_SYSTEM_COMPLETE.md

````markdown
# Input System - Complete Implementation Summary

... (omitted)
````

### INPUT_SETTINGS_GUIDE.md — /INPUT_SETTINGS_GUIDE.md

````markdown
# Guide d'utilisation - Input Settings Panel

... (omitted)
````

### FONT_PREFERENCES_UX_DESIGN.md — /FONT_PREFERENCES_UX_DESIGN.md

````markdown
# Interface Font Preferences - UX Design Implementation

... (omitted)
````

### FLOATING_3D_INFO_GUIDE.md — /FLOATING_3D_INFO_GUIDE.md

````markdown
(original file merged)
````

### FIX_TERRAIN_LAYERS_UPDATE.md — /FIX_TERRAIN_LAYERS_UPDATE.md

````markdown
(original file merged)
````

### EXTERNAL_TOOLS_IMPLEMENTATION.md — /EXTERNAL_TOOLS_IMPLEMENTATION.md

````markdown
# 🛠️ External Tools - Unity-Style Script Editor Integration

... (omitted)
````

### EXTERNAL_TOOLS_COMPLETE_GUIDE.md — /EXTERNAL_TOOLS_COMPLETE_GUIDE.md

````markdown
# External Tools - Complete Integration Guide

... (omitted)
````

### IMGUI_ID_CONFLICT_FIX.md — /IMGUI_ID_CONFLICT_FIX.md

````markdown
# ImGui ID Conflict Fix - Complete

... (omitted)
````

### TERRAIN_PLAYMODE_FIX.md — /TERRAIN_PLAYMODE_FIX.md

````markdown
# 🔧 Fix: Terrain Disparaît en Sortant du Play Mode

... (omitted)
````

### GAME_PANEL_OPTIONS.md — /GAME_PANEL_OPTIONS.md

````markdown
# 🎮 Game Panel Options - Unity-Style Settings

... (omitted)
````

### ASTRILD_UI_PRODUCTION_GUIDE.md — /ASTRILD_UI_PRODUCTION_GUIDE.md

````markdown
(original file merged)
````

### ASTRILD_UI_GUIDE.md — /ASTRILD_UI_GUIDE.md

````markdown
(original file merged)
````

### Engine/Rendering/Shadows/SHADOWS_README.md — /Engine/Rendering/Shadows/SHADOWS_README.md

````markdown
(original file merged)
````

### Engine/Rendering/Shadows/INTEGRATION_GUIDE.md — /Engine/Rendering/Shadows/INTEGRATION_GUIDE.md

````markdown
(original file merged)
````

### Engine/Rendering/Shaders/SSAO/SSAO_README.md — /Engine/Rendering/Shaders/SSAO/SSAO_README.md

````markdown
(original file merged)
````

---

Notes:
- Originals have been merged into this central document. The repository will remove the original files as requested. A `docs-backup` branch exists (remote) as a safety snapshot if you want to recover any originals.

