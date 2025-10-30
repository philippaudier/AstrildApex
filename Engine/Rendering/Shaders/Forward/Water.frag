#version 330 core
layout(location=0) out vec4 outColor;
layout(location=1) out uint outId;

in vec3 vWorldPos;
in vec3 vNormal;
in vec2 vUV;

// ============================================================================
// Global uniforms
// ============================================================================
layout(std140) uniform Global {
    mat4 uView;
    mat4 uProj;
    mat4 uViewProj;
    vec3 uCameraPos; float _pad1;

    vec3 uDirLightDirection; float _pad2;
    vec3 uDirLightColor; float uDirLightIntensity;

    int uPointLightCount; float _pad3; float _pad4; float _pad5;
    vec4 uPointLightPos0; vec4 uPointLightColor0;
    vec4 uPointLightPos1; vec4 uPointLightColor1;
    vec4 uPointLightPos2; vec4 uPointLightColor2;
    vec4 uPointLightPos3; vec4 uPointLightColor3;

    int uSpotLightCount; float _pad6; float _pad7; float _pad8;
    vec4 uSpotLightPos0; vec4 uSpotLightDir0; vec4 uSpotLightColor0; float uSpotLightAngle0; float uSpotLightInnerAngle0; float _pad9; float _pad10;
    vec4 uSpotLightPos1; vec4 uSpotLightDir1; vec4 uSpotLightColor1; float uSpotLightAngle1; float uSpotLightInnerAngle1; float _pad11; float _pad12;

    vec3 uAmbientColor; float uAmbientIntensity;
    vec3 uSkyboxTint; float uSkyboxExposure;

    int uFogEnabled; float _pad13; float _pad14; float _pad15;
    vec3 uFogColor; float uFogStart;
    float uFogEnd; vec3 _pad16;

    int uClipPlaneEnabled; float _pad17; float _pad18; float _pad19;
    vec4 uClipPlane;
};

// ============================================================================
// Water material properties
// ============================================================================
uniform vec4 u_WaterColor;      // Water base color (RGBA)
uniform float u_Opacity;        // Opacity (0-1)
uniform float u_Metallic;       // Metallic (0-1)
uniform float u_Smoothness;     // Smoothness (0-1)

// Albedo texture (optional)
uniform sampler2D u_AlbedoTex;
uniform vec4 u_AlbedoColor;
uniform int u_HasAlbedoTex;

// Transparency mode
uniform int u_TransparencyMode; // 0 = opaque, 1 = transparent

// Planar Reflection
uniform int u_EnableReflection;
uniform sampler2D u_ReflectionTexture;
uniform float u_ReflectionStrength;
uniform float u_ReflectionBlur;

// Fresnel
uniform float u_FresnelPower;
uniform vec4 u_FresnelColor;

// Screen size for proper reflection coordinate calculation
uniform vec2 u_ScreenSize;

// Object ID
uniform uint u_ObjectId;

// ============================================================================
// PBR Functions
// ============================================================================
vec3 FresnelSchlick(float cosTheta, vec3 F0) {
    return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
}

vec3 CalculatePBR(vec3 N, vec3 V, vec3 L, vec3 lightColor, float lightIntensity, vec3 baseCol, float rough, float metal, vec3 F0) {
    vec3 H = normalize(V + L);
    float NdotL = max(dot(N, L), 0.001);
    float NdotV = max(dot(N, V), 0.001);
    float NdotH = max(dot(N, H), 0.0);
    float VdotH = max(dot(V, H), 0.0);

    // GGX/Trowbridge-Reitz NDF
    float a = rough * rough;
    float a2 = a * a;
    float denom = NdotH * NdotH * (a2 - 1.0) + 1.0;
    float D = a2 / (3.14159 * denom * denom);

    // Smith's geometry function
    float k = (rough + 1.0) * (rough + 1.0) / 8.0;
    float Gv = NdotV / (NdotV * (1.0 - k) + k);
    float Gl = NdotL / (NdotL * (1.0 - k) + k);
    float G = Gv * Gl;

    // Fresnel
    vec3 F = FresnelSchlick(VdotH, F0);

    // Cook-Torrance BRDF
    vec3 spec = (D * G * F) / max(4.0 * NdotL * NdotV, 0.001);
    vec3 kd = (1.0 - F) * (1.0 - metal);
    vec3 diffuse = kd * baseCol / 3.14159;

    return (diffuse + spec) * lightColor * lightIntensity * NdotL;
}

