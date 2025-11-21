# Guide des Effets Audio

## 📋 État Actuel des Effets Audio

Les classes d'effets audio existent déjà dans le moteur :
- `ReverbEffect` - Réverbération (Room, Cathedral, Cave, Underwater, etc.)
- `ChorusEffect` - Effet chorus
- `DistortionEffect` - Distorsion
- `EchoEffect` - Écho
- `FlangerEffect` - Effet flanger
- `CompressorEffect` - Compresseur dynamique

**Emplacement** : `Engine/Audio/Effects/`

## ⚠️ Limitation Actuelle

Les effets audio sont **créés mais pas encore intégrés** aux AudioSource. Voici ce qu'il manque :

### Ce qui existe déjà :
✅ Classes d'effets audio avec tous leurs paramètres
✅ `EFXManager` pour gérer les effets OpenAL EFX
✅ Presets pour chaque type d'effet

### Ce qu'il faut ajouter :

#### 1. **Propriété Effects dans AudioSource**
```csharp
// Dans AudioSource.cs
private List<AudioEffect> _effects = new();

public List<AudioEffect> Effects => _effects;

public void AddEffect(AudioEffect effect)
{
    _effects.Add(effect);
    // Appliquer l'effet à la source OpenAL
}
```

#### 2. **Inspecteur pour ajouter des effets**
Créer `AudioEffectInspector.cs` pour afficher une liste déroulante permettant d'ajouter des effets comme dans Unity :
- Bouton "Add Effect"
- Liste des effets disponibles
- Édition des paramètres de chaque effet

#### 3. **Application des effets à OpenAL**
Les effets utilisent OpenAL EFX (Effects Extension). Il faut :
- Créer des slots d'effet OpenAL
- Attacher les effets aux sources audio
- Gérer la chaîne d'effets (plusieurs effets par source)

## 🔧 Implémentation Recommandée

### Étape 1 : Ajouter le support d'effets dans AudioSource

```csharp
public class AudioSource : Component
{
    private List<AudioEffect> _effects = new();
    private int _effectSlot = -1; // OpenAL effect slot
    
    public void AddEffect(AudioEffect effect)
    {
        if (!_effects.Contains(effect))
        {
            _effects.Add(effect);
            effect.Apply(_sourceId); // Implémenter dans chaque effet
        }
    }
    
    public void RemoveEffect(AudioEffect effect)
    {
        _effects.Remove(effect);
        // Détacher l'effet de la source
    }
}
```

### Étape 2 : Implémenter Apply() dans les effets

```csharp
public abstract class AudioEffect
{
    public abstract void Apply(int sourceId);
    public abstract void Remove(int sourceId);
}
```

### Étape 3 : Créer l'inspecteur

```csharp
// Dans AudioSourceInspector.cs, ajouter une section :

ImGui.SeparatorText("Audio Effects");

if (ImGui.Button("Add Effect"))
{
    ImGui.OpenPopup("AddEffectPopup");
}

if (ImGui.BeginPopup("AddEffectPopup"))
{
    if (ImGui.MenuItem("Reverb"))
        audioSource.AddEffect(new ReverbEffect());
    if (ImGui.MenuItem("Chorus"))
        audioSource.AddEffect(new ChorusEffect());
    // etc.
    ImGui.EndPopup();
}

// Afficher les effets existants
foreach (var effect in audioSource.Effects)
{
    DrawEffectPanel(effect);
}
```

## 🎯 Alternative Simple (Sans OpenAL EFX)

Si OpenAL EFX est trop complexe, vous pouvez implémenter des effets basiques :

### Reverb Simple avec Delay Lines
```csharp
// Utiliser plusieurs délais pour simuler la réverbération
AL.Source(_sourceId, ALSourcef.AirAbsorptionFactor, reverbAmount);
```

### Pitch Shifting
```csharp
// Déjà disponible via la propriété Pitch
audioSource.Pitch = 1.5f; // Monte d'une quinte
```

### Distance-based Effects
```csharp
// Modifier les paramètres selon la distance
if (distance > 10f)
{
    // Ajouter du low-pass filter (effet "étouffé")
}
```

## 📝 TODO pour intégration complète

- [ ] Ajouter `List<AudioEffect> Effects` à `AudioSource`
- [ ] Implémenter `Apply()` et `Remove()` dans chaque effet
- [ ] Créer l'UI pour ajouter/supprimer des effets dans l'inspecteur
- [ ] Gérer les slots d'effets OpenAL (limités à 4 par source)
- [ ] Ajouter la sérialisation des effets
- [ ] Tester avec plusieurs effets simultanés

## 🔗 Ressources

- [OpenAL Effects Extension Guide](https://openal-soft.org/openal-extensions/SOFT_effect_target.txt)
- Documentation Unity AudioMixer pour référence UI
