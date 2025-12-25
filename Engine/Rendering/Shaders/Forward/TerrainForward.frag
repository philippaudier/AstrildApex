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
uniform vec4 u_LayerTilingOffset[MAX_LAYERS];
uniform int u_LayerUseTriplanar[MAX_LAYERS];
uniform float u_LayerTriplanarScale[MAX_LAYERS];
uniform float u_LayerTriplanarBlend[MAX_LAYERS];
uniform vec4 u_LayerStylize[MAX_LAYERS];
uniform float u_LayerEmission[MAX_LAYERS];
uniform vec4 u_LayerHeightSlope[MAX_LAYERS];
uniform float u_LayerStrength[MAX_LAYERS];
uniform int u_LayerIsUnderwater[MAX_LAYERS];
uniform vec4 u_LayerUnderwaterParams[MAX_LAYERS];
uniform float u_LayerUnderwaterBlend[MAX_LAYERS];
uniform float u_LayerMetallic[MAX_LAYERS];
uniform float u_LayerSmoothness[MAX_LAYERS];
uniform vec4 u_LayerAlbedoColor[MAX_LAYERS];
uniform float u_LayerNormalStrength[MAX_LAYERS];
uniform int u_LayerTransparencyMode[MAX_LAYERS];
uniform int u_LayerCount;

uniform sampler2D u_AlbedoTex;
uniform sampler2D u_NormalTex;
uniform vec4 u_AlbedoColor;
uniform float u_Metallic;
uniform float u_Smoothness;
uniform int u_TransparencyMode;

uniform sampler2D u_SSAOTexture;
uniform int u_SSAOEnabled;
uniform float u_SSAOStrength;
uniform vec2 u_ScreenSize;

uniform sampler2D u_Splatmap[2];
uniform int u_UseSplatmap;
uniform int u_DebugFaceColor;

// Weather uniforms
uniform float u_RainIntensity;
uniform float u_SnowAccumulation;  // Snow accumulation amount (may exceed 1.0)
uniform float u_SnowIntensity;     // Snow intensity (for future calculations)
uniform float u_Wetness;

// Advanced snow parameters
uniform float u_SnowSlopeMin;
uniform float u_SnowSlopeMax;
uniform float u_SnowSparkle;
uniform float u_SnowDisplacement;

// Snow material textures
uniform sampler2D u_SnowAlbedoTex;
uniform sampler2D u_SnowNormalTex;
uniform sampler2D u_SnowMetallicRoughnessTex;
uniform vec4 u_SnowAlbedoColor;
uniform float u_SnowMetallic;
uniform float u_SnowRoughness;
uniform vec2 u_SnowTextureTiling;
uniform float u_SnowNormalStrength;

float computeSlopeNormalized(vec3 N)
{
    vec3 up = vec3(0.0, 1.0, 0.0);
    float dotp = dot(normalize(N), up);
    float slope = clamp(1.0 - dotp, 0.0, 1.0);
    return slope;
}

float inRangeSmooth(float v, float a, float b)
{
    if (b <= a)
    {
        float dist = abs(v - a);
        return step(dist, 0.01);
    }

    float rangeWidth = b - a;
    float blendWidth = rangeWidth * 0.1;

    float fadeIn = smoothstep(a - blendWidth, a, v);
    float fadeOut = 1.0 - smoothstep(b, b + blendWidth, v);

    return fadeIn * fadeOut;
}

// Calculate snow placement factor based on surface normal and slope constraints
// Returns 0.0-1.0 where 1.0 = full snow coverage
float calculateSnowPlacement(vec3 normal, float slopeMinDeg, float slopeMaxDeg)
{
    vec3 up = vec3(0, 1, 0);

    // Calculate angle between surface normal and up vector
    float dotProduct = dot(normalize(normal), up);

    // Convert dot product to angle in degrees
    // dotProduct = 1.0 -> 0deg (flat, upward-facing)
    // dotProduct = 0.0 -> 90deg (vertical)
    // dotProduct = -1.0 -> 180deg (downward-facing)
    float angleRad = acos(clamp(dotProduct, -1.0, 1.0));
    float angleDeg = angleRad * 57.29577951; // degrees = radians * (180 / PI)

    // Smooth transition at boundaries (5 degrees fade)
    float fadeWidth = 5.0;

    // Fade in from min angle (0.0 below min, 1.0 above min+fadeWidth)
    float fadeIn = smoothstep(slopeMinDeg, slopeMinDeg + fadeWidth, angleDeg);

    // Fade out at max angle (1.0 below max-fadeWidth, 0.0 above max)
    float fadeOut = 1.0 - smoothstep(slopeMaxDeg - fadeWidth, slopeMaxDeg, angleDeg);

    // Combine: fadeIn * fadeOut gives 1.0 within range, smooth transitions outside
    // FIXED: Was (1.0 - fadeIn) * fadeOut which inverted the logic
    return clamp(fadeIn * fadeOut, 0.0, 1.0);
}

