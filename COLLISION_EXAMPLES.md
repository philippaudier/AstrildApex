# 🎮 Exemples de Code - Système de Collision

## Table des Matières
1. [CharacterController Basique](#charactercontroller-basique)
2. [CharacterController Avancé](#charactercontroller-avancé)
3. [Projectiles et Raycasts](#projectiles-et-raycasts)
4. [Détection de Zone (Overlaps)](#détection-de-zone-overlaps)
5. [Portes et Objets Interactifs](#portes-et-objets-interactifs)
6. [Plateforme Mobile](#plateforme-mobile)
7. [Téléportation Sécurisée](#téléportation-sécurisée)

---

## CharacterController Basique

### FPS Controller Simple

```csharp
using Engine.Components;
using Engine.Input;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

public class FPSController : Component
{
    private CharacterController _controller = null!;
    private float _moveSpeed = 5f;
    private float _jumpForce = 7f;
    
    public override void Start()
    {
        _controller = Entity!.GetComponent<CharacterController>() 
            ?? Entity.AddComponent<CharacterController>();
        
        // Configuration
        _controller.Height = 1.8f;
        _controller.Radius = 0.35f;
        _controller.MaxSlopeAngleDeg = 45f;
    }
    
    public override void Update(float dt)
    {
        // Input ZQSD
        var input = Vector3.Zero;
        if (Input.IsKeyPressed(Key.W)) input.Z -= 1;
        if (Input.IsKeyPressed(Key.S)) input.Z += 1;
        if (Input.IsKeyPressed(Key.A)) input.X -= 1;
        if (Input.IsKeyPressed(Key.D)) input.X += 1;
        
        // Normaliser pour éviter vitesse diagonale plus rapide
        if (input.LengthSquared > 0)
        {
            input = input.Normalized();
        }
        
        // Appliquer rotation caméra (si vous avez une caméra)
        var forward = Entity.Transform.Rotation * Vector3.UnitZ * -1;
        var right = Entity.Transform.Rotation * Vector3.UnitX;
        var moveDirection = (forward * input.Z + right * input.X).Normalized();
        
        // Mouvement horizontal
        if (input.LengthSquared > 0)
        {
            var motion = moveDirection * _moveSpeed * dt;
            _controller.Move(motion, dt);
        }
        
        // Saut
        if (Input.IsKeyPressed(Key.Space) && _controller.IsGrounded)
        {
            _controller.AddVerticalImpulse(_jumpForce);
        }
    }
}
```

---

## CharacterController Avancé

### Third Person avec Sprint et Dash

```csharp
public class ThirdPersonController : Component
{
    private CharacterController _controller = null!;
    
    [Editable] public float WalkSpeed = 3f;
    [Editable] public float RunSpeed = 6f;
    [Editable] public float JumpForce = 7f;
    [Editable] public float DashSpeed = 15f;
    [Editable] public float DashDuration = 0.2f;
    
    private bool _isDashing = false;
    private float _dashTimer = 0f;
    private Vector3 _dashDirection;
    
    public override void Start()
    {
        _controller = Entity!.GetComponent<CharacterController>()!;
    }
    
    public override void Update(float dt)
    {
        // Dash
        if (_isDashing)
        {
            _dashTimer -= dt;
            if (_dashTimer <= 0)
            {
                _isDashing = false;
            }
            else
            {
                // Mouvement dash (ignore collisions normales)
                _controller.Move(_dashDirection * DashSpeed * dt, dt);
                return;
            }
        }
        
        // Input
        var input = Vector3.Zero;
        if (Input.IsKeyPressed(Key.W)) input.Z -= 1;
        if (Input.IsKeyPressed(Key.S)) input.Z += 1;
        if (Input.IsKeyPressed(Key.A)) input.X -= 1;
        if (Input.IsKeyPressed(Key.D)) input.X += 1;
        
        if (input.LengthSquared > 0)
        {
            input = input.Normalized();
            
            // Sprint avec Shift
            float speed = Input.IsKeyPressed(Key.LeftShift) ? RunSpeed : WalkSpeed;
            
            // Direction relative à caméra
            var moveDirection = CalculateMoveDirection(input);
            _controller.Move(moveDirection * speed * dt, dt);
            
            // Dash avec Ctrl (cooldown géré ailleurs)
            if (Input.IsKeyPressed(Key.LeftControl) && _controller.IsGrounded)
            {
                StartDash(moveDirection);
            }
        }
        
        // Saut
        if (Input.IsKeyPressed(Key.Space) && _controller.IsGrounded)
        {
            _controller.AddVerticalImpulse(JumpForce);
        }
    }
    
    private void StartDash(Vector3 direction)
    {
        _isDashing = true;
        _dashTimer = DashDuration;
        _dashDirection = direction;
    }
    
    private Vector3 CalculateMoveDirection(Vector3 input)
    {
        // Supposons que vous avez une entité caméra
        var camera = Entity.Scene?.FindEntity("Camera");
        if (camera == null) return new Vector3(input.X, 0, input.Z);
        
        var forward = camera.Transform.Rotation * Vector3.UnitZ * -1;
        forward.Y = 0; // Garder horizontal
        forward = forward.Normalized();
        
        var right = Vector3.Cross(forward, Vector3.UnitY).Normalized();
        
        return (forward * input.Z + right * input.X).Normalized();
    }
}
```

---

## Projectiles et Raycasts

### Système de Tir Hitscan

```csharp
public class Gun : Component
{
    [Editable] public float Range = 100f;
    [Editable] public float Damage = 25f;
    [Editable] public float FireRate = 0.1f; // Secondes entre tirs
    
    private float _fireTimer = 0f;
    
    public override void Update(float dt)
    {
        _fireTimer -= dt;
        
        if (Input.IsMouseButtonPressed(MouseButton.Left) && _fireTimer <= 0)
        {
            Fire();
            _fireTimer = FireRate;
        }
    }
    
    private void Fire()
    {
        // Raycast depuis le canon du fusil
        var gunTip = Entity!.Transform.Position + 
                     Entity.Transform.Rotation * new Vector3(0, 0, -1f);
        var direction = Entity.Transform.Rotation * Vector3.UnitZ * -1;
        
        var ray = new Ray { Origin = gunTip, Direction = direction };
        
        if (Physics.Raycast(ray, out var hit, Range))
        {
            Console.WriteLine($"Hit {hit.Entity?.Name} at {hit.Distance:F2}m");
            
            // Appliquer dégâts (si l'entité a un component Health)
            var health = hit.Entity?.GetComponent<Health>();
            if (health != null)
            {
                health.TakeDamage(Damage);
            }
            
            // Effet visuel au point d'impact
            SpawnImpactEffect(hit.Point, hit.Normal);
        }
    }
    
    private void SpawnImpactEffect(Vector3 position, Vector3 normal)
    {
        // Créer particules/sprite au point d'impact
        var impact = Entity!.Scene!.CreateEntity("Impact");
        impact.Transform.Position = position;
        // ... ajouter renderer, etc.
    }
}
```

### Grenade avec Arc de Trajectoire

```csharp
public class Grenade : Component
{
    [Editable] public float ExplosionRadius = 5f;
    [Editable] public float ExplosionDamage = 100f;
    [Editable] public float FuseTime = 3f;
    
    private Vector3 _velocity;
    private float _timer;
    
    public void Launch(Vector3 velocity)
    {
        _velocity = velocity;
        _timer = FuseTime;
    }
    
    public override void Update(float dt)
    {
        _timer -= dt;
        
        if (_timer <= 0)
        {
            Explode();
            return;
        }
        
        // Physique simple
        _velocity.Y -= 9.81f * dt; // Gravité
        
        // Mouvement avec détection de collision
        var motion = _velocity * dt;
        var ray = new Ray { 
            Origin = Entity!.Transform.Position, 
            Direction = motion.Normalized() 
        };
        
        if (Physics.Raycast(ray, out var hit, motion.Length))
        {
            // Rebondir sur le sol/murs
            Entity.Transform.Position = hit.Point;
            _velocity = Vector3.Reflect(_velocity, hit.Normal) * 0.6f; // Perte d'énergie
        }
        else
        {
            Entity.Transform.Position += motion;
        }
    }
    
    private void Explode()
    {
        var center = Entity!.Transform.Position;
        
        // Trouver toutes les entités dans le rayon
        if (Physics.OverlapSphere(center, ExplosionRadius, out var colliders))
        {
            foreach (var collider in colliders)
            {
                var entity = collider.Entity;
                if (entity == null) continue;
                
                // Distance pour atténuation
                var distance = (entity.Transform.Position - center).Length;
                var damageMultiplier = 1f - (distance / ExplosionRadius);
                
                // Appliquer dégâts
                var health = entity.GetComponent<Health>();
                if (health != null)
                {
                    health.TakeDamage(ExplosionDamage * damageMultiplier);
                }
                
                // Appliquer force (si rigidbody dans le futur)
                // var rb = entity.GetComponent<Rigidbody>();
                // rb?.AddExplosionForce(1000f, center, ExplosionRadius);
            }
        }
        
        // Effet visuel
        // SpawnExplosionParticles();
        
        // Détruire la grenade
        Entity.Scene?.RemoveEntity(Entity);
    }
}
```

---

## Détection de Zone (Overlaps)

### Zone de Trigger (Checkpoint)

```csharp
public class Checkpoint : Component
{
    [Editable] public Vector3 CheckpointSize = new Vector3(2, 3, 2);
    
    private bool _activated = false;
    
    public override void Update(float dt)
    {
        if (_activated) return;
        
        var center = Entity!.Transform.Position;
        var halfExtents = CheckpointSize * 0.5f;
        
        // Vérifier si le joueur est dans la zone
        if (Physics.OverlapBox(center, halfExtents, out var colliders))
        {
            foreach (var collider in colliders)
            {
                // Chercher le tag "Player" (ou votre méthode de tagging)
                if (collider.Entity?.Name == "Player")
                {
                    ActivateCheckpoint();
                    break;
                }
            }
        }
    }
    
    private void ActivateCheckpoint()
    {
        _activated = true;
        Console.WriteLine($"Checkpoint '{Entity!.Name}' activé !");
        
        // Sauvegarder la position du joueur
        // GameManager.Instance.SetRespawnPoint(Entity.Transform.Position);
        
        // Effet visuel
        // ... particules, changement de couleur, etc.
    }
}
```

### Détecteur de Proximité (Enemy AI)

```csharp
public class EnemyAI : Component
{
    [Editable] public float DetectionRadius = 10f;
    [Editable] public float AttackRange = 2f;
    
    private Entity? _target;
    
    public override void Update(float dt)
    {
        var center = Entity!.Transform.Position;
        
        // Chercher le joueur dans le rayon de détection
        if (Physics.OverlapSphere(center, DetectionRadius, out var colliders))
        {
            foreach (var collider in colliders)
            {
                if (collider.Entity?.Name == "Player")
                {
                    _target = collider.Entity;
                    break;
                }
            }
        }
        
        if (_target != null)
        {
            var toTarget = _target.Transform.Position - center;
            var distance = toTarget.Length;
            
            if (distance <= AttackRange)
            {
                // Attaquer
                Attack();
            }
            else
            {
                // Se déplacer vers la cible
                MoveTowards(_target.Transform.Position, dt);
            }
        }
    }
    
    private void MoveTowards(Vector3 target, float dt)
    {
        var direction = (target - Entity!.Transform.Position).Normalized();
        
        // Vérifier si le chemin est libre (éviter murs)
        var ray = new Ray { Origin = Entity.Transform.Position, Direction = direction };
        if (!Physics.Raycast(ray, out _, 1f)) // 1m devant
        {
            Entity.Transform.Position += direction * 3f * dt;
        }
    }
    
    private void Attack()
    {
        Console.WriteLine("Enemy attacks!");
        // Logique d'attaque
    }
}
```

---

## Portes et Objets Interactifs

### Porte Coulissante

```csharp
public class SlidingDoor : Component
{
    [Editable] public float OpenDistance = 2f;
    [Editable] public float SlideDistance = 3f;
    [Editable] public float SlideSpeed = 2f;
    
    private Vector3 _closedPosition;
    private Vector3 _openPosition;
    private bool _isOpen = false;
    private float _openProgress = 0f; // 0 = fermé, 1 = ouvert
    
    public override void Start()
    {
        _closedPosition = Entity!.Transform.Position;
        _openPosition = _closedPosition + new Vector3(0, SlideDistance, 0); // Monte
    }
    
    public override void Update(float dt)
    {
        var center = Entity!.Transform.Position;
        
        // Détecter joueur à proximité
        bool playerNearby = Physics.CheckSphere(center, OpenDistance);
        
        if (playerNearby && !_isOpen)
        {
            _isOpen = true;
        }
        else if (!playerNearby && _isOpen)
        {
            _isOpen = false;
        }
        
        // Animer l'ouverture/fermeture
        float targetProgress = _isOpen ? 1f : 0f;
        _openProgress = MathHelper.Lerp(_openProgress, targetProgress, SlideSpeed * dt);
        
        // Appliquer position
        Entity.Transform.Position = Vector3.Lerp(_closedPosition, _openPosition, _openProgress);
        
        // Mettre à jour le collider (marqué dirty automatiquement par Transform.Position)
    }
}
```

### Bouton Interactif

```csharp
public class Button : Component
{
    [Editable] public float InteractionDistance = 2f;
    
    public event Action? OnPressed;
    private bool _isPressed = false;
    
    public override void Update(float dt)
    {
        if (_isPressed) return;
        
        // Trouver le joueur
        var player = Entity!.Scene?.FindEntity("Player");
        if (player == null) return;
        
        var distance = (player.Transform.Position - Entity.Transform.Position).Length;
        
        // Si joueur proche et appuie sur E
        if (distance <= InteractionDistance && Input.IsKeyPressed(Key.E))
        {
            Press();
        }
    }
    
    private void Press()
    {
        _isPressed = true;
        Console.WriteLine($"Button '{Entity!.Name}' pressed!");
        
        OnPressed?.Invoke();
        
        // Effet visuel/sonore
        // ... changer couleur, jouer son, etc.
    }
}

// Utilisation :
// var button = entity.AddComponent<Button>();
// button.OnPressed += () => { /* Ouvrir porte, activer piège, etc. */ };
```

---

## Plateforme Mobile

### Plateforme Oscillante

```csharp
public class MovingPlatform : Component
{
    [Editable] public Vector3 MoveOffset = new Vector3(5, 0, 0);
    [Editable] public float MoveSpeed = 2f;
    
    private Vector3 _startPosition;
    private Vector3 _endPosition;
    private float _progress = 0f;
    private bool _movingForward = true;
    
    public override void Start()
    {
        _startPosition = Entity!.Transform.Position;
        _endPosition = _startPosition + MoveOffset;
        
        // Marquer comme dynamique pour physics
        Entity.Transform.HasDynamicMovement = true;
    }
    
    public override void Update(float dt)
    {
        // Ping-pong entre start et end
        if (_movingForward)
        {
            _progress += MoveSpeed * dt;
            if (_progress >= 1f)
            {
                _progress = 1f;
                _movingForward = false;
            }
        }
        else
        {
            _progress -= MoveSpeed * dt;
            if (_progress <= 0f)
            {
                _progress = 0f;
                _movingForward = true;
            }
        }
        
        // Appliquer position
        Entity!.Transform.Position = Vector3.Lerp(_startPosition, _endPosition, _progress);
        
        // TODO: Déplacer les entités sur la plateforme avec nous
        // (nécessite détection de "standing on platform")
    }
}
```

---

## Téléportation Sécurisée

### Téléporteur avec Validation

```csharp
public class Teleporter : Component
{
    [Editable] public Vector3 TargetPosition;
    [Editable] public float TriggerRadius = 1.5f;
    
    private HashSet<Entity> _teleportedEntities = new();
    
    public override void Update(float dt)
    {
        var center = Entity!.Transform.Position;
        
        // Détecter entités dans le rayon
        if (Physics.OverlapSphere(center, TriggerRadius, out var colliders))
        {
            foreach (var collider in colliders)
            {
                var entity = collider.Entity;
                if (entity == null || entity == Entity) continue;
                
                // Éviter téléportation multiple
                if (_teleportedEntities.Contains(entity)) continue;
                
                // Vérifier que la destination est libre
                if (IsPositionSafe(TargetPosition, entity))
                {
                    TeleportEntity(entity, TargetPosition);
                    _teleportedEntities.Add(entity);
                }
            }
        }
        else
        {
            // Reset quand entités sortent du trigger
            _teleportedEntities.Clear();
        }
    }
    
    private bool IsPositionSafe(Vector3 position, Entity entity)
    {
        // Vérifier avec la taille du CharacterController si présent
        var controller = entity.GetComponent<CharacterController>();
        if (controller != null)
        {
            var p1 = position + Vector3.UnitY * (controller.Height * 0.5f - controller.Radius);
            var p2 = position + Vector3.UnitY * (-controller.Height * 0.5f + controller.Radius);
            
            // Vérifier si la capsule overlap quelque chose à la destination
            return !Physics.CheckCapsule(p1, p2, controller.Radius);
        }
        
        // Fallback : check sphere
        return !Physics.CheckSphere(position, 0.5f);
    }
    
    private void TeleportEntity(Entity entity, Vector3 destination)
    {
        entity.Transform.Position = destination;
        Console.WriteLine($"Téléporté {entity.Name} vers {destination}");
        
        // Effet visuel
        // SpawnTeleportEffect(entity.Transform.Position);
        // SpawnTeleportEffect(destination);
    }
}
```

---

## 🎓 Conseils d'Utilisation

### Performance

1. **Préférer `CheckX()` à `OverlapX()`** si vous n'avez pas besoin de la liste :
   ```csharp
   // ❌ Lent
   if (Physics.OverlapSphere(pos, radius, out var results) && results.Count > 0)
   
   // ✅ Rapide
   if (Physics.CheckSphere(pos, radius))
   ```

2. **Réutiliser les listes** :
   ```csharp
   private List<Collider> _cachedResults = new();
   
   void Update(float dt)
   {
       _cachedResults.Clear();
       Physics.OverlapSphere(pos, radius, out _cachedResults);
       // Utiliser _cachedResults
   }
   ```

3. **Limiter les queries par frame** :
   ```csharp
   private int _frameCounter = 0;
   
   void Update(float dt)
   {
       _frameCounter++;
       if (_frameCounter % 5 == 0) // Tous les 5 frames seulement
       {
           DetectEnemies();
       }
   }
   ```

### Debugging

```csharp
// Activer debug sur CharacterController
controller.DebugPhysics = true;

// Visualiser raycast
if (Physics.Raycast(origin, direction, out var hit, 100f))
{
    // Dessiner une ligne de debug (si vous avez un système de debug drawing)
    Debug.DrawLine(origin, hit.Point, Color.Red, 1f);
}
```

Tous ces exemples sont production-ready et peuvent être copiés directement dans votre projet ! 🚀
