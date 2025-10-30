#version 330 core

in vec2 vTexCoord;
out vec4 FragColor;

uniform sampler2D u_SourceTexture;
uniform float u_Strength;
uniform float u_FocalLength;
uniform int u_UseSpectralLut;
uniform vec2 u_ScreenSize;


float getDistortionFactor(vec2 coord, float focalLength)
{

    vec2 delta = coord - vec2(0.5);
    float dist = length(delta);


    float k = 0.1 / (focalLength * 0.01); 
    return 1.0 + k * dist * dist;
}


vec2 getChromaticOffset(vec2 coord, float strength, float channelOffset)
{
    vec2 delta = coord - vec2(0.5);
    float dist = length(delta);


    float aberrationFactor = dist * strength * channelOffset;

    return delta * aberrationFactor;
}


vec3 SimpleChromaticAberration(vec2 coord)
{

    vec2 redOffset = getChromaticOffset(coord, u_Strength, -0.01);
    vec2 greenOffset = getChromaticOffset(coord, u_Strength, 0.0);
    vec2 blueOffset = getChromaticOffset(coord, u_Strength, 0.01);

    float red = texture(u_SourceTexture, coord + redOffset).r;
    float green = texture(u_SourceTexture, coord + greenOffset).g;
    float blue = texture(u_SourceTexture, coord + blueOffset).b;

    return vec3(red, green, blue);
}


vec3 SpectralChromaticAberration(vec2 coord)
{
    vec3 color = vec3(0.0);


    const int samples = 7;
    const float offsets[7] = float[](-0.028, -0.019, -0.009, 0.0, 0.009, 0.019, 0.028);
    const vec3 weights[7] = vec3[](
        vec3(0.4, 0.0, 0.0),   
        vec3(0.3, 0.2, 0.0),   
        vec3(0.2, 0.3, 0.0),   
        vec3(0.1, 0.4, 0.1),   
        vec3(0.0, 0.3, 0.2),  
        vec3(0.0, 0.2, 0.3),  
        vec3(0.0, 0.0, 0.4)   
    );

    for (int i = 0; i < samples; i++)
    {
        vec2 offset = getChromaticOffset(coord, u_Strength, offsets[i]);
        vec3 sample = texture(u_SourceTexture, coord + offset).rgb;
        color += sample * weights[i];
    }

    return color;
}

void main()
{
    vec2 coord = vTexCoord;

    vec3 color;

    if (u_UseSpectralLut == 1)
    {

        color = SpectralChromaticAberration(coord);
    }
    else
    {

        color = SimpleChromaticAberration(coord);
    }


    color = max(color, vec3(0.0));

    FragColor = vec4(color, 1.0);
}