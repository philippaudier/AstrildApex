#version 420 core

#include "../Includes/Common.glsl"
#include "../Includes/IBL.glsl"
#include "../Includes/Lighting.glsl"
#include "../Includes/Fog.glsl"
#include "../Includes/Shadows.glsl"

#define MAX_LAYERS 8

in vec3 v_WorldPos;
in vec3 v_Normal;
in vec2 v_TexCoord;

layout(location=0) out vec4 FragColor;
layout(location=1) out uint outId;

uniform uint u_ObjectId;
uniform sampler2D u_LayerAlbedo[MAX_LAYERS];
uniform sampler2D u_LayerNormal[MAX_LAYERS];
uniform vec4 u_LayerTilingOffset[MAX_LAYERS]; // tx,ty,ox,oy
uniform int u_LayerUseTriplanar[MAX_LAYERS]; // 0 = use UV, 1 = material triplanar
uniform float u_LayerTriplanarScale[MAX_LAYERS];
uniform float u_LayerTriplanarBlend[MAX_LAYERS];
uniform vec4 u_LayerStylize[MAX_LAYERS]; // saturation, brightness, contrast, hue
uniform float u_LayerEmission[MAX_LAYERS];
uniform vec4 u_LayerHeightSlope[MAX_LAYERS]; // hmin,hmax,smin,smax (slope normalized 0..1)
uniform float u_LayerStrength[MAX_LAYERS];
uniform int u_LayerIsUnderwater[MAX_LAYERS]; // 0 = normal, 1 = underwater
uniform vec4 u_LayerUnderwaterParams[MAX_LAYERS]; // waterLevel, blendDist, slopeMin, slopeMax
uniform float u_LayerUnderwaterBlend[MAX_LAYERS]; // 0 = full underwater, 1 = blend with others
uniform float u_LayerMetallic[MAX_LAYERS]; // PBR: metallic per layer
uniform float u_LayerSmoothness[MAX_LAYERS]; // PBR: smoothness per layer
uniform vec4 u_LayerAlbedoColor[MAX_LAYERS]; // Albedo color tint per layer
uniform float u_LayerNormalStrength[MAX_LAYERS]; // Normal map strength per layer
uniform int u_LayerTransparencyMode[MAX_LAYERS]; // 0 = opaque, 1 = transparent
uniform int u_LayerCount;

// Existing compatibility uniforms (kept)
uniform sampler2D u_AlbedoTex;
uniform sampler2D u_NormalTex;
uniform vec4 u_AlbedoColor;
uniform float u_Metallic;
uniform float u_Smoothness;
uniform int u_TransparencyMode;

// SSAO uniforms
uniform sampler2D u_SSAOTexture;
uniform int u_SSAOEnabled;
uniform float u_SSAOStrength;
uniform vec2 u_ScreenSize;

// Shadow debugging uniforms (optional)

// Splatmap support: up to 2 RGBA splatmaps for 8 layers
uniform sampler2D u_Splatmap[2];
uniform int u_UseSplatmap; // 0 = off, 1 = on
// Debug: when set to 1, paint front faces red and back faces blue
uniform int u_DebugFaceColor;

float computeSlopeNormalized(vec3 N)
{
    vec3 up = vec3(0.0, 1.0, 0.0);
    float dotp = dot(normalize(N), up);
    // dotp = 1.0 for flat (0°), 0.0 for vertical (90°)
    // slope = 0.0 for flat, 1.0 for vertical
    float slope = clamp(1.0 - dotp, 0.0, 1.0);
    return slope;
}

float inRangeSmooth(float v, float a, float b)
{
    if (b <= a)
    {
        // Single value - return 1 if close to a
        float dist = abs(v - a);
        return step(dist, 0.01);
    }

    // Return 1.0 if v is within [a, b], with smooth falloff outside
    float rangeWidth = b - a;
    float blendWidth = rangeWidth * 0.1;

    float fadeIn = smoothstep(a - blendWidth, a, v);
    float fadeOut = 1.0 - smoothstep(b, b + blendWidth, v);

    return fadeIn * fadeOut;
}

