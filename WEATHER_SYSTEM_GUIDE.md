# Weather System Guide - AstrildApex Engine

## 🌦️ Overview

The Weather System provides a comprehensive, artist-friendly solution for controlling environmental weather conditions in your scenes. It's designed with a clean separation between data (WeatherComponent), logic (WeatherSystem), and rendering, making it both powerful and easy to use.

## Architecture

### Components

```
Engine.Components.WeatherComponent (ECS Component)
    ↓
Engine.Systems.WeatherSystem (ECS Update System)
    ↓
Engine.Systems.WeatherManager (Global Singleton)
    ↓
Rendering Systems (ViewportRenderer, GameRenderer, Shaders)
```

### Key Features

- ✨ **9 Built-in Presets**: Clear, Windy, Light Rain, Heavy Rain, Storm, Light Snow, Heavy Snow, Blizzard, Foggy
- 🎨 **Smooth Transitions**: Interpolate between weather states over time
- 🌊 **Automatic Surface Effects**: Wetness and snow accumulation/melting
- 🎮 **Runtime Control**: Modify weather in Edit Mode or Play Mode
- 🔄 **Auto-Transitions**: Optional automatic weather changes
- 🎯 **Global System**: One weather component controls the entire scene

## Quick Start

### 1. Add Weather to Your Scene

In the Editor:
1. Create a new Entity (or use existing)
2. Add Component → **WeatherComponent**
3. The weather is now active!

### 2. Apply a Weather Preset

In the Inspector:
1. Select a preset from the dropdown (e.g., "Heavy Rain")
2. Click **"Apply Instant"** for immediate change
3. Or click **"Transition"** for smooth 10-second transition

### 3. Customize Parameters

Expand sections to fine-tune:
- **Wind**: Strength, direction, speed, gustiness
- **Rain**: Intensity, wetness effects
- **Snow**: Intensity, coverage, accumulation
- **Fog**: Density and color (future feature)

## Weather Parameters

### Wind Parameters

```csharp
WindStrength (0.0 - 1.0)      // Overall wind intensity
WindDirectionX/Z              // Normalized wind direction vector
WindSpeed (0.1 - 3.0)         // Animation speed multiplier
WindGustiness (0.0 - 1.0)     // 0=smooth, 1=turbulent
```

**Effect**: Vegetation bends and sways based on wind parameters. Top vertices move more than bottom (realistic tree/grass motion).

### Rain Parameters

```csharp
RainIntensity (0.0 - 1.0)     // 0=no rain, 1=heavy rain
Wetness (0.0 - 1.0)           // Auto-updated surface wetness
RainWetnessSpeed              // How fast surfaces get wet
RainDryingSpeed               // How fast surfaces dry
```

**Effect**: 
- Vegetation droops slightly when raining
- Surfaces become shinier (wetness affects smoothness/reflections)
- Wetness automatically increases during rain and decreases when stopped

### Snow Parameters

```csharp
SnowIntensity (0.0 - 1.0)     // 0=no snow, 1=heavy snowfall
SnowCoverage (0.0 - 1.0)      // Ground snow coverage
SnowAccumulationSpeed         // How fast snow accumulates
SnowMeltSpeed                 // How fast snow melts
```

**Effect**:
- Snow coverage gradually increases during snowfall
- Snow melts when snowfall stops
- Snow layer rendered on top surfaces (terrain, objects)

### Fog Parameters (Future)

```csharp
FogDensity (0.0 - 0.5)        // Atmospheric fog density
FogColor (RGB)                 // Fog tint color
```

**Status**: Parameters exist but fog rendering not yet implemented.

## Using Presets

### Built-in Presets

| Preset | Wind | Rain | Snow | Description |
|--------|------|------|------|-------------|
| **Clear** | 0.1 | 0.0 | 0.0 | Calm, sunny weather |
| **Windy** | 0.6 | 0.0 | 0.0 | Strong gusty wind |
| **Light Rain** | 0.3 | 0.3 | 0.0 | Drizzle with some wind |
| **Heavy Rain** | 0.5 | 0.8 | 0.0 | Downpour with strong wind |
| **Storm** | 0.9 | 1.0 | 0.0 | Violent storm |
| **Light Snow** | 0.2 | 0.0 | 0.3 | Gentle snowfall |
| **Heavy Snow** | 0.4 | 0.0 | 0.8 | Heavy snowstorm |
| **Blizzard** | 0.8 | 0.0 | 1.0 | Extreme snowstorm |
| **Foggy** | 0.05 | 0.0 | 0.0 | Dense fog, low visibility |

### Applying Presets

**Instant Application**:
```csharp
weatherComponent.ApplyPreset(WeatherPreset.Storm);
```

**Smooth Transition** (10 seconds default):
```csharp
weatherComponent.TransitionToPreset(WeatherPreset.HeavyRain);
```

**Custom Transition Duration**:
```csharp
weatherComponent.TransitionToPreset(WeatherPreset.Clear, customDuration: 5.0f);
```

## Scripting API

### Accessing Weather from Code

```csharp
// Get current weather state (thread-safe)
var weather = Engine.Systems.WeatherManager.GetCurrentWeather();

// Use weather data
float windStrength = weather.WindStrength;
Vector2 windDir = weather.GetWindDirection();
float wetness = weather.Wetness;
```

### Finding Weather Component

```csharp
// In your component's Update() or Start()
var weatherEntity = Owner.Scene.Entities
    .FirstOrDefault(e => e.GetComponent<Engine.Components.WeatherComponent>() != null);

if (weatherEntity != null)
{
    var weather = weatherEntity.GetComponent<Engine.Components.WeatherComponent>();
    // Modify weather...
}
```

