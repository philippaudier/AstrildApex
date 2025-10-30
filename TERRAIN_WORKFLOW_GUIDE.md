# Terrain System - Complete Workflow Guide

## Vue d'ensemble

Le système de terrain d'AstrildApex utilise une approche basée sur des **Materials** pour gérer les layers de terrain, offrant un workflow cohérent et réutilisable.

## Architecture du système

### 🔹 Composants principaux

1. **Terrain Component** (`Engine.Components.Terrain`)
   - Dimensions du terrain (Width, Length, Height)
   - Référence au Heightmap (texture 16-bit)
   - Référence au Material principal (TerrainForward shader)
   - Configuration de l'eau (optionnelle)

2. **Material System** (`Engine.Assets.MaterialAsset`)
   - Albedo, Normal, Roughness, Metallic
   - Shader reference
   - TerrainLayers[] array (jusqu'à 8 layers)

3. **Terrain Layers** (`Engine.Assets.TerrainLayer`)
   - Référence Material (pour textures et propriétés PBR)
   - UV Transform (Tiling, Offset) - spécifique au layer
   - Blending parameters (Height, Slope, Strength)
   - Mode Underwater (optionnel)

## 📋 Workflow complet

### Étape 1 : Créer un Terrain

1. Ajouter un composant `Terrain` à une entité
2. Configurer les dimensions :
   - **Width** : Largeur du terrain en mètres
   - **Length** : Longueur du terrain en mètres
   - **Height** : Hauteur max du terrain en mètres
3. Définir la **Mesh Resolution** (128-1024)
   - Plus haute = plus lisse mais plus lent
   - Presets disponibles : Low (128), Med (256), High (512)

### Étape 2 : Assigner un Heightmap

1. Créer ou importer une texture heightmap (16-bit PNG recommandé)
2. Drag & drop la texture dans la zone "Heightmap Texture"
3. Le heightmap définit l'élévation du terrain :
   - Noir (0) = hauteur minimale
   - Blanc (65535) = hauteur maximale

### Étape 3 : Créer le Material principal

1. Créer un nouveau Material Asset
2. Assigner le shader **TerrainForward**
3. Drag & drop ce material dans la zone "Material" du Terrain

### Étape 4 : Configurer les Layers

#### 🎨 Ajouter un Layer

1. Dans la section "Terrain Layers", cliquer **Add Layer** (max 8)
2. Donner un nom descriptif au layer (ex: "Grass", "Rock", "Sand")

#### 🖼️ Assigner un Material au Layer

**Option A : Material réutilisable**
1. Créer un Material avec vos textures (Albedo, Normal, etc.)
2. Configurer les propriétés PBR (Metallic, Roughness)
3. Drag & drop ce material dans le layer
4. ✅ **Avantage** : Le même material peut être utilisé sur plusieurs layers/terrains

**Option B : Legacy (déprécié)**
- Les anciennes propriétés AlbedoTexture/NormalTexture sont toujours supportées
- Utilisées comme fallback si aucun Material n'est assigné
- ⚠️ Migration vers Materials recommandée

#### ⚙️ Configurer le Blending

**UV Transform** (indépendant du Material)
- **Tiling** : Répétition de la texture (ex: 10x10 pour petits détails)
- **Offset** : Décalage UV pour variation

**Height Range**
- **Height Min/Max** : Plage d'altitude où le layer est visible (en mètres)
- **Height Blend** : Distance de transition (plus haute = transition douce)

**Slope Range**
- **Slope Min/Max** : Plage d'inclinaison (0-90 degrés)
  - 0° = plat
  - 45° = pente à 45°
  - 90° = vertical (falaise)
- **Slope Blend** : Distance de transition angulaire

**Strength & Priority**
- **Strength** : Intensité du layer (0-1)
- **Priority** : Ordre de rendu (plus élevé = dessus)

**Blend Mode**
- **Height And Slope** : Layer visible si DANS height ET slope range
- **Height** : Basé uniquement sur height
- **Slope** : Basé uniquement sur slope
- **Height Or Slope** : Layer visible si DANS height OU slope range

#### 🌊 Mode Underwater (optionnel)

Active le mode sous-marin pour un layer (ex: algues, sable mouillé)

1. Cocher **Enable Underwater Mode**
2. Configurer :
   - **Max Height** : Hauteur max de l'eau (niveau de surface)
   - **Blend Distance** : Distance de transition
   - **Slope Range** : Pentes sous-marines où appliquer
   - **Blend With Others** : 
     - 0 = Layer pur sous l'eau
     - 1 = Mélange total avec layers normaux

### Étape 5 : Ajouter de l'eau (optionnel)

1. Cocher **Enable Water**
2. Ajuster **Water Height** (slider)
3. Assigner un **Water Material** (shader Water/Ocean)
4. L'eau est un plan à hauteur constante

### Étape 6 : Générer le terrain

1. Cliquer **Generate Terrain**
2. Le mesh est généré automatiquement
3. Les layers sont blendés en temps réel selon les règles

## 🔄 Workflow Material → Terrain

### Propriétés synchronisées

Quand vous modifiez un Material assigné à un layer :

| Propriété Material | Utilisée dans Terrain |
|-------------------|----------------------|
| **Albedo Texture** | ✅ Texture de couleur du layer |
| **Normal Texture** | ✅ Détails de surface |
| **Metallic** | ✅ Métal vs. diélectrique |
| **Roughness** | ✅ Converti en Smoothness (1 - Roughness) |
| **AO Texture** | ⏳ À venir |

### Conversion Roughness ↔ Smoothness

- **Material** utilise **Roughness** (PBR standard)
  - 0.0 = Surface miroir
  - 1.0 = Surface très rugueuse
  
- **Shader Terrain** utilise **Smoothness** (Unity-style)
  - 0.0 = Surface très rugueuse
  - 1.0 = Surface miroir

**Conversion automatique** : `Smoothness = 1.0 - Roughness`

## 🎯 Exemples de configuration

### Terrain montagneux basique

**Layer 0 : Herbe (plat, bas)**
- Material : Grass_PBR
- Height : -1000 à 100m
- Slope : 0° à 30°
- Tiling : 20x20
- Priority : 0

**Layer 1 : Roche (pente, moyen)**
- Material : Rock_PBR
- Height : 50 à 500m
- Slope : 25° à 90°
- Tiling : 15x15
- Priority : 1

**Layer 2 : Neige (haut)**
- Material : Snow_PBR
- Height : 400 à 1000m
- Slope : 0° à 60°
- Tiling : 25x25
- Priority : 2

### Terrain avec zone underwater

**Layer 0 : Sable (plage)**
- Material : Sand_PBR
- Height : -10 à 50m
- Slope : 0° à 45°
- Priority : 0

**Layer 1 : Algues (underwater)**
- Material : Seaweed_PBR
- **Underwater : Enabled**
- Max Height : 0m (niveau mer)
- Blend Distance : 2m
- Slope : 0° à 30°
- Priority : 1

**Water**
- Enable Water : ✅
- Water Height : 0m
- Water Material : Ocean_Material

## 🛠️ Débogage

### Le terrain n'apparaît pas
- ✅ Heightmap assignée ?
- ✅ Material assigné ?
- ✅ "Generate Terrain" cliqué ?
- ✅ Caméra orientée vers le terrain ?

### Textures manquantes
- ✅ Materials assignés aux layers ?
- ✅ Textures présentes dans le Material ?
- ✅ Chemins d'assets corrects ?

### Blending incorrect
- ✅ Vérifier Height/Slope ranges
- ✅ Augmenter Blend Distance
- ✅ Vérifier Priority des layers
- ✅ Tester différents Blend Modes

### Propriétés PBR ne s'appliquent pas
- ✅ Modifier le Material, pas le layer
- ✅ Sauvegarder le Material (Ctrl+S)
- ✅ Re-générer le terrain si nécessaire

## 📊 Limites techniques

- **8 layers maximum** par terrain
- **Heightmap** : Texture 2D (16-bit recommandé)
- **Mesh resolution** : 32 à 1024 (compromis qualité/perfs)
- **Tiling** : 0.1x à 100x (recommandé 1x à 50x)

## 🚀 Performance

### Optimisations

1. **Mesh Resolution**
   - Terrain lointain : 128-256
   - Terrain proche : 512-1024
   
2. **Texture Resolution**
   - Albedo : 1024x1024 ou 2048x2048
   - Normal : 1024x1024
   - Tiling élevé = peut utiliser textures plus petites

3. **Layers**
   - Utiliser uniquement les layers nécessaires
   - Strength = 0 désactive effectivement un layer

## 🔮 Roadmap

- [ ] Support AO texture dans layers
- [ ] Support Emission pour lave/cristaux
- [ ] Height/Slope painting directement dans l'éditeur
- [ ] Terrain LOD automatique
- [ ] Baking de la composition des layers
- [ ] Procedural detail textures (micro-variations)

---

**Dernière mise à jour** : Octobre 2025
**Version** : 1.0.0
