using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Engine.Components;
using Engine.Rendering;

namespace Engine.Serialization
{
    /// <summary>
    /// Sérialiseur personnalisé pour le composant GlobalEffects
    /// Gère la sérialisation polymorphe des effets de post-processing
    /// </summary>
    public class GlobalEffectsSerializer : IComponentSerializer<GlobalEffects>
    {
        public Dictionary<string, object> Serialize(GlobalEffects component)
        {
            var result = new Dictionary<string, object>();
            
            // Sérialiser la propriété Enabled du composant de base
            result["enabled"] = component.Enabled;
            
            // Sérialiser la liste des effets avec leurs types
            var effectsData = new List<Dictionary<string, object>>();
            
            foreach (var effect in component.Effects)
            {
                var effectData = new Dictionary<string, object>
                {
                    ["type"] = effect.GetType().FullName ?? effect.GetType().Name,
                    ["enabled"] = effect.Enabled,
                    ["intensity"] = effect.Intensity,
                    ["priority"] = effect.Priority
                };
                
                // Sérialiser les propriétés spécifiques selon le type
                switch (effect)
                {
                    case ToneMappingEffect toneMap:
                        effectData["mode"] = toneMap.Mode.ToString();
                        effectData["exposure"] = toneMap.Exposure;
                        effectData["whitepoint"] = toneMap.WhitePoint;
                        effectData["gamma"] = toneMap.Gamma;
                        // Auto-exposure parameters
                        effectData["autoexposure"] = toneMap.AutoExposure;
                        effectData["minexposure"] = toneMap.MinExposure;
                        effectData["maxexposure"] = toneMap.MaxExposure;
                        effectData["adaptationspeed"] = toneMap.AdaptationSpeed;
                        effectData["targetbrightness"] = toneMap.TargetBrightness;
                        effectData["exposurecompensation"] = toneMap.ExposureCompensation;
                        break;
                        
                    case BloomEffect bloom:
                        effectData["threshold"] = bloom.Threshold;
                        effectData["softknee"] = bloom.SoftKnee;
                        effectData["radius"] = bloom.Radius;
                        effectData["iterations"] = bloom.Iterations;
                        effectData["clamp"] = bloom.Clamp;
                        effectData["scattering"] = bloom.Scattering;
                        break;
                        
                    case ChromaticAberrationEffect chromatic:
                        effectData["strength"] = chromatic.Strength;
                        effectData["usespectrallut"] = chromatic.UseSpectralLut;
                        effectData["focallength"] = chromatic.FocalLength;
                        break;
                        
                    case SSAOEffect ssao:
                        effectData["radius"] = ssao.Radius;
                        effectData["bias"] = ssao.Bias;
                        effectData["power"] = ssao.Power;
                        effectData["samplecount"] = ssao.SampleCount;
                        effectData["blursize"] = ssao.BlurSize;
                        effectData["maxdistance"] = ssao.MaxDistance;
                        break;
                        
                    case GTAOEffect gtao:
                        effectData["radius"] = gtao.Radius;
                        effectData["thickness"] = gtao.Thickness;
                        effectData["falloffrange"] = gtao.FalloffRange;
                        effectData["samplecount"] = gtao.SampleCount;
                        effectData["slicecount"] = gtao.SliceCount;
                        effectData["blurradius"] = gtao.BlurRadius;
                        effectData["maxdistance"] = gtao.MaxDistance;
                        effectData["enabletemporal"] = gtao.EnableTemporal;
                        effectData["temporalblendfactor"] = gtao.TemporalBlendFactor;
                        effectData["temporalvariancethreshold"] = gtao.TemporalVarianceThreshold;
                        effectData["miplevels"] = gtao.MipLevels;
                        effectData["mipweight0"] = gtao.MipWeight0;
                        effectData["mipweight1"] = gtao.MipWeight1;
                        effectData["mipweight2"] = gtao.MipWeight2;
                        effectData["mipweight3"] = gtao.MipWeight3;
                        effectData["mipradius0"] = gtao.MipRadius0;
                        effectData["mipradius1"] = gtao.MipRadius1;
                        effectData["mipradius2"] = gtao.MipRadius2;
                        effectData["mipradius3"] = gtao.MipRadius3;
                        break;

                    case DepthOfFieldEffect dof:
                        effectData["focusdistance"] = dof.FocusDistance;
                        effectData["focusrange"] = dof.FocusRange;
                        effectData["focallength"] = dof.FocalLength;
                        effectData["aperture"] = dof.Aperture;
                        effectData["maxcoc"] = dof.MaxCoC;
                        effectData["samplecount"] = dof.SampleCount;
                        effectData["bokehradius"] = dof.BokehRadius;
                        effectData["enableadaptive"] = dof.EnableAdaptiveDOF;
                        effectData["adaptivespeed"] = dof.AdaptiveSpeed;
                        effectData["adaptivecenterbias"] = dof.AdaptiveCenterBias;
                        effectData["adaptivemindistance"] = dof.AdaptiveMinDistance;
                        effectData["adaptivemaxdistance"] = dof.AdaptiveMaxDistance;
                        break;

                    case MotionBlurEffect motionBlur:
                        effectData["samplecount"] = motionBlur.SampleCount;
                        effectData["maxblurradius"] = motionBlur.MaxBlurRadius;
                        break;

                    case ColorGradingEffect colorGrading:
                        effectData["source"] = colorGrading.Source.ToString();
                        effectData["blendfactor"] = colorGrading.BlendFactor;
                        // Manual parameters
                        effectData["saturation"] = colorGrading.Saturation;
                        effectData["contrast"] = colorGrading.Contrast;
                        effectData["brightness"] = colorGrading.Brightness;
                        effectData["colorfilter"] = new[] { colorGrading.ColorFilter.X, colorGrading.ColorFilter.Y, colorGrading.ColorFilter.Z };
                        effectData["temperature"] = colorGrading.Temperature;
                        effectData["tint"] = colorGrading.Tint;
                        effectData["hueshift"] = colorGrading.HueShift;
                        effectData["vibrance"] = colorGrading.Vibrance;
                        // Time of day presets
                        effectData["nightsaturation"] = colorGrading.NightSaturation;
                        effectData["nightcontrast"] = colorGrading.NightContrast;
                        effectData["nighttemperature"] = colorGrading.NightTemperature;
                        effectData["daysaturation"] = colorGrading.DaySaturation;
                        effectData["daycontrast"] = colorGrading.DayContrast;
                        effectData["daytemperature"] = colorGrading.DayTemperature;
                        effectData["sunrisesaturation"] = colorGrading.SunriseSaturation;
                        effectData["sunrisecontrast"] = colorGrading.SunriseContrast;
                        effectData["sunrisetemperature"] = colorGrading.SunriseTemperature;
                        effectData["sunsetsaturation"] = colorGrading.SunsetSaturation;
                        effectData["sunsetcontrast"] = colorGrading.SunsetContrast;
                        effectData["sunsettemperature"] = colorGrading.SunsetTemperature;
                        effectData["transitionspeed"] = colorGrading.TransitionSpeed;
                        break;

                    case VolumetricFogEffect volFog:
                        // Source mode
                        effectData["source"] = volFog.Source.ToString();
                        effectData["blendfactor"] = volFog.BlendFactor;
                        // Local fog parameters
                        effectData["fogcolor"] = new[] { volFog.FogColor.X, volFog.FogColor.Y, volFog.FogColor.Z };
                        effectData["density"] = volFog.Density;
                        effectData["depthstart"] = volFog.DepthStart;
                        effectData["depthend"] = volFog.DepthEnd;
                        // Ray marching
                        effectData["raymarchsteps"] = volFog.RayMarchSteps;
                        // Height-based fog
                        effectData["useheightfog"] = volFog.UseHeightFog;
                        effectData["heightfalloff"] = volFog.HeightFalloff;
                        effectData["baseheight"] = volFog.BaseHeight;
                        effectData["maxheight"] = volFog.MaxHeight;
                        // Scattering & god rays
                        effectData["usesunscattering"] = volFog.UseSunScattering;
                        effectData["scatteringintensity"] = volFog.ScatteringIntensity;
                        effectData["mieg"] = volFog.MieG;
                        effectData["extinctionfactor"] = volFog.ExtinctionFactor;
                        effectData["sunscatteringcolor"] = new[] { volFog.SunScatteringColor.X, volFog.SunScatteringColor.Y, volFog.SunScatteringColor.Z };
                        // Scatter color options
                        effectData["scattersource"] = volFog.ScatterSource.ToString();
                        effectData["inscattercolor"] = new[] { volFog.InScatterColor.X, volFog.InScatterColor.Y, volFog.InScatterColor.Z };
                        effectData["ambientcolor"] = new[] { volFog.AmbientColor.X, volFog.AmbientColor.Y, volFog.AmbientColor.Z };
                        effectData["ambientintensity"] = volFog.AmbientIntensity;
                        effectData["useambientfromsky"] = volFog.UseAmbientFromSky;
                        // God rays radial blur
                        effectData["godraysintensity"] = volFog.GodRaysIntensity;
                        effectData["godraysdensity"] = volFog.GodRaysDensity;
                        effectData["godraysdecay"] = volFog.GodRaysDecay;
                        // Noise/detail
                        effectData["usenoise"] = volFog.UseNoise;
                        effectData["noisescale"] = volFog.NoiseScale;
                        effectData["noisespeed"] = volFog.NoiseSpeed;
                        effectData["noisestrength"] = volFog.NoiseStrength;
                        effectData["noiseoctaves"] = volFog.NoiseOctaves;
                        break;

                    case AtmosphericScatteringEffect atmo:
                        // Planet geometry
                        effectData["planetradius"] = atmo.PlanetRadius;
                        effectData["atmosphereradius"] = atmo.AtmosphereRadius;
                        // Scale heights
                        effectData["rayleighscaleheight"] = atmo.RayleighScaleHeight;
                        effectData["miescaleheight"] = atmo.MieScaleHeight;
                        // Scattering coefficients
                        effectData["rayleighcoefficients"] = new[] { atmo.RayleighCoefficients.X, atmo.RayleighCoefficients.Y, atmo.RayleighCoefficients.Z };
                        effectData["miecoefficient"] = atmo.MieCoefficient;
                        effectData["mieg"] = atmo.MieG;
                        // Sun
                        effectData["sunintensity"] = atmo.SunIntensity;
                        // Quality
                        effectData["numsamples"] = atmo.NumSamples;
                        effectData["numlightsamples"] = atmo.NumLightSamples;
                        // Output
                        effectData["exposure"] = atmo.Exposure;
                        break;

                    case UnderwaterEffect uw:
                        // Water level
                        effectData["waterlevelsource"] = uw.Source.ToString();
                        effectData["waterlevel"] = uw.WaterLevel;
                        // Fog
                        effectData["fogenabled"] = uw.FogEnabled;
                        effectData["fogcolor"] = new[] { uw.FogColor.X, uw.FogColor.Y, uw.FogColor.Z };
                        effectData["fogdensity"] = uw.FogDensity;
                        effectData["visibility"] = uw.Visibility;
                        // Absorption
                        effectData["absorptionenabled"] = uw.AbsorptionEnabled;
                        effectData["absorptionr"] = uw.AbsorptionR;
                        effectData["absorptiong"] = uw.AbsorptionG;
                        effectData["absorptionb"] = uw.AbsorptionB;
                        // God rays
                        effectData["godraysenabled"] = uw.GodRaysEnabled;
                        effectData["godraysintensity"] = uw.GodRaysIntensity;
                        effectData["godrayscolor"] = new[] { uw.GodRaysColor.X, uw.GodRaysColor.Y, uw.GodRaysColor.Z };
                        effectData["godraysdensity"] = uw.GodRaysDensity;
                        effectData["godraysdecay"] = uw.GodRaysDecay;
                        effectData["godrayssamples"] = uw.GodRaysSamples;
                        // Particles (volumetric with depth and lighting)
                        effectData["particlesenabled"] = uw.ParticlesEnabled;
                        effectData["particledensity"] = uw.ParticleDensity;
                        effectData["particlecolor"] = new[] { uw.ParticleColor.X, uw.ParticleColor.Y, uw.ParticleColor.Z };
                        effectData["particlebrightness"] = uw.ParticleBrightness;
                        effectData["particlespeed"] = uw.ParticleSpeed;
                        effectData["particlesizemin"] = uw.ParticleSizeMin;
                        effectData["particlesizemax"] = uw.ParticleSizeMax;
                        effectData["particledepthlayers"] = uw.ParticleDepthLayers;
                        effectData["particlelighting"] = uw.ParticleLighting;
                        effectData["particlescattering"] = uw.ParticleScattering;
                        effectData["particleturbulence"] = uw.ParticleTurbulence;
                        effectData["particlegodrayglow"] = uw.ParticleGodRayGlow;
                        effectData["particlefocusdistance"] = uw.ParticleFocusDistance;
                        effectData["particlefocusrange"] = uw.ParticleFocusRange;
                        effectData["particlenearfade"] = uw.ParticleNearFade;
                        effectData["particlefarfade"] = uw.ParticleFarFade;
                        if (uw.ParticleTextureGuid.HasValue)
                            effectData["particletextureguid"] = uw.ParticleTextureGuid.Value.ToString();
                        // Caustics
                        effectData["causticsenabled"] = uw.CausticsEnabled;
                        effectData["causticsintensity"] = uw.CausticsIntensity;
                        effectData["causticsscale"] = uw.CausticsScale;
                        effectData["causticsspeed"] = uw.CausticsSpeed;
                        effectData["causticsoctaves"] = uw.CausticsOctaves;
                        effectData["causticsbrightness"] = uw.CausticsBrightness;
                        effectData["causticssharpness"] = uw.CausticsSharpness;
                        effectData["causticsdistortion"] = uw.CausticsDistortion;
                        effectData["causticsdepthfalloff"] = uw.CausticsDepthFalloff;
                        effectData["causticschromatic"] = uw.CausticsChromatic;
                        // Tint & Ambient
                        effectData["tintcolor"] = new[] { uw.TintColor.X, uw.TintColor.Y, uw.TintColor.Z };
                        effectData["ambientintensity"] = uw.AmbientIntensity;
                        effectData["ambientcolor"] = new[] { uw.AmbientColor.X, uw.AmbientColor.Y, uw.AmbientColor.Z };
                        // Screen Distortion
                        effectData["distortionenabled"] = uw.DistortionEnabled;
                        effectData["distortionintensity"] = uw.DistortionIntensity;
                        effectData["distortionscale"] = uw.DistortionScale;
                        effectData["distortionspeed"] = uw.DistortionSpeed;
                        effectData["distortionchromatic"] = uw.DistortionChromatic;
                        effectData["distortionusewaves"] = uw.DistortionUseWaves;
                        effectData["distortionwaveinfluence"] = uw.DistortionWaveInfluence;
                        effectData["distortionnoiseinfluence"] = uw.DistortionNoiseInfluence;
                        effectData["distortiondepthfade"] = uw.DistortionDepthFade;
                        // Snell's Window
                        effectData["snellwindowenabled"] = uw.SnellWindowEnabled;
                        effectData["snellcriticalangle"] = uw.SnellCriticalAngle;
                        effectData["snelledgesoftness"] = uw.SnellEdgeSoftness;
                        effectData["snellreflectiontint"] = new[] { uw.SnellReflectionTint.X, uw.SnellReflectionTint.Y, uw.SnellReflectionTint.Z };
                        effectData["snellreflectionstrength"] = uw.SnellReflectionStrength;
                        effectData["snellfresnelpower"] = uw.SnellFresnelPower;
                        effectData["snellwavedistortion"] = uw.SnellWaveDistortion;
                        // Water Transition
                        effectData["transitionenabled"] = uw.TransitionEnabled;
                        effectData["transitionduration"] = uw.TransitionDuration;
                        effectData["enterbubbleintensity"] = uw.EnterBubbleIntensity;
                        effectData["enterbubblesize"] = uw.EnterBubbleSize;
                        effectData["enterbubblecount"] = uw.EnterBubbleCount;
                        effectData["enterdistortion"] = uw.EnterDistortion;
                        effectData["exitdropletintensity"] = uw.ExitDropletIntensity;
                        effectData["exitdropletsize"] = uw.ExitDropletSize;
                        effectData["exitdropletcount"] = uw.ExitDropletCount;
                        effectData["exitdripspeed"] = uw.ExitDripSpeed;
                        break;
                }

                effectsData.Add(effectData);
            }
            
            result["effects"] = effectsData;
            return result;
        }