### Creating Dynamic Weather

```csharp
public class DynamicWeatherController : Component
{
    private float _timer = 0.0f;
    
    public override void Update(float deltaTime)
    {
        _timer += deltaTime;
        
        // Change weather every 60 seconds
        if (_timer >= 60.0f)
        {
            _timer = 0.0f;
            
            var weather = Owner.GetComponent<WeatherComponent>();
            if (weather != null)
            {
                var presets = WeatherPreset.GetAllPresets();
                var random = new Random();
                var randomPreset = presets[random.Next(presets.Length)];
                
                weather.TransitionToPreset(randomPreset, customDuration: 15.0f);
            }
        }
    }
}
```

## Technical Details

### System Update Order

1. **WeatherSystem.Update()** (Play Mode only)
   - Updates weather transitions
   - Updates wetness based on rain
   - Updates snow coverage based on snowfall
   - Updates WeatherManager with current state

2. **WeatherManager** (Global Singleton)
   - Provides thread-safe access to current weather
   - Used by rendering systems

3. **Rendering Systems**
   - ViewportRenderer and GameRenderer query WeatherManager
   - Pass weather uniforms to vegetation shaders
   - Shaders apply wind/weather effects to vertices

### Shader Integration

The weather parameters are automatically sent to vegetation shaders:

**Vertex Shader Uniforms**:
```glsl
uniform float u_WindStrength;
uniform vec2 u_WindDirection;
uniform float u_WindSpeed;
uniform float u_WindGustiness;
uniform float u_RainIntensity;
uniform float u_SnowCoverage;
```

**Fragment Shader Uniforms**:
```glsl
uniform float u_Wetness;
```

## Best Practices

### Performance

- ✅ Only one WeatherComponent per scene
- ✅ Weather system is very lightweight (runs once per frame in Play Mode)
- ✅ No performance impact in Edit Mode
- ✅ WeatherManager uses thread-safe singleton pattern

### Workflow Tips

1. **Start with Presets**: Use built-in presets as starting points
2. **Test Transitions**: Always test weather transitions in Play Mode
3. **Adjust Advanced Settings**: Fine-tune wetness/accumulation speeds for your game's timescale
4. **Scene-Specific Weather**: Different scenes can have different default weather

### Common Patterns

**Dawn to Storm Progression**:
```csharp
// Start with clear weather
weather.ApplyPreset(WeatherPreset.Clear);

// After 30 seconds, transition to cloudy
await Task.Delay(30000);
weather.TransitionToPreset(WeatherPreset.Windy, 20.0f);

// After 60 seconds, transition to rain
await Task.Delay(60000);
weather.TransitionToPreset(WeatherPreset.HeavyRain, 30.0f);

// After 120 seconds, storm arrives
await Task.Delay(120000);
weather.TransitionToPreset(WeatherPreset.Storm, 15.0f);
```

**Automatic Day/Night Weather**:
Enable **"Enable Auto Transitions"** in the Inspector for automatic random weather changes every ~2 minutes.

## Future Features

### Planned Additions

- [ ] **Fog Rendering**: Volumetric fog with depth-based density
- [ ] **Puddle System**: Dynamic water puddles on terrain
- [ ] **Weather Zones**: Multiple weather areas in one scene
- [ ] **Particle Effects**: Rain/snow particle systems
- [ ] **Lightning**: Storm lightning flashes
- [ ] **Wind Audio**: Wind sound intensity tied to wind strength
- [ ] **Weather Events**: Scriptable weather event triggers

### Shader Features (Future)

- [ ] **Wetness Maps**: Per-material wetness response
- [ ] **Snow Accumulation Maps**: Control where snow accumulates
- [ ] **Wind Noise Texture**: More varied wind patterns
- [ ] **Drip Effects**: Water dripping from surfaces

## Troubleshooting

### Weather Not Visible

1. Ensure you're in **Play Mode** (weather only updates in Play Mode)
2. Check that vegetation uses **VegetationForward** shader
3. Verify WeatherComponent is on an active entity
4. Check wind strength > 0 for wind effects

### Transitions Too Fast/Slow

Adjust **Transition Speed** in "Transition Settings" section:
- `0.1` = Very slow transitions (100 seconds)
- `1.0` = Normal speed (10 seconds)
- `5.0` = Very fast transitions (2 seconds)

### Wetness Not Updating

- Wetness auto-updates based on RainIntensity
- Increase **Rain Wetness Speed** for faster wet surfaces
- Wetness only visible on materials that support it (future feature)

### Wind Direction Not Working

- Wind direction is in **degrees**: 0° = East, 90° = North, 180° = West, 270° = South
- Ensure vegetation entities are using the correct shader
- Check Wind Strength > 0

## Migration from Old System

The weather parameters were previously on the Terrain component. They have been moved to the new WeatherComponent:

**Old Way** (Deprecated):
```csharp
terrain.WindStrength = 0.5f;
terrain.RainIntensity = 0.8f;
```

**New Way**:
```csharp
weatherComponent.WindStrength = 0.5f;
weatherComponent.RainIntensity = 0.8f;
```

Old terrain files will load correctly, but weather parameters will be ignored. Add a WeatherComponent to control weather.

## Credits

Weather System designed and implemented for AstrildApex Engine.
- Component-based architecture inspired by Unity
- Smooth transitions inspired by Unreal Engine's weather system
- Preset system designed for artist-friendly workflow

---

**Version**: 1.0  
**Last Updated**: December 2024  
**Engine Version**: AstrildApex 0.4.0+
