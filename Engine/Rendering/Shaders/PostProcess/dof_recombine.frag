#version 330 core

// Recombine shader - merges blurred DOF with sharp scene
// Upsamples from half resolution and blends based on CoC

out vec4 FragColor;

in vec2 vTexCoord;

uniform sampler2D u_SharpTexture;   // Full resolution sharp scene
uniform sampler2D u_BlurredTexture; // Half resolution blurred scene
uniform sampler2D u_DepthTexture;   // Depth for edge-aware upsampling

uniform float u_NearPlane;
uniform float u_FarPlane;

// Linearize depth
float LinearizeDepth(float depth)
{
    float z = depth * 2.0 - 1.0;
    return (2.0 * u_NearPlane * u_FarPlane) / (u_FarPlane + u_NearPlane - z * (u_FarPlane - u_NearPlane));
}

void main()
{
    vec4 sharp = texture(u_SharpTexture, vTexCoord);
    float sharpCoC = sharp.a; // CoC stored in alpha from CoC pass

    // Bilateral upsampling from half-res blurred texture
    vec4 blurred = texture(u_BlurredTexture, vTexCoord);

    // Get depth for edge awareness
    float centerDepth = LinearizeDepth(texture(u_DepthTexture, vTexCoord).r);

    // Blend based on CoC
    // Small CoC = sharp, Large CoC = blurred
    float blend = smoothstep(0.0, 0.1, sharpCoC);

    vec3 finalColor = mix(sharp.rgb, blurred.rgb, blend);

    FragColor = vec4(finalColor, 1.0);
}
