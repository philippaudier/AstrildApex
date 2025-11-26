#version 330 core

// Circle of Confusion (CoC) calculation shader
// Calculates per-pixel blur amount based on depth and focus parameters

out vec4 FragColor;

in vec2 vTexCoord;

uniform sampler2D u_ColorTexture;
uniform sampler2D u_DepthTexture;

// Focus parameters
uniform float u_FocusDistance;      // Distance to focus plane
uniform float u_FocusRange;         // Range around focus that stays sharp
uniform float u_FocalLength;        // Camera focal length (mm)
uniform float u_Aperture;           // f-stop (lower = more blur)
uniform float u_MaxCoC;             // Maximum circle of confusion radius

// Projection matrix for depth linearization
uniform mat4 u_InvProjection;

// Near and far plane
uniform float u_NearPlane;
uniform float u_FarPlane;

// Linearize depth from 0-1 non-linear depth buffer
float LinearizeDepth(float depth)
{
    float z = depth * 2.0 - 1.0; // Back to NDC
    return (2.0 * u_NearPlane * u_FarPlane) / (u_FarPlane + u_NearPlane - z * (u_FarPlane - u_NearPlane));
}

// Calculate Circle of Confusion radius
float CalculateCoC(float depth)
{
    float linearDepth = LinearizeDepth(depth);

    // Physically-based CoC calculation
    // CoC = (focalLength * (focusDistance - depth)) / (depth * (focusDistance - focalLength))
    // Simplified: CoC = aperture * |depth - focusDistance| / depth

    float coc = abs(linearDepth - u_FocusDistance) / max(linearDepth, 0.001);
    coc *= u_Aperture * 0.1; // Scale by aperture (f-stop)

    // Apply focus range (area that stays sharp)
    float focusBlend = smoothstep(0.0, u_FocusRange, abs(linearDepth - u_FocusDistance));
    coc *= focusBlend;

    // Clamp to max CoC
    coc = min(coc, u_MaxCoC);

    return coc;
}

void main()
{
    vec4 color = texture(u_ColorTexture, vTexCoord);
    float depth = texture(u_DepthTexture, vTexCoord).r;

    // Calculate CoC
    float coc = CalculateCoC(depth);

    // Output: RGB = color, A = CoC radius
    FragColor = vec4(color.rgb, coc);
}
