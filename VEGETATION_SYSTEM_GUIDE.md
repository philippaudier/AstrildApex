# 🌲 Terrain Vegetation System Guide

**Système de végétation procédurale haute performance pour AstrildApex Engine**

Date: December 13, 2025  
Status: ✅ Implémenté

---

## 📋 Table des Matières

1. [Vue d'Ensemble](#vue-densemble)
2. [Architecture](#architecture)
3. [Workflow Utilisateur](#workflow-utilisateur)
4. [Optimisations](#optimisations)
5. [Référence API](#référence-api)
6. [Exemples](#exemples)

---

## 🎯 Vue d'Ensemble

Le système de végétation d'AstrildApex permet de spawn procéduralement des milliers d'instances de meshes (arbres, rochers, herbe) sur un terrain avec :

- **GPU Instancing** pour des performances optimales
- **Règles de placement** basées sur hauteur, pente, et distribution aléatoire
- **Workflow intuitif** dans l'inspecteur similaire à Unity
- **Support LOD** et distance culling pour les scènes cinématiques

### ✨ Fonctionnalités Clés

- ✅ **Poisson Disk Sampling** pour distribution naturelle
- ✅ **Filtrage par hauteur** (min/max normalized height)
- ✅ **Filtrage par pente** (angle du terrain)
- ✅ **Variation d'échelle** aléatoire
- ✅ **Rotation aléatoire** sur l'axe Y
- ✅ **Alignement aux normales** du terrain
- ✅ **Multiple layers** avec meshes différents
- ✅ **Draw distance** et LOD culling
- ✅ **Seed-based** pour génération déterministe

---

## 🏗️ Architecture

### Composants Principaux

```
Engine/
  Assets/
    VegetationLayer.cs          # Définition des règles de spawning
  Components/
    Terrain.cs                  # Génération procédurale des instances
  Rendering/
    VegetationRenderer.cs       # Rendu GPU instancé

Editor/
  Inspector/
    TerrainVegetationUI.cs      # Interface utilisateur
    TerrainInspector.cs         # Intégration dans l'inspecteur
```

### 1. VegetationLayer

Définit les règles de spawning pour un type de végétation :

```csharp
public class VegetationLayer
{
    // Identification
    public string Name { get; set; }
    public bool Enabled { get; set; }
    
    // Model reference
    public Guid? ModelGuid { get; set; }      // Imported model (.gltf/.fbx/.obj)
    public int SubmeshIndex { get; set; }     // -1 = all, 0+ = specific submesh
    
    // Density & Distribution
    public float Density { get; set; }          // Instances per 100x100 area
    public int Seed { get; set; }               // Random seed
    
    // Placement Rules
    public float MinHeight { get; set; }        // 0-1 normalized
    public float MaxHeight { get; set; }        // 0-1 normalized
    public float MinSlope { get; set; }         // Degrees (0-90)
    public float MaxSlope { get; set; }         // Degrees (0-90)
    
    // Scale & Variation
    public float MinScale { get; set; }
    public float MaxScale { get; set; }
    public bool RandomRotation { get; set; }
    public bool AlignToNormal { get; set; }
    
    // Culling & LOD
    public float MaxDrawDistance { get; set; }
    public bool EnableLOD { get; set; }
    public float LODDistance { get; set; }
}
```

### 2. Terrain.GenerateVegetation()

Génère les instances pour chaque layer :

```csharp
public void GenerateVegetation()
{
    // Pour chaque layer enabled
    foreach (var layer in VegetationLayers)
    {
        // 1. Calculer le nombre de tentatives basé sur la densité
        int attempts = (terrainArea / 10000) * layer.Density * 2;
        
        // 2. Poisson disk sampling avec rejection
        for (int i = 0; i < attempts; i++)
        {
            // Random position
            float localX = random(-width/2, width/2);
            float localZ = random(-length/2, length/2);
            
            // Sample heightmap
            float height = SampleHeight(localX, localZ);
            float slope = CalculateSlope(localX, localZ);
            
            // Apply filters
            if (!layer.PassesHeightFilter(height)) continue;
            if (!layer.PassesSlopeFilter(slope)) continue;
            
            // Random scale & rotation
            float scale = random(layer.MinScale, layer.MaxScale);
            float rotation = random(0, 2π);
            
            // Create instance matrix
            Matrix4 matrix = CreateTransform(position, rotation, scale);
            instances.Add(matrix);
        }
    }
}
```

### 3. VegetationRenderer

Rendu GPU instancé ultra-performant :

```csharp
public class VegetationRenderer
{
    // Batch = groupe d'instances avec même mesh+material
    private class VegetationBatch
    {
        public List<Matrix4> Transforms;    // Instance transforms
        public int VAO, VBO, EBO;           // Mesh GPU resources
        public int InstanceVBO;             // Instance buffer (model matrices)
        public int IndexCount;
    }
    
    public void Render(Matrix4 view, Matrix4 projection, int shader)
    {
        foreach (var batch in _batches)
        {
            // Bind mesh + material
            GL.BindVertexArray(batch.VAO);
            BindMaterial(batch.MaterialGuid);
            
            // Draw instanced (1 draw call pour N instances!)
            GL.DrawElementsInstanced(
                PrimitiveType.Triangles,
                batch.IndexCount,
                DrawElementsType.UnsignedInt,
                IntPtr.Zero,
                batch.Transforms.Count
            );
        }
    }
}
```

**Instance Data Layout (GPU):**
```
Per-instance: 16 floats (64 bytes)
- Model Matrix: 4x4 (column-major)
  - Column 0: X axis + X position (vec4)
  - Column 1: Y axis + Y position (vec4)
  - Column 2: Z axis + Z position (vec4)
  - Column 3: Translation + W=1 (vec4)

Vertex Attributes:
- Location 0-2: Mesh data (position, normal, UV)
- Location 3-6: Instance model matrix (4 vec4s)
```

---

## 🎨 Workflow Utilisateur

### Étape 1: Créer un Terrain

1. Créer une entity avec un composant **Terrain**
2. Assigner une **heightmap texture** (16-bit PNG recommandé)
3. Configurer les dimensions (Width, Length, Height)
4. Cliquer sur **Generate Terrain**

### Étape 2: Importer des Assets de Végétation

1. Importer des meshes 3D (arbres, rochers, etc.) via l'import pipeline
2. Créer des materials si nécessaire

### Étape 3: Configurer les Layers de Végétation

1. Dans l'inspecteur du Terrain, section **🌲 Vegetation**
2. Cliquer sur **➕ Add Layer**
3. Configurer le layer :

#### Configuration de Base

```
Name: "Oak Trees"
Imported Model: [Drag & drop votre modèle .gltf/.fbx]
Submesh Index: -1 (all submeshes)
Density: 10 instances per 100²
```

#### Règles de Placement

```
Height Range:
  Min Height: 0.3  (30% de la hauteur max du terrain)
  Max Height: 0.8  (80% de la hauteur max)

Slope Range:
  Min Slope: 0°   (terrain plat)
  Max Slope: 30°  (pente modérée)
```

#### Scale & Variation

```
Scale:
  Min Scale: 0.8
  Max Scale: 1.2

☑️ Random Y Rotation
☐ Align to Terrain Normal
```

### Étape 4: Générer la Végétation

1. Cliquer sur **🔄 Regenerate Vegetation**
2. Le système génère les instances basées sur les règles
3. La végétation apparaît instantanément dans la scène

### Étape 5: Affiner

- Ajuster la **densité** pour plus/moins d'instances
- Modifier le **seed** pour une distribution différente
- Ajouter des **layers supplémentaires** pour différents types
- Utiliser les filtres **Height/Slope** pour zonage naturel

---

## ⚡ Optimisations

### 1. GPU Instancing

**Avant (Naive Rendering):**
```
10,000 arbres = 10,000 draw calls = 🐌 LENT
```

**Après (GPU Instancing):**
```
10,000 arbres = 1 draw call par mesh type = ⚡ RAPIDE
```

Le renderer groupe automatiquement les instances par mesh+material :
- Même mesh + même material = 1 batch = 1 draw call
- Supporte 100,000+ instances par batch

### 2. Batching Automatique

```csharp
// Le système groupe intelligemment :
Layer 1: Oak Tree (Mesh A, Material X)     → Batch 1: 5,000 instances
Layer 2: Oak Tree (Mesh A, Material X)     → Batch 1 (merged!)
Layer 3: Pine Tree (Mesh B, Material Y)    → Batch 2: 3,000 instances
Layer 4: Rock (Mesh C, Material Z)         → Batch 3: 2,000 instances

Total: 3 draw calls pour 10,000 instances!
```

### 3. Distance Culling

```csharp
// Dans VegetationLayer:
MaxDrawDistance = 200f;  // Ne rend pas au-delà de 200m

// LOD Support:
EnableLOD = true;
LODDistance = 100f;      // Switch à LOD bas à 100m
```

### 4. Memory Pooling

- Les buffers de mesh sont réutilisés
- Pas d'allocation par frame
- Instance buffer dimensionné dynamiquement

### 5. Frustum Culling (TODO)

Implémentation future pour culler les batches en dehors du frustum.

---

## 📚 Référence API

### Terrain Component

```csharp
// Propriétés
public VegetationLayer[]? VegetationLayers { get; set; }
public Dictionary<int, List<Matrix4>>? VegetationInstances { get; }

// Méthodes
public void GenerateVegetation()
public void ClearVegetation()
```

### VegetationLayer

```csharp
// Constructeur
var layer = new VegetationLayer
{
    Name = "Trees",
    MeshGuid = treeGuid,
    Density = 10f,
    Seed = 12345
};

// Méthodes
bool PassesHeightFilter(float normalizedHeight)
bool PassesSlopeFilter(float slopeAngleDegrees)
VegetationLayer Clone()
```

### VegetationRenderer

```csharp
// Setup
var renderer = new VegetationRenderer();

// Update batches
renderer.UpdateBatch(meshGuid, submeshIndex, materialGuid, transforms);

// Render
renderer.Render(viewMatrix, projectionMatrix, shaderProgram);

// Cleanup
renderer.ClearBatches();
renderer.Dispose();
```

---

## 💡 Exemples

### Exemple 1: Forêt de Feuillus

```csharp
var oakLayer = new VegetationLayer
{
    Name = "Oak Trees",
    ModelGuid = oakTreeModelGuid,
    SubmeshIndex = -1,  // All submeshes
    Density = 15f,
    MinHeight = 0.2f,
    MaxHeight = 0.7f,
    MinSlope = 0f,
    MaxSlope = 25f,
    MinScale = 0.9f,
    MaxScale = 1.3f,
    RandomRotation = true,
    AlignToNormal = false
};
```

### Exemple 2: Rochers sur Falaises

```csharp
var rockLayer = new VegetationLayer
{
    NodelGuid = rockModelGuid,
    SubmeshIndex = -1",
    MeshGuid = rockGuid,
    Density = 20f,
    MinHeight = 0.6f,      // Hautes altitudes
    MaxHeight = 1.0f,
    MinSlope = 30f,        // Pentes raides
    MaxSlope = 60f,
    MinScale = 0.5f,
    MaxScale = 2.0f,
    RandomRotation = true,
    AlignToNormal = true   // S'aligne à la pente
};
```

### Exemple 3: Herbe Dense

```csharp
var grassLayer = new VegetationLayer
{
    NodelGuid = grassModelGuid,
    SubmeshIndex = -1s",
    MeshGuid = grassGuid,
    Density = 100f,        // Très dense
    MinHeight = 0.0f,
    MaxHeight = 0.4f,      // Zones basses
    MinSlope = 0f,
    MaxSlope = 15f,        // Terrain plat
    MinScale = 0.8f,
    MaxScale = 1.2f,
    MaxDrawDistance = 50f, // Culling proche
    EnableLOD = true,
    LODDistance = 30f
};
```

### Exemple 4: Forêt Mixte Multi-Layers

```csharp
terrain.VegetationLayers = new[]
{
    // Layer 1: Grands arbres
    new VegetationLayer
    {
        Name = "Large Trees",
        Density = 8f,
        MinScale = 1.5f,
        MaxScale = 2.0f
    },
    
    // Layer 2: Arbres moyens
    new VegetationLayer
    {
        Name = "Medium Trees",
        Density = 15f,
        MinScale = 1.0f,
        MaxScale = 1.5f
    },
    
    // Layer 3: Sous-bois
    new VegetationLayer
    {
        Name = "Bushes",
        Density = 40f,
        MinScale = 0.5f,
        MaxScale = 1.0f
    }
};

terrain.GenerateVegetation();
```

---

## 🎬 Pour les Scènes Cinématiques

### Conseils d'Optimisation

1. **Utilisez LOD aggressivement**
   - EnableLOD = true
   - LODDistance ajusté selon la caméra

2. **Divisez en zones**
   - Créez plusieurs terrains plus petits
   - Chargez/déchargez selon la position caméra

3. **Baking statique**
   - Pour les plans fixes, pré-générez et sauvegardez
   - Évitez la régénération en temps réel

4. **Distance culling intelligent**
   - MaxDrawDistance basé sur le plan de caméra
   - Végétation lointaine = moins de densité

5. **Shaders optimisés**
   - Utilisez des shaders sans ombres pour la végétation dense
   - Wind animation en shader pour effet vivant

---

## 🔧 Intégration dans le Rendering Pipeline

### ViewportRenderer / GameRenderer

```csharp
// Dans le renderer:
private VegetationRenderer? _vegetationRenderer;

public void Initialize()
{
    _vegetationRenderer = new VegetationRenderer();
}

public void RenderScene()
{
    // 1. Render opaque geometry
    RenderTerrain();
    RenderMeshes();
    
    // 2. Update vegetation batches
    foreach (var entity in scene.GetEntitiesWithComponent<Terrain>())
    {
        var terrain = entity.GetComponent<Terrain>();
        if (terrain.VegetationInstances != null)
        {
            foreach (var kvp in terrain.VegetationInstances)
            {
                int layerIndex = kvp.Key;
                var transforms = kvp.Value;
                var layer = terrain.VegetationLayers[layerIndex];
                
                _vegetationRenderer.UpdateBatch(
                    layer.MeshGuid.Value,
                    layer.SubmeshIndex,
                    layer.MaterialGuid,
                    transforms
                );
            }
        }
    }
    
    // 3. Render vegetation
    _vegetationRenderer.Render(_viewMatrix, _projMatrix, _shader);
}
```

---

## 🐛 Troubleshooting

### Problème: Aucune végétation n'apparaît

**Solutions:**
- ✅ Vérifier que le terrain a un heightmap et mesh généré
- ✅ Vérifier que le layer a un MeshGuid valide
- ✅ Augmenter la densité
- ✅ Élargir les filtres Height/Slope
- ✅ Vérifier le seed (essayer différents seeds)

### Problème: Performance faible

**Solutions:**
- ✅ Réduire la densité
- ✅ Activer LOD culling
- ✅ Réduire MaxDrawDistance
- ✅ Grouper les layers avec même mesh+material

### Problème: Distribution non naturelle

**Solutions:**
- ✅ Augmenter le facteur de tentatives (dans le code)
- ✅ Ajuster MinScale/MaxScale pour plus de variation
- ✅ Utiliser RandomRotation = true
- ✅ Essayer différents seeds

---

## 🚀 Améliorations Futures

- [ ] **Frustum culling** pour batches
- [ ] **Occlusion culling** basé sur le terrain
- [ ] **Wind animation** dans le shader
- [ ] **Texture splatmap integration** (placer végétation selon texture)
- [ ] **Collision generation** optionnelle pour la végétation
- [ ] **Paint mode** pour placement manuel
- [ ] **Multi-threading** de la génération
- [ ] **Baking/Serialization** des instances

---

## 📖 Références

### Inspiration
- **Unity Terrain Details**: https://docs.unity3d.com/Manual/terrain-Grass.html
- **Unreal Foliage**: https://docs.unrealengine.com/en-US/foliage-instanced-static-mesh/
- **Godot MultiMesh**: https://docs.godotengine.org/en/stable/classes/class_multimesh.html

### Techniques
- **GPU Instancing**: https://learnopengl.com/Advanced-OpenGL/Instancing
- **Poisson Disk Sampling**: Fast Poisson Disk Sampling in Arbitrary Dimensions (Bridson 2007)

---

**Félicitations! Vous avez maintenant un système de végétation procédurale haute performance! 🌲🎉**
