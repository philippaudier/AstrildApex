// Engine/Scene/Lighting.cs
using System.Collections.Generic;
using System.Linq;
using OpenTK.Mathematics;
using Engine.Components;

namespace Engine.Scene
{
    public sealed class LightingState
    {
        public bool HasDirectional;
        public Vector3 DirDirection;
        public Vector3 DirColor;
        public float   DirIntensity;
        // Whether the active directional light should cast shadows
        public bool DirCastShadows = false;

        public readonly List<(Vector3 pos, Vector3 color, float intensity, float range)> Points = new();
        public readonly List<(Vector3 pos, Vector3 dir, Vector3 color, float intensity, float range, float angle, float innerAngle)> Spots = new();
        
        // Environment settings
        public Vector3 AmbientColor = new Vector3(0.05f, 0.05f, 0.05f); // Reduced from 0.2 for more contrast
        public float AmbientIntensity = 1.0f;
        public AmbientMode AmbientMode = AmbientMode.Skybox;
        
        // Fog settings
        public bool FogEnabled = false;
        public Vector3 FogColor = Vector3.One;
        public float FogStart = 0.0f;
        public float FogEnd = 300.0f;
    // Fog density (for exponential fog control)
    public float FogDensity = 0.01f;
        
        // Skybox settings
        public Vector3 SkyboxTint = Vector3.One;
        public float SkyboxExposure = 1.0f;
    }

    public static class Lighting
    {
        public static LightingState Build(Scene scene)
        {
            var L = new LightingState();

            // Find environment settings first
            var envSettings = scene.Entities
                .Select(e => e.GetComponent<EnvironmentSettings>())
                .FirstOrDefault(e => e != null);

            if (envSettings != null)
            {
                // Apply environment settings
                L.AmbientColor = envSettings.AmbientColor;
                L.AmbientIntensity = envSettings.AmbientIntensity;
                L.AmbientMode = envSettings.AmbientMode;
                L.FogEnabled = envSettings.FogEnabled;
                L.FogColor = envSettings.FogColor;
                L.FogStart = envSettings.FogStart;
                L.FogEnd = envSettings.FogEnd;
                L.FogDensity = envSettings.FogDensity;
                L.SkyboxTint = envSettings.SkyboxTint;
                L.SkyboxExposure = envSettings.SkyboxExposure;
                
                // Handle sun/moon light selection based on time of day
                var activeLightId = envSettings.GetActiveLightId();
                if (activeLightId.HasValue)
                {
                    var activeEntity = scene.GetById(activeLightId.Value);
                    var activeLight = activeEntity?.GetComponent<LightComponent>();
                    
                        if (activeLight != null && activeLight.Enabled && activeLight.Type == LightType.Directional)
                    {
                        // Use environment-specified light as main directional light
                        L.HasDirectional = true;
                        L.DirDirection = activeLight.Direction.Normalized();

                        // Debug: log light direction
                        var lightEntity = activeLight.Entity;
                        if (lightEntity != null)
                        {
                            lightEntity.GetWorldTRS(out var worldPos, out var worldRot, out _);
                        }
                        
                        // Apply sun/moon blending
                        float blend = envSettings.GetSunMoonBlend();
                        L.DirColor = activeLight.Color;
                        L.DirIntensity = activeLight.Intensity;
                        L.DirCastShadows = activeLight.CastShadows;
                        
                        // If we have both sun and moon, blend them
                        if (envSettings.SunLight != null && envSettings.MoonLight != null)
                        {
                            var sunEntity = envSettings.SunLight;
                            var moonEntity = envSettings.MoonLight;
                            var sunLight = sunEntity?.GetComponent<LightComponent>();
                            var moonLight = moonEntity?.GetComponent<LightComponent>();
                            
                            if (sunLight != null && moonLight != null && sunLight.Enabled && moonLight.Enabled)
                            {
                                // Blend colors and intensities
                                L.DirColor = Vector3.Lerp(moonLight.Color, sunLight.Color, blend);
                                L.DirIntensity = MathHelper.Lerp(moonLight.Intensity, sunLight.Intensity, blend);

                                // Use the more appropriate direction based on time
                                L.DirDirection = blend > 0.5f ? sunLight.Direction.Normalized() : moonLight.Direction.Normalized();

                                // Debug: log blended light direction
                                var blendedEntity = blend > 0.5f ? sunEntity : moonEntity;
                                if (blendedEntity != null)
                                {
                                    blendedEntity.GetWorldTRS(out var worldPos, out var worldRot, out _);
                                }
                            }
                        }
                    }
                }
            }

            // Process all other lights
            foreach (var e in scene.Entities)
            {
                var light = e.GetComponent<LightComponent>();
                if (light == null || !light.Enabled) continue;

                // Skip if this light is already handled by environment settings
                if (envSettings != null && 
                    (e.Id == envSettings.SunLightEntityId || e.Id == envSettings.MoonLightEntityId))
                {
                    // Already processed above in environment-aware way
                    continue;
                }

                switch (light.Type)
                {
                    case LightType.Directional:
                        // Only use first directional light if no environment settings specified one
                        if (!L.HasDirectional)
                        {
                            L.HasDirectional = true;
                            L.DirDirection = light.Direction.Normalized();
                            L.DirColor = light.Color;
                            L.DirIntensity = light.Intensity;
                            L.DirCastShadows = light.CastShadows;
                        }
                        break;

                    case LightType.Point:
                        // Position monde = transform de l'entité (utiliser GetWorldTRS pour récupérer la vraie position monde)
                        e.GetWorldTRS(out var pointWorldPos, out _, out _);
                        L.Points.Add((pointWorldPos, light.Color, light.Intensity, light.Range));
                        break;

                    case LightType.Spot:
                        // Position + direction monde = transform de l'entité (utiliser GetWorldTRS pour récupérer la vraie position monde)
                        e.GetWorldTRS(out var spotWorldPos, out _, out _);
                        L.Spots.Add((spotWorldPos, light.Direction, light.Color, light.Intensity, light.Range, light.SpotAngle, light.SpotInnerAngle));
                        break;
                }
            }
            
            return L;
        }
    }
}
