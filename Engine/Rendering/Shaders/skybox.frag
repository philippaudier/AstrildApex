#version 330 core

layout(location=0) out vec4 FragColor;
layout(location=1) out uint outId;

in vec3 TexCoords;

uniform uint u_ObjectId;


uniform float exposure;              
uniform int   uMode;                 


uniform samplerCube skybox;
uniform vec3 tintColor;          

// Fog controls passed from engine
uniform int uFogEnabled;
uniform vec3 uFogColor;
uniform float uFogStart;
uniform float uFogEnd;
uniform float uFogDensity;


uniform vec3 skyTint;
uniform vec3 groundColor;
uniform float atmosphereThickness;  
uniform vec3 sunDirection;           
uniform vec3 sunTint;
uniform float sunSize;              
uniform float sunSizeConvergence;    


// Simple Reinhard tonemapping (LearnOpenGL standard)
// Maps HDR to LDR while preserving color ratios
vec3 ReinhardTonemap(vec3 hdr) {
    return hdr / (hdr + vec3(1.0));
}

void main()
{
    vec3 dir = normalize(TexCoords);

    vec3 color;
    if (uMode == 0) {
        // Cubemap mode: sample the cubemap texture
        // Note: dir should already be normalized from vertex shader
        color = texture(skybox, dir).rgb;
        color *= tintColor;
    } else {
       
        float t = clamp(dir.y * 0.5 + 0.5, 0.0, 1.0);

       
        float at = clamp(atmosphereThickness, 0.0, 5.0);
        float curve = pow(t, mix(0.35, 1.8, clamp(at / 5.0, 0.0, 1.0))); 
        color = mix(groundColor, skyTint, curve);

      
        float cosA = dot(dir, normalize(sunDirection));
        cosA = clamp(cosA, -1.0, 1.0);
        float ang = acos(cosA); 

      
        float sunRad = radians(mix(0.1, 5.0, clamp(sunSize, 0.0, 1.0)));

      
        float k = max(1.0, sunSizeConvergence);
        float feather = sunRad * (1.0 / k);
        float sunMask = smoothstep(sunRad, sunRad - feather, ang);
        color = mix(color, sunTint, sunMask);
    }

    // Unity-style HDR skybox processing (NO tonemapping for skybox!):
    // 1. Apply exposure to HDR linear values
    color = color * max(exposure, 0.0);

    // 2. Clamp to prevent overflow (but keep HDR feel)
    color = min(color, vec3(65504.0)); // Max value for half-float

    // 3. Gamma correct ONLY: convert from linear to sRGB for display
    // Unity does NOT apply tonemapping to skyboxes - just gamma
    // TESTING: Try without gamma to see if colors appear
    // color = pow(color, vec3(1.0/2.2));

    #ifdef SKIP_FOG
    FragColor = vec4(color, 1.0);
    #else

    if (uFogEnabled != 0)
    {
        // For skybox, approximate vertical distance-based fog: higher = nearer to sky, lower = nearer to ground
        // We'll compute a factor based on dir.y and then apply exponential fog using density for nicer falloff.
        // Map dir.y (-1 .. 1) to a normalized 0..1 height factor
        float heightFactor = clamp((dir.y * 0.5) + 0.5, 0.0, 1.0);

        // Use fog start/end to bias the effective height range (remap heightFactor into [0,1] using start/end)
        float efStart = clamp(uFogStart, -1.0, 1.0);
        float efEnd = clamp(uFogEnd, -1.0, 1.0);
        // If efEnd <= efStart use heightFactor as-is
        float remapped = heightFactor;
        if (efEnd > efStart)
            remapped = clamp((heightFactor - efStart) / max(1e-5, (efEnd - efStart)), 0.0, 1.0);

        // Exponential fog falloff: lower remapped values (near ground) are foggier
        float d = max(0.0, 1.0 - remapped);

    // Amplified multiplier (20.0) so editor-exposed Density has stronger visual effect
    float fogFactor = exp(-uFogDensity * 20.0 * d);
        fogFactor = clamp(fogFactor, 0.0, 1.0);

        color = mix(uFogColor, color, fogFactor);
    }

    FragColor = vec4(color, 1.0);
    #endif
    
    // Write entity ID for object picking and selection outline
    outId = u_ObjectId;
}