// Calculate snow sparkle effect
// Uses random micro-facet normals + real directional light for realistic sparkles
float calculateSnowSparkle(vec3 worldPos, vec3 normal, vec3 viewDir, float sparkleIntensity)
{
    // Early out if no light or no sparkle intensity
    if (uDirLightIntensity <= 0.0 || sparkleIntensity < 0.01) return 0.0;

    // Generate random sparkle pattern
    vec2 p = worldPos.xz * 10.0;
    float random1 = fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
    float random2 = fract(sin(dot(p, vec2(39.346, 11.135))) * 22345.6789);
    float sparkleNoise = random1 * random2;

    // Only 30% of surface sparkles (like real ice crystals)
    if (sparkleNoise < 0.7) return 0.0;

    vec3 N = normalize(normal);
    vec3 V = normalize(viewDir);
    vec3 L = normalize(-uDirLightDirection); // Real directional light

    // Create random micro-facet normal for this sparkle crystal
    // Each ice crystal has a random orientation
    float theta = random1 * 6.28318; // Random angle around normal (0-360°)
    float phi = acos(1.0 - random2 * 0.5); // Random tilt (0-60°)

    vec3 tangent = normalize(cross(N, vec3(0.0, 1.0, 0.0)));
    if (length(tangent) < 0.1) tangent = normalize(cross(N, vec3(1.0, 0.0, 0.0)));
    vec3 bitangent = normalize(cross(N, tangent));

    vec3 microNormal = cos(phi) * N + sin(phi) * (cos(theta) * tangent + sin(theta) * bitangent);
    microNormal = normalize(microNormal);

    // Calculate specular reflection from the light towards the view
    // This is the classic Blinn-Phong but with micro-facets
    vec3 H = normalize(L + V); // Halfway vector between light and view
    float NdotH = max(0.0, dot(microNormal, H));
    float specular = pow(NdotH, 512.0); // Very sharp, like real ice crystal reflections

    // Only sparkle if the micro-facet faces both the light and the viewer
    float NdotL = max(0.0, dot(microNormal, L));
    float NdotV = max(0.0, dot(microNormal, V));
    if (NdotL < 0.1 || NdotV < 0.1) return 0.0; // Crystal not oriented correctly

    // Normalize sparkle noise to 0-1 range
    float sparkleAmount = (sparkleNoise - 0.7) / 0.3;

    // Final sparkle = specular * random pattern * light intensity * sparkle intensity
    // Modulated by light color (sparkles take the color of the light)
    float luminance = dot(uDirLightColor, vec3(0.299, 0.587, 0.114));
    float sparkle = specular * sparkleAmount * sparkleIntensity * uDirLightIntensity * luminance * 8.0;

    return sparkle;
}

