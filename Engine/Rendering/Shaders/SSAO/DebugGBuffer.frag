```plaintext
#version 330 core

in vec2 vTexCoord;

uniform sampler2D u_PositionTex; // view-space position (rgb)
uniform sampler2D u_NormalTex;   // view-space normal (rgb)
uniform sampler2D u_DepthTex;    // optional linear depth (r)

uniform int u_DebugMode; // 0 = none, 1 = position, 2 = normal, 3 = depth

out vec4 FragColor;

void main(){
    if (u_DebugMode == 1) {
        vec3 pos = texture(u_PositionTex, vTexCoord).xyz;
        // Remap view-space position to color for visualization
        // We map X,Y in [-range,range] to [0,1] and Z to [near,far]
        float range = 10.0; // coarse range; can be adjusted in shader if needed
        vec3 col = (pos / range) * 0.5 + 0.5;
        FragColor = vec4(col, 1.0);
    } else if (u_DebugMode == 2) {
        vec3 n = normalize(texture(u_NormalTex, vTexCoord).xyz);
        vec3 col = n * 0.5 + 0.5; // normals in [-1,1] -> [0,1]
        FragColor = vec4(col, 1.0);
    } else if (u_DebugMode == 3) {
        float d = texture(u_DepthTex, vTexCoord).r;
        // visualize depth as grayscale
        FragColor = vec4(vec3(d), 1.0);
    } else {
        // fallback: show normal
        vec3 n = normalize(texture(u_NormalTex, vTexCoord).xyz);
        FragColor = vec4(n * 0.5 + 0.5, 1.0);
    }
}
```
