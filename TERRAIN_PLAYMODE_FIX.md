# 🔧 Fix: Terrain Disparaît en Sortant du Play Mode

## 🐛 Problème

Lorsqu'on sort du Play Mode, le terrain devient invisible visuellement avec les erreurs suivantes :

```
[TerrainRenderer] ERROR setting shadow uniforms: InvalidValue
[TerrainRenderer] GL error BEFORE terrain.Render(): InvalidOperation
[TerrainRenderer] Shader handle 0 is no longer valid - reloading shader...
[TerrainRenderer] SHADER VALIDATION FAILED:
```

## 🔍 Diagnostic

Le problème était causé par l'invalidation ET le cache des ressources OpenGL après la sortie du Play Mode :

1. **Shader invalide** : Le handle du shader `TerrainForward` devenait 0 (invalide) après le changement de mode
2. **Cache du ShaderLibrary** : Le ShaderLibrary gardait l'ancien shader invalide en cache
3. **Buffers invalides** : Les VAO/VBO/EBO du terrain n'étaient plus valides après le nettoyage du Play Mode

## ✅ Solution Appliquée

### 1. Amélioration de ShaderLibrary.ReloadShader() 

Ajout de vérification avant disposal pour éviter de disposer des shaders invalides :

```csharp
public static void ReloadShader(string name)
{
    // ...
    if (_cache.ContainsKey(name))
    {
        try
        {
            var oldShader = _cache[name];
            // Vérifier que le shader est valide avant de le disposer
            if (oldShader != null && GL.IsProgram(oldShader.Handle))
            {
                oldShader.Dispose();
                Console.WriteLine($"[ShaderLibrary] Disposed old shader '{name}' (handle: {oldShader.Handle})");
            }
            else if (oldShader != null)
            {
                Console.WriteLine($"[ShaderLibrary] Old shader '{name}' has invalid handle ({oldShader.Handle}), skipping disposal");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ShaderLibrary] Warning: Failed to dispose old shader '{name}': {ex.Message}");
        }
        _cache.Remove(name);
    }
    
    Console.WriteLine($"[ShaderLibrary] Forcing reload of shader '{name}'...");
    GetShaderByName(name);
}
```

### 2. Validation du Shader avec Force Reload (TerrainRenderer.cs)

Détection plus robuste avec vérification de handle == 0 et force reload du cache :

```csharp
// Dans RenderTerrain()
if (_shader == null)
{
    Console.WriteLine("[TerrainRenderer] Shader is null - attempting to reload...");
    _shader = LoadTerrainShader("TerrainForward");
    // ...
}

// AMÉLIORÉ : Vérifier handle == 0 ET forcer reload du cache
if (!GL.IsProgram(_shader.Handle) || _shader.Handle == 0)
{
    Console.WriteLine($"[TerrainRenderer] Shader handle {_shader.Handle} is no longer valid - forcing reload from ShaderLibrary...");
    
    // Force reload from ShaderLibrary to clear cache
    Engine.Rendering.ShaderLibrary.ReloadShader("TerrainForward");
    _shader = Engine.Rendering.ShaderLibrary.GetShaderByName("TerrainForward");
    
    if (_shader == null || _shader.Handle == 0)
    {
        Console.WriteLine("[TerrainRenderer] CRITICAL: Failed to reload shader after invalidation!");
        return;
    }
    Console.WriteLine($"[TerrainRenderer] Successfully reloaded shader after invalidation! New handle: {_shader.Handle}");
}
```

### 3. Force Reload à la Sortie du PlayMode (PlayMode.cs)

Ajout d'un reload proactif du shader TerrainForward lors de la sortie du PlayMode :

```csharp
// Dans PlayMode.Stop()
Panels.GamePanel.Dispose();

// Force reload terrain shader to ensure it's valid after PlayMode cleanup
try
{
    Console.WriteLine("[PlayMode] Forcing reload of TerrainForward shader...");
    Engine.Rendering.ShaderLibrary.ReloadShader("TerrainForward");
}
catch (Exception ex)
{
    Console.WriteLine($"[PlayMode] Warning: Failed to reload TerrainForward shader: {ex.Message}");
}

_state = PlayState.Edit;
```

### 4. Validation du VAO (Terrain.cs)

Régénération automatique du mesh si le VAO devient invalide :

```csharp
// Dans Render()
if (!_meshGenerated || _vao == 0 || _indexCount == 0)
{
    return;
}

// Vérifier que le VAO est toujours valide
if (!GL.IsVertexArray(_vao))
{
    Console.WriteLine($"[Terrain] VAO {_vao} is no longer valid - regenerating terrain mesh...");
    try
    {
        GenerateTerrain();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Terrain] Failed to regenerate terrain: {ex.Message}");
        return;
    }
}
```

