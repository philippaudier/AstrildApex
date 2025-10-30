# AstrildApex Game Engine

**Version 0.1.0** - Moteur de jeu 3D en C# avec éditeur intégré

---

## Description

AstrildApex est un moteur de jeu 3D développé en C# utilisant OpenGL 4.6 pour le rendu. Il adopte une architecture Entity-Component-System (ECS) avec un éditeur visuel de type Unity-like, permettant le développement de jeux 3D avec des outils complets.

### Caractéristiques Principales

- **Architecture ECS** complète avec gestion de hiérarchie
- **Rendu PBR** (Physically Based Rendering) avec shaders modulaires
- **Éditeur visuel** avec panels (Hiérarchie, Inspecteur, Assets, Console, Viewport 3D)
- **Play Mode** intégré avec clonage de scène
- **Hot-Reload** des shaders et scripts
- **Système de terrain** avec heightmap et layers multiples
- **Physique** (raycasting, collision detection, triggers)
- **UI moderne** : AstrildUI (système déclaratif basé sur ImGui.NET)
- **Post-processing** : Bloom, tone mapping, SSAO, aberration chromatique
- **Système de lumières** : Directional, Point, Spot avec ombres CSM
- **Système d'entrées** avancé avec action maps

---

## Prérequis

- **.NET 8.0** SDK
- **Windows 10/11** (x64)
- **OpenGL 4.6** compatible GPU
- **Visual Studio 2022** ou **JetBrains Rider** (recommandé)

---

## Quick Start

### 1. Cloner le repository

```bash
git clone https://github.com/votre-username/AstrildApex.git
cd AstrildApex
```

### 2. Restaurer les packages NuGet

```bash
dotnet restore
```

### 3. Compiler le projet

```bash
dotnet build
```

### 4. Lancer l'éditeur

```bash
cd Editor
dotnet run
```

Ou ouvrir `AstrildApex.sln` dans Visual Studio/Rider et lancer le projet **Editor**.

---

## Documentation

### Documentation Principale

📖 **[DOCUMENTATION.md](DOCUMENTATION.md)** - Documentation complète du moteur (1600+ lignes)

Cette documentation couvre :
- Architecture globale
- Système ECS
- Système de rendu et shaders
- Système de physique
- Système de terrain
- Système de scripting (MonoBehaviour)
- Système d'entrées
- Système UI (AstrildUI)
- Système de post-processing
- Sérialisation
- L'éditeur et ses panels
- Play Mode
- Asset management
- Conventions de code
- **Fonctionnalités à venir** (roadmap détaillée)

### Documentation Complémentaire

📘 **[ASTRILD_UI_GUIDE.md](ASTRILD_UI_GUIDE.md)** - Guide complet d'AstrildUI (700+ lignes)

Guide détaillé du système UI avec :
- API fluide (UIBuilder)
- Système de thèmes (UIStyleSheet)
- Layouts et composants
- Exemples pratiques (menus RPG, HUD, dialogues, crafting)
- Migration depuis ImGui brut
- Best practices

---

## Structure du Projet

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
├── Materials/          # Matériaux (.material)
├── DOCUMENTATION.md    # 📖 Documentation complète
├── ASTRILD_UI_GUIDE.md # 📘 Guide AstrildUI
└── README.md           # Ce fichier
```

---

## Workflow de Développement

### Créer un Jeu

1. **Lancer l'éditeur** : `cd Editor && dotnet run`
2. **Créer une scène** : File → New Scene
3. **Ajouter des entités** : Hierarchy → Create (Cube, Sphere, etc.)
4. **Configurer les composants** : Inspector
5. **Écrire des scripts** : Créer des classes héritant de `MonoBehaviour` dans `Editor/Assets/Scripts/`
6. **Attacher des scripts** : Inspector → Add Component
7. **Tester** : Play Mode (Ctrl+P ou bouton Play ▶️)
8. **Sauvegarder** : File → Save Scene (Ctrl+S)

### Créer des Scripts

```csharp
using Engine.Scripting;
using OpenTK.Mathematics;

public class PlayerController : MonoBehaviour
{
    public float MoveSpeed = 5f;

