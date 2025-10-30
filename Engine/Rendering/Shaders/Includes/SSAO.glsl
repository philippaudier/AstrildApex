// SSAO.glsl - Screen Space Ambient Occlusion calculations

// SSAO parameters
struct SSAOParams {
    float radius;           // Sampling radius in world space
    float bias;            // Small bias to prevent self-occlusion
    float intensity;       // Occlusion intensity multiplier
    int sampleCount;       // Number of samples (8, 16, 32, 64)
    float falloff;         // Distance falloff factor
    float scale;          // Screen space scale
};

// Generate pseudo-random value based on screen coordinates
float random(vec2 co) {
    return fract(sin(dot(co.xy, vec2(12.9898, 78.233))) * 43758.5453);
}

// Generate random hemisphere sample
vec3 randomHemisphereSample(vec2 coord, float index, float totalSamples) {
    float angle = 2.0 * PI * random(coord + index);
    float z = random(coord * index);
    float r = sqrt(1.0 - z * z);
    return vec3(r * cos(angle), r * sin(angle), z);
}

// Convert world position to screen space
vec2 worldToScreen(vec3 worldPos, mat4 viewProj, vec2 screenSize) {
    vec4 clipPos = viewProj * vec4(worldPos, 1.0);
    vec3 ndcPos = clipPos.xyz / clipPos.w;
    return (ndcPos.xy * 0.5 + 0.5) * screenSize;
}

// Sample depth from depth buffer and convert to linear depth
float sampleLinearDepth(sampler2D depthTex, vec2 uv, float near, float far) {
    float depth = texture(depthTex, uv).r;
    return (2.0 * near * far) / (far + near - depth * (far - near));
}

// Reconstruct world position from depth
vec3 reconstructWorldPosition(vec2 uv, float depth, mat4 invViewProj) {
    vec4 clipPos = vec4(uv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
    vec4 worldPos = invViewProj * clipPos;
    return worldPos.xyz / worldPos.w;
}

// Main SSAO calculation function (VIEW SPACE approach following LearnOpenGL)
float calculateSSAO(vec2 screenCoord, vec3 fragPos, vec3 normal,
                   sampler2D positionTex, mat4 projMatrix, SSAOParams params,
                   float nearPlane, float farPlane) {

    float occlusion = 0.0;

    // Generate simple random vector for this fragment
    vec3 randomVec = normalize(vec3(
        random(screenCoord) * 2.0 - 1.0,
        random(screenCoord * 2.0) * 2.0 - 1.0,
        0.0
    ));

    // Create TBN matrix (Gramm-Schmidt process)
    vec3 tangent = normalize(randomVec - normal * dot(randomVec, normal));
    vec3 bitangent = cross(normal, tangent);
    mat3 TBN = mat3(tangent, bitangent, normal);

    // Sample points in hemisphere around the pixel
    for (int i = 0; i < params.sampleCount; i++) {
        // Generate hemisphere sample (simplified)
        float scale = float(i) / float(params.sampleCount);
        scale = mix(0.1, 1.0, scale * scale); // More samples closer to origin
        
        vec3 sampleVec = randomHemisphereSample(screenCoord, float(i), float(params.sampleCount));
        sampleVec = TBN * sampleVec * scale; // Transform to view-space
        sampleVec = fragPos + sampleVec * params.radius;

        // Project sample to screen-space
        vec4 offset = vec4(sampleVec, 1.0);
        offset = projMatrix * offset; // Use projection matrix
        offset.xyz /= offset.w; // Perspective divide
        offset.xyz = offset.xyz * 0.5 + 0.5; // Transform to [0,1]

        // Skip samples outside screen
        if (offset.x < 0.0 || offset.x > 1.0 || offset.y < 0.0 || offset.y > 1.0) {
            continue;
        }

        // Get sample depth from position texture
        vec3 samplePos = texture(positionTex, offset.xy).xyz;
        float sampleDepth = samplePos.z;

        // Range check + occlusion test
        float rangeCheck = smoothstep(0.0, 1.0, params.radius / abs(fragPos.z - sampleDepth));
        occlusion += (sampleDepth >= sampleVec.z + params.bias ? 1.0 : 0.0) * rangeCheck;
    }

    // Normalize and apply intensity
    occlusion = occlusion / float(params.sampleCount);
    occlusion = saturate(occlusion * params.intensity);

    return 1.0 - occlusion; // Return ambient factor (1.0 = no occlusion)
}

// Simplified SSAO for performance (fewer samples)
float calculateSSAOFast(vec2 screenCoord, vec3 worldPos, vec3 normal,
                       sampler2D depthTex, SSAOParams params,
                       mat4 viewProj, mat4 invViewProj, vec2 screenSize,
                       float nearPlane, float farPlane) {

    SSAOParams fastParams = params;
    fastParams.sampleCount = min(params.sampleCount, 8); // Limit samples for performance

    return calculateSSAO(screenCoord, worldPos, normal, depthTex, fastParams,
                        viewProj, invViewProj, screenSize, nearPlane, farPlane);
}

// Bilateral blur for SSAO (preserves edges)
float bilateralBlurSSAO(sampler2D ssaoTex, sampler2D depthTex, vec2 uv, vec2 texelSize,
                       float blurRadius, float depthThreshold) {

    float centerDepth = texture(depthTex, uv).r;
    float result = 0.0;
    float totalWeight = 0.0;

    int samples = int(blurRadius);
    for (int x = -samples; x <= samples; x++) {
        for (int y = -samples; y <= samples; y++) {
            vec2 offset = vec2(float(x), float(y)) * texelSize;
            vec2 sampleUV = uv + offset;

            if (sampleUV.x >= 0.0 && sampleUV.x <= 1.0 && sampleUV.y >= 0.0 && sampleUV.y <= 1.0) {
                float sampleDepth = texture(depthTex, sampleUV).r;
                float depthDiff = abs(centerDepth - sampleDepth);

                // Weight based on depth similarity (preserve edges)
                float weight = 1.0 - smoothstep(0.0, depthThreshold, depthDiff);

                // Gaussian weight based on distance
                float distance = length(vec2(x, y));
                weight *= exp(-distance * distance / (blurRadius * blurRadius));

                result += texture(ssaoTex, sampleUV).r * weight;
                totalWeight += weight;
            }
        }
    }

    return totalWeight > 0.0 ? result / totalWeight : texture(ssaoTex, uv).r;
}

// Apply SSAO to ambient lighting
vec3 applySSAOToAmbient(vec3 ambientColor, float ssaoFactor, float ssaoStrength) {
    float aoFactor = mix(1.0, ssaoFactor, ssaoStrength);
    return ambientColor * aoFactor;
}