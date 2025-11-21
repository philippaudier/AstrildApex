# 🐛 Fix : CharacterController flotte / monte au lieu de tomber

## Diagnostic

Votre CharacterController ne détecte pas le sol et monte quand vous bougez.

### Causes Possibles

1. ✅ **Le plane n'a PAS de collider** (cause #1 - 90% des cas)
2. ⚠️ Le plane est trop loin (> 3m sous le player)
3. ⚠️ Le CharacterController passe un mouvement vertical positif dans `Move()`

---

## Solution 1 : Ajouter un BoxCollider au Plane

### Dans l'Éditeur :

1. Sélectionner votre entité "Plane" (ou Ground/Floor)
2. Dans l'Inspector, cliquer sur **"Add Component"**
3. Choisir **"Box Collider"**
4. Ajuster la taille du collider :
   - **Size X** : 10 (largeur du plane)
   - **Size Y** : 0.1 (hauteur très petite pour un plane plat)
   - **Size Z** : 10 (profondeur du plane)

### Ou par code (dans votre script de setup de scène) :

```csharp
// Trouver le plane
var plane = scene.Entities.FirstOrDefault(e => e.Name == "Plane");
if (plane != null)
{
    var boxCollider = plane.AddComponent<BoxCollider>();
    boxCollider.Size = new Vector3(10, 0.1f, 10); // Plane plat
    Console.WriteLine("✅ BoxCollider ajouté au Plane");
}
```

---

## Solution 2 : Vérifier la Distance

Le CharacterController détecte le sol jusqu'à **`GroundCheckDistance`** (défaut: 3m).

Si votre player est à Y=10 et le plane à Y=0, il faut :
- Baisser le player plus près du sol (Y=1 à Y=2)
- OU augmenter `GroundCheckDistance`

```csharp
controller.GroundCheckDistance = 10f; // Au lieu de 3m par défaut
```

---

## Solution 3 : Corriger votre Script de Mouvement

### ❌ **MAUVAIS** (fait monter le personnage) :

```csharp
// NE PAS FAIRE ÇA
controller.Move(new Vector3(x, 0.1f, z), dt); // Le 0.1f fait monter !
```

### ✅ **BON** :

```csharp
// Mouvement horizontal SEULEMENT
controller.Move(new Vector3(x, 0, z), dt); // Y = 0

// Pour sauter :
if (Input.IsKeyPressed(Key.Space) && controller.IsGrounded)
{
    controller.AddVerticalImpulse(7f); // Fonction dédiée
}
```

---

## Test de Diagnostic

Ajoutez ce component de debug à votre Player pour voir ce qui se passe :

### CharacterControllerDebug.cs (déjà créé)

```csharp
using Engine.Components;
using OpenTK.Mathematics;

public class CharacterControllerDebug : Component
{
    private CharacterController? _controller;
    private int _frameCount = 0;

    public override void Start()
    {
        _controller = Entity?.GetComponent<CharacterController>();
        if (_controller != null)
        {
            _controller.DebugPhysics = true; // Active les logs
        }
    }

    public override void Update(float dt)
    {
        if (_controller == null) return;
        
        _frameCount++;
        if (_frameCount % 60 == 0) // Toutes les secondes
        {
            Console.WriteLine($"[DEBUG] Y={Entity.Transform.Position.Y:F3}, IsGrounded={_controller.IsGrounded}, Velocity={_controller.Velocity:F3}");
            
            // Test raycast
            var ray = new Physics.Ray { Origin = Entity.Transform.Position, Direction = Vector3.UnitY * -1 };
            if (Physics.Physics.Raycast(ray, out var hit, 10f))
            {
                Console.WriteLine($"[DEBUG] ✅ Ground found at {hit.Distance:F3}m below");
            }
            else
            {
                Console.WriteLine($"[DEBUG] ❌ NO GROUND (raycast 10m down)");
            }
        }
    }
}
```

### Ce que vous devriez voir :

#### ✅ **Si ça fonctionne** :
```
[DEBUG] Y=1.000, IsGrounded=True, Velocity=(0, 0, 0)
[DEBUG] ✅ Ground found at 0.100m below
[CharacterController] Ground detected at Y=0.900, distance=0.100, collider=BoxCollider
```

#### ❌ **Si le plane n'a pas de collider** :
```
[DEBUG] Y=5.432, IsGrounded=False, Velocity=(0, -9.81, 0)
[DEBUG] ❌ NO GROUND (raycast 10m down)
[CharacterController] No ground detected from position Y=5.432
```

---

## Checklist de Vérification

- [ ] Le **Plane a un BoxCollider** (vérifier dans l'Inspector)
- [ ] Le **BoxCollider.Size** est correct (ex: 10x0.1x10 pour un plane)
- [ ] Le **Player est proche du sol** (1-2m au-dessus, pas 10m)
- [ ] Le **script de mouvement** passe **Y=0** dans `Move()`
- [ ] **`DebugPhysics = true`** est activé pour voir les logs

---

## Exemple Complet de Setup

```csharp
// Dans votre scène de test
var scene = new Scene();

// 1. Créer le plane avec collider
var plane = scene.CreateEntity("Plane");
plane.AddComponent<MeshRendererComponent>().Mesh = MeshKind.Plane;
plane.Transform.Position = new Vector3(0, 0, 0);
plane.Transform.Scale = new Vector3(10, 1, 10);

// IMPORTANT : Ajouter le collider !
var planeCollider = plane.AddComponent<BoxCollider>();
planeCollider.Size = new Vector3(10, 0.1f, 10);

// 2. Créer le player avec CharacterController
var player = scene.CreateEntity("Player");
player.Transform.Position = new Vector3(0, 2, 0); // 2m au-dessus du plane

var controller = player.AddComponent<CharacterController>();
controller.Height = 1.8f;
controller.Radius = 0.35f;
controller.DebugPhysics = true; // Debug

// 3. Ajouter le debug (optionnel)
player.AddComponent<CharacterControllerDebug>();

// 4. Script de mouvement (exemple)
player.AddComponent<PlayerMovement>();
```

---

## Si Ça Ne Marche Toujours Pas

1. **Vérifier les layers** : Le CharacterController et le Plane doivent être sur des layers qui se détectent
2. **Vérifier que le collider est activé** : `collider.Enabled = true`
3. **Augmenter GroundCheckDistance** : `controller.GroundCheckDistance = 10f;`
4. **Regarder la console** : Les logs `[DEBUG]` et `[CharacterController]` vous diront exactement ce qui se passe

---

## Résumé Rapide

**Le problème #1 est TOUJOURS : Le plane n'a pas de collider !**

Ajoutez un `BoxCollider` à votre plane et tout fonctionnera. 🎯
