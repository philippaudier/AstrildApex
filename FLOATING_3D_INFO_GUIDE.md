# Guide: Système d'Affichage d'Informations Flottantes 3D

## Vue d'ensemble

Le système permet d'afficher des informations UI (nom, barre de vie, etc.) au-dessus d'objets 3D dans le monde du jeu. Le HUD convertit automatiquement les positions 3D en coordonnées 2D écran.

## Architecture

### 1. Conversion 3D → 2D (`WorldToScreen`)

**Fichier:** `Editor/Assets/Scripts/RPGHudController.cs`

La méthode `WorldToScreen()` transforme une position 3D du monde en position 2D écran:

```csharp
private Vector2? WorldToScreen(Vector3 worldPos)
{
    // 1. Transformer la position 3D à travers les matrices view et projection
    var worldPos4 = new Vector4(worldPos, 1.0f);
    var viewPos = worldPos4 * _viewMatrix;
    var clipPos = viewPos * _projMatrix;
    
    // 2. Vérifier si derrière la caméra
    if (clipPos.W <= 0) 
        return null;
    
    // 3. Division de perspective (clip space → NDC)
    var ndc = new Vector3(
        clipPos.X / clipPos.W,
        clipPos.Y / clipPos.W,
        clipPos.Z / clipPos.W
    );
    
    // 4. Vérifier si hors de l'écran
    if (ndc.X < -1 || ndc.X > 1 || ndc.Y < -1 || ndc.Y > 1 || ndc.Z < -1 || ndc.Z > 1)
        return null;
    
    // 5. Conversion NDC → coordonnées écran
    var screenPos = new Vector2(
        (ndc.X + 1.0f) * 0.5f * _viewportSize.X + _viewportPos.X,
        (1.0f - ndc.Y) * 0.5f * _viewportSize.Y + _viewportPos.Y
    );
    
    return screenPos;
}
```

**Retour:**
- `Vector2?` : Coordonnées écran si visible, `null` si hors champ ou derrière la caméra

### 2. Matrices de Caméra

**Fichier:** `RPGHudController.cs`

```csharp
private Matrix4 _viewMatrix;   // Position/rotation de la caméra
private Matrix4 _projMatrix;   // Projection (perspective/ortho)

public void SetCameraMatrices(Matrix4 view, Matrix4 proj)
{
    _viewMatrix = view;
    _projMatrix = proj;
}
```

**Fichier:** `Editor/Panels/GamePanel.cs` (lignes 359-365)

GamePanel fournit les matrices à chaque frame:

```csharp
if (camera != null)
{
    float aspect = (float)w / (float)h;
    var viewMatrix = camera.ViewMatrix;
    var projMatrix = camera.ProjectionMatrix(aspect);
    hud.SetCameraMatrices(viewMatrix, projMatrix);
}
```

### 3. Rendu des Informations Flottantes

**Fichier:** `RPGHudController.cs` - Méthode `RenderFloatingInfo()`

```csharp
private void RenderFloatingInfo()
{
    // 1. Obtenir la scène du mode Play
    var scene = PlayMode.PlayScene;
    if (scene == null) return;
    
    // 2. Trouver l'entité joueur
    var playerEntity = scene.Entities.FirstOrDefault(e => 
    {
        var name = e.Name?.ToLower() ?? "";
        return name.Contains("player") || name.Contains("character");
    });
    
    if (playerEntity != null)
    {
        // 3. Calculer la position 3D au-dessus de la tête
        var transform = playerEntity.Transform;
        var playerWorldPos = new OpenTK.Mathematics.Vector3(
            transform.Position.X,
            transform.Position.Y + 2.5f, // 2.5 unités au-dessus
            transform.Position.Z
        );
        
        // 4. Convertir en position 2D écran
        var screenPos = WorldToScreen(playerWorldPos);
        
        if (screenPos.HasValue)
        {
            // 5. Dessiner l'UI à la position 2D
            var drawList = ImGui.GetForegroundDrawList();
            
            // Nom du joueur
            string playerName = playerEntity.Name ?? "Player";
            var nameSize = ImGui.CalcTextSize(playerName);
            Vector2 namePos = new Vector2(
                screenPos.Value.X - nameSize.X * 0.5f, 
                screenPos.Value.Y - 40
            );
            
            // Ombre du texte
            drawList.AddText(
                namePos + new Vector2(1, 1), 
                ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.8f)), 
                playerName
            );
            
            // Texte principal
            drawList.AddText(
                namePos, 
                ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 1)), 
                playerName
            );
            
            // Barre de vie (voir code complet pour détails)
            // ...
        }
    }
}
```

