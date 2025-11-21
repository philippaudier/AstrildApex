#version 330 core

out float FragColor;

in vec2 vTexCoord;

uniform sampler2D u_SSAOTexture;
uniform int u_BlurSize;

void main()
{
    vec2 texSize = vec2(textureSize(u_SSAOTexture, 0));
    vec2 texelSize = 1.0 / texSize;
    float result = 0.0;
    float totalWeight = 0.0;
    
    int halfSize = u_BlurSize / 2;
    
    // Flou gaussien bilatéral
    for (int x = -halfSize; x <= halfSize; x++)
    {
        for (int y = -halfSize; y <= halfSize; y++)
        {
            vec2 offset = vec2(float(x), float(y)) * texelSize;
            vec2 sampleCoord = vTexCoord + offset;
            
            // Clamp to valid texture coordinates to avoid edge artifacts
            sampleCoord = clamp(sampleCoord, vec2(0.0), vec2(1.0));
            
            float sample = texture(u_SSAOTexture, sampleCoord).r;
            
            // Poids gaussien simple
            float weight = 1.0 / (1.0 + length(vec2(x, y)));
            
            result += sample * weight;
            totalWeight += weight;
        }
    }
    
    FragColor = result / totalWeight;
}
