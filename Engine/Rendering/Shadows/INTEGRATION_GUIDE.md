# Guide d'intégration du nouveau système d'ombres

## Modifications à faire dans `ViewportRenderer.cs`

### Étape 1: Remplacer les variables de shadow (lignes ~606-615)

**ANCIEN CODE** :
```csharp
private Engine.Rendering.Shadows.ShadowManager? _shadowManager = null;
private int _shadowFbo = 0;
private int _shadowProg = 0; // simple depth-only shader program
private int _shadowColorProg = 0;
private int _shadowOverlayProg = 0;
private int _shadowDebugColorTex = 0;
private int _shadowDebugColorTexSize = 0;
```

**NOUVEAU CODE** :
```csharp
// === NEW Modern Shadow System ===
private Engine.Rendering.Shadows.ShadowManager? _shadowManager = null;
private Engine.Rendering.ShaderProgram? _shadowDepthShader = null;
```

### Étape 2: Charger le shadow shader (lignes ~1006-1030)

**ANCIEN CODE** (créait le shader inline) - SUPPRIMER TOUT ÇA

**NOUVEAU CODE** :
```csharp
// --- Modern Shadow System Init ---
try
{
    // Initialize shadow manager with settings from editor
    var shadowSettings = Editor.State.EditorSettings.ShadowsSettings;
    _shadowManager = new Engine.Rendering.Shadows.ShadowManager(shadowSettings.ShadowMapSize);

    // Load shadow depth shader from files
    _shadowDepthShader = Engine.Rendering.ShaderProgram.FromFiles(
        "Engine/Rendering/Shaders/Shadow/ShadowDepth.vert",
        "Engine/Rendering/Shaders/Shadow/ShadowDepth.frag"
    );

    Console.WriteLine("[ViewportRenderer] Shadow system initialized successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"[ViewportRenderer] Failed to initialize shadows: {ex.Message}");
    _shadowManager = null;
    _shadowDepthShader = null;
}
```

### Étape 3: Créer une fonction RenderShadowPass() (ajouter après ligne ~2400)

```csharp
/// <summary>
/// NEW: Modern shadow rendering using single directional light shadow map
/// </summary>
private void RenderShadowPass()
{
    var shadowSettings = Editor.State.EditorSettings.ShadowsSettings;

    if (_shadowManager == null || _shadowDepthShader == null || !shadowSettings.Enabled)
        return;

    // Get directional light direction
    Vector3 lightDir = _globalUniforms.DirLightDirection;
    if (lightDir.Length < 0.01f)
        lightDir = new Vector3(0.5f, -1.0f, 0.3f); // Default light direction

    // Calculate scene bounds (adjust based on your scene)
    Vector3 sceneCenter = CameraPosition(); // Use camera position as scene center
    float sceneRadius = shadowSettings.ShadowDistance; // From settings

    // Calculate light-space matrix
    _shadowManager.CalculateLightMatrix(lightDir, sceneCenter, sceneRadius);

    // Begin shadow pass
    _shadowManager.BeginShadowPass();

    // Use shadow depth shader
    _shadowDepthShader.Use();
    _shadowDepthShader.SetMat4("u_LightSpaceMatrix", _shadowManager.LightSpaceMatrix);

    // Render all shadow casters
    RenderShadowCasters();

    // End shadow pass
    _shadowManager.EndShadowPass();
}

/// <summary>
/// Render all objects that cast shadows
/// </summary>
private void RenderShadowCasters()
{
    if (_scene == null || _shadowDepthShader == null) return;

    // Render terrain
    foreach (var entity in _scene.Entities)
    {
        if (entity.HasComponent<Engine.Components.Terrain>())
        {
            try
            {
                var terrain = entity.GetComponent<Engine.Components.Terrain>();
                entity.GetModelAndNormalMatrix(out var model, out _);
                _shadowDepthShader.SetMat4("u_Model", model);
                terrain.Render(new System.Numerics.Vector3(0, 0, 0)); // Render to shadow map
            }
            catch { }
        }
    }

    // Render regular objects with mesh filter
    foreach (var entity in _scene.Entities)
    {
        if (!entity.HasComponent<Engine.Components.MeshFilterComponent>())
            continue;

        var meshFilter = entity.GetComponent<Engine.Components.MeshFilterComponent>();
        if (meshFilter.MeshGuid == Guid.Empty)
            continue;

        // Get mesh from asset database
        if (!Engine.Assets.AssetDatabase.TryGet(meshFilter.MeshGuid, out var meshRecord))
            continue;

        var mesh = meshRecord.Asset as Engine.Assets.MeshAsset;
        if (mesh == null)
            continue;

        // Get model matrix
        entity.GetModelAndNormalMatrix(out var model, out _);
        _shadowDepthShader.SetMat4("u_Model", model);

        // Render mesh
        try
        {
            GL.BindVertexArray(mesh.VAO);
            GL.DrawElements(PrimitiveType.Triangles, mesh.IndexCount, DrawElementsType.UnsignedInt, 0);
        }
        catch { }
    }

    GL.BindVertexArray(0);
}
```