void main()
{
    // Clipping plane for water reflections
    // Do this FIRST before any other processing
    if (uClipPlaneEnabled > 0.5) {
        // Calculate distance from fragment to clip plane
        // Plane equation: dot(normal, point) + d = 0
        float distance = dot(uClipPlane.xyz, v_WorldPos) + uClipPlane.w;

        if (distance < 0.0) {
            discard; // Fragment is below water surface, don't render
        }
    }

    float height = v_WorldPos.y;
    float slope = computeSlopeNormalized(v_Normal);

    vec3 accumColor = vec3(0.0);
    vec3 accumNormal = vec3(0.0);
    float accumMetallic = 0.0;
    float accumSmoothness = 0.0;
    float accumAlpha = 0.0;
    float accumWeight = 0.0;

    // Fallback color si pas de layers ou si le total des poids est 0
    vec3 fallbackColor = vec3(1.0, 0.5, 0.0); // Orange vif pour la couleur fallback

    if (u_LayerCount <= 0)
    {
        FragColor = vec4(fallbackColor, 1.0);
        return;
    }

    float weights[MAX_LAYERS];
    for (int i = 0; i < MAX_LAYERS; i++) weights[i] = 0.0;

    if (u_UseSplatmap == 1)
    {
        vec4 s0 = texture(u_Splatmap[0], v_TexCoord.xy);
        vec4 s1 = texture(u_Splatmap[1], v_TexCoord.xy);
        weights[0] = s0.r; weights[1] = s0.g; weights[2] = s0.b; weights[3] = s0.a;
        weights[4] = s1.r; weights[5] = s1.g; weights[6] = s1.b; weights[7] = s1.a;
    }
    else
    {
        // Compute procedural weights per layer
        for (int i = 0; i < u_LayerCount; i++)
        {
            float weight = 0.0;

            if (u_LayerIsUnderwater[i] == 1)
            {
                // Underwater mode: full coverage below water level
                float waterLevel = u_LayerUnderwaterParams[i].x;
                float blendDist = u_LayerUnderwaterParams[i].y;
                float slopeMinNorm = u_LayerUnderwaterParams[i].z;
                float slopeMaxNorm = u_LayerUnderwaterParams[i].w;

                // Height weight: 1.0 below water, smooth blend at surface
                float heightWeight = 0.0;
                if (height <= waterLevel)
                {
                    if (height <= waterLevel - blendDist)
                    {
                        heightWeight = 1.0;
                    }
                    else
                    {
                        float t = (waterLevel - height) / blendDist;
                        heightWeight = smoothstep(0.0, 1.0, t);
                    }
                }

                // Slope weight: only apply within slope range
                float slopeWeight = inRangeSmooth(slope, slopeMinNorm, slopeMaxNorm);

                weight = heightWeight * slopeWeight;
            }
            else
            {
                // Normal mode: height and slope blending
                float hmin = u_LayerHeightSlope[i].x;
                float hmax = u_LayerHeightSlope[i].y;
                float smin = u_LayerHeightSlope[i].z;
                float smax = u_LayerHeightSlope[i].w;
                float wh = inRangeSmooth(height, hmin, hmax);
                float ws = inRangeSmooth(slope, smin, smax);
                weight = wh * ws * u_LayerStrength[i];
            }

            weights[i] = weight;
        }
    }

    // Handle underwater blend mode
    // If any layer is underwater with blendWithOthers < 1, it overrides other layers
    for (int i = 0; i < u_LayerCount; i++)
    {
        if (u_LayerIsUnderwater[i] == 1 && weights[i] > 0.001)
        {
            float blendMode = u_LayerUnderwaterBlend[i];
            if (blendMode < 0.999)
            {
                // Override mode: suppress other layers based on blend factor
                float suppressFactor = 1.0 - blendMode; // 0 = no suppress, 1 = full suppress
                for (int j = 0; j < u_LayerCount; j++)
                {
                    if (j != i)
                    {
                        weights[j] *= (1.0 - weights[i] * suppressFactor);
                    }
                }
            }
        }
    }

    // Normalize weights defensively
    float total = 0.0;
    for (int i = 0; i < u_LayerCount; i++) total += weights[i];
    if (total <= 0.0001)
    {
        // Utiliser la couleur fallback orange au lieu du gris
        FragColor = vec4(fallbackColor, 1.0);
        return;
    }
    for (int i = 0; i < u_LayerCount; i++) weights[i] /= total;

    // Per-layer mapping will either use UV tiling/offset (material preferred) or material triplanar settings.

    for (int i = 0; i < u_LayerCount; i++)
    {
        float w = weights[i];
        if (w <= 0.0001) continue;
        
        vec2 tilingOffset = u_LayerTilingOffset[i].xy;
        vec2 offset = u_LayerTilingOffset[i].zw;
        int useTriplanar = u_LayerUseTriplanar[i];
        float triScale = u_LayerTriplanarScale[i];
        float triBlend = u_LayerTriplanarBlend[i];

        vec2 uvX;
        vec2 uvY;
        vec2 uvZ;
        vec3 localBlendWeights;

        if (useTriplanar == 1)
        {
            // Use material triplanar scale/offset
            uvX = v_WorldPos.yz * triScale + offset;
            uvY = v_WorldPos.xz * triScale + offset;
            uvZ = v_WorldPos.xy * triScale + offset;

            localBlendWeights = abs(normalize(v_Normal));
            localBlendWeights = localBlendWeights / (localBlendWeights.x + localBlendWeights.y + localBlendWeights.z);
            localBlendWeights = pow(localBlendWeights, vec3(triBlend));
            localBlendWeights = localBlendWeights / (localBlendWeights.x + localBlendWeights.y + localBlendWeights.z);
        }
        else
        {
            // Use UV tiling/offset (from material or layer)
            uvX = v_WorldPos.yz * tilingOffset + offset; // Side X
            uvY = v_WorldPos.xz * tilingOffset + offset; // Top/Bottom Y
            uvZ = v_WorldPos.xy * tilingOffset + offset; // Side Z

            localBlendWeights = abs(normalize(v_Normal));
            localBlendWeights = localBlendWeights / (localBlendWeights.x + localBlendWeights.y + localBlendWeights.z);
            // Use a reasonable default sharpness
            float defaultSharp = 4.0;
            localBlendWeights = pow(localBlendWeights, vec3(defaultSharp));
            localBlendWeights = localBlendWeights / (localBlendWeights.x + localBlendWeights.y + localBlendWeights.z);
        }
        
        // Sample albedo from all 3 projections
        vec3 albedoX = texture(u_LayerAlbedo[i], uvX).rgb;
        vec3 albedoY = texture(u_LayerAlbedo[i], uvY).rgb;
        vec3 albedoZ = texture(u_LayerAlbedo[i], uvZ).rgb;
        
        // Blend albedo based on surface normal direction
        vec3 al = albedoX * localBlendWeights.x + 
              albedoY * localBlendWeights.y + 
              albedoZ * localBlendWeights.z;
        
        // Apply albedo color tint
        al *= u_LayerAlbedoColor[i].rgb;
        float layerAlpha = u_LayerAlbedoColor[i].a;

        // Apply material stylization (sat, brightness, contrast, hue)
        vec4 styl = u_LayerStylize[i];
        float sat = styl.x;
        float bright = styl.y;
        float contrast = styl.z;
        float hue = styl.w;

        // Saturation: blend towards luminance
        float lum = dot(al, vec3(0.3, 0.59, 0.11));
        al = mix(vec3(lum), al, sat);

        // Brightness
        al *= bright;

        // Contrast
        al = (al - 0.5) * contrast + 0.5;

        // Hue shift (approx via RGB->HSV and back)
        if (abs(hue) > 1e-6)
        {
            // RGB to HSV
            vec3 c = al;
            float cmax = max(c.r, max(c.g, c.b));
            float cmin = min(c.r, min(c.g, c.b));
            float delta = cmax - cmin;
            float H = 0.0;
            if (delta > 1e-6)
            {
                if (cmax == c.r) H = mod(((c.g - c.b) / delta), 6.0);
                else if (cmax == c.g) H = ((c.b - c.r) / delta) + 2.0;
                else H = ((c.r - c.g) / delta) + 4.0;
                H /= 6.0;
                if (H < 0.0) H += 1.0;
            }
            float S = (cmax < 1e-6) ? 0.0 : delta / cmax;
            float V = cmax;

            H = fract(H + hue);

            // HSV to RGB
            float C = V * S;
            float X = C * (1.0 - abs(mod(H * 6.0, 2.0) - 1.0));
            float m = V - C;
            vec3 rgb1;
            if (0.0 <= H && H < 1.0/6.0) rgb1 = vec3(C, X, 0);
            else if (H < 2.0/6.0) rgb1 = vec3(X, C, 0);
            else if (H < 3.0/6.0) rgb1 = vec3(0, C, X);
            else if (H < 4.0/6.0) rgb1 = vec3(0, X, C);
            else if (H < 5.0/6.0) rgb1 = vec3(X, 0, C);
            else rgb1 = vec3(C, 0, X);
            al = rgb1 + vec3(m);
        }
        
        // Sample normal maps from all 3 projections
        vec3 normalX = texture(u_LayerNormal[i], uvX).xyz * 2.0 - 1.0;
        vec3 normalY = texture(u_LayerNormal[i], uvY).xyz * 2.0 - 1.0;
        vec3 normalZ = texture(u_LayerNormal[i], uvZ).xyz * 2.0 - 1.0;
        
        // Transform tangent-space normals to world space for each projection axis
        // X-axis projection (YZ plane): tangent=Y, bitangent=Z, normal=X
        vec3 worldNormalX = vec3(normalX.z, normalX.x, normalX.y);
        // Y-axis projection (XZ plane): tangent=X, bitangent=Z, normal=Y  
        vec3 worldNormalY = vec3(normalY.x, normalY.z, normalY.y);
        // Z-axis projection (XY plane): tangent=X, bitangent=Y, normal=Z
        vec3 worldNormalZ = vec3(normalZ.x, normalZ.y, normalZ.z);
        
        // Flip X-projection to match world-space orientation
        worldNormalX.x = -worldNormalX.x;
        
        // Blend world-space normals based on surface orientation
        vec3 blendedNormal = worldNormalX * localBlendWeights.x + 
                     worldNormalY * localBlendWeights.y + 
                     worldNormalZ * localBlendWeights.z;

        // Apply normal strength (lerp between flat and full normal)
        float strength = u_LayerNormalStrength[i];
        blendedNormal = normalize(mix(vec3(0.0, 1.0, 0.0), blendedNormal, strength));

        // Blend with base geometry normal using Reoriented Normal Mapping
        vec3 baseNormal = normalize(v_Normal);
        vec3 n = normalize(vec3(
            baseNormal.xy * blendedNormal.z + blendedNormal.xy,
            baseNormal.z * blendedNormal.z
        ));

        accumColor += al * w;
        accumNormal += n * w;
        accumMetallic += u_LayerMetallic[i] * w;
        accumSmoothness += u_LayerSmoothness[i] * w;
        accumAlpha += layerAlpha * w;
        accumWeight += w;
    }

    // Safety check: prevent division by zero
    if (accumWeight < 0.0001)
    {
        accumWeight = 1.0;
        accumColor = vec3(1.0, 0.0, 1.0); // Magenta fallback if no layers accumulated
    }

    vec3 finalColor = accumColor / accumWeight;
    vec3 finalNormal = normalize(accumNormal);
    float finalMetallic = accumMetallic / accumWeight;
    float finalSmoothness = accumSmoothness / accumWeight;
    float finalAlpha = accumAlpha / accumWeight;

    // Safety check: if finalNormal is degenerate, use up vector
    if (length(finalNormal) < 0.1)
    {
        finalNormal = vec3(0.0, 1.0, 0.0);
    }

    // DEBUG: Visualize baseColor to check if it's valid
    // Uncomment to debug albedo issues
    // FragColor = vec4(finalColor, 1.0);
    // return;

    // Setup material properties for lighting calculations
    MaterialProperties material;
    material.baseColor = finalColor;
    material.normal = finalNormal;
    material.metallic = finalMetallic;
    material.roughness = 1.0 - finalSmoothness;
    material.F0 = mix(vec3(0.04), material.baseColor, material.metallic);

    // Calculate lighting
    vec3 V = normalize(uCameraPos - v_WorldPos);
    vec3 N = material.normal;

    // Accumulate lighting contributions
    vec3 Lo = vec3(0.0);

    // Directional light with shadow mapping (CSM-aware)
    vec3 dirLighting = calculateDirectionalLight(N, V, material);
    vec3 viewPos = v_WorldPos - uCameraPos;
    // Compute light vector used for bias calculation (direction FROM surface TO light)
    vec3 L = normalize(-uDirLightDirection);
    float shadowFactor = calculateShadowWithNL(v_WorldPos, viewPos, N, L);
    Lo += dirLighting * shadowFactor;

    // Point lights
    Lo += calculatePointLights(v_WorldPos, N, V, material);

    // Spot lights
    Lo += calculateSpotLights(v_WorldPos, N, V, material);

    // Ambient lighting with SSAO (only for opaque materials)
    vec3 ambient;
    if (u_TransparencyMode == 0) {
        // Opaque materials: apply SSAO
        ambient = calculateAmbientLightingWithSSAO(material, v_WorldPos, gl_FragCoord.xy, u_ScreenSize,
                                                   u_SSAOTexture, u_SSAOEnabled, u_SSAOStrength);
    } else {
        // Transparent materials: no SSAO
        ambient = calculateAmbientLighting(material, v_WorldPos);
    }

    vec3 shaded = ambient + Lo;

    // Apply fog
    shaded = processFog(shaded, v_WorldPos);

    // DEBUG: Removed magenta fallback - SSAO can legitimately make areas very dark
    // The previous debug code was triggering on valid SSAO occlusion

    // Debug face coloring
    if (u_DebugFaceColor == 1)
    {
        if (gl_FrontFacing)
            FragColor = vec4(1.0, 0.0, 0.0, 1.0);
        else
            FragColor = vec4(0.0, 0.0, 1.0, 1.0);
    }
    else
    {
        FragColor = vec4(shaded, finalAlpha);
    }
    
    // Write entity ID for object picking and selection outline
    outId = u_ObjectId;
}
