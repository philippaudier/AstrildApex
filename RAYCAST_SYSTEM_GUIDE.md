# Raycast System - API Reference

## 📋 Vue d'ensemble

Le système de Raycast d'AstrildApex est **robuste**, **performant** et **facile à utiliser**. Il est inspiré de Unity mais avec des optimisations spécifiques.

## 🎯 API Principale

### Physics.Raycast()

#### Variante simple (bool)

```csharp
// Détecte s'il y a quelque chose dans la direction
bool hit = Physics.Raycast(origin, direction, maxDistance);

if (hit)
{
    // Il y a un obstacle
}
```

#### Variante avec informations de hit

```csharp
bool hit = Physics.Raycast(origin, direction, out RaycastHit hitInfo, maxDistance);

if (hit)
{
    Debug.Log($"Hit: {hitInfo.Entity.Name}");
    Debug.Log($"Point: {hitInfo.Point}");
    Debug.Log($"Normal: {hitInfo.Normal}");
    Debug.Log($"Distance: {hitInfo.Distance}");
}
```

#### Paramètres optionnels

```csharp
Physics.Raycast(
    origin: startPosition,
    direction: Vector3.UnitY,
    out RaycastHit hit,
    maxDistance: 100f,
    layerMask: ~0,                                  // Tous les layers
    query: QueryTriggerInteraction.Ignore          // Ignorer les triggers
);
```

### Physics.RaycastAll()

Pour récupérer **tous** les hits :

```csharp
RaycastHit[] hits = Physics.RaycastAll(origin, direction, maxDistance);

foreach (var hit in hits)
{
    Debug.Log($"Hit: {hit.Entity.Name} at {hit.Distance}m");
}
```

**Note** : Les résultats sont automatiquement triés par distance (du plus proche au plus loin).

### Physics.RaycastNonAlloc()

Version **sans allocation** pour la performance :

```csharp
RaycastHit[] results = new RaycastHit[10]; // Réutiliser ce tableau
int count = Physics.RaycastNonAlloc(origin, direction, results, maxDistance);

for (int i = 0; i < count; i++)
{
    Debug.Log($"Hit {i}: {results[i].Entity.Name}");
}
```

**Avantage** : Aucune allocation mémoire → parfait pour les boucles chaudes (Update, FixedUpdate).

## 🔮 Casts avancés

### SphereCast

Lance une **sphère** au lieu d'un rayon :

```csharp
bool hit = Physics.SphereCast(
    ray: new Ray { Origin = origin, Direction = direction },
    radius: 0.5f,                  // Rayon de la sphère
    out RaycastHit hitInfo,
    maxDistance: 10f
);
```