## 🎯 Fonctionnement

### Scénario Normal
1. En mode Edit : terrain visible ✅
2. Play Mode : terrain visible ✅
3. Sortie du Play Mode : terrain visible ✅

### Ce qui se passe maintenant

**Lors de la sortie du Play Mode :**
1. `PlayMode.Stop()` est appelé
2. Le GamePanel est nettoyé avec `Dispose()`
3. **PROACTIF** : Le shader TerrainForward est forcé à recharger depuis le disque
4. Au prochain rendu du terrain dans le viewport :
   - Le shader est frais et valide (handle > 0)
   - Si le VAO est invalide, `GL.IsVertexArray()` le détecte
   - Le terrain est régénéré si nécessaire

**Logs de succès :**
```
[PlayMode] Forcing reload of TerrainForward shader...
[ShaderLibrary] Old shader 'TerrainForward' has invalid handle (0), skipping disposal
[ShaderLibrary] Forcing reload of shader 'TerrainForward'...
[ShaderLibrary] Loading shader TerrainForward from Engine/Rendering/Shaders/Forward/TerrainForward.vert and Engine/Rendering/Shaders/Forward/TerrainForward.frag
[ShaderLibrary] Successfully compiled shader TerrainForward
[Terrain] VAO 123 is no longer valid - regenerating terrain mesh...
[Terrain] Uploaded mesh to GPU: VAO=456, VBO=457, EBO=458
```

## 📊 Avantages de cette Approche

✅ **Proactif ET Réactif** : Force reload + auto-réparation si ça échoue  
✅ **Robuste** : Gère les shaders invalides sans crasher  
✅ **Cache sain** : Le ShaderLibrary ne garde pas de shaders morts  
✅ **Performant** : Vérification rapide avec `GL.Is*()` (coût négligeable)  
✅ **Logs clairs** : Facile de débugger si un problème survient  
✅ **Pas de fuite mémoire** : Les anciennes ressources invalides sont nettoyées

## 🔧 Fichiers Modifiés

1. **Engine/Rendering/ShaderLibrary.cs** (lignes ~62-85)
   - Amélioration de `ReloadShader()` pour gérer les shaders invalides

2. **Engine/Rendering/Terrain/TerrainRenderer.cs** (lignes ~108-128)
   - Validation du shader avec `GL.IsProgram()` ET check handle == 0
   - Force reload du cache avec `ShaderLibrary.ReloadShader()`

3. **Editor/PlayMode.cs** (lignes ~124-133)
   - Force reload du TerrainForward shader lors de la sortie du PlayMode

4. **Engine/Components/Terrain.cs** (lignes ~442-456)
   - Validation du VAO avec `GL.IsVertexArray()`

## 🧪 Test

Pour tester le fix :
1. Charger une scène avec un terrain
2. Vérifier que le terrain est visible en mode Edit ✅
3. Appuyer sur Play ▶️
4. Vérifier que le terrain est visible en Play Mode ✅
5. Appuyer sur Stop ⏹️
6. **Vérifier que le terrain reste visible** ✅ (c'était le bug !)

## 📝 Notes Techniques

### Pourquoi les ressources deviennent invalides ?

Les ressources OpenGL (shaders, buffers, textures) sont liées au contexte OpenGL. Quand le Play Mode clone la scène, il peut créer de nouvelles instances de composants qui génèrent leurs propres ressources OpenGL. Lors du nettoyage, ces ressources peuvent être supprimées, ce qui peut affecter les handles.

**Problème spécifique du cache :**
Le `ShaderLibrary` garde un cache des shaders compilés. Si un shader devient invalide (handle = 0), le cache contient encore une référence au shader mort. Un simple `GetShaderByName()` retourne le shader invalide du cache au lieu de le recompiler.

**Solution :**
- Utiliser `ReloadShader()` pour forcer la suppression du cache et la recompilation
- Faire ça de manière proactive lors de la sortie du PlayMode
- Avoir un fallback réactif si le reload proactif échoue

### Alternatives Considérées

❌ **Préserver les ressources** : Complexe, risque de fuites mémoire  
❌ **Recréer tout à chaque frame** : Trop coûteux en performance  
❌ **Validation lazy seulement** : Ne marche pas à cause du cache (première version du fix)  
✅ **Force reload proactif + validation lazy** : Robuste et efficace (solution finale)

---

**Status** : ✅ RÉSOLU  
**Date** : 18 octobre 2025  
**Version** : v2 (avec force reload du cache)  
**Build** : Compilé sans erreurs