void main()
{
    // Clipping plane for water reflections
    if (uClipPlaneEnabled > 0.5) {
        float distance = dot(uClipPlane.xyz, v_WorldPos) + uClipPlane.w;
        if (distance < 0.0) {
            discard;
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

    vec3 fallbackColor = vec3(1.0, 0.5, 0.0);

    if (u_LayerCount <= 0)
    {
        FragColor = vec4(fallbackColor, 1.0);
        outId = u_ObjectId;
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
        for (int i = 0; i < u_LayerCount; i++)
        {
            float weight = 0.0;

            if (u_LayerIsUnderwater[i] == 1)
            {
                float waterLevel = u_LayerUnderwaterParams[i].x;
                float blendDist = u_LayerUnderwaterParams[i].y;
                float slopeMinNorm = u_LayerUnderwaterParams[i].z;
                float slopeMaxNorm = u_LayerUnderwaterParams[i].w;

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

                float slopeWeight = inRangeSmooth(slope, slopeMinNorm, slopeMaxNorm);
                weight = heightWeight * slopeWeight;
            }
            else
            {
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
    for (int i = 0; i < u_LayerCount; i++)
    {
        if (u_LayerIsUnderwater[i] == 1 && weights[i] > 0.001)
        {
            float blendMode = u_LayerUnderwaterBlend[i];
            if (blendMode < 0.999)
            {
                float suppressFactor = 1.0 - blendMode;
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

    // Normalize weights
    float total = 0.0;
    for (int i = 0; i < u_LayerCount; i++) total += weights[i];
    if (total <= 0.0001)
    {
        FragColor = vec4(fallbackColor, 1.0);
        outId = u_ObjectId;
        return;
    }
    for (int i = 0; i < u_LayerCount; i++) weights[i] /= total;

    // Sample layers
    for (int i = 0; i < u_LayerCount; i++)
    {
        float w = weights[i];
        if (w <= 0.0001) continue;

        vec2 tilingOffset = u_LayerTilingOffset[i].xy;
        vec2 offset = u_LayerTilingOffset[i].zw;

        vec3 al;
        if (u_LayerUseTriplanar[i] == 1)
        {
            float triScale = u_LayerTriplanarScale[i];
            float triBlend = u_LayerTriplanarBlend[i];

            vec3 blendWeights = abs(normalize(v_Normal));
            blendWeights = pow(blendWeights, vec3(triBlend));
            blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z);

            vec2 uvX = v_WorldPos.yz * triScale;
            vec2 uvY = v_WorldPos.xz * triScale;
            vec2 uvZ = v_WorldPos.xy * triScale;

            vec3 albedoX = texture(u_LayerAlbedo[i], uvX).rgb;
            vec3 albedoY = texture(u_LayerAlbedo[i], uvY).rgb;
            vec3 albedoZ = texture(u_LayerAlbedo[i], uvZ).rgb;

            al = albedoX * blendWeights.x + albedoY * blendWeights.y + albedoZ * blendWeights.z;
        }
        else
        {
            vec2 uv = v_TexCoord * tilingOffset + offset;
            al = texture(u_LayerAlbedo[i], uv).rgb;
        }

        al *= u_LayerAlbedoColor[i].rgb;
        float layerAlpha = u_LayerAlbedoColor[i].a;

        // Stylization
        vec4 styl = u_LayerStylize[i];
        float lum = dot(al, vec3(0.3, 0.59, 0.11));
        al = mix(vec3(lum), al, styl.x);
        al *= styl.y;
        al = (al - 0.5) * styl.z + 0.5;

        // Hue shift
        if (abs(styl.w) > 1e-6)
        {
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

            H = fract(H + styl.w);

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

        // Normal mapping
        vec3 normalMap;
        if (u_LayerUseTriplanar[i] == 1)
        {
            float triScale = u_LayerTriplanarScale[i];
            float triBlend = u_LayerTriplanarBlend[i];

            vec3 blendWeights = abs(normalize(v_Normal));
            blendWeights = pow(blendWeights, vec3(triBlend));
            blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z);

            vec2 uvX = v_WorldPos.yz * triScale;
            vec2 uvY = v_WorldPos.xz * triScale;
            vec2 uvZ = v_WorldPos.xy * triScale;

            vec3 nX = texture(u_LayerNormal[i], uvX).xyz * 2.0 - 1.0;
            vec3 nY = texture(u_LayerNormal[i], uvY).xyz * 2.0 - 1.0;
            vec3 nZ = texture(u_LayerNormal[i], uvZ).xyz * 2.0 - 1.0;

            nX.y = -nX.y; nY.y = -nY.y; nZ.y = -nZ.y;

            vec3 worldNormalX = vec3(nX.z, nX.x, nX.y);
            vec3 worldNormalY = vec3(nY.x, nY.z, nY.y);
            vec3 worldNormalZ = vec3(nZ.x, nZ.y, nZ.z);
            worldNormalX.x = -worldNormalX.x;

            normalMap = worldNormalX * blendWeights.x + worldNormalY * blendWeights.y + worldNormalZ * blendWeights.z;
        }
        else
        {
            vec2 uv = v_TexCoord * tilingOffset + offset;
            normalMap = texture(u_LayerNormal[i], uv).xyz * 2.0 - 1.0;
            normalMap.y = -normalMap.y;
        }

        float strength = u_LayerNormalStrength[i];
        vec3 n = normalize(v_Normal + normalMap * strength);

        accumColor += al * w;
        accumNormal += n * w;
        accumMetallic += u_LayerMetallic[i] * w;
        accumSmoothness += u_LayerSmoothness[i] * w;
        accumAlpha += layerAlpha * w;
        accumWeight += w;
    }

    if (accumWeight < 0.0001)
    {
        accumWeight = 1.0;
        accumColor = vec3(1.0, 0.0, 1.0);
    }

    vec3 finalColor = accumColor / accumWeight;
    vec3 finalNormal = normalize(accumNormal);
    float finalMetallic = accumMetallic / accumWeight;
    float finalSmoothness = accumSmoothness / accumWeight;
    float finalAlpha = accumAlpha / accumWeight;

    if (length(finalNormal) < 0.1)
    {
        finalNormal = vec3(0.0, 1.0, 0.0);
    }

    // Setup material
    MaterialProperties material;
    material.baseColor = finalColor;
    material.normal = finalNormal;
    material.metallic = finalMetallic;
    material.roughness = 1.0 - finalSmoothness;

    // === ENHANCED SNOW SYSTEM ===
    if (u_SnowAccumulation > 0.0)
    {
        // Calculate snow placement based on surface angle
        float snowPlacement = calculateSnowPlacement(material.normal, u_SnowSlopeMin, u_SnowSlopeMax);

        // Final snow amount = accumulation * placement (NOT clamped - can exceed 1.0)
        float snowAmount = u_SnowAccumulation * snowPlacement;

        if (snowAmount > 0.01)
        {
            // Sample snow material textures with tiling
            vec2 snowUV = v_WorldPos.xz * u_SnowTextureTiling;
            vec3 snowAlbedo = texture(u_SnowAlbedoTex, snowUV).rgb * u_SnowAlbedoColor.rgb;
            vec3 snowNormalMap = texture(u_SnowNormalTex, snowUV).rgb * 2.0 - 1.0; // Unpack normal map
            vec2 snowMetallicRoughness = texture(u_SnowMetallicRoughnessTex, snowUV).rg;

            float snowMetallic = snowMetallicRoughness.r * u_SnowMetallic;
            float snowRoughness = snowMetallicRoughness.g * u_SnowRoughness;

            // Blend snow normal with terrain normal using proper normal blending
            // Scale by NormalStrength parameter for artist control
            vec3 snowNormal = normalize(material.normal + snowNormalMap * u_SnowNormalStrength);

            // Calculate sparkle effect (more sparkle with thick snow)
            vec3 V = normalize(uCameraPos - v_WorldPos);
            float sparkle = calculateSnowSparkle(v_WorldPos, snowNormal, V, u_SnowSparkle);
            sparkle *= min(snowAmount, 1.0); // Sparkle saturates at accumulation = 1.0

            // Add sparkle to snow albedo (brightens snow based on viewing angle)
            snowAlbedo += vec3(sparkle * 0.5);

            // Soft saturation for blending (accumulation can exceed 1.0 but blend saturates smoothly)
            // Use a curve that reaches near-white at high accumulation
            float blendFactor = 1.0 - exp(-snowAmount * 1.5); // Exponential saturation

            // Blend snow with underlying surface
            material.baseColor = mix(material.baseColor, snowAlbedo, blendFactor);
            material.normal = mix(material.normal, snowNormal, blendFactor);
            material.roughness = mix(material.roughness, snowRoughness, blendFactor);
            material.metallic = mix(material.metallic, snowMetallic, blendFactor);
        }
    }

    // Rain wetness (makes surfaces darker and more reflective)
    if (u_Wetness > 0.0) {
        // Darken surfaces when wet
        float darken = 1.0 - (u_Wetness * 0.2);
        material.baseColor *= darken;
        // Increase smoothness (reduce roughness) when wet
        material.roughness = mix(material.roughness, material.roughness * 0.5, u_Wetness);
    }

    material.F0 = mix(vec3(0.04), material.baseColor, material.metallic);

    // Lighting
    vec3 V = normalize(uCameraPos - v_WorldPos);
    vec3 N = material.normal;
    vec3 Lo = vec3(0.0);

    // Directional light with shadows
    vec3 dirLighting = calculateDirectionalLight(N, V, material);
    vec3 viewPos = v_WorldPos - uCameraPos;
    vec3 L = normalize(-uDirLightDirection);
    float shadowFactor = calculateShadowWithNL(v_WorldPos, viewPos, N, L);
    Lo += dirLighting * shadowFactor;

    // Point lights
    Lo += calculatePointLights(v_WorldPos, N, V, material);

    // Spot lights
    Lo += calculateSpotLights(v_WorldPos, N, V, material);

    // Ambient lighting with SSAO
    vec3 ambient;
    if (u_TransparencyMode == 0) {
        ambient = calculateAmbientLightingWithSSAO(material, v_WorldPos, gl_FragCoord.xy, u_ScreenSize,
                                                   u_SSAOTexture, u_SSAOEnabled, u_SSAOStrength);
    } else {
        ambient = calculateAmbientLighting(material, v_WorldPos);
    }

    // CRITICAL FIX: Apply shadows to ambient IBL too!
    // Shadowed areas receive less indirect light from the sky
    // mix(0.3, 1.0, shadowFactor) means: 30% ambient in full shadow, 100% in full light
    ambient *= mix(0.3, 1.0, shadowFactor);

    vec3 shaded = ambient + Lo;

    // Apply fog
    shaded = processFog(shaded, v_WorldPos);

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

    outId = u_ObjectId;
}
