#version 420 core
layout(location = 0) in vec3 a_Position;
layout(location = 1) in vec3 a_Normal;
layout(location = 2) in vec2 a_TexCoord;

uniform mat4 u_Model;
uniform mat3 u_NormalMat;
uniform mat4 u_View;
uniform mat4 u_Projection;

// Snow displacement parameters
uniform float u_SnowAccumulation;  // Accumulated snow (can exceed 1.0)
uniform float u_SnowDisplacement;  // Max displacement height
uniform float u_SnowSlopeMin;      // Min slope for snow placement (degrees)
uniform float u_SnowSlopeMax;      // Max slope for snow placement (degrees)

out vec3 v_WorldPos;
out vec3 v_Normal;
out vec2 v_TexCoord;

// Calculate snow placement based on surface angle (same as fragment shader)
float calculateSnowPlacement(vec3 normal, float slopeMinDeg, float slopeMaxDeg)
{
    vec3 up = vec3(0, 1, 0);
    float dotProduct = dot(normalize(normal), up);
    float angleRad = acos(clamp(dotProduct, -1.0, 1.0));
    float angleDeg = angleRad * 57.29577951; // radians to degrees

    float fadeWidth = 5.0;
    float fadeIn = smoothstep(slopeMinDeg, slopeMinDeg + fadeWidth, angleDeg);
    float fadeOut = 1.0 - smoothstep(slopeMaxDeg - fadeWidth, slopeMaxDeg, angleDeg);

    return clamp(fadeIn * fadeOut, 0.0, 1.0);
}

void main()
{
    vec4 worldPos = u_Model * vec4(a_Position, 1.0);
    vec3 worldNormal = normalize(u_NormalMat * a_Normal);

    // Calculate snow displacement
    float snowPlacement = calculateSnowPlacement(worldNormal, u_SnowSlopeMin, u_SnowSlopeMax);
    float snowAmount = u_SnowAccumulation * snowPlacement;

    // Vertical displacement based on snow accumulation
    // Uses exponential curve for natural build-up (more snow = more height)
    float displacementAmount = (1.0 - exp(-snowAmount * 0.8)) * u_SnowDisplacement;

    // Displace vertex upward along world Y-axis
    worldPos.y += displacementAmount;

    // Smooth normals for rounded snow edges
    // Blend normal towards up vector based on snow amount
    float normalSmoothFactor = clamp(snowAmount * 0.3, 0.0, 0.7);
    vec3 smoothedNormal = normalize(mix(worldNormal, vec3(0, 1, 0), normalSmoothFactor));

    v_WorldPos = worldPos.xyz;
    v_Normal = smoothedNormal;
    v_TexCoord = a_TexCoord;
    gl_Position = u_Projection * u_View * worldPos;
}
