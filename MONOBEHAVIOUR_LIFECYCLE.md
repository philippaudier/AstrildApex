# MonoBehaviour Lifecycle Methods

## Vue d'ensemble

Les `MonoBehaviour` dans AstrildApex suivent le modèle de lifecycle Unity pour faciliter le développement de scripts de gameplay.

## Ordre d'exécution des méthodes

### 1. Initialisation (Une fois)

#### `Awake()`
- **Quand**: Appelée quand le script est chargé (avant `Start()`)
- **Usage**: Initialisation qui ne dépend pas d'autres objets
- **Exemple**: Récupérer des références internes, initialiser des variables
```csharp
public override void Awake()
{
    base.Awake();
    _rigidbody = GetComponent<RigidbodyComponent>();
    _initialHealth = 100;
}
```

#### `OnEnable()`
- **Quand**: Appelée quand le component/entity est activé
- **Usage**: Souscrire à des événements, activer des systèmes
- **Exemple**: Enregistrer des callbacks, démarrer des coroutines
```csharp
public override void OnEnable()
{
    base.OnEnable();
    InputManager.Instance.OnJumpPressed += HandleJump;
}
```

#### `Start()`
- **Quand**: Appelée avant le premier `Update()` (après `Awake()`)
- **Usage**: Initialisation qui dépend d'autres objets déjà prêts
- **Exemple**: Trouver des références à d'autres entities, configurer l'état initial
```csharp
public override void Start()
{
    base.Start();
    _target = Find("Player")?.GetComponent<TransformComponent>();
    _currentDistance = Distance;
}
```

### 2. Boucle de jeu (Chaque frame)

#### `Update(float deltaTime)`
- **Quand**: Appelée chaque frame
- **Usage**: Logique de gameplay générale, input, animations
- **Exemple**: Contrôle de caméra, input du joueur
```csharp
public override void Update(float deltaTime)
{
    base.Update(deltaTime);
    if (InputManager.Instance.IsKeyDown(Keys.W))
        transform.Position += transform.Forward * speed * deltaTime;
}
```

#### `LateUpdate(float deltaTime)`
- **Quand**: Appelée après tous les `Update()`
- **Usage**: Caméras qui suivent, ajustements finaux de position
- **Exemple**: Caméra third-person qui suit le joueur
```csharp
public override void LateUpdate(float deltaTime)
{
    base.LateUpdate(deltaTime);
    // Suivre le joueur avec un léger délai
    Camera.Position = Vector3.Lerp(Camera.Position, _target.Position, smoothing * deltaTime);
}
```

#### `FixedUpdate(float fixedDeltaTime)`
- **Quand**: Appelée à intervalle fixe (physique)
- **Usage**: Physique, forces, mouvements rigides
- **Exemple**: Appliquer des forces à un rigidbody
```csharp
public override void FixedUpdate(float fixedDeltaTime)
{
    base.FixedUpdate(fixedDeltaTime);
    if (_isGrounded && _jumpRequested)
    {
        _rigidbody.AddForce(Vector3.UnitY * jumpForce, ForceMode.Impulse);
        _jumpRequested = false;
    }
}
```

### 3. Nettoyage (Une fois)

#### `OnDisable()`
- **Quand**: Appelée quand le component/entity est désactivé
- **Usage**: Désabonner des événements, arrêter des systèmes
- **Exemple**: Retirer des callbacks
```csharp
public override void OnDisable()
{
    base.OnDisable();
    InputManager.Instance.OnJumpPressed -= HandleJump;
}
```

#### `OnDestroy()`
- **Quand**: Appelée quand le component est détruit (remove, scene unload, Play Mode stop)
- **Usage**: Nettoyage de ressources, libération mémoire
- **Exemple**: Nettoyer l'état du curseur, fermer des fichiers
```csharp
public override void OnDestroy()
{
    base.OnDestroy();
    if (_cursorWasLocked)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        InputManager.Instance?.UnlockCursor();
    }
}
```

### 4. Collision/Trigger (Physics)

#### `OnCollisionEnter(Collision collision)`
- **Quand**: Appelée au premier contact avec un autre collider
- **Usage**: Réagir aux collisions physiques
```csharp
public override void OnCollisionEnter(Collision collision)
{
    base.OnCollisionEnter(collision);
    if (collision.Entity.Name == "Ground")
        _isGrounded = true;
}
```

#### `OnCollisionStay(Collision collision)`
- **Quand**: Appelée chaque frame pendant le contact
- **Usage**: Effets continus pendant la collision
```csharp
public override void OnCollisionStay(Collision collision)
{
    base.OnCollisionStay(collision);
    if (collision.Entity.HasComponent<DamageZone>())
        TakeDamage(damagePerSecond * Time.deltaTime);
}
```

#### `OnCollisionExit(Collision collision)`
- **Quand**: Appelée quand les colliders se séparent
- **Usage**: Nettoyer l'état après collision
```csharp
public override void OnCollisionExit(Collision collision)
{
    base.OnCollisionExit(collision);
    if (collision.Entity.Name == "Ground")
        _isGrounded = false;
}
```

#### `OnTriggerEnter/Stay/Exit(Collision collision)`
- **Similaire aux OnCollision*** mais pour les triggers (colliders sans physique)
- **Usage**: Zones de détection, pickups, checkpoints

## Bonnes pratiques

