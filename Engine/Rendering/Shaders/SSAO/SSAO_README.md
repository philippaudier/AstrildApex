# SSAO (Screen Space Ambient Occlusion) - Implementation Guide

## Overview

This SSAO implementation follows **LearnOpenGL best practices** for stable, view-independent ambient occlusion. It uses hemisphere sampling in view-space with proper depth testing and range checks.

## Key Changes from Previous Implementation

### 1. **Simplified TBN Matrix Construction**
- **Old**: Complex tangent generation with conditional logic and cos/sin rotation
- **New**: Simple Gram-Schmidt orthogonalization with random vector from noise texture
- **Benefit**: More stable, easier to understand, fewer edge cases

### 2. **Improved Depth Testing**
- **Old**: Single-line comparison with implicit assumptions
- **New**: Explicit depth difference calculation with clear occlusion conditions
- **Benefit**: Easier to debug, more robust handling of edge cases

### 3. **Better Range Check**
- **Old**: `smoothstep(0.0, 1.0, radius / abs(fragPos.z - sampleDepth))`
- **New**: `smoothstep(0.0, 1.0, radius / (abs(depthDifference) + 0.001))`
- **Benefit**: Prevents division by zero, more intuitive behavior

### 4. **Screen Bounds Checking**
- **New**: Skip samples that project outside screen [0,1] range
- **Benefit**: Prevents sampling invalid texture coordinates

### 5. **Simpler Noise Texture**
- **Old**: cos/sin rotation angles
- **New**: Random XY vectors (LearnOpenGL standard)
- **Benefit**: Both work, but random vectors are more intuitive

## How SSAO Works (View-Space)

### Important: SSAO is View-Dependent (This is Correct!)

**You may notice that SSAO changes as you move the camera. This is NORMAL and CORRECT.**

SSAO is calculated in **view-space** (camera-relative coordinates), which means:
- Positions are relative to the camera
- Normals are in camera space
- Occlusion is calculated from the camera's perspective

This is the **standard and correct** approach used by all modern engines (Unity, Unreal, CryEngine, etc.).

### Why View-Space?

1. **Efficiency**: Works with existing G-buffer data (already in view-space)
2. **Accuracy**: Occlusion is inherently view-dependent in real-time rendering
3. **Simplicity**: Easier projection math (already have projection matrix)
4. **Stability**: Camera-relative coordinates avoid precision issues with large world coordinates

### What You Should NOT See (Bugs to Watch For)

✅ **CORRECT**: SSAO changes smoothly as you rotate/move the camera
❌ **BUG**: SSAO "pops" or flickers as you move
❌ **BUG**: Bright spots that follow the camera rotation
❌ **BUG**: SSAO becomes brighter when looking down vs looking up
❌ **BUG**: Harsh bands or patterns in occlusion

## Algorithm Steps

### Step 1: G-Buffer Pass (SSAOGeometry.vert/frag)
Renders scene geometry to store:
- **Position texture**: View-space positions (RGB32F)
- **Normal texture**: View-space normals (RGB16F)
- **Depth texture**: Linear depth (Depth32F)

### Step 2: SSAO Calculation (SSAOCalc.frag)

For each pixel:

1. **Sample G-buffer**: Get view-space position and normal
2. **Create TBN matrix**: Orient sample hemisphere along surface normal
   - Sample noise texture for random rotation
   - Use Gram-Schmidt to create tangent perpendicular to normal
   - Build TBN matrix: [tangent, bitangent, normal]
3. **Sample kernel**: For each of 16-64 sample points:
   - Transform sample from tangent-space to view-space via TBN
   - Project sample to screen-space via projection matrix
   - Sample depth at that screen location
   - Compare depths to determine occlusion
4. **Accumulate occlusion**: Count how many samples are occluded
5. **Output**: Normalized occlusion value (1.0 = no occlusion, 0.0 = full occlusion)

### Step 3: Blur Pass (SSAOBlur.frag)
Apply Gaussian blur to remove high-frequency noise from 4x4 noise texture tiling.

## Parameters Guide

### `u_SSAORadius` (default: 0.5)
- **Purpose**: Size of the sampling hemisphere in view-space units
- **Effect**:
  - **Too small** (< 0.2): Only very tight crevices show occlusion
  - **Too large** (> 1.0): Distant geometry incorrectly darkens surfaces
  - **Recommended**: 0.3 - 0.7 depending on scene scale

### `u_SSAOBias` (default: 0.025)
- **Purpose**: Minimum depth difference to count as occlusion
- **Effect**:
  - **Too small** (< 0.01): Self-shadowing artifacts (acne)
  - **Too large** (> 0.1): Loss of contact shadows
  - **Recommended**: 0.02 - 0.05

