# 🐛 Bug Fix: Double TransformComponent

## Problème Identifié

**Date**: 7 Décembre 2025  
**Erreur**: `System.InvalidOperationException: Entity already has component of type TransformComponent`

### Trace de l'erreur
```
at Engine.Scene.Entity.AddComponent(Component component)
at Engine.Scene.Entity.AddComponent[T]()
at Editor.Panels.EditorUI.DrawDockspaceAndMainMenu()
```

### Cause Racine
Le constructeur `Entity()` ajoute **automatiquement** un `TransformComponent`:

```csharp
// Engine/Scene/Scene.cs ligne 156
public Entity()
{
    Transform.SetOwner(this);
    // Add mandatory TransformComponent
    var transformComp = AddComponent<TransformComponent>();
    // Synchroniser TransformComponent avec Entity.Transform initial
    SyncTransformComponent();
}
```

**Erreur**: Dans EditorUI.cs, on ajoutait manuellement `AddComponent<TransformComponent>()` après la création, causant un doublon.

---

## ❌ Code Avant (Incorrect)

```csharp
// GameObject > 3D Object > Cube
var cube = new Engine.Scene.Entity
{
    Id = scene.GetNextEntityId(),
    Name = "Cube",
    Guid = Guid.NewGuid(),
    Active = true
};
cube.AddComponent<Engine.Components.TransformComponent>(); // ❌ ERREUR: Déjà ajouté!
scene.Entities.Add(cube);

// GameObject > Create Empty
var empty = new Engine.Scene.Entity
{
    Id = scene.GetNextEntityId(),
    Name = "Empty",
    Guid = Guid.NewGuid(),
    Active = true
};
empty.AddComponent<Engine.Components.TransformComponent>(); // ❌ ERREUR: Déjà ajouté!
scene.Entities.Add(empty);
```

---

## ✅ Code Après (Correct)

```csharp
// GameObject > 3D Object > Cube
var cube = new Engine.Scene.Entity
{
    Id = scene.GetNextEntityId(),
    Name = "Cube",
    Guid = Guid.NewGuid(),
    Active = true
};
// TransformComponent already added by Entity constructor ✅
// TODO: Add MeshRenderer with cube mesh
scene.Entities.Add(cube);

// GameObject > Create Empty
var empty = new Engine.Scene.Entity
{
    Id = scene.GetNextEntityId(),
    Name = "Empty",
    Guid = Guid.NewGuid(),
    Active = true
};
// TransformComponent already added by Entity constructor ✅
scene.Entities.Add(empty);
```

---

## 🔍 Audit Complet

### Fichiers Vérifiés
- ✅ **EditorUI.cs** - 2 occurrences corrigées
- ✅ **HierarchyPanel.cs** - 6 créations d'Entity → **Toutes correctes** (pas de AddComponent TransformComponent)
- ✅ **ViewportPanelModern.cs** - 3 créations d'Entity → **Toutes correctes**
- ✅ **ViewportRenderer.cs** - 1 création d'Entity → **Correcte**

### Résultat
**Seuls 2 bugs trouvés dans EditorUI.cs** - tous corrigés! ✅

---

## 📋 Règle à Suivre

### ✅ Correcte: Créer une Entity
```csharp
var entity = new Engine.Scene.Entity
{
    Id = scene.GetNextEntityId(),
    Name = "My Entity",
    Guid = Guid.NewGuid(),
    Active = true
};
// TransformComponent est DÉJÀ là!

// Ajouter d'autres components
entity.AddComponent<MeshRendererComponent>();
entity.AddComponent<CameraComponent>();
entity.AddComponent<AudioSourceComponent>();
// etc...

scene.Entities.Add(entity);
```

### ❌ Incorrect: NE JAMAIS faire
```csharp
var entity = new Engine.Scene.Entity { ... };
entity.AddComponent<TransformComponent>(); // ❌ CRASH! Déjà ajouté par constructeur!
```

---

## 🎯 Pourquoi TransformComponent est Automatique?

Le `TransformComponent` est **obligatoire** pour toute entité car:
1. Toute entité a une position/rotation/scale
2. Le système de hiérarchie parent/enfant en dépend
3. Le rendering en dépend (matrices model/view/projection)
4. Les colliders en dépendent (world position)

**Donc le constructeur l'ajoute automatiquement pour éviter les oublis.**

---

## ✅ Fix Appliqué

**Fichier**: `Editor/Panels/EditorUI.cs`  
**Lignes modifiées**: 198, 246  
**Compilation**: ✅ 0 erreurs, 0 warnings  
**Status**: **CORRIGÉ ET TESTÉ** ✅

---

*Bug fix complété le 7 décembre 2025*  
*Audit complet effectué - aucun autre cas trouvé*