### Étape 4: Appeler RenderShadowPass() dans la boucle de rendu (ligne ~4150)

**Trouver** : La fonction principale de rendu (cherchez `public void Render(`)

**AJOUTER AVANT** le rendu principal :
```csharp
// === SHADOW PASS (render before main scene) ===
RenderShadowPass();

// Restore viewport and framebuffer for main pass
GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
GL.Viewport(0, 0, _w, _h);
```

### Étape 5: Configurer les uniforms shadow dans le PBR shader (ligne ~4150-4200)

**Trouver** : Là où le PBR shader est activé avant le rendu

**AJOUTER** :
```csharp
// === Configure Shadow Uniforms ===
var shadowSettings = Editor.State.EditorSettings.ShadowsSettings;

if (shadowSettings.Enabled && _shadowManager != null)
{
    // Bind shadow texture
    _shadowManager.BindShadowTexture(TextureUnit.Texture5);

    // Set shadow uniforms
    _pbrShader.SetInt("u_ShadowMap", 5);
    _pbrShader.SetMat4("u_ShadowMatrix", _shadowManager.LightSpaceMatrix);
    _pbrShader.SetInt("u_UseShadows", 1);
    _pbrShader.SetInt("u_ShadowQuality", shadowSettings.ShadowQuality);
    _pbrShader.SetFloat("u_ShadowMapSize", (float)_shadowManager.ShadowMapSize);
    _pbrShader.SetFloat("u_ShadowBias", shadowSettings.ShadowBias);
    _pbrShader.SetFloat("u_ShadowNormalBias", shadowSettings.ShadowNormalBias);
    _pbrShader.SetInt("u_PCFSamples", shadowSettings.PCFSamples);
    _pbrShader.SetFloat("u_LightSize", shadowSettings.LightSize);
}
else
{
    _pbrShader.SetInt("u_UseShadows", 0);
}
```

### Étape 6: Cleanup (dans la fonction Dispose)

**Trouver** : La fonction `Dispose()`

**AJOUTER** :
```csharp
_shadowManager?.Dispose();
_shadowDepthShader?.Dispose();
```

## Ordre de rendu final

Votre boucle de rendu devrait ressembler à :

```csharp
public void Render(Scene scene, ...)
{
    // 1. SSAO Pass (si activé)
    if (SSAOSettings.Enabled)
    {
        RenderSSAOPasses(...);
    }

    // 2. Shadow Pass (NOUVEAU)
    RenderShadowPass();

    // 3. Restore main framebuffer
    GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
    GL.Viewport(0, 0, _w, _h);

    // 4. Clear and setup
    GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

    // 5. Configure PBR shader with shadows
    _pbrShader.Use();
    ConfigureShadowUniforms(); // Code de l'étape 5

    // 6. Render scene normally
    RenderSkybox();
    RenderTerrain();
    RenderObjects();
    RenderWater();
    // ...
}
```

## Vérification

Après implémentation, vérifiez :

1. ✅ Les shaders `ShadowDepth.vert` et `.frag` sont trouvés
2. ✅ `_shadowManager` et `_shadowDepthShader` sont non-null
3. ✅ La shadow pass s'exécute AVANT le rendu principal
4. ✅ Les uniforms shadow sont bien passés au PBR shader
5. ✅ Pas d'erreurs OpenGL (vérifier `GL.GetError()`)

## Debug

Si les ombres ne fonctionnent pas :

1. Activez `DebugShowShadowMap` dans Rendering Settings
2. Vérifiez que `u_UseShadows` est bien à 1
3. Vérifiez que la lumière directionnelle existe
4. Vérifiez `sceneRadius` (commencez avec 50.0f)
5. Regardez les logs console pour les erreurs de shader

## Nettoyage de l'ancien code

Une fois que le nouveau système fonctionne, vous pouvez supprimer :

- `_shadowFbo`
- `_shadowProg`, `_shadowColorProg`, `_shadowOverlayProg`
- `_shadowDebugColorTex`, `_shadowDebugColorTexSize`
- La fonction `RenderCascadedShadowMaps()` (l'ancienne)
- Toutes les références à CSM (Cascaded Shadow Maps)

Le nouveau système est beaucoup plus simple et propre !