// ============================================================================
// Main
// ============================================================================
void main(){
    // Clipping plane support (for reflections)
    if (uClipPlaneEnabled == 1) {
        float distance = dot(uClipPlane.xyz, vWorldPos) + uClipPlane.w;
        if (distance < 0.0) {
            discard;
        }
    }

    // Base color: use albedo texture if available, otherwise water color
    vec3 baseCol;
    if (u_HasAlbedoTex == 1) {
        baseCol = texture(u_AlbedoTex, vUV).rgb * u_AlbedoColor.rgb;
    } else {
        baseCol = u_WaterColor.rgb;
    }

    // Normal (simple flat normal for now, can be enhanced with normal maps later)
    vec3 N = normalize(vNormal);
    vec3 V = normalize(uCameraPos - vWorldPos);

    // PBR parameters
    float smoothness = clamp(u_Smoothness, 0.0, 1.0);
    float rough = clamp(1.0 - smoothness, 0.01, 0.99);
    float metal = clamp(u_Metallic, 0.0, 1.0);
    vec3 F0 = mix(vec3(0.04), baseCol, metal);

    // Lighting accumulation
    vec3 Lo = vec3(0.0);

    // Directional light
    if (uDirLightIntensity > 0.0) {
        vec3 L = normalize(-uDirLightDirection);
        Lo += CalculatePBR(N, V, L, uDirLightColor, uDirLightIntensity, baseCol, rough, metal, F0);
    }

    // Point lights
    vec4 pointPos[4] = vec4[4](uPointLightPos0, uPointLightPos1, uPointLightPos2, uPointLightPos3);
    vec4 pointColor[4] = vec4[4](uPointLightColor0, uPointLightColor1, uPointLightColor2, uPointLightColor3);

    for (int i = 0; i < min(uPointLightCount, 4); i++) {
        vec3 lightPos = pointPos[i].xyz;
        float range = pointPos[i].w;
        vec3 lightColor = pointColor[i].rgb;
        float intensity = pointColor[i].a;

        vec3 L = lightPos - vWorldPos;
        float distance = length(L);
        L = normalize(L);

        float attenuation = 1.0 / (1.0 + 0.09 * distance + 0.032 * distance * distance);
        if (range > 0.0) {
            float falloff = clamp(1.0 - distance / range, 0.0, 1.0);
            attenuation *= falloff * falloff;
        }

        Lo += CalculatePBR(N, V, L, lightColor, intensity * attenuation, baseCol, rough, metal, F0);
    }

    // Spot lights
    vec4 spotPos[2] = vec4[2](uSpotLightPos0, uSpotLightPos1);
    vec4 spotDir[2] = vec4[2](uSpotLightDir0, uSpotLightDir1);
    vec4 spotColor[2] = vec4[2](uSpotLightColor0, uSpotLightColor1);
    float spotAngle[2] = float[2](uSpotLightAngle0, uSpotLightAngle1);
    float spotInnerAngle[2] = float[2](uSpotLightInnerAngle0, uSpotLightInnerAngle1);

    for (int i = 0; i < min(uSpotLightCount, 2); i++) {
        vec3 lightPos = spotPos[i].xyz;
        float range = spotPos[i].w;
        vec3 lightDir = normalize(spotDir[i].xyz);
        vec3 lightColor = spotColor[i].rgb;
        float intensity = spotColor[i].a;
        float outerAngle = spotAngle[i];
        float innerAngle = spotInnerAngle[i];

        vec3 L = lightPos - vWorldPos;
        float distance = length(L);
        L = normalize(L);

        float cosOuterAngle = cos(radians(outerAngle * 0.5));
        float cosInnerAngle = cos(radians(innerAngle * 0.5));
        float spotEffect = dot(-L, lightDir);

        if (spotEffect > cosOuterAngle) {
            float spotAttenuation = 1.0;
            if (spotEffect < cosInnerAngle) {
                float falloffRange = cosInnerAngle - cosOuterAngle;
                float falloffFactor = (spotEffect - cosOuterAngle) / falloffRange;
                spotAttenuation = smoothstep(0.0, 1.0, falloffFactor);
            }

            float attenuation = 1.0 / (1.0 + 0.09 * distance + 0.032 * distance * distance);
            if (range > 0.0) {
                float rangeFalloff = clamp(1.0 - distance / range, 0.0, 1.0);
                attenuation *= rangeFalloff * rangeFalloff;
            }

            Lo += CalculatePBR(N, V, L, lightColor, intensity * attenuation * spotAttenuation, baseCol, rough, metal, F0);
        }
    }

    // Ambient lighting
    vec3 ambient = baseCol * uAmbientColor * uAmbientIntensity * (1.0 - metal * 0.5);

    // Combine lighting
    vec3 col = ambient + Lo;

    // ============================================================================
    // Planar Reflection
    // ============================================================================
    if (u_EnableReflection == 1) {
        // Calculate screen-space coordinates using the main viewport size
        // gl_FragCoord is in pixel coordinates of the main viewport
        vec2 screenCoord = gl_FragCoord.xy / u_ScreenSize;

        // Flip both X and Y coordinates for proper mirror effect
        // Y flip: Mountains at the top should appear at the bottom in the reflection
        // X flip: Left side should appear on the left (not swapped to right)
        screenCoord.x = 1.0 - screenCoord.x;
        screenCoord.y = 1.0 - screenCoord.y;

        // Sample reflection texture (it will automatically scale to the reflection texture resolution)
        vec3 reflectionColor = texture(u_ReflectionTexture, screenCoord).rgb;

        // Calculate Fresnel effect (view-dependent reflection)
        // For water, we want strong reflections even at steep angles
        float NdotV = max(dot(N, V), 0.0);
        float fresnel = pow(1.0 - NdotV, u_FresnelPower);

        // Bias fresnel to have more visible reflections (add base reflection amount)
        // This ensures reflections are visible from all angles, not just grazing angles
        fresnel = mix(0.3, 1.0, fresnel); // Base 30% reflection, up to 100% at edges

        // Mix fresnel with fresnel color
        vec3 fresnelTint = mix(vec3(1.0), u_FresnelColor.rgb, u_FresnelColor.a);

        // Apply reflection with fresnel and strength
        float reflectionAmount = fresnel * u_ReflectionStrength;
        col = mix(col, reflectionColor * fresnelTint, reflectionAmount);
    }

    // Fog
    if (uFogEnabled != 0) {
        float dist = length(uCameraPos - vWorldPos);
        float fogFactor = clamp((uFogEnd - dist) / max(1e-5, (uFogEnd - uFogStart)), 0.0, 1.0);
        col = mix(uFogColor, col, fogFactor);
    }

    // Alpha/transparency
    float outAlpha = 1.0;
    if (u_TransparencyMode != 0) {
        // Use opacity from material
        outAlpha = clamp(u_Opacity, 0.0, 1.0);

        // If using albedo texture, multiply by texture alpha
        if (u_HasAlbedoTex == 1) {
            float texAlpha = texture(u_AlbedoTex, vUV).a;
            outAlpha *= texAlpha * u_AlbedoColor.a;
        } else {
            // Use water color alpha
            outAlpha *= u_WaterColor.a;
        }
    }

    outColor = vec4(col, outAlpha);
    outId = u_ObjectId;
}