## Utilisation

### Afficher des infos pour une entité

```csharp
// Dans RenderFloatingInfo()
foreach (var entity in scene.Entities)
{
    // Filtrer les entités qui vous intéressent
    if (entity.HasComponent<HealthComponent>())
    {
        var worldPos = entity.Transform.Position;
        var screenPos = WorldToScreen(worldPos + new Vector3(0, 2, 0));
        
        if (screenPos.HasValue)
        {
            var drawList = ImGui.GetForegroundDrawList();
            
            // Dessiner nom, barre de vie, niveau, etc.
            drawList.AddText(screenPos.Value, 0xFFFFFFFF, entity.Name);
            
            // Barre de vie
            var health = entity.GetComponent<HealthComponent>();
            float healthPercent = health.Current / health.Max;
            
            Vector2 barPos = screenPos.Value + new Vector2(-25, 15);
            Vector2 barSize = new Vector2(50, 5);
            
            // Background
            drawList.AddRectFilled(
                barPos, 
                barPos + barSize,
                0x80000000 // Noir semi-transparent
            );
            
            // Fill
            drawList.AddRectFilled(
                barPos,
                barPos + new Vector2(barSize.X * healthPercent, barSize.Y),
                0xFF00FF00 // Vert
            );
            
            // Border
            drawList.AddRect(barPos, barPos + barSize, 0xFF000000, 0, 0, 1.5f);
        }
    }
}
```

### Exemple: Affichage de dégâts flottants

```csharp
// Classe DamageNumber à ajouter dans RPGHudController
private class DamageNumber
{
    public Vector3 WorldPosition;
    public float Value;
    public float TimeAlive;
    public Vector4 Color;
}

private List<DamageNumber> _damageNumbers = new List<DamageNumber>();

public void ShowDamage(Vector3 worldPos, float damage)
{
    _damageNumbers.Add(new DamageNumber
    {
        WorldPosition = worldPos,
        Value = damage,
        TimeAlive = 0,
        Color = new Vector4(1, 0.2f, 0.2f, 1) // Rouge
    });
}

// Dans Update()
void Update()
{
    float dt = Time.deltaTime; // Vous devrez passer deltaTime
    
    for (int i = _damageNumbers.Count - 1; i >= 0; i--)
    {
        var dmg = _damageNumbers[i];
        dmg.TimeAlive += dt;
        
        // Monter et disparaître après 1 seconde
        if (dmg.TimeAlive > 1.0f)
        {
            _damageNumbers.RemoveAt(i);
            continue;
        }
        
        // Animation: monter et fade out
        dmg.WorldPosition.Y += dt * 2.0f;
        dmg.Color.W = 1.0f - dmg.TimeAlive; // Fade alpha
    }
}

// Dans RenderFloatingInfo()
void RenderFloatingInfo()
{
    // ... code existant ...
    
    // Dessiner les nombres de dégâts
    var drawList = ImGui.GetForegroundDrawList();
    foreach (var dmg in _damageNumbers)
    {
        var screenPos = WorldToScreen(dmg.WorldPosition);
        if (screenPos.HasValue)
        {
            string text = $"-{(int)dmg.Value}";
            var textSize = ImGui.CalcTextSize(text);
            var textPos = screenPos.Value - textSize * 0.5f;
            
            // Ombre
            drawList.AddText(
                textPos + new Vector2(2, 2),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, dmg.Color.W * 0.8f)),
                text
            );
            
            // Texte
            drawList.AddText(
                textPos,
                ImGui.ColorConvertFloat4ToU32(dmg.Color),
                text
            );
        }
    }
}
```

## Optimisations

### Distance Culling

Ne dessiner que les entités proches:

```csharp
var cameraPos = /* position de la caméra */;
var distance = Vector3.Distance(cameraPos, entity.Transform.Position);

if (distance > 50.0f) // Max 50 unités
    continue;
```

### Occlusion Detection (Avancé)

Vérifier si un obstacle bloque la vue (nécessite raycast):

