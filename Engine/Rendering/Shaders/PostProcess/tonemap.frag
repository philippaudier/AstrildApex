#version 330 core

in vec2 vTexCoord;
out vec4 FragColor;

uniform sampler2D u_SourceTexture;
uniform sampler2D u_LuminanceTexture; // Mipmap chain for average luminance
uniform int u_ToneMappingMode;
uniform float u_Exposure;
uniform float u_WhitePoint;
uniform float u_Gamma;
uniform bool u_AutoExposure;
uniform float u_MinExposure;
uniform float u_MaxExposure;
uniform float u_TargetBrightness;
uniform float u_AdaptationSpeed;
uniform float u_DeltaTime;


vec3 ReinhardToneMapping(vec3 color)
{
    return color / (1.0 + color);
}


vec3 ReinhardExtendedToneMapping(vec3 color, float whitePoint)
{
    vec3 numerator = color * (1.0 + (color / (whitePoint * whitePoint)));
    return numerator / (1.0 + color);
}


vec3 FilmicToneMapping(vec3 color)
{
    const float A = 0.15;  
    const float B = 0.50;  
    const float C = 0.10;  
    const float D = 0.20; 
    const float E = 0.02; 
    const float F = 0.30; 

    vec3 x = color;
    return ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F)) - E / F;
}


vec3 ACESToneMapping(vec3 color)
{
    const float a = 2.51;
    const float b = 0.03;
    const float c = 2.43;
    const float d = 0.59;
    const float e = 0.14;

    return clamp((color * (a * color + b)) / (color * (c * color + d) + e), 0.0, 1.0);
}

// Calculate luminance from RGB
float Luminance(vec3 color)
{
    return dot(color, vec3(0.2126, 0.7152, 0.0722));
}

void main()
{
    vec3 color = texture(u_SourceTexture, vTexCoord).rgb;

    float finalExposure = u_Exposure;

    // Auto-exposure: calculate average scene luminance and adjust exposure
    if (u_AutoExposure)
    {
        // Sample the smallest mipmap level (1x1) for average luminance
        // This assumes u_LuminanceTexture has a full mipmap chain
        float avgLuminance = textureLod(u_LuminanceTexture, vec2(0.5), 10.0).r;

        // Avoid division by zero
        avgLuminance = max(avgLuminance, 0.001);

        // Calculate auto-exposure using key value (target brightness)
        // EV = log2(avgLum / targetBrightness)
        // exposure = 1.0 / (avgLum / targetBrightness) = targetBrightness / avgLum
        float autoExposure = u_TargetBrightness / avgLuminance;

        // Clamp to min/max range
        autoExposure = clamp(autoExposure, u_MinExposure, u_MaxExposure);

        // Smooth adaptation over time (exponential interpolation)
        // In a real implementation, this would use the previous frame's exposure
        // For now, we'll use the calculated value directly
        // TODO: Add temporal smoothing via a separate pass or uniform

        finalExposure *= autoExposure;
    }

    // Apply exposure
    color *= finalExposure;

    // Apply tone mapping
    switch (u_ToneMappingMode)
    {
        case 1:
            color = ReinhardToneMapping(color);
            break;
        case 2:
            color = ReinhardExtendedToneMapping(color, u_WhitePoint);
            break;
        case 3:
            color = FilmicToneMapping(color);

            vec3 whiteScale = 1.0 / FilmicToneMapping(vec3(u_WhitePoint));
            color *= whiteScale;
            break;
        case 4:
            color = ACESToneMapping(color);
            break;
        default:
            color = clamp(color, 0.0, 1.0);
            break;
    }

    // Apply gamma correction
    color = pow(color, vec3(1.0 / u_Gamma));

    FragColor = vec4(color, 1.0);
}