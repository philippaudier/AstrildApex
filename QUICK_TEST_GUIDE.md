# Guide Rapide - Import de Modèle 3D avec Collisions

## 🚀 Modifications Apportées

### 1. **Auto-ajout du MeshCollider**
Quand vous glissez-déposez un mesh dans la scène, le système ajoute maintenant **automatiquement** un MeshCollider !

### 2. **Debug Amélioré**
Des messages de log apparaissent dans la Console pour diagnostiquer :
```
[ViewportPanel] SetCustomMesh called with GUID=xxx, submesh=0
[ViewportPanel] CustomMeshGuid after set: xxx
[ViewportPanel] IsUsingCustomMesh: True
[ViewportPanel] Auto-added MeshCollider to 'YourModel'
[MeshCollider] Using mesh from MeshRenderer: xxx
[MeshCollider] Loading mesh from: Assets/.../model.meshasset
[MeshCollider] Mesh loaded: ModelName, SubMeshes: X
[MeshCollider] Cached XXXX triangles for YourModel
```

### 3. **Mode Debug dans l'Inspector**
Maintenez **Shift** dans l'Inspector pour voir les valeurs debug :
```
[DEBUG] CustomMeshGuid: xxx-xxx-xxx
[DEBUG] IsUsingCustomMesh: True
[DEBUG] Mesh: Cube
```

---

## 📝 Procédure de Test

### Étape 1 : Supprimer l'Ancien Modèle
1. Supprimer l'entité de ville actuelle de la scène
2. Supprimer la scène actuelle (ne pas sauvegarder)
3. Créer une nouvelle scène vierge

### Étape 2 : Réimporter le Modèle (Optionnel)
Si le modèle a déjà été importé, passez à l'étape 3.
Sinon :
1. File → Import 3D Model
2. Sélectionner votre fichier FBX
3. Attendre la fin de l'import

### Étape 3 : Glisser-Déposer dans la Scène
1. Ouvrir l'Assets Panel
2. Naviguer vers Models/YourModel/
3. **Glisser-déposer** le fichier .meshasset dans le viewport
4. Relâcher la souris

### Étape 4 : Vérifier dans la Console
Regarder les messages :
```
✓ [ViewportPanel] SetCustomMesh called with GUID=xxx
✓ [ViewportPanel] IsUsingCustomMesh: True
✓ [ViewportPanel] Auto-added MeshCollider to 'Model'
✓ [MeshCollider] Cached 15234 triangles for Model
```

### Étape 5 : Vérifier dans l'Inspector
1. Sélectionner l'entité créée
2. Regarder le **MeshRenderer** :
   - ✅ Doit afficher "Mesh Type: **Custom (Imported)**" en VERT
   - ✅ Doit afficher "Mesh Asset: **YourModelName**" en bleu
3. Regarder le **MeshCollider** :
   - ✅ Doit afficher "✓ XXXX triangles cached" en vert
   - ✅ "Collision will follow mesh geometry precisely."

### Étape 6 : Tester les Collisions
1. Créer une entité Player avec CharacterController
2. Activer `DebugPhysics = true` dans le CharacterController
3. Déplacer le player vers le modèle
4. Le player **ne doit PAS** traverser

---

## 🐛 Si ça Ne Marche Toujours Pas

### Symptôme : "Mesh Type: Primitive" au lieu de "Custom"

**Solution A** : Mode Debug
1. Sélectionner l'entité
2. Maintenir **Shift** dans l'Inspector
3. Regarder `[DEBUG] CustomMeshGuid:`
4. Si c'est `null` ou vide → Le GUID n'a pas été défini

**Solution B** : Vérifier la Console
Chercher :
```
[ViewportPanel] SetCustomMesh called with GUID=xxx
```
Si absent → Le drag & drop n'a pas fonctionné correctement

**Solution C** : Méthode Manuelle
1. Sélectionner l'entité
2. Dans le MeshRenderer, section "Custom Mesh Asset"
3. Sélectionner le mesh dans le dropdown
4. Cliquer sur "Refresh Mesh" dans le MeshCollider

### Symptôme : "0 triangles" dans le MeshCollider

**Vérifier dans la Console** :
```
[MeshCollider] Aucun mesh trouvé
OU
[MeshCollider] Mesh GUID xxx not found in AssetDatabase
```

**Solutions** :
1. Vérifier que le fichier .meshasset existe dans Assets/Models/
2. Cliquer sur "Refresh Mesh" dans l'Inspector
3. Réassigner manuellement le mesh dans le MeshRenderer
4. Redémarrer l'éditeur si nécessaire

---

## 🎯 Test Complet

1. ✅ Drag & drop d'un mesh
2. ✅ Vérifier "Custom (Imported)" dans MeshRenderer
3. ✅ Vérifier "X triangles cached" dans MeshCollider
4. ✅ Voir le gizmo du collider (devrait être orange/jaune autour du mesh)
5. ✅ Tester collision avec CharacterController
6. ✅ Le personnage ne traverse PAS le modèle

---

## 📸 Ce Que Vous Devriez Voir

### Dans l'Inspector du MeshRenderer :
```
┌─ Mesh ────────────────────────┐
│ Mesh Type: Custom (Imported)  │  ← EN VERT
│ Mesh Asset: CityModel          │  ← EN BLEU
│ [Clear]                        │
│                                │
│ Custom Mesh Asset:             │
│ [CityModel (MeshAsset)    ▼]  │
└────────────────────────────────┘
```

### Dans l'Inspector du MeshCollider :
```
┌─ MeshCollider ────────────────┐
│ ☑ Is Trigger                   │
│ Layer: 0                       │
│ Center: 0.00, 0.00, 0.00      │
│                                │
│ ☑ Use MeshRenderer Mesh        │
│ Using: CityModel               │  ← EN BLEU
│                                │
│ [Refresh Mesh]                 │
│                                │
│ Collision Mesh Info:           │
│ ✓ 15,234 triangles cached     │  ← EN VERT
│ Collision will follow mesh...  │
│ Bounds: 100.50 x 50.20 x 80.30│
│ Source: CityModel              │  ← EN BLEU
└────────────────────────────────┘
```

---

## 💬 Rapport à Donner

Après avoir testé, envoyez :
1. **Screenshot** de l'Inspector (MeshRenderer + MeshCollider)
2. **Copie de la Console** (tous les messages)
3. **Ce qui se passe** : "Ça marche !" ou "Toujours un cube"

Je pourrai alors diagnostiquer précisément le problème ! 🔍