        public void Deserialize(GlobalEffects component, Dictionary<string, JsonElement> data)
        {
            // Désérialiser la propriété Enabled
            if (data.TryGetValue("enabled", out var enabledElement))
            {
                component.Enabled = enabledElement.GetBoolean();
            }
            
            // Désérialiser la liste des effets
            if (data.TryGetValue("effects", out var effectsElement) && 
                effectsElement.ValueKind == JsonValueKind.Array)
            {
                component.RemoveAllEffects(); // Nettoyer les effets existants
                
                foreach (var effectElement in effectsElement.EnumerateArray())
                {
                    if (effectElement.ValueKind != JsonValueKind.Object) continue;
                    
                    var effectObj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(effectElement);
                    if (effectObj == null || !effectObj.TryGetValue("type", out var typeElement)) continue;
                    
                    var typeName = typeElement.GetString();
                    PostProcessEffect? effect = typeName switch
                    {
                        "Engine.Rendering.ToneMappingEffect" or "ToneMappingEffect" => new ToneMappingEffect(),
                        "Engine.Rendering.BloomEffect" or "BloomEffect" => new BloomEffect(),
                        "Engine.Rendering.FXAAEffect" or "FXAAEffect" => new FXAAEffect(),
                        "Engine.Rendering.ChromaticAberrationEffect" or "ChromaticAberrationEffect" => new ChromaticAberrationEffect(),
                        "Engine.Rendering.SSAOEffect" or "SSAOEffect" => new SSAOEffect(),
                        "Engine.Rendering.GTAOEffect" or "GTAOEffect" => new GTAOEffect(),
                        "Engine.Rendering.DepthOfFieldEffect" or "DepthOfFieldEffect" => new DepthOfFieldEffect(),
                        "Engine.Rendering.MotionBlurEffect" or "MotionBlurEffect" => new MotionBlurEffect(),
                        "Engine.Rendering.ColorGradingEffect" or "ColorGradingEffect" => new ColorGradingEffect(),
                        "Engine.Rendering.VolumetricFogEffect" or "VolumetricFogEffect" => new VolumetricFogEffect(),
                        "Engine.Rendering.AtmosphericScatteringEffect" or "AtmosphericScatteringEffect" => new AtmosphericScatteringEffect(),
                        "Engine.Rendering.UnderwaterEffect" or "UnderwaterEffect" => new UnderwaterEffect(),
                        _ => null
                    };
                    
                    if (effect == null) continue;
                    
                    // Désérialiser les propriétés de base
                    if (effectObj.TryGetValue("enabled", out var effectEnabledElement))
                        effect.Enabled = effectEnabledElement.GetBoolean();
                    
                    if (effectObj.TryGetValue("intensity", out var intensityElement))
                        effect.Intensity = intensityElement.GetSingle();
                        
                    if (effectObj.TryGetValue("priority", out var priorityElement))
                        effect.Priority = priorityElement.GetInt32();
                    
                    // Désérialiser les propriétés spécifiques
                    switch (effect)
                    {
                        case ToneMappingEffect toneMap:
                            if (effectObj.TryGetValue("mode", out var modeElement))
                            {
                                if (Enum.TryParse<ToneMappingEffect.ToneMappingMode>(modeElement.GetString(), out var mode))
                                    toneMap.Mode = mode;
                            }
                            if (effectObj.TryGetValue("exposure", out var exposureElement))
                                toneMap.Exposure = exposureElement.GetSingle();
                            if (effectObj.TryGetValue("whitepoint", out var whitePointElement))
                                toneMap.WhitePoint = whitePointElement.GetSingle();
                            if (effectObj.TryGetValue("gamma", out var gammaElement))
                                toneMap.Gamma = gammaElement.GetSingle();
                                // Auto-exposure
                                if (effectObj.TryGetValue("autoexposure", out var aeElement))
                                    toneMap.AutoExposure = aeElement.GetBoolean();
                                if (effectObj.TryGetValue("minexposure", out var minEl))
                                    toneMap.MinExposure = minEl.GetSingle();
                                if (effectObj.TryGetValue("maxexposure", out var maxEl))
                                    toneMap.MaxExposure = maxEl.GetSingle();
                                if (effectObj.TryGetValue("adaptationspeed", out var adaptEl))
                                    toneMap.AdaptationSpeed = adaptEl.GetSingle();
                                if (effectObj.TryGetValue("targetbrightness", out var targetEl))
                                    toneMap.TargetBrightness = targetEl.GetSingle();
                                if (effectObj.TryGetValue("exposurecompensation", out var compEl))
                                    toneMap.ExposureCompensation = compEl.GetSingle();
                            break;
                            
                        case BloomEffect bloom:
                            if (effectObj.TryGetValue("threshold", out var thresholdElement))
                                bloom.Threshold = thresholdElement.GetSingle();
                            if (effectObj.TryGetValue("softknee", out var softKneeElement))
                                bloom.SoftKnee = softKneeElement.GetSingle();
                            if (effectObj.TryGetValue("radius", out var bloomRadiusElement))
                                bloom.Radius = bloomRadiusElement.GetSingle();
                            if (effectObj.TryGetValue("iterations", out var iterationsElement))
                                bloom.Iterations = iterationsElement.GetInt32();
                            if (effectObj.TryGetValue("clamp", out var clampElement))
                                bloom.Clamp = clampElement.GetSingle();
                            if (effectObj.TryGetValue("scattering", out var scatteringElement))
                                bloom.Scattering = scatteringElement.GetSingle();
                            break;
                            
                        case ChromaticAberrationEffect chromatic:
                            if (effectObj.TryGetValue("strength", out var strengthElement))
                                chromatic.Strength = strengthElement.GetSingle();
                            if (effectObj.TryGetValue("usespectrallut", out var spectralElement))
                                chromatic.UseSpectralLut = spectralElement.GetBoolean();
                            if (effectObj.TryGetValue("focallength", out var focalElement))
                                chromatic.FocalLength = focalElement.GetSingle();
                            break;
                            
                        case SSAOEffect ssao:
                            if (effectObj.TryGetValue("radius", out var ssaoRadiusElement))
                                ssao.Radius = ssaoRadiusElement.GetSingle();
                            if (effectObj.TryGetValue("bias", out var biasElement))
                                ssao.Bias = biasElement.GetSingle();
                            if (effectObj.TryGetValue("power", out var powerElement))
                                ssao.Power = powerElement.GetSingle();
                            if (effectObj.TryGetValue("samplecount", out var ssaoSampleElement))
                                ssao.SampleCount = ssaoSampleElement.GetInt32();
                            if (effectObj.TryGetValue("blursize", out var blurSizeElement))
                                ssao.BlurSize = blurSizeElement.GetInt32();
                            if (effectObj.TryGetValue("maxdistance", out var ssaoMaxDistElement))
                                ssao.MaxDistance = ssaoMaxDistElement.GetSingle();
                            break;
                            
                        case GTAOEffect gtao:
                            if (effectObj.TryGetValue("radius", out var gtaoRadiusElement))
                                gtao.Radius = gtaoRadiusElement.GetSingle();
                            if (effectObj.TryGetValue("thickness", out var thicknessElement))
                                gtao.Thickness = thicknessElement.GetSingle();
                            if (effectObj.TryGetValue("falloffrange", out var falloffElement))
                                gtao.FalloffRange = falloffElement.GetSingle();
                            if (effectObj.TryGetValue("samplecount", out var gtaoSampleElement))
                                gtao.SampleCount = gtaoSampleElement.GetInt32();
                            if (effectObj.TryGetValue("slicecount", out var sliceCountElement))
                                gtao.SliceCount = sliceCountElement.GetInt32();
                            if (effectObj.TryGetValue("blurradius", out var blurRadiusElement))
                                gtao.BlurRadius = blurRadiusElement.GetInt32();
                            if (effectObj.TryGetValue("maxdistance", out var gtaoMaxDistElement))
                                gtao.MaxDistance = gtaoMaxDistElement.GetSingle();
                            if (effectObj.TryGetValue("enabletemporal", out var enableTemporalElement))
                                gtao.EnableTemporal = enableTemporalElement.GetBoolean();
                            if (effectObj.TryGetValue("temporalblendfactor", out var temporalBlendElement))
                                gtao.TemporalBlendFactor = temporalBlendElement.GetSingle();
                            if (effectObj.TryGetValue("temporalvariancethreshold", out var temporalVarianceElement))
                                gtao.TemporalVarianceThreshold = temporalVarianceElement.GetSingle();
                            if (effectObj.TryGetValue("miplevels", out var mipLevelsElement))
                                gtao.MipLevels = mipLevelsElement.GetInt32();
                            if (effectObj.TryGetValue("mipweight0", out var mipWeight0Element))
                                gtao.MipWeight0 = mipWeight0Element.GetSingle();
                            if (effectObj.TryGetValue("mipweight1", out var mipWeight1Element))
                                gtao.MipWeight1 = mipWeight1Element.GetSingle();
                            if (effectObj.TryGetValue("mipweight2", out var mipWeight2Element))
                                gtao.MipWeight2 = mipWeight2Element.GetSingle();
                            if (effectObj.TryGetValue("mipweight3", out var mipWeight3Element))
                                gtao.MipWeight3 = mipWeight3Element.GetSingle();
                            if (effectObj.TryGetValue("mipradius0", out var mipRadius0Element))
                                gtao.MipRadius0 = mipRadius0Element.GetSingle();
                            if (effectObj.TryGetValue("mipradius1", out var mipRadius1Element))
                                gtao.MipRadius1 = mipRadius1Element.GetSingle();
                            if (effectObj.TryGetValue("mipradius2", out var mipRadius2Element))
                                gtao.MipRadius2 = mipRadius2Element.GetSingle();
                            if (effectObj.TryGetValue("mipradius3", out var mipRadius3Element))
                                gtao.MipRadius3 = mipRadius3Element.GetSingle();
                            break;
                        case FXAAEffect fxaa:
                            if (effectObj.TryGetValue("quality", out var qualElement))
                                fxaa.Quality = qualElement.GetSingle();
                            break;

                        case DepthOfFieldEffect dof:
                            if (effectObj.TryGetValue("focusdistance", out var focusDistanceElement))
                                dof.FocusDistance = focusDistanceElement.GetSingle();
                            if (effectObj.TryGetValue("focusrange", out var focusRangeElement))
                                dof.FocusRange = focusRangeElement.GetSingle();
                            if (effectObj.TryGetValue("focallength", out var focalLengthElement))
                                dof.FocalLength = focalLengthElement.GetSingle();
                            if (effectObj.TryGetValue("aperture", out var apertureElement))
                                dof.Aperture = apertureElement.GetSingle();
                            if (effectObj.TryGetValue("maxcoc", out var maxCoCElement))
                                dof.MaxCoC = maxCoCElement.GetSingle();
                            if (effectObj.TryGetValue("samplecount", out var dofSampleCountElement))
                                dof.SampleCount = dofSampleCountElement.GetInt32();
                            if (effectObj.TryGetValue("bokehradius", out var bokehRadiusElement))
                                dof.BokehRadius = bokehRadiusElement.GetSingle();
                            if (effectObj.TryGetValue("enableadaptive", out var enableAdaptiveElement))
                                dof.EnableAdaptiveDOF = enableAdaptiveElement.GetBoolean();
                            if (effectObj.TryGetValue("adaptivespeed", out var adaptiveSpeedElement))
                                dof.AdaptiveSpeed = adaptiveSpeedElement.GetSingle();
                            if (effectObj.TryGetValue("adaptivecenterbias", out var adaptiveCenterBiasElement))
                                dof.AdaptiveCenterBias = adaptiveCenterBiasElement.GetSingle();
                            if (effectObj.TryGetValue("adaptivemindistance", out var adaptiveMinDistanceElement))
                                dof.AdaptiveMinDistance = adaptiveMinDistanceElement.GetSingle();
                            if (effectObj.TryGetValue("adaptivemaxdistance", out var adaptiveMaxDistanceElement))
                                dof.AdaptiveMaxDistance = adaptiveMaxDistanceElement.GetSingle();
                            break;

                        case MotionBlurEffect motionBlur:
                            if (effectObj.TryGetValue("samplecount", out var mbSampleCountElement))
                                motionBlur.SampleCount = mbSampleCountElement.GetInt32();
                            if (effectObj.TryGetValue("maxblurradius", out var maxBlurRadiusElement))
                                motionBlur.MaxBlurRadius = maxBlurRadiusElement.GetSingle();
                            break;

                        case ColorGradingEffect colorGrading:
                            if (effectObj.TryGetValue("source", out var sourceElement))
                            {
                                if (Enum.TryParse<ColorGradingEffect.ColorGradingSource>(sourceElement.GetString(), out var source))
                                    colorGrading.Source = source;
                            }
                            if (effectObj.TryGetValue("blendfactor", out var blendFactorElement))
                                colorGrading.BlendFactor = blendFactorElement.GetSingle();
                            // Manual parameters
                            if (effectObj.TryGetValue("saturation", out var saturationElement))
                                colorGrading.Saturation = saturationElement.GetSingle();
                            if (effectObj.TryGetValue("contrast", out var contrastElement))
                                colorGrading.Contrast = contrastElement.GetSingle();
                            if (effectObj.TryGetValue("brightness", out var brightnessElement))
                                colorGrading.Brightness = brightnessElement.GetSingle();
                            if (effectObj.TryGetValue("colorfilter", out var colorFilterElement))
                            {
                                var arr = colorFilterElement.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                                if (arr.Length == 3)
                                    colorGrading.ColorFilter = new OpenTK.Mathematics.Vector3(arr[0], arr[1], arr[2]);
                            }
                            if (effectObj.TryGetValue("temperature", out var tempElement))
                                colorGrading.Temperature = tempElement.GetSingle();
                            if (effectObj.TryGetValue("tint", out var tintElement))
                                colorGrading.Tint = tintElement.GetSingle();
                            if (effectObj.TryGetValue("hueshift", out var hueShiftElement))
                                colorGrading.HueShift = hueShiftElement.GetSingle();
                            if (effectObj.TryGetValue("vibrance", out var vibranceElement))
                                colorGrading.Vibrance = vibranceElement.GetSingle();
                            // Time of day presets
                            if (effectObj.TryGetValue("nightsaturation", out var nightSatElement))
                                colorGrading.NightSaturation = nightSatElement.GetSingle();
                            if (effectObj.TryGetValue("nightcontrast", out var nightContElement))
                                colorGrading.NightContrast = nightContElement.GetSingle();
                            if (effectObj.TryGetValue("nighttemperature", out var nightTempElement))
                                colorGrading.NightTemperature = nightTempElement.GetSingle();
                            if (effectObj.TryGetValue("daysaturation", out var daySatElement))
                                colorGrading.DaySaturation = daySatElement.GetSingle();
                            if (effectObj.TryGetValue("daycontrast", out var dayContElement))
                                colorGrading.DayContrast = dayContElement.GetSingle();
                            if (effectObj.TryGetValue("daytemperature", out var dayTempElement))
                                colorGrading.DayTemperature = dayTempElement.GetSingle();
                            if (effectObj.TryGetValue("sunrisesaturation", out var sunriseSatElement))
                                colorGrading.SunriseSaturation = sunriseSatElement.GetSingle();
                            if (effectObj.TryGetValue("sunrisecontrast", out var sunriseContElement))
                                colorGrading.SunriseContrast = sunriseContElement.GetSingle();
                            if (effectObj.TryGetValue("sunrisetemperature", out var sunriseTempElement))
                                colorGrading.SunriseTemperature = sunriseTempElement.GetSingle();
                            if (effectObj.TryGetValue("sunsetsaturation", out var sunsetSatElement))
                                colorGrading.SunsetSaturation = sunsetSatElement.GetSingle();
                            if (effectObj.TryGetValue("sunsetcontrast", out var sunsetContElement))
                                colorGrading.SunsetContrast = sunsetContElement.GetSingle();
                            if (effectObj.TryGetValue("sunsettemperature", out var sunsetTempElement))
                                colorGrading.SunsetTemperature = sunsetTempElement.GetSingle();
                            if (effectObj.TryGetValue("transitionspeed", out var transSpeedElement))
                                colorGrading.TransitionSpeed = transSpeedElement.GetSingle();
                            break;

                        case VolumetricFogEffect volFog:
                            // Source mode
                            if (effectObj.TryGetValue("source", out var volFogSourceElement))
                            {
                                if (Enum.TryParse<VolumetricFogEffect.FogSource>(volFogSourceElement.GetString(), out var volFogSource))
                                    volFog.Source = volFogSource;
                            }
                            if (effectObj.TryGetValue("blendfactor", out var volFogBlendElement))
                                volFog.BlendFactor = volFogBlendElement.GetSingle();
                            // Local fog parameters
                            if (effectObj.TryGetValue("fogcolor", out var volFogColorElement))
                            {
                                var arr = volFogColorElement.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                                if (arr.Length == 3)
                                    volFog.FogColor = new OpenTK.Mathematics.Vector3(arr[0], arr[1], arr[2]);
                            }
                            if (effectObj.TryGetValue("density", out var volFogDensityElement))
                                volFog.Density = volFogDensityElement.GetSingle();
                            if (effectObj.TryGetValue("depthstart", out var volFogDepthStartElement))
                                volFog.DepthStart = volFogDepthStartElement.GetSingle();
                            if (effectObj.TryGetValue("depthend", out var volFogDepthEndElement))
                                volFog.DepthEnd = volFogDepthEndElement.GetSingle();
                            // Ray marching
                            if (effectObj.TryGetValue("raymarchsteps", out var volFogRayMarchElement))
                                volFog.RayMarchSteps = volFogRayMarchElement.GetInt32();
                            // Height-based fog
                            if (effectObj.TryGetValue("useheightfog", out var volFogUseHeightElement))
                                volFog.UseHeightFog = volFogUseHeightElement.GetBoolean();
                            if (effectObj.TryGetValue("heightfalloff", out var volFogHeightFalloffElement))
                                volFog.HeightFalloff = volFogHeightFalloffElement.GetSingle();
                            if (effectObj.TryGetValue("baseheight", out var volFogBaseHeightElement))
                                volFog.BaseHeight = volFogBaseHeightElement.GetSingle();
                            if (effectObj.TryGetValue("maxheight", out var volFogMaxHeightElement))
                                volFog.MaxHeight = volFogMaxHeightElement.GetSingle();
                            // Scattering & god rays
                            if (effectObj.TryGetValue("usesunscattering", out var volFogUseSunElement))
                                volFog.UseSunScattering = volFogUseSunElement.GetBoolean();
                            if (effectObj.TryGetValue("scatteringintensity", out var volFogScatterIntElement))
                                volFog.ScatteringIntensity = volFogScatterIntElement.GetSingle();
                            if (effectObj.TryGetValue("mieg", out var volFogMieGElement))
                                volFog.MieG = volFogMieGElement.GetSingle();
                            if (effectObj.TryGetValue("extinctionfactor", out var volFogExtinctionElement))
                                volFog.ExtinctionFactor = volFogExtinctionElement.GetSingle();
                            if (effectObj.TryGetValue("sunscatteringcolor", out var volFogSunColorElement))
                            {
                                var arr = volFogSunColorElement.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                                if (arr.Length == 3)
                                    volFog.SunScatteringColor = new OpenTK.Mathematics.Vector3(arr[0], arr[1], arr[2]);
                            }
                            // Scatter color options
                            if (effectObj.TryGetValue("scattersource", out var volFogScatterSourceElement))
                            {
                                if (Enum.TryParse<VolumetricFogEffect.ScatterColorSource>(volFogScatterSourceElement.GetString(), out var scatterSource))
                                    volFog.ScatterSource = scatterSource;
                            }
                            if (effectObj.TryGetValue("inscattercolor", out var volFogInScatterColorElement))
                            {
                                var arr = volFogInScatterColorElement.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                                if (arr.Length == 3)
                                    volFog.InScatterColor = new OpenTK.Mathematics.Vector3(arr[0], arr[1], arr[2]);
                            }
                            if (effectObj.TryGetValue("ambientcolor", out var volFogAmbientColorElement))
                            {
                                var arr = volFogAmbientColorElement.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                                if (arr.Length == 3)
                                    volFog.AmbientColor = new OpenTK.Mathematics.Vector3(arr[0], arr[1], arr[2]);
                            }
                            if (effectObj.TryGetValue("ambientintensity", out var volFogAmbientElement))
                                volFog.AmbientIntensity = volFogAmbientElement.GetSingle();
                            if (effectObj.TryGetValue("useambientfromsky", out var volFogUseAmbientFromSkyElement))
                                volFog.UseAmbientFromSky = volFogUseAmbientFromSkyElement.GetBoolean();
                            // God rays radial blur
                            if (effectObj.TryGetValue("godraysintensity", out var volFogGodRaysIntElement))
                                volFog.GodRaysIntensity = volFogGodRaysIntElement.GetSingle();
                            if (effectObj.TryGetValue("godraysdensity", out var volFogGodRaysDensityElement))
                                volFog.GodRaysDensity = volFogGodRaysDensityElement.GetSingle();
                            if (effectObj.TryGetValue("godraysdecay", out var volFogGodRaysDecayElement))
                                volFog.GodRaysDecay = volFogGodRaysDecayElement.GetSingle();
                            // Noise/detail
                            if (effectObj.TryGetValue("usenoise", out var volFogUseNoiseElement))
                                volFog.UseNoise = volFogUseNoiseElement.GetBoolean();
                            if (effectObj.TryGetValue("noisescale", out var volFogNoiseScaleElement))
                                volFog.NoiseScale = volFogNoiseScaleElement.GetSingle();
                            if (effectObj.TryGetValue("noisespeed", out var volFogNoiseSpeedElement))
                                volFog.NoiseSpeed = volFogNoiseSpeedElement.GetSingle();
                            if (effectObj.TryGetValue("noisestrength", out var volFogNoiseStrengthElement))
                                volFog.NoiseStrength = volFogNoiseStrengthElement.GetSingle();
                            if (effectObj.TryGetValue("noiseoctaves", out var volFogNoiseOctavesElement))
                                volFog.NoiseOctaves = volFogNoiseOctavesElement.GetInt32();
                            break;

                        case AtmosphericScatteringEffect atmo:
                            // Planet geometry
                            if (effectObj.TryGetValue("planetradius", out var planetRadiusElement))
                                atmo.PlanetRadius = planetRadiusElement.GetSingle();
                            if (effectObj.TryGetValue("atmosphereradius", out var atmoRadiusElement))
                                atmo.AtmosphereRadius = atmoRadiusElement.GetSingle();
                            // Scale heights
                            if (effectObj.TryGetValue("rayleighscaleheight", out var rayScaleElement))
                                atmo.RayleighScaleHeight = rayScaleElement.GetSingle();
                            if (effectObj.TryGetValue("miescaleheight", out var mieScaleElement))
                                atmo.MieScaleHeight = mieScaleElement.GetSingle();
                            // Scattering coefficients
                            if (effectObj.TryGetValue("rayleighcoefficients", out var rayCoeffElement))
                            {
                                var arr = rayCoeffElement.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                                if (arr.Length == 3)
                                    atmo.RayleighCoefficients = new OpenTK.Mathematics.Vector3(arr[0], arr[1], arr[2]);
                            }
                            if (effectObj.TryGetValue("miecoefficient", out var mieCoeffElement))
                                atmo.MieCoefficient = mieCoeffElement.GetSingle();
                            if (effectObj.TryGetValue("mieg", out var mieGElement))
                                atmo.MieG = mieGElement.GetSingle();
                            // Sun
                            if (effectObj.TryGetValue("sunintensity", out var sunIntensityElement))
                                atmo.SunIntensity = sunIntensityElement.GetSingle();
                            // Quality
                            if (effectObj.TryGetValue("numsamples", out var numSamplesElement))
                                atmo.NumSamples = numSamplesElement.GetInt32();
                            if (effectObj.TryGetValue("numlightsamples", out var numLightSamplesElement))
                                atmo.NumLightSamples = numLightSamplesElement.GetInt32();
                            // Output
                            if (effectObj.TryGetValue("exposure", out var atmoExposureElement))
                                atmo.Exposure = atmoExposureElement.GetSingle();
                            break;

                        case UnderwaterEffect uw:
                            // Water level
                            if (effectObj.TryGetValue("waterlevelsource", out var uwSourceElement))
                            {
                                if (Enum.TryParse<UnderwaterEffect.WaterLevelSource>(uwSourceElement.GetString(), out var uwSource))
                                    uw.Source = uwSource;
                            }
                            if (effectObj.TryGetValue("waterlevel", out var uwWaterLevelElement))
                                uw.WaterLevel = uwWaterLevelElement.GetSingle();
                            // Fog
                            if (effectObj.TryGetValue("fogenabled", out var uwFogEnabledElement))
                                uw.FogEnabled = uwFogEnabledElement.GetBoolean();
                            if (effectObj.TryGetValue("fogcolor", out var uwFogColorElement))
                            {
                                var arr = uwFogColorElement.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                                if (arr.Length == 3)
                                    uw.FogColor = new OpenTK.Mathematics.Vector3(arr[0], arr[1], arr[2]);
                            }
                            if (effectObj.TryGetValue("fogdensity", out var uwFogDensityElement))
                                uw.FogDensity = uwFogDensityElement.GetSingle();
                            if (effectObj.TryGetValue("visibility", out var uwVisibilityElement))
                                uw.Visibility = uwVisibilityElement.GetSingle();
                            // Absorption
                            if (effectObj.TryGetValue("absorptionenabled", out var uwAbsEnabledElement))
                                uw.AbsorptionEnabled = uwAbsEnabledElement.GetBoolean();
                            if (effectObj.TryGetValue("absorptionr", out var uwAbsRElement))
                                uw.AbsorptionR = uwAbsRElement.GetSingle();
                            if (effectObj.TryGetValue("absorptiong", out var uwAbsGElement))
                                uw.AbsorptionG = uwAbsGElement.GetSingle();
                            if (effectObj.TryGetValue("absorptionb", out var uwAbsBElement))
                                uw.AbsorptionB = uwAbsBElement.GetSingle();
                            // God rays
                            if (effectObj.TryGetValue("godraysenabled", out var uwGodRaysEnabledElement))
                                uw.GodRaysEnabled = uwGodRaysEnabledElement.GetBoolean();
                            if (effectObj.TryGetValue("godraysintensity", out var uwGodRaysIntensityElement))
                                uw.GodRaysIntensity = uwGodRaysIntensityElement.GetSingle();
                            if (effectObj.TryGetValue("godrayscolor", out var uwGodRaysColorElement))
                            {
                                var arr = uwGodRaysColorElement.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                                if (arr.Length == 3)
                                    uw.GodRaysColor = new OpenTK.Mathematics.Vector3(arr[0], arr[1], arr[2]);
                            }
                            if (effectObj.TryGetValue("godraysdensity", out var uwGodRaysDensityElement))
                                uw.GodRaysDensity = uwGodRaysDensityElement.GetSingle();
                            if (effectObj.TryGetValue("godraysdecay", out var uwGodRaysDecayElement))
                                uw.GodRaysDecay = uwGodRaysDecayElement.GetSingle();
                            if (effectObj.TryGetValue("godrayssamples", out var uwGodRaysSamplesElement))
                                uw.GodRaysSamples = uwGodRaysSamplesElement.GetInt32();
                            // Particles (volumetric with depth and lighting)
                            if (effectObj.TryGetValue("particlesenabled", out var uwParticlesEnabledElement))
                                uw.ParticlesEnabled = uwParticlesEnabledElement.GetBoolean();
                            if (effectObj.TryGetValue("particledensity", out var uwParticleDensityElement))
                                uw.ParticleDensity = uwParticleDensityElement.GetSingle();
                            if (effectObj.TryGetValue("particlecolor", out var uwParticleColorElement))
                            {
                                var arr = uwParticleColorElement.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                                if (arr.Length == 3)
                                    uw.ParticleColor = new OpenTK.Mathematics.Vector3(arr[0], arr[1], arr[2]);
                            }
                            if (effectObj.TryGetValue("particlebrightness", out var uwParticleBrightnessElement))
                                uw.ParticleBrightness = uwParticleBrightnessElement.GetSingle();
                            if (effectObj.TryGetValue("particlespeed", out var uwParticleSpeedElement))
                                uw.ParticleSpeed = uwParticleSpeedElement.GetSingle();
                            if (effectObj.TryGetValue("particlesizemin", out var uwParticleSizeMinElement))
                                uw.ParticleSizeMin = uwParticleSizeMinElement.GetSingle();
                            if (effectObj.TryGetValue("particlesizemax", out var uwParticleSizeMaxElement))
                                uw.ParticleSizeMax = uwParticleSizeMaxElement.GetSingle();
                            if (effectObj.TryGetValue("particledepthlayers", out var uwParticleDepthLayersElement))
                                uw.ParticleDepthLayers = uwParticleDepthLayersElement.GetInt32();
                            if (effectObj.TryGetValue("particlelighting", out var uwParticleLightingElement))
                                uw.ParticleLighting = uwParticleLightingElement.GetSingle();
                            if (effectObj.TryGetValue("particlescattering", out var uwParticleScatteringElement))
                                uw.ParticleScattering = uwParticleScatteringElement.GetSingle();
                            if (effectObj.TryGetValue("particleturbulence", out var uwParticleTurbulenceElement))
                                uw.ParticleTurbulence = uwParticleTurbulenceElement.GetSingle();
                            if (effectObj.TryGetValue("particlegodrayglow", out var uwParticleGodRayGlowElement))
                                uw.ParticleGodRayGlow = uwParticleGodRayGlowElement.GetSingle();
                            if (effectObj.TryGetValue("particlefocusdistance", out var uwParticleFocusDistanceElement))
                                uw.ParticleFocusDistance = uwParticleFocusDistanceElement.GetSingle();
                            if (effectObj.TryGetValue("particlefocusrange", out var uwParticleFocusRangeElement))
                                uw.ParticleFocusRange = uwParticleFocusRangeElement.GetSingle();
                            if (effectObj.TryGetValue("particlenearfade", out var uwParticleNearFadeElement))
                                uw.ParticleNearFade = uwParticleNearFadeElement.GetSingle();
                            if (effectObj.TryGetValue("particlefarfade", out var uwParticleFarFadeElement))
                                uw.ParticleFarFade = uwParticleFarFadeElement.GetSingle();
                            if (effectObj.TryGetValue("particletextureguid", out var uwParticleTextureGuidElement))
                            {
                                var guidStr = uwParticleTextureGuidElement.GetString();
                                if (!string.IsNullOrEmpty(guidStr) && Guid.TryParse(guidStr, out var guid))
                                    uw.ParticleTextureGuid = guid;
                            }
                            // Caustics
                            if (effectObj.TryGetValue("causticsenabled", out var uwCausticsEnabledElement))
                                uw.CausticsEnabled = uwCausticsEnabledElement.GetBoolean();
                            if (effectObj.TryGetValue("causticsintensity", out var uwCausticsIntensityElement))
                                uw.CausticsIntensity = uwCausticsIntensityElement.GetSingle();
                            if (effectObj.TryGetValue("causticsscale", out var uwCausticsScaleElement))
                                uw.CausticsScale = uwCausticsScaleElement.GetSingle();
                            if (effectObj.TryGetValue("causticsspeed", out var uwCausticsSpeedElement))
                                uw.CausticsSpeed = uwCausticsSpeedElement.GetSingle();
                            if (effectObj.TryGetValue("causticsoctaves", out var uwCausticsOctavesElement))
                                uw.CausticsOctaves = uwCausticsOctavesElement.GetInt32();
                            if (effectObj.TryGetValue("causticsbrightness", out var uwCausticsBrightnessElement))
                                uw.CausticsBrightness = uwCausticsBrightnessElement.GetSingle();
                            if (effectObj.TryGetValue("causticssharpness", out var uwCausticsSharpnessElement))
                                uw.CausticsSharpness = uwCausticsSharpnessElement.GetSingle();
                            if (effectObj.TryGetValue("causticsdistortion", out var uwCausticsDistortionElement))
                                uw.CausticsDistortion = uwCausticsDistortionElement.GetSingle();
                            if (effectObj.TryGetValue("causticsdepthfalloff", out var uwCausticsDepthFalloffElement))
                                uw.CausticsDepthFalloff = uwCausticsDepthFalloffElement.GetSingle();
                            if (effectObj.TryGetValue("causticschromatic", out var uwCausticsChromaticElement))
                                uw.CausticsChromatic = uwCausticsChromaticElement.GetSingle();
                            // Tint & Ambient
                            if (effectObj.TryGetValue("tintcolor", out var uwTintColorElement))
                            {
                                var arr = uwTintColorElement.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                                if (arr.Length == 3)
                                    uw.TintColor = new OpenTK.Mathematics.Vector3(arr[0], arr[1], arr[2]);
                            }
                            if (effectObj.TryGetValue("ambientintensity", out var uwAmbientIntensityElement))
                                uw.AmbientIntensity = uwAmbientIntensityElement.GetSingle();
                            if (effectObj.TryGetValue("ambientcolor", out var uwAmbientColorElement))
                            {
                                var arr = uwAmbientColorElement.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                                if (arr.Length == 3)
                                    uw.AmbientColor = new OpenTK.Mathematics.Vector3(arr[0], arr[1], arr[2]);
                            }
                            // Screen Distortion
                            if (effectObj.TryGetValue("distortionenabled", out var uwDistEnabledElement))
                                uw.DistortionEnabled = uwDistEnabledElement.GetBoolean();
                            if (effectObj.TryGetValue("distortionintensity", out var uwDistIntensityElement))
                                uw.DistortionIntensity = uwDistIntensityElement.GetSingle();
                            if (effectObj.TryGetValue("distortionscale", out var uwDistScaleElement))
                                uw.DistortionScale = uwDistScaleElement.GetSingle();
                            if (effectObj.TryGetValue("distortionspeed", out var uwDistSpeedElement))
                                uw.DistortionSpeed = uwDistSpeedElement.GetSingle();
                            if (effectObj.TryGetValue("distortionchromatic", out var uwDistChromaticElement))
                                uw.DistortionChromatic = uwDistChromaticElement.GetSingle();
                            if (effectObj.TryGetValue("distortionusewaves", out var uwDistUseWavesElement))
                                uw.DistortionUseWaves = uwDistUseWavesElement.GetBoolean();
                            if (effectObj.TryGetValue("distortionwaveinfluence", out var uwDistWaveInfluenceElement))
                                uw.DistortionWaveInfluence = uwDistWaveInfluenceElement.GetSingle();
                            if (effectObj.TryGetValue("distortionnoiseinfluence", out var uwDistNoiseInfluenceElement))
                                uw.DistortionNoiseInfluence = uwDistNoiseInfluenceElement.GetSingle();
                            if (effectObj.TryGetValue("distortiondepthfade", out var uwDistDepthFadeElement))
                                uw.DistortionDepthFade = uwDistDepthFadeElement.GetSingle();
                            // Snell's Window
                            if (effectObj.TryGetValue("snellwindowenabled", out var uwSnellEnabledElement))
                                uw.SnellWindowEnabled = uwSnellEnabledElement.GetBoolean();
                            if (effectObj.TryGetValue("snellcriticalangle", out var uwSnellAngleElement))
                                uw.SnellCriticalAngle = uwSnellAngleElement.GetSingle();
                            if (effectObj.TryGetValue("snelledgesoftness", out var uwSnellSoftnessElement))
                                uw.SnellEdgeSoftness = uwSnellSoftnessElement.GetSingle();
                            if (effectObj.TryGetValue("snellreflectiontint", out var uwSnellTintElement))
                            {
                                var arr = uwSnellTintElement.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                                if (arr.Length == 3)
                                    uw.SnellReflectionTint = new OpenTK.Mathematics.Vector3(arr[0], arr[1], arr[2]);
                            }
                            if (effectObj.TryGetValue("snellreflectionstrength", out var uwSnellStrengthElement))
                                uw.SnellReflectionStrength = uwSnellStrengthElement.GetSingle();
                            if (effectObj.TryGetValue("snellfresnelpower", out var uwSnellFresnelElement))
                                uw.SnellFresnelPower = uwSnellFresnelElement.GetSingle();
                            if (effectObj.TryGetValue("snellwavedistortion", out var uwSnellWaveDistortionElement))
                                uw.SnellWaveDistortion = uwSnellWaveDistortionElement.GetSingle();
                            // Water Transition
                            if (effectObj.TryGetValue("transitionenabled", out var uwTransitionEnabledElement))
                                uw.TransitionEnabled = uwTransitionEnabledElement.GetBoolean();
                            if (effectObj.TryGetValue("transitionduration", out var uwTransitionDurationElement))
                                uw.TransitionDuration = uwTransitionDurationElement.GetSingle();
                            if (effectObj.TryGetValue("enterbubbleintensity", out var uwEnterBubbleIntensityElement))
                                uw.EnterBubbleIntensity = uwEnterBubbleIntensityElement.GetSingle();
                            if (effectObj.TryGetValue("enterbubblesize", out var uwEnterBubbleSizeElement))
                                uw.EnterBubbleSize = uwEnterBubbleSizeElement.GetSingle();
                            if (effectObj.TryGetValue("enterbubblecount", out var uwEnterBubbleCountElement))
                                uw.EnterBubbleCount = uwEnterBubbleCountElement.GetSingle();
                            if (effectObj.TryGetValue("enterdistortion", out var uwEnterDistortionElement))
                                uw.EnterDistortion = uwEnterDistortionElement.GetSingle();
                            if (effectObj.TryGetValue("exitdropletintensity", out var uwExitDropletIntensityElement))
                                uw.ExitDropletIntensity = uwExitDropletIntensityElement.GetSingle();
                            if (effectObj.TryGetValue("exitdropletsize", out var uwExitDropletSizeElement))
                                uw.ExitDropletSize = uwExitDropletSizeElement.GetSingle();
                            if (effectObj.TryGetValue("exitdropletcount", out var uwExitDropletCountElement))
                                uw.ExitDropletCount = uwExitDropletCountElement.GetSingle();
                            if (effectObj.TryGetValue("exitdripspeed", out var uwExitDripSpeedElement))
                                uw.ExitDripSpeed = uwExitDripSpeedElement.GetSingle();
                            break;
                    }

                    component.AddEffect(effect);
                }
            }
        }
    }
}