// Minimal debug stub for Shadows to isolate compile error
uniform int u_UseShadows;

float calculateShadowWithNL(vec3 worldPos, vec3 viewPos, vec3 N, vec3 L) {
    if (u_UseShadows == 0) return 1.0;
    return 1.0; // no shadowing in debug stub
}

float calculateShadow(vec3 worldPos, vec3 viewPos) { return calculateShadowWithNL(worldPos, viewPos, vec3(0.0), vec3(0.0)); }
float calculateShadow(vec3 worldPos) { return 1.0; }