**Utilité** :
- Détection de sol pour CharacterController (plus robuste qu'un raycast)
- Détection d'ennemis dans une zone
- Projectiles avec hitbox (rocket, grenade)

### CapsuleCast

Lance une **capsule** au lieu d'un rayon :

```csharp
bool hit = Physics.CapsuleCast(
    point1: bottomPoint,
    point2: topPoint,
    radius: 0.4f,
    direction: moveDirection,
    out RaycastHit hitInfo,
    maxDistance: moveDistance
);
```

**Utilité** :
- **Swept collision detection** pour CharacterController
- Prévenir le tunneling (traverser les murs)
- Détecter les obstacles avant de bouger

### BoxCast

Lance une **boîte** au lieu d'un rayon :

```csharp
bool hit = Physics.BoxCast(
    center: boxCenter,
    halfExtents: new Vector3(0.5f, 0.5f, 0.5f),
    direction: Vector3.UnitY,
    out RaycastHit hitInfo,
    maxDistance: 10f
);
```

**Utilité** :
- Détecter les objets dans une zone rectangulaire
- Vérifier si un objet peut passer dans un espace étroit

## 🔍 Overlap Queries

### OverlapSphere

Trouve tous les colliders dans une sphère :

```csharp
bool found = Physics.OverlapSphere(
    center: explosionCenter,
    radius: 10f,
    out List<Collider> results
);

foreach (var collider in results)
{
    // Appliquer des dégâts, knockback, etc.
}
```

### OverlapBox

Trouve tous les colliders dans une boîte :

```csharp
bool found = Physics.OverlapBox(
    center: areaCenter,
    halfExtents: new Vector3(5f, 2f, 5f),
    out List<Collider> results
);
```

## 🎭 Layer Masks

### Utilisation basique

```csharp
// Raycast uniquement sur le layer 0
int layerMask = 1 << 0;
Physics.Raycast(origin, direction, out hit, 100f, layerMask);

// Raycast sur les layers 0 et 1
int layerMask = (1 << 0) | (1 << 1);
Physics.Raycast(origin, direction, out hit, 100f, layerMask);

// Raycast sur TOUS les layers SAUF le layer 2 (ignorer le joueur)
int layerMask = ~(1 << 2);
Physics.Raycast(origin, direction, out hit, 100f, layerMask);
```

### Constantes utiles

```csharp
// Tous les layers
Physics.Raycast(origin, direction, out hit, 100f, ~0);

// Aucun layer (désactivé)
Physics.Raycast(origin, direction, out hit, 100f, 0);
```

### Exemples concrets

```csharp
// Layer 0 : Default
// Layer 1 : Player
// Layer 2 : Enemy
// Layer 3 : Environment
// Layer 4 : Trigger

// AI vision : ignorer player et triggers
int visionMask = ~((1 << 1) | (1 << 4));
bool seeEnemy = Physics.Raycast(aiPosition, toEnemy, out hit, 50f, visionMask);

// Weapon raycast : toucher player et enemy, ignorer environment
int weaponMask = (1 << 1) | (1 << 2);
bool hitTarget = Physics.Raycast(gunPosition, aimDirection, out hit, 1000f, weaponMask);
```

## 🎮 Query Trigger Interaction

Contrôle si les **triggers** (colliders avec `IsTrigger = true`) sont détectés :

```csharp
// Ignorer les triggers (défaut recommandé pour CharacterController)
Physics.Raycast(origin, direction, out hit, 100f, ~0, QueryTriggerInteraction.Ignore);

// Inclure les triggers
Physics.Raycast(origin, direction, out hit, 100f, ~0, QueryTriggerInteraction.Include);

// Utiliser le réglage global (CollisionSystem.QueriesHitTriggers)
Physics.Raycast(origin, direction, out hit, 100f, ~0, QueryTriggerInteraction.UseGlobal);
```

### Réglage global

```csharp
// Changer le comportement par défaut
CollisionSystem.QueriesHitTriggers = QueryTriggerInteraction.Include;
```

## 📦 RaycastHit struct

Informations retournées par un raycast :

```csharp
public struct RaycastHit
{
    public Component? Component;           // Composant touché (Collider)
    public Component? ColliderComponent;   // Alias pour Component
    public Entity? Entity;                 // Entity touchée
    public Vector3 Point;                  // Point d'impact (monde)
    public Vector3 Normal;                 // Normale de la surface
    public float Distance;                 // Distance depuis l'origine
}
```

### Utilisation

```csharp
if (Physics.Raycast(origin, direction, out RaycastHit hit, 100f))
{
    // Téléporter le joueur au point d'impact
    player.Transform.Position = hit.Point;
    
    // Aligner avec la surface
    player.Transform.Rotation = Quaternion.FromToRotation(Vector3.UnitY, hit.Normal);
    
    // Spawn un effet visuel
    var fx = Instantiate(impactEffect, hit.Point, Quaternion.identity);
    
    // Appliquer des dégâts à l'entité touchée
    if (hit.Entity?.GetComponent<Health>() is Health health)
    {
        health.TakeDamage(10f);
    }
}
```

## ⚡ Optimisations

### Spatial Hash

Le système utilise un **spatial hash** pour accélérer les requêtes :

- ✅ **O(N)** au lieu de O(N²) pour les raycasts
- ✅ Query seulement les colliders **proches** du rayon
- ✅ Gère automatiquement les terrains (HeightfieldCollider)

### Conseils de performance

1. **Utilisez des layer masks** pour limiter les tests
   ```csharp
   // ❌ Teste tous les colliders
   Physics.Raycast(origin, direction, out hit, 100f);
   
   // ✅ Teste uniquement les colliders pertinents
   int groundLayer = 1 << 3;
   Physics.Raycast(origin, direction, out hit, 100f, groundLayer);
   ```

2. **RaycastNonAlloc** pour éviter les allocations
   ```csharp
   // ❌ Allocation chaque frame
   RaycastHit[] hits = Physics.RaycastAll(origin, direction, 100f);
   
   // ✅ Réutilise le même tableau
   private RaycastHit[] _hitBuffer = new RaycastHit[10];
   int count = Physics.RaycastNonAlloc(origin, direction, _hitBuffer, 100f);
   ```

3. **Limitez la distance** des raycasts
   ```csharp
   // ❌ Distance infinie
   Physics.Raycast(origin, direction, out hit, float.MaxValue);
   
   // ✅ Distance raisonnable
   Physics.Raycast(origin, direction, out hit, 50f);
   ```

## 🎯 Exemples pratiques

### Ground detection (CharacterController)

```csharp
Vector3 rayOrigin = position + Vector3.UnitY * 0.1f;
Vector3 rayDirection = -Vector3.UnitY;
float rayLength = 0.5f;

Ray ray = new Ray { Origin = rayOrigin, Direction = rayDirection };

if (Physics.SphereCast(ray, 0.3f, out RaycastHit hit, rayLength,
    layerMask: ~0, query: QueryTriggerInteraction.Ignore))
{
    if (hit.Entity != selfEntity)
    {
        IsGrounded = true;
        GroundNormal = hit.Normal;
    }
}
```

### Weapon raycast (FPS)

```csharp
Vector3 gunPosition = gun.Transform.Position;
Vector3 aimDirection = gun.Transform.Forward;

if (Physics.Raycast(gunPosition, aimDirection, out RaycastHit hit, 1000f))
{
    // Spawn impact effect
    Instantiate(bulletImpact, hit.Point, Quaternion.LookRotation(hit.Normal));
    
    // Deal damage
    if (hit.Entity?.GetComponent<Health>() is Health health)
    {
        health.TakeDamage(25f);
    }
}
```

### Explosion damage

```csharp
Vector3 explosionCenter = grenade.Transform.Position;
float explosionRadius = 10f;

if (Physics.OverlapSphere(explosionCenter, explosionRadius, out List<Collider> results))
{
    foreach (var collider in results)
    {
        if (collider.Entity?.GetComponent<Health>() is Health health)
        {
            float distance = Vector3.Distance(explosionCenter, collider.Entity.Transform.Position);
            float damageScale = 1f - (distance / explosionRadius); // Falloff
            health.TakeDamage(100f * damageScale);
        }
    }
}
```

### AI vision cone

```csharp
Vector3 aiPosition = ai.Transform.Position + Vector3.UnitY * 1.5f; // Eye height
Vector3 toTarget = (target.Transform.Position - aiPosition).Normalized();
float angle = Vector3.Dot(ai.Transform.Forward, toTarget);

// Check if in vision cone (45° = 0.7 dot product)
if (angle > 0.7f)
{
    float distance = Vector3.Distance(aiPosition, target.Transform.Position);
    
    if (Physics.Raycast(aiPosition, toTarget, out RaycastHit hit, distance))
    {
        if (hit.Entity == target)
        {
            // Can see target!
            ai.State = AIState.Chase;
        }
    }
}
```

## 🐛 Troubleshooting

### "Raycast ne détecte rien"

**Vérifier** :
1. ✅ Les colliders sont activés (`Enabled = true`)
2. ✅ L'entity a un collider attaché
3. ✅ Le layer mask est correct
4. ✅ La direction est normalisée
5. ✅ La distance maximale est suffisante

### "Raycast détecte l'entité elle-même"

**Solution** :
```csharp
if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance))
{
    if (hit.Entity != selfEntity)
    {
        // Traiter le hit
    }
}
```

### "Performance lente avec beaucoup de raycasts"

**Solutions** :
1. Utiliser `RaycastNonAlloc()` au lieu de `RaycastAll()`
2. Limiter la distance des raycasts
3. Utiliser des layer masks pour filtrer
4. Espacer les raycasts dans le temps (pas tous les frames)

## 📚 Comparaison avec Unity

| Feature | Unity Physics | AstrildApex Physics |
|---------|---------------|---------------------|
| Raycast | ✅ | ✅ |
| SphereCast | ✅ | ✅ |
| CapsuleCast | ✅ | ✅ |
| BoxCast | ✅ | ✅ |
| RaycastAll | ✅ | ✅ |
| RaycastNonAlloc | ✅ | ✅ |
| Layer masks | ✅ | ✅ |
| QueryTriggerInteraction | ✅ | ✅ |
| Spatial optimization | ✅ (Octree) | ✅ (Spatial Hash) |

**AstrildApex = API compatible Unity !** Migration facile.

---

**Créé le** : 10 décembre 2025  
**Version** : 1.0  
**Auteur** : GitHub Copilot pour AstrildApex Engine