### `u_SSAOIntensity` (default: 1.0)
- **Purpose**: Power curve exponent for final occlusion
- **Effect**:
  - **1.0**: Linear occlusion
  - **> 1.0**: More contrast, darker shadows
  - **< 1.0**: Subtle, softer occlusion
  - **Recommended**: 1.0 - 2.0

### `u_SSAOSamples` (default: 64)
- **Purpose**: Number of hemisphere samples
- **Effect**:
  - **16**: Fast, more noise (requires stronger blur)
  - **32**: Balanced quality/performance
  - **64**: High quality, less noise
  - **Recommended**: 32-64

### `u_BlurSize` (default: 2)
- **Purpose**: Blur kernel radius in pixels
- **Effect**:
  - **1**: 3x3 blur (subtle)
  - **2**: 5x5 blur (recommended)
  - **3**: 7x7 blur (softer but may over-blur)

## Debugging Tips

### Problem: SSAO too strong everywhere
**Solution**: Decrease `Intensity` or increase `Radius`

### Problem: Self-shadowing artifacts (dotted patterns)
**Solution**: Increase `Bias` (start with 0.03-0.05)

### Problem: No occlusion visible
**Solutions**:
- Check G-buffer textures are valid (not black)
- Verify normals are in view-space (not world-space)
- Increase `Radius` and/or `Intensity`
- Check that SSAO texture is bound correctly in lighting shader

### Problem: Flickering/unstable SSAO
**Solutions**:
- Verify noise texture is static (not regenerated per frame)
- Check kernel samples are uploaded only once
- Ensure G-buffer isn't being regenerated unnecessarily

### Problem: Banding artifacts
**Solutions**:
- Increase sample count
- Verify blur pass is active
- Check noise texture wrapping is set to REPEAT

## Integration Checklist

- [x] G-buffer stores view-space positions and normals
- [x] Noise texture is 4x4 with GL_REPEAT wrapping
- [x] Sample kernel is hemisphere (z > 0) with quadratic distribution
- [x] Kernel samples are uploaded once (not per frame)
- [x] SSAO shader uses projection matrix for screen-space projection
- [x] Blur pass removes noise while preserving edges
- [x] Final SSAO texture is bound in lighting/PBR shader
- [x] SSAO multiplied with ambient lighting component

## Performance Notes

**G-Buffer Pass**: ~0.5ms (depends on scene complexity)
**SSAO Calculation**: ~1-3ms (depends on sample count and resolution)
**Blur Pass**: ~0.2-0.5ms

**Total**: ~2-4ms for 1920x1080 @ 64 samples

**Optimization Tips**:
- Use 32 samples instead of 64 (-50% calculation time)
- Render SSAO at half resolution, upsample with blur
- Use compute shaders for SSAO calculation (advanced)
- Cache SSAO for static geometry (advanced)

## References

- [LearnOpenGL SSAO Tutorial](https://learnopengl.com/Advanced-Lighting/SSAO)
- [OGLDev SSAO Tutorial](https://ogldev.org/www/tutorial45/tutorial45.html)
- [Alchemy AO Paper (Original SSAO)](http://developer.download.nvidia.com/SDK/10.5/direct3d/Source/ScreenSpaceAO/doc/ScreenSpaceAO.pdf)

## Technical Details

### View-Space Coordinate System
```
+X: Right
+Y: Up
-Z: Forward (into screen)
```

### Depth Comparison Logic
```glsl
// In OpenGL view-space:
// More negative Z = farther from camera
// Less negative Z = closer to camera

float depthDiff = samplePos.z - sampleDepth;

// Sample is occluded if:
// 1. sampleDepth < samplePos.z (geometry is closer than sample)
// 2. depthDiff > bias (difference exceeds noise threshold)
// 3. depthDiff < radius (within sampling range)

if (sampleDepth < samplePos.z - bias && depthDiff < radius) {
    // Occluded!
}
```

### Hemisphere Kernel Distribution
Samples are distributed within a unit hemisphere oriented along +Z:
- 70% of samples are within 0.5 units of origin (quadratic falloff)
- This emphasizes local occlusion over distant geometry
- Prevents over-darkening from far samples

## Common Mistakes to Avoid

1. ❌ Mixing world-space and view-space coordinates
2. ❌ Using sphere sampling instead of hemisphere
3. ❌ Regenerating noise texture per frame
4. ❌ Incorrect depth comparison direction
5. ❌ Forgetting to normalize noise texture samples
6. ❌ Using wrong coordinate space for normals
7. ❌ Skipping blur pass (banding will be very visible)
8. ❌ Setting radius too large (causes incorrect darkening)

## Version History

**v2.0 (Current)**: Complete refactoring based on LearnOpenGL
- Simplified TBN construction
- Improved depth testing
- Better range checks
- Screen bounds validation
- Comprehensive documentation

**v1.0**: Initial implementation with complex TBN rotation logic
