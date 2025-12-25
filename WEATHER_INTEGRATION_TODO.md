# Weather System Integration - Remaining Tasks

## ✅ Completed
1. WeatherComponent avec fog complet (FogEnabled, FogStart, FogEnd, FogDensity, FogColor)
2. Material references (WetnessMapMaterial, SnowMapMaterial) dans WeatherComponent
3. Particle System references (RainParticleSystem, SnowParticleSystem) dans WeatherComponent
4. WeatherInspector UI mis à jour avec fog, materials, et particle systems
5. EnvironmentPanel fog section remplacée par message de migration
6. WeatherSystem contrôle maintenant les ParticleSystems automatiquement
7. WeatherState/WeatherManager mis à jour avec fog complet
8. ForwardBase.frag: weather uniforms ajoutés + logique snow/wetness
9. TerrainForward.frag: weather uniforms ajoutés + logique snow/wetness
10. VegetationForward.frag: avait déjà les weather uniforms

## 🔧 Remaining Tasks

### 1. Modifier Engine/Scene/Lighting.cs
**Fichier:** `Engine/Scene/Lighting.cs`
**Ligne:** ~50-62

Remplacer la logique fog de EnvironmentSettings par WeatherComponent:

```csharp
// OLD (lines ~56-62):
L.FogEnabled = envSettings.FogEnabled;
L.FogColor = envSettings.FogColor;
L.FogStart = envSettings.FogStart;
L.FogEnd = envSettings.FogEnd;
L.FogDensity = envSettings.FogDensity;

// NEW:
// Try to find WeatherComponent for fog settings
var weatherComp = scene.Entities
    .Select(e => e.GetComponent<Engine.Components.WeatherComponent>())
    .FirstOrDefault(w => w != null);

if (weatherComp != null)
{
    L.FogEnabled = weatherComp.FogEnabled;
    L.FogColor = weatherComp.FogColor;
    L.FogStart = weatherComp.FogStart;
    L.FogEnd = weatherComp.FogEnd;
    L.FogDensity = weatherComp.FogDensity;
}
else
{
    // Fallback: no fog
    L.FogEnabled = false;
    L.FogColor = Vector3.One;
    L.FogStart = 0.0f;
    L.FogEnd = 300.0f;
    L.FogDensity = 0.01f;
}
```

### 2. Passer weather uniforms dans ViewportRenderer.cs
**Fichiers:** `Editor/Rendering/ViewportRenderer.cs`

Chercher où ForwardBase et TerrainForward sont utilisés, et ajouter:

```csharp
// Get weather state
var weather = Engine.Systems.WeatherManager.GetCurrentWeather();

// Set weather uniforms
shader.SetFloat("u_RainIntensity", weather.RainIntensity);
shader.SetFloat("u_SnowCoverage", weather.SnowCoverage);
shader.SetFloat("u_Wetness", weather.Wetness);
```

**Locations to search:**
- Line ~1318: ForwardBase shader load
- Line ~4894: VegetationForward usage (déjà fait dans ViewportRenderer)
- Search for: `_pbrShader.Use()` ou `SetupCommonUniforms`

### 3. Passer weather uniforms dans GameRenderer.cs
**Fichier:** `Editor/Rendering/GameRenderer.cs`

Même chose que ViewportRenderer - ajouter weather uniforms aux shaders ForwardBase et TerrainForward.

### 4. Tester le système complet
1. Créer une entity avec WeatherComponent
2. Tester les presets (Storm, Blizzard, Foggy)
3. Vérifier que le fog fonctionne
4. Vérifier que snow et wetness affectent les matériaux
5. Assigner des ParticleSystems pour rain/snow
6. Tester les transitions

## 📝 Notes Importantes

- **EnvironmentSettings** garde les propriétés fog pour backward compatibility mais elles ne sont plus utilisées
- Le fog est maintenant contrôlé par **WeatherComponent** uniquement
- Les shaders reçoivent le fog via **LightingState UBO** (pas de changement côté shader pour fog)
- Les weather uniforms (rain/snow/wetness) sont nouveaux et doivent être setés explicitement

## 🎯 Architecture Finale

```
Scene
├── EnvironmentSettings (skybox, ambient, time of day)
│   └── Fog properties deprecated (kept for compatibility)
│
└── WeatherComponent (NEW)
    ├── Wind (vegetation)
    ├── Rain/Snow (intensities)
    ├── Wetness/Coverage (auto-accumulated)
    ├── Fog (enabled, color, density, start, end)
    ├── Materials (wetness map, snow map)
    └── ParticleSystems (rain, snow)

WeatherSystem
├── Updates wetness/coverage
├── Controls ParticleSystems
└── Updates WeatherManager singleton

WeatherManager (Global Singleton)
└── Provides WeatherState to renderers

Renderers (ViewportRenderer, GameRenderer)
├── Get WeatherState from WeatherManager
├── Set weather uniforms on shaders
└── Lighting.Build() uses WeatherComponent for fog in LightingState UBO
```

## 🔍 Testing Checklist

- [ ] Fog enabled/disabled works
- [ ] Fog color changes in real-time
- [ ] Fog density/start/end affect visuals
- [ ] Snow coverage appears on horizontal surfaces
- [ ] Wetness darkens and increases smoothness
- [ ] Rain ParticleSystem activates when RainIntensity > 0
- [ ] Snow ParticleSystem activates when SnowIntensity > 0
- [ ] Weather presets apply correctly
- [ ] Transitions are smooth
- [ ] ForwardBase objects get weather effects
- [ ] TerrainForward terrain gets weather effects
- [ ] VegetationForward vegetation sways with wind
