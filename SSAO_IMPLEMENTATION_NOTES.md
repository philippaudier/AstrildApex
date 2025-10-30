# SSAO Implementation Notes

## 🎯 Question : Pourquoi le bruit SSAO suit-il l'écran ?

### Réponse courte
**C'est NORMAL et ATTENDU !** Le bruit SSAO doit être en espace écran (screen-space), pas en espace monde.

## 📚 Comment le SSAO fonctionne dans les moteurs professionnels

### **Unity**
- Texture de bruit : **4x4 pixels**
- Échantillonnage : **En espace écran** (suit la caméra)
- Blur : **Très fort** (bilateral blur)
- Résultat : Le pattern de bruit est **invisible** après le blur

### **Unreal Engine**
- Méthode : **Interleaved Gradient Noise** (procédural)
- Échantillonnage : **En espace écran** avec offset temporel
- Change de frame en frame pour éliminer les patterns
- Blur : **Adaptatif** basé sur la profondeur

### **LearnOpenGL (standard académique)**
- Texture de bruit : **4x4 pixels**
- Échantillonnage : **En espace écran**
- C'est l'approche de référence pour SSAO

## 🔍 Pourquoi le bruit DOIT être en espace écran ?

### SSAO = Screen-Space Ambient Occlusion

Le SSAO fonctionne en **espace écran** :
1. On calcule l'occlusion pour **chaque pixel** visible
2. On utilise un kernel de samples autour du pixel
3. Le bruit sert à **randomiser la rotation** du kernel pour éviter le banding

Si le bruit était en espace monde :
- ❌ Il faudrait recalculer les rotations pour chaque pixel à chaque frame
- ❌ Les objets qui bougent auraient des patterns changeants
- ❌ Le coût de calcul serait beaucoup plus élevé
- ❌ Les patterns seraient plus visibles (cohérence spatiale)

## ✅ Notre implémentation (après fixes)

### Changements appliqués :

1. **Texture de bruit : 64x64 → 4x4 pixels**
   - Pourquoi ? Pattern plus petit = répétition plus rapide = moins visible après blur
   - C'est le standard utilisé par Unity et LearnOpenGL

2. **BlurRadius : 4.0 → 6.0**
   - Blur plus fort pour complètement cacher le pattern de bruit
   - Bilateral blur préserve les edges tout en lissant le bruit

3. **DepthThreshold : 0.01 → 0.02**
   - Meilleure préservation des edges pendant le blur

## 🎨 Résultat attendu

Après ces changements :
- ✅ Le bruit suit toujours l'écran (c'est normal !)
- ✅ Le pattern de bruit est **invisible** grâce au blur
- ✅ Les edges sont bien préservés
- ✅ L'occlusion ambiante est lisse et naturelle

## 🔧 Si vous voyez encore le bruit

Si le pattern de bruit reste visible :

### Option 1 : Augmenter le blur
```csharp
BlurRadius = 8.0f, // Au lieu de 6.0
```

### Option 2 : Réduire le nombre de samples
```csharp
SampleCount = 32, // Au lieu de 64 (moins de bruit à moyenner)
```

### Option 3 : Ajuster l'intensité
```csharp
Intensity = 1.0f, // Au lieu de 1.2 (moins agressif)
```

## 📖 Références

- [LearnOpenGL SSAO Tutorial](https://learnopengl.com/Advanced-Lighting/SSAO)
- [Unity SSAO Documentation](https://docs.unity3d.com/Manual/PostProcessing-AmbientOcclusion.html)
- [Unreal Engine SSAO](https://docs.unrealengine.com/en-US/RenderingAndGraphics/PostProcessEffects/AmbientOcclusion/)

## 🎓 Conclusion

Le comportement que vous observez (bruit qui suit l'écran) est **correct et intentionnel**. 

Le SSAO est une technique **screen-space** par définition. Le bruit doit être en espace écran, mais il doit être **imperceptible** après le blur.

Les changements appliqués (texture 4x4 + blur plus fort) devraient rendre le pattern de bruit invisible dans 99% des cas.
