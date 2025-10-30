# Analyse des menus contextuels - Hierarchy & Assets Panel

## 📋 État actuel

### HierarchyPanel - Menu Create (clic droit sur le fond)
**Actuellement disponible :**
- Create
  - Empty GameObject ✅
  - Camera ✅
  - 3D
    - Cube ✅
    - Capsule ✅
    - Sphere ✅
    - Plane ✅
    - Quad ✅
  - Generation
    - Terrain Generator ✅
  - Water ✅
  - Light
    - Directional Light ✅
    - Point Light ✅
    - Spot Light ✅
  - Effects
    - Global Effects ✅

### HierarchyPanel - Menu Item (clic droit sur entity)
**Actuellement disponible :**
- Select ✅
- Unparent ✅
- Delete ✅
- Duplicate ✅

### AssetsPanel - Menu contextuel (clic droit sur le fond)
**Actuellement disponible :**
- New Material ✅
- New Folder ✅

---

## 🔍 Composants disponibles dans le moteur

### Composants de rendu
- ✅ MeshRendererComponent - **Présent dans Create → 3D objects**
- ✅ LightComponent - **Présent dans Create → Light**
- ✅ CameraComponent - **Présent dans Create → Camera**
- ✅ WaterComponent - **Présent dans Create → Water**
- ✅ Terrain - **Présent dans Create → Generation**

### Composants de physique/collision
- ❌ BoxCollider - **MANQUANT** (ajouté automatiquement au Cube mais pas dans menu)
- ❌ SphereCollider - **MANQUANT**
- ❌ CapsuleCollider - **MANQUANT**
- ❌ HeightfieldCollider - **MANQUANT**
- ❌ CharacterController - **MANQUANT**
- ❌ Collider (base) - **MANQUANT**

### Composants UI
- ❌ CanvasComponent - **MANQUANT**
- ❌ UITextComponent - **MANQUANT**
- ❌ UIImageComponent - **MANQUANT**
- ❌ UIButtonComponent - **MANQUANT**
- ❌ UIElementComponent - **MANQUANT**
- ❌ UIComponent (base) - **MANQUANT**

### Composants environnement/effets
- ✅ GlobalEffects - **Présent dans Create → Effects**
- ❌ EnvironmentSettings - **MANQUANT**

### Composants core
- ✅ TransformComponent - **Automatique sur toutes les entités**
- ✅ Component (base) - **Classe de base**

---

## 🎯 Recommandations d'amélioration

### 1. HierarchyPanel → Menu Create - À AJOUTER

#### **Nouveau sous-menu : "UI"**
```
Create → UI
  ├─ Canvas          (CanvasComponent)
  ├─ Text            (Canvas + UITextComponent)
  ├─ Image           (Canvas + UIImageComponent)
  └─ Button          (Canvas + UIButtonComponent)
```

#### **Nouveau sous-menu : "Physics"**
```
Create → Physics
  ├─ Box Collider        (Empty + BoxCollider)
  ├─ Sphere Collider     (Empty + SphereCollider)
  ├─ Capsule Collider    (Empty + CapsuleCollider)
  ├─ Character Controller (Empty + CharacterController)
  └─ Heightfield Collider (Empty + HeightfieldCollider)
```

#### **Amélioration du sous-menu "Effects"**
```
Create → Effects
  ├─ Global Effects        (existe déjà ✅)
  └─ Environment Settings  (nouveau ❌)
```

#### **Amélioration du sous-menu "Generation"**
Renommer en "Terrain & Generation" pour plus de clarté

### 2. HierarchyPanel → Menu Item - À AJOUTER

#### **Options de composants**
```
Add Component → (sous-menu dynamique)
  ├─ Rendering
  │   ├─ Mesh Renderer
  │   ├─ Light
  │   └─ Camera
  ├─ Physics
  │   ├─ Box Collider
  │   ├─ Sphere Collider
  │   ├─ Capsule Collider
  │   ├─ Character Controller
  │   └─ Heightfield Collider
  ├─ UI
  │   ├─ Canvas
  │   ├─ Text
  │   ├─ Image
  │   └─ Button
  ├─ Effects
  │   ├─ Global Effects
  │   └─ Environment Settings
  └─ Terrain & Generation
      ├─ Terrain
      └─ Water
```

#### **Options utilitaires**
```
Copy              (Ctrl+C) - Copier l'entité dans presse-papier
Paste             (Ctrl+V) - Coller depuis presse-papier
Paste as Child            - Coller comme enfant de l'entité sélectionnée
---
Rename            (F2)     - Renommer l'entité
---
Set as Active                - Définir comme entité active
Create Empty Child           - Créer un enfant vide
```

### 3. AssetsPanel → Menu contextuel - À AJOUTER

#### **Options de création d'assets**
```
Create →
  ├─ Material             (existe déjà ✅)
  ├─ Skybox Material      (dans toolbar mais pas menu ❌)
  ├─ Folder              (existe déjà ✅)
  ├─ Scene               (nouveau ❌)
  └─ Script              (nouveau ❌ - si système de scripting)
```

