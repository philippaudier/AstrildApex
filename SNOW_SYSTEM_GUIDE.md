# ❄️ Snow System Guide - AstrildApex Engine

## Table of Contents
1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Creating a Snow Material](#creating-a-snow-material)
4. [Shader Implementation](#shader-implementation)
5. [Advanced Techniques](#advanced-techniques)
6. [Best Practices](#best-practices)

---

## Overview

The Snow System in AstrildApex provides realistic, physically-based snow accumulation across all surfaces in your scene. The system supports:

- **Progressive accumulation** - Snow builds up gradually based on `SnowIntensity`
- **Normal-based placement** - Snow accumulates on upward-facing surfaces with configurable slope angles
- **PBR snow materials** - Use custom textures for realistic snow appearance
- **Sparkle effects** - Simulate light reflecting off ice crystals
- **Displacement mapping** - Add 3D depth to the snow layer

---

## Architecture

### Data Flow

```
WeatherComponent
    ├─ SnowIntensity (0-1)       → Controls snowfall rate
    ├─ SnowCoverage (0-1)        → Current accumulation level
    ├─ SnowSlopeMin/Max          → Angle range where snow sticks
    ├─ SnowSparkle (0-1)         → Sparkle effect intensity
    ├─ SnowDisplacement          → Height variation
    └─ SnowMapMaterial (Guid)    → Reference to snow material asset
            ↓
    WeatherSystem (Update loop)
            ↓
    Shader Uniforms → Applied to all surfaces
```

### Key Parameters

| Parameter | Range | Description |
|-----------|-------|-------------|
| `SnowIntensity` | 0.0 - 1.0 | Snowfall intensity (0 = no snow, 1 = blizzard) |
| `SnowCoverage` | 0.0 - 1.0 | Current snow accumulation (auto-updated or manual) |
| `SnowSlopeMin` | 0° - 90° | Minimum surface angle for snow (0° = flat) |
| `SnowSlopeMax` | 0° - 90° | Maximum surface angle for snow (45° typical) |
| `SnowSparkle` | 0.0 - 1.0 | Sparkle/glitter intensity |
| `SnowDisplacement` | 0.0 - 0.1 | Height offset in world units |

---

## Creating a Snow Material

### Step 1: Gather Textures

You need at minimum:
- **Albedo** - Snow color (typically white with slight blue tint: RGB 245, 250, 255)
- **Normal Map** - Micro-surface details (bumps, ridges, ice crystals)
- **Roughness** - Surface smoothness (fresh snow = rough ~0.7, icy snow = smooth ~0.3)

Recommended texture resolution: **1024x1024** or **2048x2048** for tiling

### Step 2: Find Free Snow Textures (2025 Resources)

Free PBR snow texture sources:
1. **Polyhaven** - https://polyhaven.com/textures (CC0, highest quality)
2. **ambientCG** - https://ambientcg.com/ (CC0)
3. **Texture Haven** - https://texturehaven.com/ (CC0)
4. **FreePBR** - https://freepbr.com/ (CC0)

Search for: "snow", "ice", "frost"

### Step 3: Import Textures

1. Copy texture files to `Assets/Textures/Snow/`
2. The AssetDatabase will auto-detect them
3. Textures appear in the Assets panel

### Step 4: Create Material Asset

1. In Assets panel, right-click in a folder
2. Select "Create → Material"
3. Name it "SnowMaterial"
4. Assign textures:
   - Albedo → your snow albedo texture
   - Normal → your snow normal map
   - Metallic → 0.0 (snow is non-metallic)
   - Smoothness → 0.3 (or use Roughness texture)

### Step 5: Assign to WeatherComponent

1. Select the entity with `WeatherComponent` in the scene
2. In the Inspector, find the **Snow** section
3. Drag & drop your `SnowMaterial` into the **Snow Material** field
4. OR click the field and select from the popup

---

## Shader Implementation

### Current State (Basic)

The existing shaders (`TerrainForward.frag`, `ForwardBase.frag`, `VegetationForward.frag`) use a basic snow system:

```glsl
// === WEATHER EFFECTS ===
// Snow coverage (upward-facing surfaces)
if (u_SnowCoverage > 0.0) {
    vec3 up = vec3(0, 1, 0);
    float upFacing = max(0.0, dot(material.normal, up));
    float snowAmount = pow(upFacing, 2.0) * u_SnowCoverage;
    vec3 snowColor = vec3(0.95, 0.96, 1.0);
    material.baseColor = mix(material.baseColor, snowColor, snowAmount);
    // Snow is less metallic and has medium roughness
    material.metallic = mix(material.metallic, 0.0, snowAmount);
    material.roughness = mix(material.roughness, 0.3, snowAmount);
}
```

**Limitations:**
- Uses a flat color instead of textures
- No angle control (slope min/max)
- No sparkle or displacement effects

### Enhanced Implementation (2025 Standard)

To upgrade to a modern snow system, follow these steps:

#### Step 1: Add Shader Uniforms

Add to the top of your fragment shaders (after existing weather uniforms):

```glsl
// === ADVANCED SNOW PARAMETERS ===
uniform float u_SnowSlopeMin;      // Minimum slope angle (radians)
uniform float u_SnowSlopeMax;      // Maximum slope angle (radians)
uniform float u_SnowSparkle;       // Sparkle intensity
uniform float u_SnowDisplacement;  // Height displacement

// Snow material textures
uniform sampler2D u_SnowAlbedo;
uniform sampler2D u_SnowNormal;
uniform sampler2D u_SnowRoughness;
```

#### Step 2: Add Snow Placement Function

Add this function to calculate snow placement based on surface angle:

```glsl
/// Calculate snow placement factor based on surface normal and slope constraints
/// Returns 0.0-1.0 where 1.0 = full snow coverage
float calculateSnowPlacement(vec3 normal, float slopeMinRad, float slopeMaxRad)
{
    vec3 up = vec3(0, 1, 0);

    // Calculate angle between surface normal and up vector
    float dotProduct = dot(normalize(normal), up);

    // Convert dot product to angle in radians
    // dotProduct = 1.0 → 0° (flat, upward-facing)
    // dotProduct = 0.0 → 90° (vertical)
    // dotProduct = -1.0 → 180° (downward-facing)
    float angleRad = acos(clamp(dotProduct, -1.0, 1.0));

    // Smooth transition at boundaries
    float fadeWidth = 0.1; // radians (~5.7 degrees)

    // Fade in from min angle
    float fadeIn = smoothstep(slopeMinRad - fadeWidth, slopeMinRad + fadeWidth, angleRad);

    // Fade out at max angle
    float fadeOut = 1.0 - smoothstep(slopeMaxRad - fadeWidth, slopeMaxRad + fadeWidth, angleRad);

    // Combine: 1.0 within range, smooth transitions outside
    return (1.0 - fadeIn) * fadeOut;
}
```

#### Step 3: Add Snow Sparkle Function

Snow sparkles when light reflects off ice crystals. Add this function:

```glsl
/// Calculate snow sparkle based on view angle and randomness
float calculateSnowSparkle(vec3 worldPos, vec3 normal, vec3 viewDir, float sparkleIntensity)
{
    if (sparkleIntensity <= 0.0) return 0.0;

    // Create pseudo-random sparkle pattern using world position
    vec3 sparkleNoise = fract(sin(worldPos * 123.456) * 43758.5453);
    float sparkle = sparkleNoise.x * sparkleNoise.y * sparkleNoise.z;

    // Sparkle more when viewing at grazing angles (Fresnel-like)
    float fresnel = pow(1.0 - max(0.0, dot(normal, viewDir)), 3.0);

    // Combine randomness + viewing angle
    float sparkleAmount = sparkle * fresnel * sparkleIntensity;

    return sparkleAmount;
}
```

#### Step 4: Replace Snow Application Code

Replace the existing basic snow code with this enhanced version:

```glsl
// === ENHANCED SNOW SYSTEM ===
if (u_SnowCoverage > 0.0)
{
    // Convert slope angles from degrees to radians (C# sends degrees)
    float slopeMinRad = radians(u_SnowSlopeMin);
    float slopeMaxRad = radians(u_SnowSlopeMax);

    // Calculate snow placement based on surface angle
    float snowPlacement = calculateSnowPlacement(material.normal, slopeMinRad, slopeMaxRad);

    // Final snow amount = coverage * placement
    float snowAmount = u_SnowCoverage * snowPlacement;

    if (snowAmount > 0.01)
    {
        // Sample snow material textures
        // (Use triplanar mapping if u_UseTriplanar == 1, otherwise use UVs)
        vec3 snowAlbedo;
        vec3 snowNormal;
        float snowRoughness;

        if (u_UseTriplanar == 1) {
            // Triplanar mapping for seamless snow on all surfaces
            snowAlbedo = SampleTriplanarColor(u_SnowAlbedo, vWorldPos, material.normal,
                                              u_TriplanarScale * 2.0, u_TriplanarBlendSharpness);
            snowNormal = SampleTriplanarNormalMap(u_SnowNormal, vWorldPos, material.normal,
                                                  u_TriplanarScale * 2.0, u_TriplanarBlendSharpness);
            snowRoughness = SampleTriplanarGray(u_SnowRoughness, vWorldPos, material.normal,
                                                u_TriplanarScale * 2.0, u_TriplanarBlendSharpness);
        } else {
            // UV mapping
            vec2 snowUV = vUV * 4.0; // Tile snow texture 4x for detail
            snowAlbedo = texture(u_SnowAlbedo, snowUV).rgb;
            snowNormal = texture(u_SnowNormal, snowUV).xyz * 2.0 - 1.0;
            snowRoughness = texture(u_SnowRoughness, snowUV).r;
        }

        // Apply snow displacement (offset position along normal)
        // NOTE: This is a visual effect; true displacement requires vertex shader changes
        vec3 displacedPos = vWorldPos + material.normal * (u_SnowDisplacement * snowAmount);

        // Calculate sparkle effect
        vec3 V = normalize(uCameraPos - vWorldPos);
        float sparkle = calculateSnowSparkle(vWorldPos, material.normal, V, u_SnowSparkle);

        // Add sparkle to snow albedo (brightens snow based on viewing angle)
        snowAlbedo += vec3(sparkle * 0.5);

        // Blend snow with underlying surface
        material.baseColor = mix(material.baseColor, snowAlbedo, snowAmount);
        material.normal = normalize(mix(material.normal, snowNormal, snowAmount * 0.5));
        material.roughness = mix(material.roughness, snowRoughness, snowAmount);
        material.metallic = mix(material.metallic, 0.0, snowAmount); // Snow is non-metallic
    }
}
```

#### Step 5: Update C# Uniform Bindings

In your shader binding code (e.g., `MaterialRuntime.cs`, `TerrainRenderer.cs`, `VegetationRenderer.cs`), add:

```csharp
// Send snow parameters to shader
GL.Uniform1(shader.GetUniformLocation("u_SnowCoverage"), weather.SnowCoverage);
GL.Uniform1(shader.GetUniformLocation("u_SnowSlopeMin"), weather.SnowSlopeMin);
GL.Uniform1(shader.GetUniformLocation("u_SnowSlopeMax"), weather.SnowSlopeMax);
GL.Uniform1(shader.GetUniformLocation("u_SnowSparkle"), weather.SnowSparkle);
GL.Uniform1(shader.GetUniformLocation("u_SnowDisplacement"), weather.SnowDisplacement);

// Bind snow material textures
if (weather.SnowMapMaterial.HasValue)
{
    var snowMaterial = AssetDatabase.TryGet(weather.SnowMapMaterial.Value, out var record);
    if (snowMaterial)
    {
        // Load and bind snow textures
        int snowAlbedoHandle = TextureCache.GetOrLoad(/* albedo texture guid */);
        int snowNormalHandle = TextureCache.GetOrLoad(/* normal texture guid */);
        int snowRoughnessHandle = TextureCache.GetOrLoad(/* roughness texture guid */);

        GL.ActiveTexture(TextureUnit.Texture10); // Use free texture unit
        GL.BindTexture(TextureTarget.Texture2D, snowAlbedoHandle);
        GL.Uniform1(shader.GetUniformLocation("u_SnowAlbedo"), 10);

        GL.ActiveTexture(TextureUnit.Texture11);
        GL.BindTexture(TextureTarget.Texture2D, snowNormalHandle);
        GL.Uniform1(shader.GetUniformLocation("u_SnowNormal"), 11);

        GL.ActiveTexture(TextureUnit.Texture12);
        GL.BindTexture(TextureTarget.Texture2D, snowRoughnessHandle);
        GL.Uniform1(shader.GetUniformLocation("u_SnowRoughness"), 12);
    }
}
```

---

## Advanced Techniques

### 1. True Vertex Displacement

For actual geometric snow depth (not just visual):

**Vertex Shader:**
```glsl
// Apply snow displacement in vertex shader
vec3 worldPos = (u_Model * vec4(aPosition, 1.0)).xyz;
float snowHeight = texture(u_SnowHeightMap, aTexCoord).r * u_SnowDisplacement;
worldPos += aNormal * snowHeight * u_SnowCoverage;
gl_Position = u_ViewProjection * vec4(worldPos, 1.0);
```

### 2. Snow Trails & Footprints

Use a dynamic texture that stores "snow disturbance":

```glsl
uniform sampler2D u_SnowTrailMap; // Generated by gameplay (footsteps, etc.)

float trailFactor = texture(u_SnowTrailMap, worldUV).r;
snowAmount *= (1.0 - trailFactor); // Reduce snow where trails exist
```

### 3. Accumulation Based on Occlusion

Snow doesn't accumulate under overhangs. Use ambient occlusion:

```glsl
float occlusion = texture(u_OcclusionTex, vUV).r;
snowAmount *= occlusion; // Less snow in occluded areas
```

### 4. Time-Based Melting

In `WeatherSystem`, gradually reduce `SnowCoverage` based on temperature:

```csharp
if (weather.SnowIntensity > 0.0f)
{
    // Accumulate snow
    weather.SnowCoverage = Math.Min(1.0f,
        weather.SnowCoverage + weather.SnowAccumulationSpeed * deltaTime * weather.SnowIntensity);
}
else
{
    // Melt snow
    weather.SnowCoverage = Math.Max(0.0f,
        weather.SnowCoverage - weather.SnowMeltSpeed * deltaTime);
}
```

### 5. Animated Sparkle

For dynamic sparkle that changes over time:

```glsl
float calculateSnowSparkle(vec3 worldPos, vec3 normal, vec3 viewDir, float sparkleIntensity, float time)
{
    // Add time to create animated sparkle
    vec3 sparkleNoise = fract(sin((worldPos + time * 0.1) * 123.456) * 43758.5453);
    // ... rest of function
}
```

---

## Best Practices

### Performance

1. **Use LOD** - Reduce snow detail on distant objects
2. **Texture atlases** - Combine snow textures to reduce draw calls
3. **Conditional compilation** - Use `#ifdef SNOW_ENABLED` to skip snow code when not needed
4. **Shared uniforms** - Send weather uniforms once per frame, not per object

### Visual Quality

1. **Color temperature** - Snow should have a subtle blue tint (RGB: 245, 250, 255) not pure white
2. **Roughness variation** - Fresh snow = 0.7, packed snow = 0.5, ice = 0.2
3. **Normal map strength** - Use 50-70% strength to avoid over-bumpy snow
4. **Sparkle subtlety** - Keep sparkle < 0.5 for realism; too much looks fake

### Workflow

1. **Test in isolation** - Set `SnowCoverage = 1.0` to see full snow effect
2. **Iterate on angles** - Adjust `SlopeMin/Max` to control where snow sticks
3. **Reference photos** - Look at real snow accumulation patterns
4. **Night testing** - Snow looks different at night (more blue from moonlight)

### Common Issues

| Problem | Solution |
|---------|----------|
| Snow appears on vertical walls | Reduce `SnowSlopeMax` to 30-45° |
| No snow visible | Check `SnowCoverage > 0` and material is assigned |
| Texture tiling visible | Increase triplanar scale or add noise to UVs |
| Sparkle too bright | Reduce `SnowSparkle` to 0.2-0.4 range |
| Performance drop | Reduce texture resolution or disable sparkle |

---

## Example Preset

Here's a good starting point for realistic winter conditions:

```csharp
var winterPreset = new WeatherPreset
{
    Name = "Winter Day",
    SnowIntensity = 0.2f,           // Light snowfall
    SnowCoverage = 0.7f,            // 70% coverage
    SnowSlopeMin = 0.0f,            // Flat surfaces
    SnowSlopeMax = 35.0f,           // Steeper than 35° no snow
    SnowSparkle = 0.4f,             // Moderate sparkle
    SnowDisplacement = 0.03f,       // 3cm depth
    FogEnabled = true,
    FogDensity = 0.04f,
    FogColor = new Vector3(0.85f, 0.9f, 0.95f) // Cold, blueish fog
};
```

---

## Technical Reference

### Slope Angle Conversion

```
Degrees → Radians: radians(degrees) or degrees * (π / 180)
Dot Product → Angle: acos(dotProduct)

Surface Types:
- 0° = flat ground (dot = 1.0)
- 45° = steep slope (dot = 0.707)
- 90° = vertical wall (dot = 0.0)
```

### Recommended Texture Formats

- **Albedo**: sRGB, 8-bit per channel (PNG or JPG)
- **Normal**: Linear, 8-bit per channel (PNG)
- **Roughness**: Linear, 8-bit grayscale (PNG)

### Memory Budget

| Asset | Size | Memory |
|-------|------|--------|
| Snow Albedo 2K | 2048x2048 | ~16 MB (compressed) |
| Snow Normal 2K | 2048x2048 | ~16 MB (compressed) |
| Snow Roughness 2K | 2048x2048 | ~4 MB (compressed) |
| **Total** | | **~36 MB** |

For mobile/low-end: Use 1024x1024 textures (~9 MB total)

---

## Conclusion

You now have a complete, modern snow system! The key innovations:

✅ **Material-based** - Use real textures, not flat colors
✅ **Physically-based** - Respects surface angles and properties
✅ **Temporally dynamic** - Accumulates and melts over time
✅ **Artistically controllable** - Tweak every parameter
✅ **Performance-conscious** - Optimized for real-time rendering

For questions or improvements, refer to the AstrildApex documentation or community forums.

---

**Document Version:** 1.0
**Last Updated:** 2025-12-22
**Author:** AstrildApex Engine Team
