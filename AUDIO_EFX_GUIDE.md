# Audio EFX System - Unity-like Audio Effects Pipeline

This guide explains how to use the OpenAL EFX-based audio effects system in your game engine, which provides Unity-like functionality for audio filters, reverb, and spatial effects.

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Per-Source Filters](#per-source-filters)
3. [Mixer Group Effects](#mixer-group-effects)
4. [Reverb Zones](#reverb-zones)
5. [Code Examples](#code-examples)
6. [Troubleshooting](#troubleshooting)

---

## Architecture Overview

The EFX system is built on OpenAL Soft's EFX (Effects Extension) and integrates seamlessly with your existing audio architecture:

### Core Components

1. **AudioEfxBackend** (`Engine/Audio/Effects/AudioEfxBackend.cs`)
   - Low-level wrapper around OpenAL EFX P/Invoke bindings
   - Manages creation/destruction of effects, filters, and auxiliary slots
   - Provides clean API for higher-level components

2. **AudioSource Filters** (`Engine/Audio/Components/AudioSourceFilters.cs`)
   - Per-source direct filters (Low-Pass, High-Pass, Band-Pass)
   - Similar to Unity's `AudioLowPassFilter` / `AudioHighPassFilter` components

3. **AudioMixerGroup Effects** (`Engine/Audio/Mixing/AudioMixerGroup.cs`)
   - Bus-level effects applied to all sources in a mixer group
   - Supports Reverb, Echo, and filters on groups (Master, Music, SFX, Voice, Ambient)

4. **ReverbZoneComponent** (`Engine/Audio/Components/ReverbZoneComponent.cs`)
   - 3D spatial reverb zones (Unity-like)
   - Apply reverb to 3D AudioSources based on position and distance

### How It Works

- **Direct Filters**: Attached directly to an AudioSource (only one active at a time per OpenAL limitation)
- **Auxiliary Effect Slots**: Used for reverb/echo effects that can be sent to multiple sources
- **Send Indices**:
  - Send 0-1: Reserved for mixer group effects
  - Send 2: Reserved for reverb zones
  - Send 3: Available for custom effects

---

## Per-Source Filters

Per-source filters are applied directly to individual AudioSource components, affecting only that specific sound.

### Types of Filters

1. **Low-Pass Filter**: Attenuates high frequencies (makes sound muffled)
2. **High-Pass Filter**: Attenuates low frequencies (makes sound thin/tinny)
3. **Band-Pass Filter**: Attenuates both low and high frequencies (telephone effect)

### Using Filters in Code

```csharp
using Engine.Audio.Components;

// Get AudioSource component
var audioSource = entity.GetComponent<AudioSource>();

// Add a low-pass filter (makes sound muffled)
var lowPassFilter = audioSource.AddLowPassFilter(cutoffFrequency: 2000f);

// Add a high-pass filter (removes low frequencies)
var highPassFilter = audioSource.AddHighPassFilter(cutoffFrequency: 500f);

// Modify filter settings
if (lowPassFilter.Settings is LowPassSettings lowPass)
{
    lowPass.GainHF = 0.3f; // Attenuate high frequencies (0 = max attenuation, 1 = no attenuation)
    lowPassFilter.UpdateFilter();
}

// Enable/disable filter
lowPassFilter.Enabled = false;

// Remove filter
audioSource.Filters.Remove(lowPassFilter);
AudioSourceFilterExtensions.DestroyFilterHandle(lowPassFilter);
```

### Using Filters in the Editor

1. Select an entity with an AudioSource component
2. In the **Inspector**, scroll to the **Audio Filters** section
3. Click **Add Filter** → Choose **Low-Pass Filter** or **High-Pass Filter**
4. Adjust the filter parameters:
   - **Gain**: Overall volume multiplier
   - **Gain HF** (Low-Pass): High-frequency attenuation (0 = muffled, 1 = clear)
   - **Gain LF** (High-Pass): Low-frequency attenuation (0 = thin, 1 = full)
5. Toggle **Enabled** to turn the filter on/off

**Note**: Only one direct filter can be active per source due to OpenAL limitations.

---

## Mixer Group Effects

Mixer group effects are applied to entire groups (Master, Music, SFX, Voice, Ambient), affecting all AudioSources routed to that group.

### Types of Group Effects

1. **Reverb**: Simulates acoustic environments (room, cathedral, cave, etc.)
2. **Echo**: Creates echo/delay effects
3. **Low-Pass / High-Pass Filters**: Apply filtering to the entire group

### Using Group Effects in Code

```csharp
using Engine.Audio.Mixing;
using Engine.Audio.Effects;
using Engine.Audio.Core;

// Get mixer from AudioEngine
var mixer = AudioEngine.Instance.Mixer;
var musicGroup = mixer.GetGroup("Music");

// Add reverb effect to Music group
var reverbEffect = musicGroup.AddEffect(AudioMixerGroupEffectType.Reverb);

// Configure reverb settings
if (reverbEffect.Settings is ReverbSettings reverb)
{
    reverb.DecayTime = 3.0f;      // Longer decay for cathedral-like reverb
    reverb.LateReverbGain = 1.5f; // Increase reverb presence
    musicGroup.UpdateEffect(reverbEffect);
}

// Add low-pass filter to entire SFX group (muffle all SFX sounds)
var sfxGroup = mixer.GetGroup("SFX");
var lowPassEffect = sfxGroup.AddEffect(AudioMixerGroupEffectType.LowPass);

if (lowPassEffect.Settings is LowPassSettings lowPass)
{
    lowPass.GainHF = 0.5f; // Reduce high frequencies
    sfxGroup.UpdateEffect(lowPassEffect);
}

// Enable/disable effect
reverbEffect.Enabled = false;

// Remove effect
musicGroup.RemoveEffect(reverbEffect);
```

### Using Group Effects in the Editor

1. Open the **Audio Mixer** panel
2. Find the mixer group row (Master, Music, SFX, Voice, Ambient)
3. Click the **FX** button in the row
4. In the popup:
   - Click **Add Effect** → Choose effect type (Reverb, Echo, Low-Pass, High-Pass)
   - Expand the effect to see parameters
   - Adjust parameters (they update in real-time)
   - Toggle **Enabled** to activate/deactivate
   - Click **Remove** to delete the effect

**Practical Use Cases**:
- Add reverb to Music group for atmospheric background music
- Add low-pass to SFX when player is underwater
- Add echo to Voice group for radio/comms effects

---

## Reverb Zones

Reverb zones create 3D spatial regions where reverb is applied to AudioSources based on their position.

### How Reverb Zones Work

- **Inner Radius**: Distance where reverb is at full strength (blend = 1.0)
- **Outer Radius**: Distance where reverb fades to zero (blend = 0.0)
- **Between Inner and Outer**: Reverb interpolates linearly based on distance

### Using Reverb Zones in Code

```csharp
using Engine.Audio.Components;

// Create an entity for the reverb zone
var zoneEntity = scene.CreateEntity("Cathedral Reverb Zone");

// Add ReverbZoneComponent
var reverbZone = zoneEntity.AddComponent<ReverbZoneComponent>();

// Configure zone
reverbZone.InnerRadius = 10f;  // Full reverb within 10 units
reverbZone.OuterRadius = 30f;  // Fade to no reverb at 30 units
reverbZone.Preset = ReverbZoneComponent.ReverbPreset.Cathedral;
reverbZone.Enabled = true;

// Or manually configure reverb settings
reverbZone.ReverbSettings.DecayTime = 5.0f;
reverbZone.ReverbSettings.LateReverbGain = 2.0f;
reverbZone.UpdateReverbSettings(reverbZone.ReverbSettings);

// Position the zone in 3D space
zoneEntity.Transform.Position = new Vector3(100, 0, 200);
```

### Using Reverb Zones in the Editor

1. Create a new entity in your scene
2. Add a **ReverbZoneComponent** to it
3. In the **Inspector**:
   - Set **Inner Radius** and **Outer Radius**
   - Choose a **Reverb Preset** (Generic, Room, Cathedral, Cave, etc.)
   - Or expand **Advanced Reverb Settings** to fine-tune parameters
   - Toggle **Enabled** to activate/deactivate
4. Position the entity in your 3D scene

**How It Affects AudioSources**:
- Only affects AudioSources with `SpatialBlend > 0.1` (3D sounds)
- Automatically calculates distance and applies appropriate reverb blend
- If multiple zones overlap, the dominant zone (highest blend) is used

**Practical Use Cases**:
- Cathedral reverb zone in a large church interior
- Cave reverb in underground areas
- Room reverb in enclosed spaces
- Underwater reverb for submerged areas

---

## Code Examples

### Example 1: Underwater Effect

```csharp
// When player enters water
void OnEnterWater()
{
    // Add low-pass filter to all SFX
    var mixer = AudioEngine.Instance.Mixer;
    var sfxGroup = mixer.GetGroup("SFX");
    var underwaterFilter = sfxGroup.AddEffect(AudioMixerGroupEffectType.LowPass);

    if (underwaterFilter.Settings is LowPassSettings lowPass)
    {
        lowPass.GainHF = 0.2f; // Heavily muffle sounds
        sfxGroup.UpdateEffect(underwaterFilter);
    }

    // Optionally add underwater reverb preset
    var reverbEffect = sfxGroup.AddEffect(AudioMixerGroupEffectType.Reverb);
    if (reverbEffect.Settings is ReverbSettings reverb)
    {
        reverb.Density = 1.0f;
        reverb.Diffusion = 0.7f;
        reverb.Gain = 0.2f;
        reverb.DecayTime = 1.5f;
        sfxGroup.UpdateEffect(reverbEffect);
    }
}

// When player exits water
void OnExitWater()
{
    var mixer = AudioEngine.Instance.Mixer;
    var sfxGroup = mixer.GetGroup("SFX");

    // Remove all effects from SFX group
    foreach (var effect in sfxGroup.Effects.ToArray())
    {
        sfxGroup.RemoveEffect(effect);
    }
}
```

### Example 2: Dynamic Radio Filter

```csharp
// Add radio effect to a specific voice AudioSource
void ApplyRadioEffect(AudioSource voiceSource)
{
    // Add band-pass filter (removes low and high frequencies)
    var filter = new AudioSourceFilter(AudioSourceFilterType.BandPass)
    {
        Settings = new BandPassSettings
        {
            Gain = 1.0f,
            GainLF = 0.3f,  // Reduce low frequencies
            GainHF = 0.4f   // Reduce high frequencies
        }
    };

    voiceSource.Filters.Add(filter);
    AudioSourceFilterExtensions.CreateFilterHandle(filter);

    // Optionally add distortion effect for extra radio crackle
    // (requires custom implementation)
}
```

### Example 3: Dynamic Reverb Zone for Caves

```csharp
// Create a cave reverb zone when player enters a cave area
void CreateCaveReverb(Vector3 caveCenter)
{
    var zoneEntity = scene.CreateEntity("Cave Reverb");
    var reverbZone = zoneEntity.AddComponent<ReverbZoneComponent>();

    reverbZone.InnerRadius = 15f;
    reverbZone.OuterRadius = 40f;
    reverbZone.Preset = ReverbZoneComponent.ReverbPreset.Cave;
    reverbZone.Enabled = true;

    zoneEntity.Transform.Position = caveCenter;
}
```

---

## Troubleshooting

### EFX Not Working

**Problem**: Effects/filters have no audible effect.

**Solutions**:
1. Check that OpenAL Soft is installed (not Creative's OpenAL32.dll)
2. Verify EFX is supported:
   ```csharp
   if (AudioEfxBackend.Instance.IsEFXSupported)
   {
       Debug.Log("EFX is supported");
   }
   else
   {
       Debug.Log("EFX NOT supported - install OpenAL Soft");
   }
   ```
3. Make sure `OpenAL32.dll` from OpenAL Soft is in your project directory
4. Check the console/logs for EFX-related warnings or errors

### Filters Not Applying

**Problem**: Added a filter but sound is unchanged.

**Solutions**:
1. Ensure filter is **enabled**: `filter.Enabled = true`
2. Only ONE direct filter can be active per source (OpenAL limitation)
3. Call `filter.UpdateFilter()` after changing settings
4. Verify the filter handle is valid: `filter.FilterHandle.IsValid`

### Reverb Zones Not Working

**Problem**: 3D AudioSource not affected by reverb zone.

**Solutions**:
1. Ensure AudioSource has `SpatialBlend > 0.1` (must be 3D sound)
2. Verify zone is **enabled**: `reverbZone.Enabled = true`
3. Check zone's InnerRadius and OuterRadius cover the source's position
4. Reverb zones use send index 2; ensure mixer group effects aren't using all sends
5. Call `ReverbZoneExtensions.ApplyReverbZones(audioSource, sourceId)` manually if needed

### Multiple Filters on One Source

**Problem**: Only one filter seems to work.

**Explanation**: OpenAL supports only ONE direct filter per source. This is a limitation of OpenAL, not this implementation.

**Workaround**: Combine effects using mixer groups or use auxiliary slots for additional processing.

### Performance Issues

**Problem**: Too many effects causing lag.

**Solutions**:
1. Limit the number of active reverb zones (< 5 recommended)
2. Use mixer group effects instead of per-source effects when possible
3. Disable unused effects: `effect.Enabled = false`
4. Clean up effects when done:
   ```csharp
   audioSource.ClearFilters();
   mixerGroup.Effects.Clear();
   ```

---

## API Reference Summary

### AudioEfxBackend

```csharp
// Singleton access
AudioEfxBackend.Instance

// Properties
bool IsEFXSupported { get; }

// Effect creation
EfxEffectHandle CreateReverbEffect(ReverbSettings settings)
EfxEffectHandle CreateEchoEffect(EchoSettings settings)
void UpdateReverbEffect(EfxEffectHandle handle, ReverbSettings settings)
void DestroyEffect(EfxEffectHandle handle)

// Filter creation
EfxFilterHandle CreateLowPassFilter(LowPassSettings settings)
EfxFilterHandle CreateHighPassFilter(HighPassSettings settings)
void UpdateLowPassFilter(EfxFilterHandle handle, LowPassSettings settings)
void DestroyFilter(EfxFilterHandle handle)

// Auxiliary slot creation
EfxAuxSlotHandle CreateAuxSlot(EfxEffectHandle effect, float gain = 1.0f)
void UpdateAuxSlotEffect(EfxAuxSlotHandle slot, EfxEffectHandle effect)
void DestroyAuxSlot(EfxAuxSlotHandle handle)

// Source attachment
void AttachDirectFilterToSource(int sourceId, EfxFilterHandle filter)
void AttachAuxSlotToSource(int sourceId, EfxAuxSlotHandle slot, int sendIndex, EfxFilterHandle? sendFilter = null)
void DetachDirectFilterFromSource(int sourceId)
void DetachAuxSlotFromSource(int sourceId, int sendIndex)
```

### AudioSource Extensions

```csharp
// Add filters
AudioSourceFilter AddLowPassFilter(this AudioSource source, float cutoffFrequency = 5000f)
AudioSourceFilter AddHighPassFilter(this AudioSource source, float cutoffFrequency = 500f)

// Manage filters
source.Filters // List<AudioSourceFilter>
filter.Enabled = true/false
filter.UpdateFilter()
AudioSourceFilterExtensions.DestroyFilterHandle(filter)
```

### AudioMixerGroup

```csharp
// Add effects
AudioMixerGroupEffect AddEffect(AudioMixerGroupEffectType type)

// Manage effects
group.Effects // List<AudioMixerGroupEffect>
group.UpdateEffect(effect)
group.RemoveEffect(effect)
effect.Enabled = true/false
```

### ReverbZoneComponent

```csharp
// Properties
float InnerRadius { get; set; }
float OuterRadius { get; set; }
ReverbPreset Preset { get; set; }
bool Enabled { get; set; }
ReverbSettings ReverbSettings { get; }

// Methods
void SetPreset(ReverbPreset preset)
void UpdateReverbSettings(ReverbSettings settings)
float CalculateBlendFactor(Vector3 position)

// Static methods
static ReverbZoneComponent? FindDominantZone(Vector3 position, out float blendFactor)
static IReadOnlyList<ReverbZoneComponent> GetActiveZones()
```

---

## Advanced Topics

### Custom Effect Presets

You can create custom reverb presets:

```csharp
public static ReverbSettings CustomHangarPreset() => new ReverbSettings
{
    Density = 1.0f,
    Diffusion = 0.9f,
    Gain = 0.32f,
    GainHF = 0.7f,
    DecayTime = 4.0f,
    DecayHFRatio = 0.6f,
    ReflectionsGain = 0.1f,
    ReflectionsDelay = 0.05f,
    LateReverbGain = 2.0f,
    LateReverbDelay = 0.02f,
    AirAbsorptionGainHF = 0.99f,
    RoomRolloffFactor = 0.0f,
    DecayHFLimit = true
};
```

### Mixing Effects

Combine multiple effects for complex audio:

1. Mixer group reverb (send 0)
2. Per-source low-pass filter (direct filter)
3. Reverb zone (send 2)

All three can be active simultaneously on one AudioSource.

---

## Best Practices

1. **Initialize Once**: EFX objects are expensive to create. Create them once and reuse.
2. **Clean Up**: Always destroy EFX objects when done (handled automatically by components).
3. **Limit Active Effects**: Don't create hundreds of reverb zones; use them sparingly.
4. **Use Mixer Groups**: Apply effects to groups rather than individual sources when possible.
5. **Test Performance**: Profile your game to ensure EFX isn't causing bottlenecks.

---

## Conclusion

The EFX system provides powerful Unity-like audio effects for your game engine. Experiment with different combinations of filters, reverb zones, and mixer group effects to create immersive soundscapes!

For questions or issues, refer to the source code documentation or OpenAL EFX specification.
