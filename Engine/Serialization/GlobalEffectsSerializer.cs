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
                    }
                    
                    component.AddEffect(effect);
                }
            }
        }
    }
}