```csharp
var rayOrigin = cameraPos;
var rayDir = Vector3.Normalize(worldPos - cameraPos);
var rayLength = Vector3.Distance(cameraPos, worldPos);

if (Physics.Raycast(rayOrigin, rayDir, rayLength, LayerMask.Environment))
{
    // Objet occlus, ne pas dessiner ou dessiner en transparence
    continue;
}
```

### Distance-Based Fading

Réduire l'opacité avec la distance:

```csharp
float fadeDistance = 30.0f;
float maxDistance = 50.0f;
float alpha = 1.0f;

if (distance > fadeDistance)
{
    alpha = 1.0f - (distance - fadeDistance) / (maxDistance - fadeDistance);
    alpha = Math.Max(0, Math.Min(1, alpha));
}

var color = new Vector4(1, 1, 1, alpha);
```

## Points Clés

1. **`WorldToScreen()` retourne `null`** si l'objet est:
   - Derrière la caméra (W <= 0)
   - Hors de l'écran (NDC hors [-1, 1])

2. **Utiliser `GetForegroundDrawList()`** pour dessiner par-dessus tout le reste

3. **Les matrices sont mises à jour** par `GamePanel` avant chaque rendu

4. **Accès à la scène:** `PlayMode.PlayScene` (en mode Play uniquement)

5. **Pour plusieurs entités:** Boucler sur `scene.Entities` et filtrer selon vos besoins

## Exemple Complet: NPC avec Info

```csharp
private void RenderFloatingInfo()
{
    var scene = PlayMode.PlayScene;
    if (scene == null) return;
    
    var drawList = ImGui.GetForegroundDrawList();
    
    foreach (var entity in scene.Entities)
    {
        // Filtrer seulement les NPCs
        var npcComponent = entity.GetComponent<NPCComponent>();
        if (npcComponent == null) continue;
        
        // Position au-dessus de la tête
        var worldPos = entity.Transform.Position + new Vector3(0, 2.5f, 0);
        var screenPos = WorldToScreen(worldPos);
        
        if (!screenPos.HasValue) continue;
        
        // Nom du NPC
        string name = npcComponent.DisplayName ?? entity.Name;
        var nameSize = ImGui.CalcTextSize(name);
        var namePos = screenPos.Value - new Vector2(nameSize.X * 0.5f, 40);
        
        // Ombre
        drawList.AddText(
            namePos + new Vector2(1, 1),
            ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.8f)),
            name
        );
        
        // Nom (couleur selon faction)
        Vector4 nameColor = npcComponent.Faction switch
        {
            Faction.Friendly => new Vector4(0.2f, 1.0f, 0.2f, 1), // Vert
            Faction.Neutral => new Vector4(1.0f, 1.0f, 0.2f, 1),  // Jaune
            Faction.Hostile => new Vector4(1.0f, 0.2f, 0.2f, 1),  // Rouge
            _ => new Vector4(1, 1, 1, 1)
        };
        
        drawList.AddText(
            namePos,
            ImGui.ColorConvertFloat4ToU32(nameColor),
            name
        );
        
        // Niveau
        string level = $"Lv.{npcComponent.Level}";
        var levelSize = ImGui.CalcTextSize(level);
        var levelPos = screenPos.Value - new Vector2(levelSize.X * 0.5f, 25);
        
        drawList.AddText(
            levelPos,
            ImGui.ColorConvertFloat4ToU32(new Vector4(0.8f, 0.8f, 0.8f, 1)),
            level
        );
        
        // Icône de quête si applicable
        if (npcComponent.HasQuest)
        {
            var questIconPos = screenPos.Value + new Vector2(0, -55);
            drawList.AddText(questIconPos, 0xFFFFFF00, "!");
        }
    }
}
```

## Résumé

Le système de floating info 3D est maintenant complet et fonctionnel:

✅ **Conversion 3D → 2D** avec `WorldToScreen()`
✅ **Matrices de caméra** passées par `GamePanel`
✅ **Vérification de visibilité** (derrière caméra, hors écran)
✅ **Rendu par-dessus** avec `GetForegroundDrawList()`
✅ **Exemple d'implémentation** pour le joueur avec nom et barre de vie

Vous pouvez maintenant étendre ce système pour:
- Afficher des infos sur les ennemis
- Montrer des nombres de dégâts animés
- Indiquer des objectifs de quête
- Afficher des marqueurs 3D (waypoints, etc.)