### 1. Toujours appeler `base.Method()`
```csharp
public override void Start()
{
    base.Start(); // Important !
    // Votre code ici
}
```

### 2. Awake vs Start
- **Awake**: Initialisation interne, indépendante
- **Start**: Initialisation qui dépend d'autres objets

### 3. OnEnable vs Start
- **OnEnable**: Appelée à chaque activation (peut être multiple fois)
- **Start**: Appelée une seule fois

### 4. OnDisable vs OnDestroy
- **OnDisable**: Désactivation temporaire (peut être réactivé)
- **OnDestroy**: Destruction définitive (cleanup complet)

### 5. Update vs FixedUpdate
- **Update**: Framerate variable, logique visuelle/input
- **FixedUpdate**: Framerate fixe, physique déterministe

## Helpers disponibles

### Component Access
```csharp
var renderer = GetComponent<MeshRendererComponent>();
var rigidbody = AddComponent<RigidbodyComponent>();
```

### Lifecycle automatique

Les méthodes sont appelées automatiquement par le système :
- **PlayMode.Play()**: Appelle `Awake()`, `OnEnable()`, `Start()` sur tous les components
- **PlayMode.Stop()**: Appelle `OnDestroy()` sur tous les components
- **Entity.RemoveComponent()**: Appelle `OnDestroy()`, puis `OnDetached()`

## Exemple complet : PlayerController

```csharp
using Engine.Scripting;
using Engine.Components;
using Engine.Input;

public class PlayerController : MonoBehaviour
{
    // Serialized fields
    [Editable] public float Speed = 5f;
    [Editable] public float JumpForce = 10f;
    
    // Internal state
    private RigidbodyComponent? _rigidbody;
    private bool _isGrounded = false;
    private bool _jumpRequested = false;
    
    public override void Awake()
    {
        base.Awake();
        // Initialize internal references
        _rigidbody = GetComponent<RigidbodyComponent>();
    }
    
    public override void OnEnable()
    {
        base.OnEnable();
        // Subscribe to input events
        InputManager.Instance.OnJumpPressed += RequestJump;
    }
    
    public override void Start()
    {
        base.Start();
        // Find external references
        var camera = Find("MainCamera");
        Console.WriteLine($"Player initialized at {Entity?.Name}");
    }
    
    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);
        // Handle movement input (framerate-dependent)
        float horizontal = InputManager.Instance.GetAxis("Horizontal");
        float vertical = InputManager.Instance.GetAxis("Vertical");
        
        var movement = new Vector3(horizontal, 0, vertical) * Speed * deltaTime;
        Entity?.Transform.Position += movement;
    }
    
    public override void FixedUpdate(float fixedDeltaTime)
    {
        base.FixedUpdate(fixedDeltaTime);
        // Handle physics (framerate-independent)
        if (_isGrounded && _jumpRequested)
        {
            _rigidbody?.AddForce(Vector3.UnitY * JumpForce, ForceMode.Impulse);
            _jumpRequested = false;
        }
    }
    
    public override void LateUpdate(float deltaTime)
    {
        base.LateUpdate(deltaTime);
        // Final adjustments (after all Update calls)
        // e.g., camera following
    }
    
    public override void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);
        if (collision.Entity.Name == "Ground")
            _isGrounded = true;
    }
    
    public override void OnCollisionExit(Collision collision)
    {
        base.OnCollisionExit(collision);
        if (collision.Entity.Name == "Ground")
            _isGrounded = false;
    }
    
    public override void OnDisable()
    {
        base.OnDisable();
        // Unsubscribe from events
        InputManager.Instance.OnJumpPressed -= RequestJump;
    }
    
    public override void OnDestroy()
    {
        base.OnDestroy();
        // Final cleanup
        Console.WriteLine("Player destroyed");
    }
    
    private void RequestJump()
    {
        _jumpRequested = true;
    }
}
```

## Diagramme de flux

```
Scene Load
    ↓
Awake()           // Initialize internal state
    ↓
OnEnable()        // Subscribe to events
    ↓
Start()           // Initialize with external dependencies
    ↓
┌─────────────────────────────────┐
│  Game Loop                      │
│                                 │
│  FixedUpdate() (fixed timestep) │  ← Physics
│         ↓                       │
│  Update() (every frame)         │  ← Gameplay/Input
│         ↓                       │
│  LateUpdate() (every frame)     │  ← Camera/Final adjustments
│         ↓                       │
│  (back to FixedUpdate)          │
└─────────────────────────────────┘
    ↓
OnDisable()       // Unsubscribe from events
    ↓
OnDestroy()       // Final cleanup
    ↓
Component Removed
```

## Notes importantes

1. **OnDestroy() est maintenant appelé** :
   - Quand un component est retiré avec `RemoveComponent<T>()`
   - Quand Play Mode s'arrête (tous les components de la scène)
   - Avant `OnDetached()`

2. **Ordre d'appel garantis** :
   - `Awake()` → `OnEnable()` → `Start()` (initialisation)
   - `OnDisable()` → `OnDestroy()` → `OnDetached()` (destruction)

3. **Base calls** :
   - Toujours appeler `base.Method()` en premier
   - Permet aux futurs héritages de fonctionner correctement

4. **Performance** :
   - `Update()` est appelée chaque frame → éviter les allocations
   - `FixedUpdate()` pour la physique uniquement
   - `LateUpdate()` pour les ajustements finaux (camera follow)
