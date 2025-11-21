# Tests de Validation du Système de Collision

## 🧪 Tests à Effectuer

### Test 1 : MeshCollider sur un Modèle Importé
**Objectif** : Vérifier que le MeshCollider détecte correctement les collisions sur un modèle 3D complexe.

**Étapes** :
1. Importer un modèle 3D (FBX, OBJ, GLTF) via File → Import 3D Model
2. Placer le modèle dans la scène
3. Sélectionner l'entité du modèle
4. Dans l'Inspector, cliquer sur "Add MeshCollider" (ou Add Component → Physics → Mesh Collider)
5. Créer une entité avec un CharacterController
6. Déplacer le personnage vers le modèle

**Résultat Attendu** :
- ✅ Le personnage ne traverse PAS le modèle
- ✅ Le personnage glisse le long du modèle
- ✅ Les collisions sont précises (pas de collision à distance)

---

### Test 2 : CharacterController sur Terrain (HeightfieldCollider)
**Objectif** : Vérifier que le CharacterController détecte correctement le terrain.

**Étapes** :
1. Créer une entité Terrain
2. Ajouter un HeightfieldCollider au terrain
3. Créer une entité Player avec CharacterController
4. Positionner le player au-dessus du terrain
5. Lancer le jeu

**Résultat Attendu** :
- ✅ Le personnage tombe et s'arrête sur le terrain
- ✅ Le personnage ne flotte PAS en l'air
- ✅ Le personnage ne traverse PAS le terrain
- ✅ Le personnage suit les dénivelés du terrain

**Debug** :
Si le personnage flotte, activer `DebugPhysics = true` dans le CharacterController et vérifier les logs :
```
[CharacterController] Ground detected at Y=xxx, distance=xxx, collider=HeightfieldCollider
```

---

### Test 3 : Collision Horizontale avec Murs
**Objectif** : Vérifier que le CharacterController ne traverse pas les murs.

**Étapes** :
1. Créer plusieurs cubes avec BoxCollider (murs)
2. Créer une entité Player avec CharacterController
3. Déplacer le player vers les murs

**Résultat Attendu** :
- ✅ Le personnage s'arrête au contact du mur
- ✅ Le personnage glisse le long du mur si on se déplace en diagonale
- ✅ Pas de pénétration dans les colliders

---

### Test 4 : Ville Importée avec Plusieurs Bâtiments
**Objectif** : Tester les collisions dans un environnement complexe.

**Étapes** :
1. Importer un modèle de ville (FBX avec plusieurs bâtiments)
2. Dans la console, exécuter :
   ```csharp
   var city = scene.FindEntity("City");
   ColliderSetupHelper.EnsureCollidersRecursive(city);
   ```
3. Créer un Player avec CharacterController
4. Se déplacer dans la ville

**Résultat Attendu** :
- ✅ Le personnage ne traverse aucun bâtiment
- ✅ Les collisions sont fluides
- ✅ Pas de lag (les MeshColliders sont optimisés)

---

### Test 5 : Saut et Gravité
**Objectif** : Vérifier que le saut et la gravité fonctionnent correctement.

**Étapes** :
1. Créer un sol (plan avec BoxCollider ou Terrain)
2. Créer un Player avec CharacterController
3. Dans le script de contrôle, ajouter :
   ```csharp
   if (Input.IsKeyPressed(Key.Space) && controller.IsGrounded)
   {
       controller.AddVerticalImpulse(5f);
   }
   ```
4. Appuyer sur Espace

**Résultat Attendu** :
- ✅ Le personnage saute
- ✅ Le personnage retombe avec la gravité
- ✅ Le personnage s'arrête au sol (pas de rebond infini)
- ✅ `IsGrounded` est true au sol, false en l'air

---

### Test 6 : Pentes et Escaliers
**Objectif** : Vérifier la montée/descente de pentes.

**Étapes** :
1. Créer une rampe inclinée (BoxCollider)
2. Régler `MaxSlopeAngleDeg` dans le CharacterController (ex: 45°)
3. Monter et descendre la rampe

**Résultat Attendu** :
- ✅ Le personnage monte les pentes < 45°
- ✅ Le personnage ne monte PAS les pentes > 45°
- ✅ La descente est fluide (pas de saccades)
- ✅ Le personnage reste collé au sol (ground snapping)

---

### Test 7 : MeshCollider vs Triggers
**Objectif** : Vérifier que les triggers fonctionnent.

