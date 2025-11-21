# Guide de Diagnostic MeshCollider

## 🔍 Problème : Le MeshCollider affiche un cube au lieu de la géométrie du mesh

### Symptômes
- Le wireframe du collider montre une boîte englobante (cube) au lieu de suivre la géométrie
- L'inspecteur affiche "0 triangles" ou "⚠ 0 triangles - No collision!"
- Les collisions ne sont pas précises

### Causes Possibles

#### 1. **Le MeshRenderer n'a pas de mesh assigné**
**Vérification** :
- Sélectionner l'entité
- Dans l'Inspector, regarder le MeshRenderer
- Si "Mesh Type: Primitive" et "Shape: Cube" → Le mesh importé n'est PAS utilisé

**Solution** :
1. Dans la section "Custom Mesh Asset" du MeshRenderer
2. Sélectionner votre mesh importé dans le dropdown
3. Cliquer sur "Refresh Mesh" dans le MeshCollider

#### 2. **Le MeshCollider a été ajouté AVANT que le mesh ne soit assigné**
**Solution** :
1. Assigner d'abord le mesh dans le MeshRenderer
2. Puis ajouter le MeshCollider
3. OU cliquer sur "Refresh Mesh" dans le MeshCollider

#### 3. **Le mesh n'est pas dans l'AssetDatabase**
**Vérification dans la Console** :
```
[MeshCollider] Mesh GUID xxx not found in AssetDatabase
```

**Solution** :
1. Réimporter le modèle 3D via File → Import 3D Model
2. Attendre que l'import se termine
3. Rafraîchir le MeshCollider

#### 4. **Le fichier .meshasset est corrompu ou manquant**
**Vérification dans la Console** :
```
[MeshCollider] Erreur lors du chargement du mesh xxx.meshasset
```

**Solution** :
1. Supprimer le fichier .meshasset
2. Réimporter le modèle 3D

---

## ✅ Comment Vérifier que ça Marche

### Dans l'Inspector du MeshCollider :
```
Collision Mesh Info:
✓ 15,234 triangles cached           ← DOIT être > 0
Collision will follow mesh geometry precisely.
Bounds: 10.5 x 5.2 x 8.3
Source: YourModelName                ← Nom de votre mesh
```

### Dans la Console :
```
[MeshCollider] Using mesh from MeshRenderer: {guid}
[MeshCollider] Loading mesh from: Assets/Models/YourModel/YourModel.meshasset
[MeshCollider] Mesh loaded: YourModelName, SubMeshes: 1
[MeshCollider] Cached 15234 triangles for YourEntity
```

---

## 🎯 Procédure Correcte d'Import

### Étape 1 : Importer le Modèle
1. File → Import 3D Model
2. Sélectionner votre fichier FBX/OBJ/GLTF
3. Attendre la fin de l'import
4. Vérifier dans la Console : "Model imported successfully"

### Étape 2 : Placer dans la Scène
1. Créer une nouvelle entité (ou drag & drop depuis l'asset browser)
2. Ajouter un MeshRenderer
3. **IMPORTANT** : Sélectionner le mesh importé dans "Custom Mesh Asset"
4. Vérifier que "Mesh Type: Custom (Imported)" est affiché

### Étape 3 : Ajouter les Collisions
1. Cliquer sur "Add MeshCollider" dans le MeshRenderer
2. OU Add Component → Physics → Mesh Collider
3. Vérifier que "Use MeshRenderer Mesh" est coché
4. Vérifier le nombre de triangles dans l'inspector

### Étape 4 : Vérification Visuelle
- Activer le mode Debug/Gizmos pour voir les colliders
- Le wireframe doit suivre la forme du modèle
- Pas une simple boîte englobante

---

## 🐛 Messages d'Erreur Communs

### "MeshRenderer has no custom mesh!"
**Cause** : Le MeshRenderer utilise un primitif (Cube, Sphere, etc.) au lieu d'un mesh importé
**Solution** : Assigner un mesh importé dans le MeshRenderer

### "No MeshRenderer component found!"
**Cause** : L'entité n'a pas de MeshRenderer
**Solution** : Ajouter un MeshRenderer d'abord

### "Mesh GUID xxx not found in AssetDatabase"
**Cause** : Le mesh a été supprimé ou l'AssetDatabase n'est pas à jour
**Solution** : 
- Menu Edit → Refresh Asset Database (si disponible)
- OU Réimporter le modèle

### "0 triangles - No collision!"
**Cause** : Le mesh n'a pas pu être chargé
**Solutions** :
1. Vérifier que le fichier .meshasset existe dans Assets/Models/
2. Cliquer sur "Refresh Mesh"
3. Réassigner le mesh dans le MeshRenderer
4. Réimporter le modèle si nécessaire

---

## 💡 Bonnes Pratiques

### ✅ À FAIRE
1. **Toujours** assigner le mesh dans le MeshRenderer en premier
2. **Puis** ajouter le MeshCollider
3. Utiliser "Use MeshRenderer Mesh" pour la cohérence
4. Vérifier le nombre de triangles après ajout
5. Cliquer sur "Refresh Mesh" si le mesh change

### ❌ À NE PAS FAIRE
1. Ajouter un MeshCollider avant d'avoir un mesh
2. Utiliser un MeshCollider sur un primitif (utilisez BoxCollider/SphereCollider à la place)
3. Oublier de sauvegarder la scène après avoir ajouté des colliders

---

## 🔧 Script de Diagnostic

Copiez ce code dans la console pour diagnostiquer :

```csharp
// Sélectionner votre entité dans la hiérarchie, puis :
var entity = selection.FirstOrDefault();
var meshRenderer = entity?.GetComponent<MeshRendererComponent>();
var meshCollider = entity?.GetComponent<MeshCollider>();

Console.WriteLine("=== DIAGNOSTIC MESHCOLLIDER ===");
Console.WriteLine($"Entity: {entity?.Name}");
Console.WriteLine($"Has MeshRenderer: {meshRenderer != null}");
Console.WriteLine($"Has CustomMesh: {meshRenderer?.CustomMeshGuid.HasValue}");
Console.WriteLine($"Has MeshCollider: {meshCollider != null}");

if (meshCollider != null)
{
    Console.WriteLine($"UseMeshRendererMesh: {meshCollider.UseMeshRendererMesh}");
    Console.WriteLine($"Cached Triangles: {meshCollider.CachedTriangleCount}");
    Console.WriteLine($"Cache Dirty: {meshCollider.IsTriangleCacheDirty}");
}

if (meshRenderer?.CustomMeshGuid.HasValue == true)
{
    var guid = meshRenderer.CustomMeshGuid.Value;
    Console.WriteLine($"Mesh GUID: {guid}");
    var inDb = AssetDatabase.TryGet(guid, out var rec);
    Console.WriteLine($"In AssetDatabase: {inDb}");
    if (inDb) Console.WriteLine($"Path: {rec.Path}");
}
```

---

## 📞 Aide Supplémentaire

Si le problème persiste après avoir suivi ce guide :
1. Copier tous les messages de la Console
2. Vérifier que le fichier .meshasset existe
3. Essayer avec un autre modèle 3D simple (cube exporté depuis Blender par exemple)
4. Vérifier que les autres colliders (BoxCollider) fonctionnent normalement
