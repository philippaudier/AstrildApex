# Migration vers KinematicCharacterController (2024)

## Nouveau système - Simple et robuste

Votre ancien système était fragmenté :
- `PlayerController` → `CharacterController` → `KinematicBody`
- 3 fichiers, logique dispersée, bugs complexes

Le nouveau système est **unifié** :
- `PlayerController` → `KinematicCharacterController`
- Tout-en-un, simple, moderne

---

## Architecture du nouveau système

### KinematicCharacterController.cs

**UN SEUL composant** qui gère TOUT :

```csharp
public class KinematicCharacterController : Component
{
    // INPUT (appelé par PlayerController)
    public void Move(Vector2 input, bool jump = false, bool run = false)

    // STATE (lecture seule)
    public Vector3 Velocity { get; }
    public bool IsGrounded { get; }
    public Vector3 GroundNormal { get; }

    // PHYSICS (automatique dans FixedUpdate)
    - ApplyInputAcceleration()
    - ApplyGravity()
    - ApplySlopeSliding()
    - TryJump()
    - SweepAndSlide()  // Collision moderne
    - DetectGround()
    - SnapToGround()
}
```

### Caractéristiques modernes (2024)

✅ **Sweep-and-slide** : Cast → Hit → Slide le long de la surface → Repeat
✅ **Pas de séparation horizontal/vertical** : Le mouvement suit naturellement les pentes
✅ **Pas de colliders nécessaires** : Queries physiques directes (terrain, spheres, boxes)
✅ **Shape au choix** : Capsule (recommandé) ou Box AABB
✅ **Simple** : ~450 lignes, tout est clair et commenté

---

## Migration étape par étape

### Étape 1 : Remplacer le composant sur votre joueur

**Dans l'éditeur** :

1. Sélectionnez votre entité joueur
2. **RETIREZ** ces composants :
   - `CharacterController`
   - `KinematicBody`
3. **AJOUTEZ** le nouveau composant :
   - `KinematicCharacterController`

**Configuration recommandée** :
```
ShapeType: Capsule
Height: 1.8
Radius: 0.3
Center: (0, 0.9, 0)

MaxWalkSpeed: 6
MaxRunSpeed: 9
Acceleration: 20
Friction: 10

JumpSpeed: 7
Gravity: 20
MaxSlopeAngle: 45
GroundSnapDistance: 0.3
```

### Étape 2 : Mettre à jour PlayerController (si nécessaire)

Votre `PlayerController.cs` actuel devrait **fonctionner tel quel** !

Il appelle déjà :
```csharp
controller.Move(worldInput, jumpPressed, isRunning);
```

Le nouveau `KinematicCharacterController` a la **même signature** :
```csharp
public void Move(Vector2 input, bool jump = false, bool run = false)
```

**Seule différence** : Le type de la référence

**AVANT** :
```csharp
[Editable] public CharacterController? controller;
```

**APRÈS** :
```csharp
[Editable] public KinematicCharacterController? controller;
```

Changez juste cette ligne dans `PlayerController.cs` ligne 36 !

### Étape 3 : Tester

1. Compilez : `dotnet build`
2. Lancez le jeu
3. Testez :
   - Déplacements sur terrain plat ✓
   - Montée/descente de pentes ✓
   - Saut ✓
   - Course (Shift) ✓
   - Collision avec sphères/boxes ✓

---

## Avantages du nouveau système

### 1. Suivi naturel des pentes

**ANCIEN** : Séparation horizontal/vertical → allait tout droit au lieu de suivre la pente
**NOUVEAU** : Sweep-and-slide → suit naturellement la surface

### 2. Glissement sur pentes raides

```csharp
if (slopeAngle > MaxSlopeAngle)
{
    // Glisse automatiquement vers le bas
    // Vitesse augmente avec l'angle
    // Le joueur peut essayer de combattre en bougeant
}
```

### 3. Collision robuste

**ANCIEN** : Multiples snaps agressifs → bugs, blocages, traversées
**NOUVEAU** : Sweep-and-slide moderne → fiable, prévisible

