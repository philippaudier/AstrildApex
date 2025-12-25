# Time Class - Documentation

## 📋 Vue d'ensemble

La classe **Time** fournit des informations temporelles essentielles pour le moteur de jeu AstrildApex. Elle est inspirée de Unity mais adaptée pour notre architecture.

## 🎯 API Principale

### Temps de frame

```csharp
// Frame time (variable framerate) - utiliser dans Update()
float dt = Time.DeltaTime;
transform.Position += velocity * dt;

// Physics timestep (fixed 60Hz) - utiliser dans FixedUpdate()
float fixedDt = Time.FixedDeltaTime; // Toujours 0.01666s
velocity += force * fixedDt;
```

### Temps total écoulé

```csharp
// Temps total depuis le début du jeu (affecté par TimeScale)
float totalTime = Time.TimeValue;

// Temps réel (NON affecté par TimeScale)
float realTime = Time.UnscaledTime;
```

### DeltaTime variations

```csharp
// DeltaTime normal (affecté par TimeScale)
float dt = Time.DeltaTime;

// DeltaTime réel (NON affecté par TimeScale)
float realDt = Time.UnscaledDeltaTime;

// DeltaTime lissé (réduit les pics/jitter)
float smoothDt = Time.SmoothDeltaTime; // Parfait pour la caméra
```

## ⚙️ Time Scale

Contrôle la vitesse du jeu :

```csharp
// Pause
Time.Pause(); // ou Time.TimeScale = 0f;

// Resume
Time.Resume(); // ou Time.TimeScale = 1f;

// Slow motion (ralenti)
Time.SetSlowMotion(0.5f); // 50% de la vitesse normale

// Fast forward (accéléré)
Time.SetFastForward(2.0f); // 200% de la vitesse normale

// Custom
Time.TimeScale = 0.3f; // 30% de la vitesse

// Vérifier si pausé
if (Time.IsPaused)
{
    // Afficher menu pause
}
```

**Important** : TimeScale affecte :
- ✅ `Time.DeltaTime`
- ✅ `Time.TimeValue`
- ✅ `Time.SmoothDeltaTime`

TimeScale N'affecte PAS :
- ❌ `Time.UnscaledDeltaTime`
- ❌ `Time.UnscaledTime`
- ❌ `Time.FixedDeltaTime`

## 📊 Compteurs de frames

```csharp
// Nombre total de frames rendues
int frameCount = Time.FrameCount;

// Nombre total de frames physiques (FixedUpdate)
int physicsFrames = Time.FixedFrameCount;

// Utile pour synchroniser des animations
if (Time.FrameCount % 60 == 0)
{
    // Toutes les 60 frames (~1 seconde à 60 FPS)
}
```

## 📈 Métriques de performance

```csharp
// FPS actuel (lissé sur ~1 seconde)
float fps = Time.FPS;
Debug.Log($"Running at {fps:F1} FPS");

// Delta time maximum enregistré (dernière seconde)
float maxDt = Time.MaxDeltaTime;
if (maxDt > 0.033f) // > 30 FPS
{
    Debug.Log($"Performance warning: Max frame time {maxDt * 1000:F1}ms");
}
```

## 💡 Exemples d'utilisation

### Mouvement frame-rate independent

```csharp
public override void Update(float dt)
{
    // ❌ MAUVAIS : dépend du framerate
    position += velocity;
    
    // ✅ BON : frame-rate independent
    position += velocity * Time.DeltaTime;
}
```

### Animation avec TimeScale

```csharp
public class AnimatedObject : MonoBehaviour
{
    public override void Update(float dt)
    {
        // Animation affectée par TimeScale (pause, slow-mo)
        angle += rotationSpeed * Time.DeltaTime;
        
        // Animation NON affectée par TimeScale (UI, particules système)
        uiAngle += uiRotationSpeed * Time.UnscaledDeltaTime;
    }
}
```

### Smooth camera movement

```csharp
public class SmoothCamera : MonoBehaviour
{
    public override void LateUpdate(float dt)
    {
        // Utiliser SmoothDeltaTime pour réduire le jitter
        Vector3 targetPosition = player.Position;
        transform.Position = Vector3.Lerp(
            transform.Position, 
            targetPosition, 
            smoothSpeed * Time.SmoothDeltaTime
        );
    }
}
```

### Timer avec TimeScale

```csharp
public class Timer
{
    private float _timeRemaining;
    
    public void Update()
    {
        // Timer affecté par TimeScale (pause fonctionnera)
        _timeRemaining -= Time.DeltaTime;
        
        if (_timeRemaining <= 0f)
        {
            OnTimerExpired();
        }
    }
}
```

### Timer ignorant TimeScale

```csharp
public class RealTimeTimer
{
    private float _timeRemaining;
    
    public void Update()
    {
        // Timer réel (pause n'affecte PAS)
        _timeRemaining -= Time.UnscaledDeltaTime;
        
        if (_timeRemaining <= 0f)
        {
            OnTimerExpired();
        }
    }
}
```

### Bullet time effect