**Étapes** :
1. Créer un objet avec MeshCollider
2. Cocher `IsTrigger` dans l'Inspector
3. Implémenter OnTriggerEnter dans un script :
   ```csharp
   public override void OnTriggerEnter(Collision collision)
   {
       Console.WriteLine($"Trigger entered: {collision.OtherCollider.Entity.Name}");
   }
   ```
4. Déplacer le player dans le trigger

**Résultat Attendu** :
- ✅ OnTriggerEnter est appelé
- ✅ Le personnage traverse le trigger (pas de collision physique)

---

### Test 8 : Performance avec Beaucoup de MeshColliders
**Objectif** : Vérifier que les performances restent bonnes.

**Étapes** :
1. Créer 50+ objets avec MeshCollider
2. Déplacer le player dans la scène
3. Vérifier le FPS

**Résultat Attendu** :
- ✅ FPS > 60 (ou selon votre cible)
- ✅ Pas de freezes
- ✅ Collisions toujours détectées

**Optimisation** :
Si les perfs sont mauvaises :
- Utiliser des colliders plus simples (BoxCollider) pour les objets éloignés
- Réduire la complexité des mesh colliders (utiliser des versions LOD)

---

### Test 9 : Coins et Angles
**Objectif** : Vérifier le sliding dans les coins.

**Étapes** :
1. Créer deux murs qui forment un angle (90°)
2. Pousser le player dans le coin en diagonale

**Résultat Attendu** :
- ✅ Le personnage glisse le long des murs
- ✅ Pas de "blocage" dans le coin
- ✅ Mouvement fluide même avec 3 rebonds

---

### Test 10 : Auto-ajout de Colliders
**Objectif** : Vérifier que l'auto-ajout fonctionne.

**Étapes** :
1. Importer un nouveau modèle 3D
2. Le placer dans la scène
3. Sélectionner l'entité
4. Vérifier le message dans l'Inspector : "💡 This mesh has no collision"
5. Cliquer sur "Add MeshCollider"

**Résultat Attendu** :
- ✅ Le bouton ajoute un MeshCollider
- ✅ `UseMeshRendererMesh` est automatiquement à true
- ✅ Le message disparaît après l'ajout
- ✅ Les collisions fonctionnent immédiatement

---

## 🐛 Problèmes Connus et Solutions

### Problème : Le personnage flotte au-dessus du terrain
**Solution** :
1. Vérifier que le terrain a un HeightfieldCollider
2. Activer `DebugPhysics` dans CharacterController
3. Augmenter `GroundCheckDistance` (essayer 5.0)
4. Vérifier que `GroundOffset` = 0.0

### Problème : Le personnage traverse les murs
**Solution** :
1. Vérifier que les murs ont des colliders
2. Vérifier que les colliders ne sont PAS en trigger
3. Vérifier que `SkinWidth` n'est pas trop grand (0.02 recommandé)
4. S'assurer que `ComputeSafeMovement` est bien appelé

### Problème : Les MeshColliders ne détectent pas les collisions
**Solution** :
1. Vérifier que le mesh a bien été chargé (bouton "Refresh Mesh")
2. Vérifier dans les logs : "Cached X triangles"
3. Si 0 triangles, vérifier que le GUID du mesh est correct
4. Vérifier que `UseMeshRendererMesh` est true si vous utilisez le mesh du renderer

### Problème : Performance faible avec MeshColliders
**Solution** :
1. Utiliser des versions simplifiées des mesh pour les collisions
2. Créer un mesh "collider" séparé avec moins de triangles
3. Utiliser BoxCollider/SphereCollider pour les objets simples
4. Activer `Convex = true` (future optimisation)

---

## ✅ Checklist Finale

Avant de considérer le système terminé, vérifier :

- [ ] MeshCollider fonctionne sur modèles importés
- [ ] CharacterController détecte HeightfieldCollider
- [ ] CharacterController détecte MeshCollider
- [ ] Pas de traversée de murs
- [ ] Sliding le long des obstacles fonctionne
- [ ] Saut et gravité fonctionnent
- [ ] Détection du sol robuste
- [ ] Pentes montées/descendues correctement
- [ ] Bouton "Add MeshCollider" dans l'Inspector fonctionne
- [ ] ColliderSetupHelper.EnsureCollider fonctionne
- [ ] Performance acceptable (>60 FPS)
- [ ] Pas de bugs avec plusieurs colliders sur la même entité

---

## 📝 Notes de Test

Utilisez cette section pour noter vos résultats de tests :

```
[Date] Test 1 : ✅ Réussi
[Date] Test 2 : ❌ Échec - Personnage flotte (CORRIGÉ)
[Date] Test 3 : ✅ Réussi
...
```
