// Lighting.glsl - PBR lighting calculations and light processing

// Fresnel-Schlick approximation (with clamping)
vec3 fresnelSchlick(float cosTheta, vec3 F0) {
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

// GGX/Trowbridge-Reitz normal distribution function
float distributionGGX(float NdotH, float roughness) {
    float a = roughness * roughness;
    float a2 = a * a;
    float NdotH2 = NdotH * NdotH;
    float denom = NdotH2 * (a2 - 1.0) + 1.0;
    denom = PI * denom * denom;
    return a2 / max(denom, 1e-7);
}

// Smith-GGX geometry using Schlick-GGX per-direction term
float geometrySchlickGGX(float NdotV, float roughness) {
    float r = roughness + 1.0;
    float k = (r * r) / 8.0;
    return NdotV / (NdotV * (1.0 - k) + k);
}

float geometrySmith(float NdotV, float NdotL, float roughness) {
    float ggxV = geometrySchlickGGX(NdotV, roughness);
    float ggxL = geometrySchlickGGX(NdotL, roughness);
    return ggxV * ggxL;
}

// Core PBR lighting calculation
vec3 calculatePBRLighting(vec3 N, vec3 V, vec3 L, vec3 lightColor, float lightIntensity,
                         MaterialProperties material) {
    vec3 H = normalize(V + L);

    // Use looser clamps to avoid hard artifacts; handle small denominators explicitly
    float NdotL = max(dot(N, L), 0.0);
    float NdotV = max(dot(N, V), 0.0);
    float NdotH = max(dot(N, H), 0.0);
    float HdotV = max(dot(H, V), 0.0);

    // Early out if not lit
    if (NdotL <= 0.0) return vec3(0.0);

    // Calculate PBR terms
    float D = distributionGGX(NdotH, material.roughness);
    float G = geometrySmith(NdotV, NdotL, material.roughness);
    vec3 F = fresnelSchlick(HdotV, material.F0);

    // Specular with epsilon to avoid division issues
    vec3 numerator = D * G * F;
    float denom = 4.0 * max(NdotV, 1e-4) * max(NdotL, 1e-4);
    vec3 spec = numerator / denom;

    // Energy-conserving diffuse
    vec3 kS = F;
    vec3 kD = (vec3(1.0) - kS) * (1.0 - material.metallic);
    vec3 diffuse = kD * material.baseColor / PI;

    return (diffuse + spec) * lightColor * lightIntensity * NdotL;
}

// Directional light calculation
vec3 calculateDirectionalLight(vec3 N, vec3 V, MaterialProperties material) {
    if (uDirLightIntensity <= 0.0) return vec3(0.0);

    vec3 L = normalize(-uDirLightDirection);
    return calculatePBRLighting(N, V, L, uDirLightColor, uDirLightIntensity, material);
}

// Point light calculation with distance attenuation
vec3 calculatePointLight(vec3 worldPos, vec3 N, vec3 V, MaterialProperties material,
                        vec3 lightPos, float range, vec3 lightColor, float intensity) {
    vec3 L = lightPos - worldPos;
    float distance = length(L);
    L /= distance; // normalize

    // Physically plausible attenuation with smooth falloff near range
    float attenuation = 1.0 / (distance * distance + 1.0);
    if (range > 0.0) {
        float distanceRatio = distance / range;
        float windowFunction = pow(saturate(1.0 - pow(distanceRatio, 4.0)), 2.0);
        attenuation *= windowFunction;
    }

    return calculatePBRLighting(N, V, L, lightColor, intensity * attenuation, material);
}

// Spot light calculation with cone attenuation
vec3 calculateSpotLight(vec3 worldPos, vec3 N, vec3 V, MaterialProperties material,
                       vec3 lightPos, float range, vec3 lightDir, vec3 lightColor, float intensity,
                       float outerAngle, float innerAngle) {
    vec3 L = lightPos - worldPos;
    float distance = length(L);
    L = normalize(L);

    // Check if point is within spotlight cone
    float cosOuterAngle = cos(radians(outerAngle * 0.5));
    float cosInnerAngle = cos(radians(innerAngle * 0.5));
    float spotEffect = dot(-L, normalize(lightDir));

    if (spotEffect <= cosOuterAngle) return vec3(0.0);

    // Calculate spot attenuation
    float spotAttenuation = 1.0;
    if (spotEffect < cosInnerAngle) {
        float falloffRange = cosInnerAngle - cosOuterAngle;
        float falloffFactor = (spotEffect - cosOuterAngle) / falloffRange;
        spotAttenuation = smoothstep(0.0, 1.0, falloffFactor);
    }

    // Calculate distance attenuation
    float attenuation = 1.0 / (1.0 + 0.09 * distance + 0.032 * distance * distance);
    if (range > 0.0) {
        float rangeFalloff = saturate(1.0 - distance / range);
        attenuation *= rangeFalloff * rangeFalloff;
    }

    return calculatePBRLighting(N, V, L, lightColor, intensity * attenuation * spotAttenuation, material);
}

// Process all point lights
vec3 calculatePointLights(vec3 worldPos, vec3 N, vec3 V, MaterialProperties material) {
    vec3 result = vec3(0.0);

    vec4 pointPos[4] = vec4[4](uPointLightPos0, uPointLightPos1, uPointLightPos2, uPointLightPos3);
    vec4 pointColor[4] = vec4[4](uPointLightColor0, uPointLightColor1, uPointLightColor2, uPointLightColor3);

    for (int i = 0; i < min(uPointLightCount, 4); i++) {
        vec3 lightPos = pointPos[i].xyz;
        float range = pointPos[i].w;
        vec3 lightColor = pointColor[i].rgb;
        float intensity = pointColor[i].a;

        result += calculatePointLight(worldPos, N, V, material, lightPos, range, lightColor, intensity);
    }

    return result;
}

// Process all spot lights
vec3 calculateSpotLights(vec3 worldPos, vec3 N, vec3 V, MaterialProperties material) {
    vec3 result = vec3(0.0);

    vec4 spotPos[2] = vec4[2](uSpotLightPos0, uSpotLightPos1);
    vec4 spotDir[2] = vec4[2](uSpotLightDir0, uSpotLightDir1);
    vec4 spotColor[2] = vec4[2](uSpotLightColor0, uSpotLightColor1);
    float spotAngle[2] = float[2](uSpotLightAngle0, uSpotLightAngle1);
    float spotInnerAngle[2] = float[2](uSpotLightInnerAngle0, uSpotLightInnerAngle1);

    for (int i = 0; i < min(uSpotLightCount, 2); i++) {
        vec3 lightPos = spotPos[i].xyz;
        float range = spotPos[i].w;
        vec3 lightDir = spotDir[i].xyz;
        vec3 lightColor = spotColor[i].rgb;
        float intensity = spotColor[i].a;
        float outerAngle = spotAngle[i];
        float innerAngle = spotInnerAngle[i];

        result += calculateSpotLight(worldPos, N, V, material, lightPos, range, lightDir,
                                   lightColor, intensity, outerAngle, innerAngle);
    }

    return result;
}

// Calculate ambient lighting contribution
// Note: This function needs world position which is not in MaterialProperties
vec3 calculateAmbientLighting(MaterialProperties material, vec3 worldPos) {
    // Use IBL if available, otherwise fallback to uniform ambient color
    vec3 ambient;
    if (u_HasIBL != 0) {
        // Use full IBL calculation including diffuse + specular (metallic/roughness aware)
        vec3 V = normalize(uCameraPos - worldPos);

        // material.F0 is already calculated, but recalculate for consistency
        vec3 F0 = mix(vec3(0.04), material.baseColor, material.metallic);

        // calculateIBL returns proper diffuse + specular based on material properties
        ambient = calculateIBL(material.normal, V, material.roughness, F0, material.baseColor) * uAmbientIntensity;
    } else {
        // Fallback to uniform ambient color
        ambient = material.baseColor * uAmbientColor * uAmbientIntensity * (1.0 - material.metallic * 0.5);
    }
    return ambient;
}

// Calculate ambient lighting with SSAO
vec3 calculateAmbientLightingWithSSAO(MaterialProperties material, vec3 worldPos, vec2 screenCoord, vec2 screenSize,
                                     sampler2D ssaoTexture, int ssaoEnabled, float ssaoStrength) {
    vec3 ambient = calculateAmbientLighting(material, worldPos);

    // Apply SSAO if enabled
    if (ssaoEnabled != 0) {
        vec2 ssaoUV = screenCoord / screenSize;
        float ssaoFactor = texture(ssaoTexture, ssaoUV).r;

        // ssaoStrength controls how much the AO darkens (1.0 = normal, >1.0 = stronger darkening)
        // Apply power to increase darkening effect
        ssaoFactor = pow(ssaoFactor, ssaoStrength);
        ssaoFactor = clamp(ssaoFactor, 0.0, 1.0);

        ambient *= ssaoFactor;
    }

    return ambient;
}