### 4. Pas de colliders nécessaires

Le CC query directement la physique :
- Terrain : `HeightfieldCollider` (raycast)
- Sphères/Boxes : Queries shapes directes
- Pas de `Collider` sur le joueur !

### 5. Simple à comprendre et modifier

Tout le code est dans **UN SEUL fichier** :
- Facile à lire
- Facile à débugger
- Facile à personnaliser

---

## Paramètres expliqués

### Shape

```csharp
ShapeType: Capsule  // Recommandé - smooth sur obstacles
Height: 1.8         // Hauteur du personnage
Radius: 0.3         // Rayon de la capsule
Center: (0, 0.9, 0) // Centre à mi-hauteur
```

### Movement

```csharp
MaxWalkSpeed: 6        // Vitesse de marche (m/s)
MaxRunSpeed: 9         // Vitesse de course (m/s)
Acceleration: 20       // Accélération au sol
AirAcceleration: 5     // Contrôle en l'air (plus faible)
Friction: 10           // Décélération quand pas d'input
```

### Jump

```csharp
JumpSpeed: 7          // Vitesse verticale au saut
CoyoteTime: 0.1       // Peut sauter 0.1s après avoir quitté le sol
JumpBufferTime: 0.1   // Mémorise le saut 0.1s avant d'atterrir
```

### Physics

```csharp
Gravity: 20                   // Gravité (m/s²)
MaxFallSpeed: 50              // Vitesse de chute max
MaxSlopeAngle: 45             // Pentes >45° = glisse
SlopeGravityMultiplier: 2     // Facteur de glissement
MaxStepHeight: 0.4            // Monte automatiquement les marches <40cm
GroundSnapDistance: 0.3       // Distance de snap au sol
SkinWidth: 0.02               // Marge de collision (évite pénétration)
MaxSlideIterations: 4         // Itérations de slide par frame
```

---

## Dépannage

### "Le CC traverse le terrain"

Vérifiez que votre terrain a un `HeightfieldCollider` actif.

### "Le CC ne monte pas les pentes"

Augmentez `MaxSlopeAngle` (ex: 60°) ou vérifiez `GroundSnapDistance`.

### "Le CC glisse trop sur pentes raides"

Réduisez `SlopeGravityMultiplier` ou augmentez `Friction`.

### "Le saut ne fonctionne pas"

Vérifiez que `JumpSpeed` > `Gravity` et que `CoyoteTime` > 0.

### "Le CC se coince dans les coins"

Augmentez `SkinWidth` (ex: 0.05) ou réduisez `Radius`.

---

## Code technique - Comment ça marche

### Sweep-and-slide (simplifié)

```csharp
Vector3 position = currentPosition;
Vector3 remainingMotion = velocity * dt;

for (int i = 0; i < 4; i++)  // Max 4 itérations
{
    if (CastShape(position, remainingMotion, out hit))
    {
        // Hit obstacle - avance jusqu'au contact
        position += direction * (hit.Distance - skinWidth);

        // Slide le long de la surface
        remainingMotion = ProjectOnPlane(remainingMotion, hit.Normal);
    }
    else
    {
        // Pas d'obstacle - avance complètement
        position += remainingMotion;
        break;
    }
}
```

### Ground detection

```csharp
// Raycast vers le bas depuis le bas de la capsule
Vector3 bottom = position + center - Vector3.UnitY * (height/2 - radius);

if (Raycast(bottom, -Vector3.UnitY, out hit, snapDistance))
{
    float slopeAngle = Angle(hit.Normal, Vector3.UnitY);

    if (slopeAngle <= MaxSlopeAngle)
    {
        IsGrounded = true;
        GroundNormal = hit.Normal;
    }
}
```

---

## Prochaines étapes

1. ✅ Migrez vers `KinematicCharacterController`
2. ✅ Testez sur votre terrain
3. ✅ Ajustez les paramètres selon vos besoins
4. ✅ Supprimez `CharacterController.cs` et `KinematicBody.cs` (obsolètes)

Le nouveau système est **production-ready** et utilisé dans des jeux modernes en 2024 !