```csharp
public class BulletTime
{
    private float _normalTimeScale = 1f;
    private float _bulletTimeScale = 0.2f;
    
    public void EnableBulletTime()
    {
        Time.TimeScale = _bulletTimeScale;
    }
    
    public void DisableBulletTime()
    {
        Time.TimeScale = _normalTimeScale;
    }
    
    // Transition smooth
    public void TransitionToBulletTime(float duration)
    {
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.UnscaledDeltaTime; // Use unscaled!
            float t = elapsed / duration;
            Time.TimeScale = Mathf.Lerp(_normalTimeScale, _bulletTimeScale, t);
        }
    }
}
```

### Debug overlay

```csharp
public class DebugOverlay : MonoBehaviour
{
    public override void Update(float dt)
    {
        // Afficher des infos de performance
        ImGui.Text($"FPS: {Time.FPS:F1}");
        ImGui.Text($"Frame: {Time.FrameCount}");
        ImGui.Text($"Time: {Time.TimeValue:F2}s");
        ImGui.Text($"TimeScale: {Time.TimeScale:F2}x");
        
        if (Time.MaxDeltaTime > 0.033f)
        {
            ImGui.TextColored(Color.Red, $"Performance warning: {Time.MaxDeltaTime * 1000:F1}ms");
        }
    }
}
```

## 🔧 Intégration moteur

La classe Time est mise à jour automatiquement par le moteur :

```csharp
// EngineUpdatePipeline.cs - appelé chaque frame
public void ExecuteFrame(float deltaTime)
{
    Time.Update(deltaTime); // ← Mis à jour ici
    // ...
}

// PhysicsManager.cs - appelé à chaque FixedUpdate
private void FixedStep(float fixedDeltaTime)
{
    Time.IncrementFixedFrameCount(); // ← Incrémenté ici
    // ...
}
```

**Vous n'avez JAMAIS besoin d'appeler ces méthodes !** C'est géré automatiquement.

## ⚠️ Pièges courants

### 1. Oublier Time.DeltaTime

```csharp
// ❌ MAUVAIS : vitesse dépend du framerate
position.X += 5f; // 5 unités/frame (300 unités/s à 60 FPS, 600 à 120 FPS !)

// ✅ BON : vitesse constante
position.X += 5f * Time.DeltaTime; // 5 unités/s peu importe le framerate
```

### 2. Utiliser DeltaTime dans FixedUpdate

```csharp
public override void FixedUpdate(float dt)
{
    // ❌ MAUVAIS : DeltaTime est variable
    velocity += force * Time.DeltaTime;
    
    // ✅ BON : FixedDeltaTime est constant
    velocity += force * Time.FixedDeltaTime;
    
    // ✅ ENCORE MIEUX : utiliser le paramètre dt
    velocity += force * dt; // dt == FixedDeltaTime
}
```

### 3. Timer avec TimeScale non désiré

```csharp
// Timer pour cooldown d'attaque
private float _attackCooldown = 0f;

public void Update()
{
    // ❌ MAUVAIS : si on met le jeu en pause, le cooldown continue !
    _attackCooldown -= Time.UnscaledDeltaTime;
    
    // ✅ BON : cooldown respecte la pause
    _attackCooldown -= Time.DeltaTime;
}
```

### 4. UI avec TimeScale

```csharp
// Menu pause animé
public void UpdatePauseMenu()
{
    // ✅ BON : UI doit bouger même en pause
    float pulse = Mathf.Sin(Time.UnscaledTime * 2f);
    pauseButton.Alpha = 0.5f + pulse * 0.5f;
}
```

## 📚 Comparaison avec Unity

| Propriété | Unity | AstrildApex | Notes |
|-----------|-------|-------------|-------|
| DeltaTime | ✅ | ✅ | Identique |
| FixedDeltaTime | ✅ | ✅ | Identique |
| Time | ✅ Time.time | ✅ Time.TimeValue | Nom différent |
| UnscaledTime | ✅ | ✅ | Identique |
| UnscaledDeltaTime | ✅ | ✅ | Identique |
| SmoothDeltaTime | ✅ | ✅ | Identique |
| TimeScale | ✅ | ✅ | Identique |
| FrameCount | ✅ | ✅ | Identique |
| FixedFrameCount | ❌ | ✅ | AstrildApex only |
| FPS | ❌ | ✅ | AstrildApex only |
| MaxDeltaTime | ❌ | ✅ | AstrildApex only |

**Migration Unity → AstrildApex** : Remplacer `Time.time` par `Time.TimeValue`

## 🎯 Best Practices

1. **Update() → Time.DeltaTime**
   ```csharp
   public override void Update(float dt)
   {
       // Toujours utiliser Time.DeltaTime
       position += velocity * Time.DeltaTime;
   }
   ```

2. **FixedUpdate() → paramètre dt**
   ```csharp
   public override void FixedUpdate(float dt)
   {
       // Utiliser le paramètre dt (== Time.FixedDeltaTime)
       velocity += force * dt;
   }
   ```

3. **Smooth camera → Time.SmoothDeltaTime**
   ```csharp
   public override void LateUpdate(float dt)
   {
       // Moins de jitter
       cameraPos = Lerp(cameraPos, target, Time.SmoothDeltaTime * speed);
   }
   ```

4. **UI/Menus → Time.UnscaledDeltaTime**
   ```csharp
   public void UpdateUI()
   {
       // Fonctionne même en pause
       menuAlpha += Time.UnscaledDeltaTime * fadeSpeed;
   }
   ```

---

**Créé le** : 10 décembre 2025  
**Version** : 1.0  
**Auteur** : GitHub Copilot pour AstrildApex Engine
