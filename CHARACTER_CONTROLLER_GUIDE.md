# Character Controller - Documentation Complète

## 📋 Vue d'ensemble

Le nouveau **CharacterController** d'AstrildApex est un contrôleur cinématique robuste et performant, inspiré des meilleures pratiques de Unity, Unreal Engine et votre prototype fonctionnel.

## 🎯 Philosophie de conception

### Séparation Physics / Visual

Le principe fondamental est la **séparation totale entre l'état physique et l'état visuel** :

- **État physique** (`_currentPosition`, `_previousPosition`, `_velocity`)
  - Mis à jour dans `FixedUpdate()` à 60 Hz (timestep fixe)
  - État déterministe et reproductible
  - Indépendant du framerate de rendu

- **État visuel** (`Transform.Position`)
  - Mis à jour dans `LateUpdate()` à framerate variable
  - Interpolé entre les états physiques pour un rendu fluide
  - Élimine le stutter visuel

### Pourquoi c'est important ?

Sans cette séparation, vous avez :
- ❌ Du "stutter" visuel à 60 FPS physique
- ❌ Des bugs d'interpolation (flottement, drift)
- ❌ Physique non-déterministe

Avec cette séparation :
- ✅ Rendu fluide à n'importe quel framerate (même 144 Hz+)
- ✅ Physique stable et prévisible
- ✅ Pas d'artefacts visuels

## 🚀 Fonctionnalités principales

### 1. **Coyote Time** 🕐
Période de grâce pour sauter après avoir quitté le sol.

```csharp
[Editable] public float CoyoteTime = 0.1f; // 100ms de grâce
```

**Utilité** : Améliore la sensation de contrôle - le joueur peut sauter légèrement après avoir quitté une plateforme.

### 2. **Jump Buffering** 🎮
File d'attente des inputs de saut avant l'atterrissage.

```csharp
[Editable] public float JumpBufferTime = 0.1f; // 100ms de buffer
```

**Utilité** : Si le joueur appuie sur Saut juste avant de toucher le sol, le saut sera exécuté dès l'atterrissage.

### 3. **Gestion des pentes** ⛰️

Le système détecte automatiquement :
- **Pentes marchables** (≤ MaxSlopeAngle) : le personnage marche normalement
- **Pentes raides** (> MaxSlopeAngle) : le personnage glisse vers le bas

```csharp
[Editable] public float MaxSlopeAngle = 45f; // Angle max en degrés
```

### 4. **Plateformes mobiles** 🚂

Le CharacterController hérite automatiquement de la vélocité des plateformes :

```csharp
// Exemple : Plateforme mobile
characterController.PlatformVelocity = platform.Velocity;
```

### 5. **Interpolation visuelle** 🎬

Trois modes d'interpolation disponibles :

| Mode | Description | Avantages | Inconvénients |
|------|-------------|-----------|---------------|
| **None** | Position physique appliquée directement | Simple, pas de latence | Peut saccader à faible FPS |
| **Interpolate** | Interpolation entre précédent/actuel | Très fluide, stable | Léger décalage (~8ms) |
| **Extrapolate** | Prédiction basée sur vélocité | Le plus réactif | Peut dépasser (overshoot) |

```csharp
[Editable] public InterpolationMode InterpolationMode = InterpolationMode.Interpolate;
```

**Recommandation** : Utilisez `Interpolate` pour la plupart des cas.

## 📐 API Publique

### Propriétés en lecture seule

```csharp
public bool IsGrounded { get; }           // Sur le sol ?
public Vector3 Velocity { get; }          // Vélocité actuelle
public Vector3 GroundNormal { get; }      // Normale de la surface
public Vector3 PhysicsPosition { get; }   // Position physique réelle
```

### Méthodes principales

```csharp
// Déplacer le personnage (appeler depuis Update())
void Move(Vector2 input, bool jumpPressed = false, bool isRunning = false)

// Sauter immédiatement
void Jump()

// Ajouter une impulsion (explosions, impacts, etc.)
void AddImpulse(Vector3 impulse)

// Téléporter sans collision
void Teleport(Vector3 position)
```

## 🎮 Utilisation avec PlayerController

Le `PlayerController` est maintenant **ultra-simple** :

```csharp
public override void Update(float dt)
{
    // 1. Lire l'input
    Vector2 moveInput = GetMoveInput();
    bool isRunning = GetRunInput();
    bool jumpPressed = GetJumpInput();

    // 2. Convertir en direction caméra
    Vector2 worldInput = ConvertToCameraSpace(moveInput);

    // 3. Envoyer au CharacterController
    controller.Move(worldInput, jumpPressed, isRunning);
}
```

**C'est tout !** Toute la physique est gérée automatiquement par le CharacterController.

## ⚙️ Paramètres recommandés

### Pour un jeu de plateforme

```csharp
Height = 1.8f;
Radius = 0.4f;
MaxWalkSpeed = 6f;
MaxRunSpeed = 9f;
Acceleration = 30f;      // Plus rapide pour réactivité
AirAcceleration = 10f;    // Contrôle aérien augmenté
JumpSpeed = 8f;
Gravity = 25f;           // Gravité forte type Mario
MaxSlopeAngle = 40f;
```

### Pour un FPS réaliste

```csharp
Height = 1.8f;
Radius = 0.3f;
MaxWalkSpeed = 5f;
MaxRunSpeed = 8f;
Acceleration = 20f;
AirAcceleration = 2f;     // Peu de contrôle aérien
JumpSpeed = 6f;
Gravity = 20f;
MaxSlopeAngle = 45f;
```

