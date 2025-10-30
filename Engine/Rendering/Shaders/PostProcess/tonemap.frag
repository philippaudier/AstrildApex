#version 330 core

in vec2 vTexCoord;
out vec4 FragColor;

uniform sampler2D u_SourceTexture;
uniform int u_ToneMappingMode;   
uniform float u_Exposure;
uniform float u_WhitePoint;
uniform float u_Gamma;


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

void main()
{
    vec3 color = texture(u_SourceTexture, vTexCoord).rgb;


    color *= u_Exposure;


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

  
    color = pow(color, vec3(1.0 / u_Gamma));

    FragColor = vec4(color, 1.0);
}