#### **Options sur assets sélectionnés**
```
(Si asset sélectionné)
Open                    - Ouvrir dans l'inspecteur
Show in Explorer       - Afficher dans l'explorateur Windows
---
Rename            (F2)  - Renommer
Duplicate               - Dupliquer l'asset
Delete          (Del)   - Supprimer (existe via touche ✅)
---
Copy Path              - Copier le chemin relatif
Copy GUID              - Copier le GUID de l'asset
```

#### **Options sur dossiers sélectionnés**
```
(Si dossier sélectionné)
Open in Explorer       - Ouvrir dans l'explorateur Windows
---
Rename            (F2) - Renommer (existe ✅)
Delete          (Del)  - Supprimer (existe via touche ✅)
---
Import to This Folder  - Importer des fichiers ici
```

---

## 📊 Résumé des modifications proposées

### Priorité HAUTE (fonctionnalités essentielles manquantes)
1. ✅ **Menu UI** dans HierarchyPanel (Canvas, Text, Image, Button)
2. ✅ **Menu Physics** dans HierarchyPanel (Colliders, CharacterController)
3. ✅ **Add Component** dans le menu item de HierarchyPanel
4. ✅ **Copy/Paste** dans le menu item de HierarchyPanel
5. ✅ **Skybox Material** dans le menu Assets

### Priorité MOYENNE (améliore l'UX)
1. ⚠️ **Rename** (F2) dans le menu item HierarchyPanel
2. ⚠️ **Create Empty Child** dans le menu item
3. ⚠️ **Show in Explorer** dans AssetsPanel
4. ⚠️ **Copy Path / Copy GUID** dans AssetsPanel
5. ⚠️ **Environment Settings** dans Create → Effects

### Priorité BASSE (nice-to-have)
1. 💡 **Create Scene** dans AssetsPanel
2. 💡 **Create Script** dans AssetsPanel (si scripting system)
3. 💡 **Paste as Child** dans HierarchyPanel
4. 💡 **Set as Active** dans HierarchyPanel
5. 💡 **Import to This Folder** dans AssetsPanel

---

## ⚠️ Éléments obsolètes / à vérifier

### HierarchyPanel
- ❓ **Duplicate** - Fonctionne-t-il correctement avec tous les composants ?
  - Actuellement : crée uniquement un Cube avec même transform
  - Devrait : dupliquer l'entité complète avec tous ses composants

### AssetsPanel
- ❓ **Menu UX** - Référence supprimée dans le code (ligne commentée)
  - Vérifier si c'était intentionnel ou si des items UI manquent

---

## 🔧 Implémentation suggérée

### Ordre recommandé d'implémentation

1. **Phase 1 : Menus de création essentiels**
   - Ajouter sous-menu UI dans HierarchyPanel
   - Ajouter sous-menu Physics dans HierarchyPanel
   - Ajouter Skybox Material dans AssetsPanel

2. **Phase 2 : Menu "Add Component"**
   - Créer système de menu dynamique pour Add Component
   - Organiser par catégories (Rendering, Physics, UI, etc.)
   - Permettre l'ajout de composants à l'entity sélectionnée

3. **Phase 3 : Copy/Paste & utilitaires**
   - Implémenter système de clipboard pour entities
   - Ajouter Copy/Paste/Duplicate amélioré
   - Ajouter Show in Explorer

4. **Phase 4 : Polish & extras**
   - Ajouter raccourcis clavier manquants
   - Améliorer le Duplicate pour copier tous les composants
   - Ajouter Create Scene si nécessaire

---

## 📝 Notes techniques

### HierarchyPanel
- **Fichier** : `Editor/Panels/HierarchyPanel.cs`
- **Ligne menu Create** : ~345-475
- **Ligne menu Item** : ~642-696
- **Système de sélection** : Déjà robuste (multi-sélection, drag & drop)

### AssetsPanel
- **Fichier** : `Editor/Panels/AssetsPanel.cs`
- **Ligne menu contextuel** : ~424-441
- **Système de création** : Déjà en place (NewKind enum)

### Composants disponibles
- **Répertoire** : `Engine/Components/`
- **UI** : `Engine/Components/UI/`
- **Total** : ~15 types de composants identifiés

---

## ✅ Validation

### Tests recommandés après implémentation
1. Créer chaque type d'objet depuis le menu Create
2. Vérifier que tous les composants sont correctement attachés
3. Tester Add Component sur une entity vide
4. Tester Copy/Paste avec différents types d'entities
5. Vérifier Duplicate avec entities complexes (multi-composants)
6. Tester la création d'assets depuis AssetsPanel
7. Vérifier Show in Explorer sur Windows

### Cas limites à gérer
- Ajouter un composant déjà présent (Camera, Transform, etc.)
- Coller une entity sur elle-même
- Dupliquer une entity avec enfants
- Créer un asset avec un nom en conflit
- Drag & drop entre Hierarchy et Assets

