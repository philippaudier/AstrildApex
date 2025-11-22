// IBL.glsl - Image Based Lighting helpers
// Uses HDR cubemap with mipmaps as an approximation for prefiltered environment map.
// Requires the application to provide:
//   uniform samplerCube u_IrradianceMap;       // RGB16F cubemap (base HDR env with mipmaps)
//   uniform samplerCube u_PrefilteredEnvMap;   // (can be same as u_IrradianceMap) used for specular LOD sampling
//   uniform sampler2D  u_BRDFLUT;               // optional BRDF integration LUT (RG)
//   uniform int       u_HasIBL;                // 0 = disabled, 1 = enabled
//   uniform float     u_PrefilterMaxLod;       // max mip level for prefiltered env map

uniform samplerCube u_IrradianceMap;
uniform samplerCube u_PrefilteredEnvMap;
uniform sampler2D  u_BRDFLUT;
uniform int u_HasIBL;
uniform float u_PrefilterMaxLod;

// Sample diffuse irradiance from dedicated irradiance cubemap (32x32, single mip level)
vec3 sampleIrradiance(vec3 N)
{
    if (u_HasIBL == 0) return vec3(0.0);

    // CRITICAL FIX: Irradiance map is pre-convolved 32x32 cubemap with NO mipmaps
    // Sample at LOD 0 directly using texture() or textureLod(..., 0.0)
    // The irradiance map is already blurred/convolved for diffuse lighting
    vec3 irr = texture(u_IrradianceMap, N).rgb;

    // Apply environment tint/exposure from Global UBO so editor sky tint/exposure affect IBL
    irr *= uSkyboxTint * uSkyboxExposure;

    return irr;
}

// Sample prefiltered specular environment map using reflection vector R and roughness
vec3 samplePrefilteredEnv(vec3 R, float roughness)
{
    if (u_HasIBL == 0) return vec3(0.0);
    // Map roughness [0,1] to mip levels [0, maxLod]
    // Avoid negative values and clamp to available range so we never sample invalid LODs
    float maxMapLod = max(0.0, u_PrefilterMaxLod);

    // Map roughness [0,1] to mip levels [0, maxMapLod]
    float mip = clamp(roughness * maxMapLod, 0.0, maxMapLod);

    // Manually blend between two mip levels to guarantee smooth transitions
    // (some drivers may not trilinearly interpolate when using explicit textureLod)
    float mipFloor = floor(mip);
    float mipFrac = mip - mipFloor;

    float mipHigh = min(mipFloor + 1.0, maxMapLod);

    vec3 preLo = textureLod(u_PrefilteredEnvMap, R, mipFloor).rgb;
    vec3 preHi = textureLod(u_PrefilteredEnvMap, R, mipHigh).rgb;

    vec3 pre = mix(preLo, preHi, mipFrac);

    // Apply sky tint/exposure to prefiltered environment as well (affects specular IBL)
    pre *= uSkyboxTint * uSkyboxExposure;

    return pre;
}

// Simple BRDF approximation using either LUT or analytic fallback
vec2 integrateBRDF(float NdotV, float roughness)
{
    // If LUT provided, sample it; otherwise use a cheap analytic approximation
    // Note: Detecting presence of a sampler in GLSL is not straightforward; assume LUT may be bound.
    // Try to sample; if not provided it will often be white neutral.
    vec2 lut = texture(u_BRDFLUT, vec2(NdotV, roughness)).rg;
    // If LUT is (0,0) fallback to rough analytic approx to avoid complete black
    if (lut.x == 0.0 && lut.y == 0.0)
    {
        float a = roughness * (1.0 - NdotV) + NdotV;
        float b = roughness * 0.5;
        return vec2(a, b);
    }
    return lut;
}

// Main IBL helper that returns added ambient contribution (diffuse + specular)
// Note: This returns the IBL contribution WITHOUT baseColor multiplication
// The baseColor should be applied by the caller for diffuse part
vec3 calculateIBL(vec3 N, vec3 V, float roughness, vec3 F0, vec3 baseColor, float metallic)
{
    if (u_HasIBL == 0) return vec3(0.0);

    // Diffuse part (Irradiance)
    vec3 irradiance = sampleIrradiance(N);

    // Energy-conserving diffuse: metals have NO diffuse component
    // kd represents the amount of light that goes into the surface (not reflected)
    vec3 kd = (vec3(1.0) - F0) * (1.0 - metallic);
    vec3 diffuse = irradiance * baseColor * kd;

    // Specular part
    vec3 R = reflect(-V, N);
    float NdotV = clamp(dot(N, V), 0.0, 1.0);
    vec3 prefiltered = samplePrefilteredEnv(R, roughness);
    vec2 brdf = integrateBRDF(NdotV, roughness);

    // PBR specular calculation
    // Standard split-sum: specular = prefiltered * (F0 * brdf.x + brdf.y)
    vec3 specular = prefiltered * (F0 * brdf.x + brdf.y);

    // CRITICAL FIX: For non-metals at high roughness, specular should fade to ZERO
    // Calculate average F0 to detect metal vs dielectric
    float avgF0 = (F0.r + F0.g + F0.b) / 3.0;

    // Metallic factor: 0.0 for dielectrics (F0~=0.04), 1.0 for metals (F0>=0.5)
    float metallicFactor = saturate((avgF0 - 0.04) / 0.46); // Maps 0.04->0.0, 0.5->1.0

    // Smoothness factor: 0.0 at roughness=1, 1.0 at roughness=0
    float smoothnessFactor = 1.0 - roughness;

    // For dielectrics, fade specular with roughness
    // For metals, keep specular even at high roughness (they always reflect)
    float specularFade = mix(smoothnessFactor, 1.0, metallicFactor);

    specular *= specularFade;

    return diffuse + specular;
}