    protected override void Update(float deltaTime)
    {
        // Logique de déplacement
        if (Input.GetKey(Keys.W))
        {
            Entity.Transform.Position += Vector3.UnitZ * MoveSpeed * deltaTime;
        }
    }
}
```

Les scripts sont automatiquement compilés au changement et peuvent être attachés aux entités via l'inspecteur.

### Créer des UI avec AstrildUI

```csharp
using Engine.UI.AstrildUI;

var ui = new UIBuilder(UIStyleSheet.CreateRPGTheme());

ui.Window("Inventory", () =>
{
    ui.Text("Your Inventory", UITextStyle.Colored);
    ui.Separator();

    UILayout.Grid(4, () =>
    {
        foreach (var item in inventory)
        {
            if (UIComponents.ItemCard(item.Name, item.Rarity, item.Quantity))
            {
                // Item clicked
            }
        }
    });
});
```

Voir **[ASTRILD_UI_GUIDE.md](ASTRILD_UI_GUIDE.md)** pour des exemples complets.

---

## Dépendances

- **OpenTK** 4.9.4 - Graphics, Windowing, Mathematics
- **ImGui.NET** 1.91.6.1 - UI de l'éditeur
- **Serilog** 4.3.0 - Logging
- **StbImageSharp** 2.30.15 - Chargement d'images
- **SixLabors.ImageSharp** 3.1.11 - Traitement d'images
- **SharpGLTF.Core** 1.0.5 - Import de modèles GLTF
- **Microsoft.CodeAnalysis.CSharp** 4.11.0 - Compilation de scripts

---

## Fonctionnalités à Venir

Voir la section **"Fonctionnalités à Venir"** dans [DOCUMENTATION.md](DOCUMENTATION.md) pour la roadmap complète.

### Priorité Critique (v0.2.0)
- Système d'animation squelettique
- Système audio spatial
- Prefab system

### Priorité Haute (v0.3.0)
- Système de particules
- Navmesh et pathfinding
- Deferred rendering pipeline

### Priorité Moyenne (v0.4.0)
- Visual scripting
- Physically-based rigidbody dynamics
- LOD system
- Scene streaming et occlusion culling

### Priorité Basse (v0.5.0+)
- Réseau multijoueur
- Support mobile (Android, iOS)
- VR/XR support
- Advanced weather system
- Vegetation system

---

## Raccourcis Clavier (Éditeur)

### Général
- **Ctrl+S** : Sauvegarder la scène
- **Ctrl+Z** : Undo
- **Ctrl+Shift+Z** / **Ctrl+Y** : Redo
- **Ctrl+P** : Play/Stop Mode
- **Delete** : Supprimer l'entité sélectionnée

### Viewport
- **W** : Outil de translation (Move)
- **E** : Outil de rotation (Rotate)
- **R** : Outil de mise à l'échelle (Scale)
- **F** : Frame Selected (centrer la caméra sur l'entité sélectionnée)
- **Clic droit + WASD** : Fly camera
- **Clic milieu + drag** : Pan
- **Molette** : Zoom
- **Alt + clic gauche** : Orbit autour du pivot

### Play Mode
- **ESC** : Toggle menu pause (avec AstrildUI)

---

## Conventions de Code

### Naming
- **Classes** : PascalCase (`MeshRendererComponent`)
- **Methods** : PascalCase (`GetWorldTRS()`)
- **Private fields** : _camelCase (`_vao`, `_meshCache`)
- **Public fields/props** : PascalCase (`Entity`, `Enabled`)

### Serialization
- Attribut `[Serializable("name")]` sur fields/properties à sérialiser
- Types supportés : primitifs, Vector, Quaternion, Enum, Guid

---

## Contribution

Les contributions sont les bienvenues! Consultez [DOCUMENTATION.md](DOCUMENTATION.md) section "Comment Contribuer" pour plus d'informations.

### Pour proposer une fonctionnalité :
1. Vérifier qu'elle n'existe pas déjà dans la roadmap
2. Créer une issue sur GitHub avec le tag `feature-request`
3. Décrire le problème, les cas d'usage, la complexité

### Pour implémenter une fonctionnalité :
1. Commenter l'issue correspondante
2. Créer une branche `feature/nom-de-la-feature`
3. Développer en suivant les conventions de code
4. Tester exhaustivement
5. Documenter dans le code et dans DOCUMENTATION.md
6. Pull Request avec description détaillée

---

## Licence

[À définir]

---

## Contact

[À définir]

---

**AstrildApex** - Créé avec ❤️ en C#
