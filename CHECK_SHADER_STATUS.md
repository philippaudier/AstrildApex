# 🔍 Shader Status Checker

## Comment diagnostiquer le problème du terrain invisible

### Étape 1: Vérifier les Logs de Shader

Quand vous lancez l'éditeur, cherchez dans la console :

```
[TerrainRenderer] ...
[ShaderLibrary] ...
ERROR:
WARNING:
failed to compile
```

**Questions:**
- Y a-t-il des erreurs de compilation du shader TerrainForward ?
- Y a-t-il des warnings concernant des uniforms manquants ?

### Étape 2: Test avec Neige Désactivée

**État actuel:** La neige est désactivée dans le shader (`if (false && ...)`)

**Attendu:**
- Le terrain DOIT être visible
- Aucune neige ne devrait apparaître

**Si le terrain est VISIBLE:**
→ Le problème est dans le code de neige (fonctions calculateSnowPlacement/Sparkle)

**Si le terrain est INVISIBLE:**
→ Le problème est ailleurs (uniforms déclarés mais non initialisés, autre erreur shader)

### Étape 3: Vérifier les Uniforms Non Initialisés

Même si le code de neige est désactivé, les **uniforms sont toujours déclarés** :

```glsl
uniform float u_SnowSlopeMin;
uniform float u_SnowSlopeMax;
uniform float u_SnowSparkle;
uniform float u_SnowDisplacement;
```

Si ces uniforms ne sont PAS initialisés par le C#, le shader peut échouer.

**Solution:** Vérifier que ViewportRenderer les initialise TOUJOURS.

### Étape 4: Test Sans Uniforms Avancés

Si le terrain est toujours invisible, commentez les déclarations d'uniforms dans TerrainForward.frag :

```glsl
// COMMENTER TEMPORAIREMENT CES LIGNES
// uniform float u_SnowSlopeMin;
// uniform float u_SnowSlopeMax;
// uniform float u_SnowSparkle;
// uniform float u_SnowDisplacement;
```

Rebuild et testez. Si le terrain réapparaît → les uniforms causent le problème.

### Étape 5: Vérifier l'Ordre d'Exécution

Dans ViewportRenderer, on envoie les uniforms PUIS on appelle RenderTerrain :

```csharp
// 1. Get shader
var terrainShader = ShaderLibrary.GetShaderByName("TerrainForward");
terrainShader.Use();

// 2. Set uniforms
terrainShader.SetFloat("u_SnowSlopeMin", ...);
// ...

// 3. Call RenderTerrain (which will call shader.Use() again)
_terrainRenderer.RenderTerrain(...);
```

**Problème potentiel:** RenderTerrain() appelle `_shader.Use()` à nouveau.

**Question:** Est-ce que `shader.Use()` reset les uniforms ?
**Réponse:** Non, les uniforms persistent après Use().

Mais RenderTerrain() peut set d'autres uniforms qui écrasent les nôtres.

### Étape 6: Vérifier la Console pour GL Errors

Cherchez dans la console :

```
GL error: ...
InvalidOperation
InvalidValue
```

---

## 🛠️ Actions Immédiates

1. **Relancez l'éditeur** avec la neige désactivée
2. **Regardez la console** pour des erreurs
3. **Notez** :
   - Le terrain est-il visible ? (Oui/Non)
   - Y a-t-il des erreurs de shader ? (copier/coller)
   - Y a-t-il des GL errors ? (copier/coller)

4. **Si terrain toujours invisible**, essayez :
   - Commenter les déclarations d'uniforms avancés
   - Rebuild
   - Relancer

---

## 📋 Checklist de Diagnostic

- [ ] Éditeur relancé avec neige désactivée
- [ ] Console vérifiée (erreurs ?)
- [ ] Terrain visible ? (Oui/Non)
- [ ] Si Non : Uniforms avancés commentés ?
- [ ] Après commentaire : Terrain visible ?

---

## 🎯 Solutions Potentielles

### Solution A: Uniforms Non Initialisés

**Problème:** Uniforms déclarés mais jamais initialisés

**Fix:** Dans ViewportRenderer, TOUJOURS initialiser :

```csharp
// AVANT RenderTerrain
terrainShader.SetFloat("u_SnowSlopeMin", 0.0f);
terrainShader.SetFloat("u_SnowSlopeMax", 45.0f);
terrainShader.SetFloat("u_SnowSparkle", 0.0f);
terrainShader.SetFloat("u_SnowDisplacement", 0.0f);
```

### Solution B: Erreur dans calculateSnowPlacement()

**Problème:** Division par zéro, NaN, ou autre

**Fix:** Désactiver temporairement (déjà fait avec `if (false && ...)`)

### Solution C: Shader Ne Compile Pas

**Problème:** Erreur GLSL non détectée

**Fix:** Vérifier les logs de compilation

---

## 🔄 Plan de Rollback

Si rien ne fonctionne, revenir à la version d'avant mes modifications :

```bash
git diff HEAD Engine/Rendering/Shaders/Forward/TerrainForward.frag
git checkout HEAD -- Engine/Rendering/Shaders/Forward/TerrainForward.frag
```

Puis réintégrer la neige plus prudemment, en testant chaque étape.

---

**Attendez les résultats du test avant de continuer.**
