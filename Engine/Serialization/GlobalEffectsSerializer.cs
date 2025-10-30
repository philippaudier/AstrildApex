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
                        
                    case ChromaticAberrationEffect chromatic:
                        effectData["strength"] = chromatic.Strength;
                        effectData["usespectrallut"] = chromatic.UseSpectralLut;
                        effectData["focallength"] = chromatic.FocalLength;
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
                        "Engine.Rendering.ChromaticAberrationEffect" or "ChromaticAberrationEffect" => new ChromaticAberrationEffect(),
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
                            
                        case ChromaticAberrationEffect chromatic:
                            if (effectObj.TryGetValue("strength", out var strengthElement))
                                chromatic.Strength = strengthElement.GetSingle();
                            if (effectObj.TryGetValue("usespectrallut", out var spectralElement))
                                chromatic.UseSpectralLut = spectralElement.GetBoolean();
                            if (effectObj.TryGetValue("focallength", out var focalElement))
                                chromatic.FocalLength = focalElement.GetSingle();
                            break;
                    }
                    
                    component.AddEffect(effect);
                }
            }
        }
    }
}