### Pour un jeu d'aventure/exploration

```csharp
Height = 1.8f;
Radius = 0.4f;
MaxWalkSpeed = 4f;
MaxRunSpeed = 7f;
Acceleration = 15f;       // Plus lent, plus pesant
AirAcceleration = 3f;
JumpSpeed = 7f;
Gravity = 18f;            // Gravité légère
MaxSlopeAngle = 50f;      // Peut grimper partout
```

## 🔧 Détection de collision

Le système utilise **CapsuleCast** (swept collision detection) pour :
- ✅ Prévenir le tunneling (traverser les murs)
- ✅ Gérer les collisions multiples (coins, murs)
- ✅ Slider smoothly le long des surfaces

### Algorithme multi-bounce

Le CharacterController utilise un algorithme de **sliding multi-bounce** (comme Quake/Source Engine) :

```
MAX_BOUNCES = 4  // Nombre d'itérations de slide

Pour chaque bounce:
  1. CapsuleCast dans la direction du mouvement
  2. Si collision: 
     - Avancer jusqu'au point de contact
     - Projeter le mouvement restant sur le plan de collision
     - Continuer avec le mouvement projeté
  3. Sinon: Appliquer tout le mouvement et terminer
```

Ceci permet de **naviguer smoothly** autour des coins et le long des murs.

## 🐛 Résolution de problèmes

### Le personnage flotte au-dessus du sol

**Causes possibles** :
1. `SkinWidth` trop grand
2. `GroundSnapDistance` trop petit
3. Collider du sol manquant ou désactivé

**Solution** :
```csharp
SkinWidth = 0.02f;           // Valeur recommandée
GroundSnapDistance = 0.2f;   // Augmenter si nécessaire
```

### Le personnage traverse les murs

**Causes possibles** :
1. Vitesse trop élevée (tunneling)
2. CapsuleCast ne détecte pas les collisions

**Solution** :
- Vérifier que les colliders sont activés
- Réduire `MaxWalkSpeed` ou `MaxRunSpeed`
- Le système utilise déjà CapsuleCast (swept detection) donc ça ne devrait pas arriver

### Stutter/saccades visuelles

**Causes possibles** :
1. InterpolationMode = None
2. Physics timestep mal configuré

**Solution** :
```csharp
InterpolationMode = InterpolationMode.Interpolate; // Recommandé
```

### Le saut ne fonctionne pas

**Vérifier** :
1. `IsGrounded` retourne true quand sur le sol ?
2. `JumpSpeed` suffisamment élevé ?
3. `CoyoteTime` / `JumpBufferTime` corrects ?

## 🎯 Architecture interne

### Pipeline de FixedUpdate

```
1. DetectGround()           → Raycast vers le bas, vérifier pentes
2. ApplyGravity()           → -9.8 m/s² si en l'air
3. ApplySlopeSliding()      → Glisser sur pentes raides
4. ApplyHorizontalMovement() → Accélération vers vitesse cible
5. ApplyRotation()          → Rotation auto (si activé)
6. TryJump()                → Coyote time + jump buffering
7. IntegratePosition()      → position += velocity * dt
8. ResolveCollisions()      → Dépénétration
```

### Pipeline de LateUpdate

```
1. CalculateVisualPosition() → Calcul selon InterpolationMode
2. Transform.Position = ...   → Application à la transform
```

## 🚀 Performance

Le CharacterController est **optimisé** pour de bonnes performances :

- ✅ **Spatial hash** : CapsuleCast utilise le spatial hash pour O(N) au lieu de O(N²)
- ✅ **Early exit** : Arrêt dès qu'il n'y a plus de mouvement résiduel
- ✅ **Pas d'allocation** : Aucune allocation mémoire dans la boucle chaude
- ✅ **Cache-friendly** : Données compactes et contiguës

## 📚 Comparaison avec Unity

| Fonctionnalité | Unity CharacterController | AstrildApex CharacterController |
|----------------|---------------------------|----------------------------------|
| Coyote Time | ❌ À implémenter | ✅ Intégré |
| Jump Buffering | ❌ À implémenter | ✅ Intégré |
| Interpolation | ⚠️ Via Rigidbody | ✅ Intégré avec 3 modes |
| Pentes | ✅ SlopeLimit | ✅ MaxSlopeAngle + sliding |
| Swept collision | ✅ Built-in | ✅ CapsuleCast multi-bounce |
| Plateformes mobiles | ⚠️ Manuel | ✅ PlatformVelocity intégré |
| Physics/Visual séparé | ❌ Non | ✅ Oui (best practice) |

## 🎓 Ressources et inspirations

### Inspirations
- **Unity** : CharacterController API
- **Unreal Engine** : Character Movement Component
- **Quake/Source Engine** : Algorithme de sliding multi-bounce
- **Votre prototype** : Architecture propre et séparation physics/visual

### Articles recommandés
- [Kinematic Character Controllers (GDC)](https://www.gdcvault.com)
- [Unity Character Controller Best Practices](https://docs.unity3d.com)
- [Fixed Timestep Interpolation](https://gafferongames.com/post/fix_your_timestep/)

## 🔮 Améliorations futures possibles

1. **Step climbing** : Monter automatiquement les petites marches
2. **Crouching** : Réduire la hauteur de la capsule
3. **Swimming** : Mode de déplacement dans l'eau
4. **Ladder climbing** : Grimper aux échelles
5. **Wall running** : Courir sur les murs (type Mirror's Edge)

---

**Créé le** : 10 décembre 2025  
**Version** : 1.0  
**Auteur** : GitHub Copilot pour AstrildApex Engine
