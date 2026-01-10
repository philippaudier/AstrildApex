# ☁️ Cloud System - Quick Start Guide

## 🚀 Démarrage Rapide (5 minutes)

### Étape 1: Activer les Nuages
1. Sélectionnez votre entity avec **WeatherComponent**
2. Section **☁️ Clouds** → Cocher **Enabled**
3. Choisir un **Type**: Cumulus (pour commencer)
4. **Coverage**: 0.5 (moitié du ciel)
5. **Density**: 0.8 (assez opaques)

✅ Vous devriez maintenant voir des nuages!

### Étape 2: Les Faire Bouger
1. Ouvrir **Advanced Cloud** (cliquer sur le triangle)
2. **Animation Speed**: 1.0 (vitesse normale)
3. **Morph Speed**: 0.7 (morphing visible)
4. **Detail Evolution**: 0.5 (évolution douce)

✅ Les nuages bougent maintenant!

### Étape 3: Ajouter du Morphing Organique
1. Ouvrir **🌪️ Dual-Layer Scrolling Noise**
2. **Layer 1 Direction**: 90° (Nord)
3. **Layer 2 Direction**: -45° (Sud-Ouest)
4. **Layer 2 Speed**: 1.5 (plus rapide que Layer 1)

✅ Les nuages morphent et évoluent naturellement!

### Étape 4: Ajouter Détails Fins et Déchirures
1. Ouvrir **🔬 FBM (Fractal Brownian Motion)**
2. **Octaves**: 5 (plus de détails)
3. **Erosion**: 0.4 (déchirures visibles)
4. **Sharpness**: 0.6 (edges bien définis)

✅ Les nuages ont maintenant des détails fins et peuvent se déchirer!

## 🎨 Presets Recommandés

### Beau Temps (Cirrus Wispy)
```
Type: Cirrus
Coverage: 0.2
Density: 0.4
Erosion: 0.5 (très fragmenté)
Layer2 Speed: 2.0 (rapide)
```

### Temps Nuageux (Cumulus Cotonneux)
```
Type: Cumulus
Coverage: 0.5
Density: 0.8
Worley Weight: 0.7 (billowy)
Erosion: 0.25
FBM Octaves: 4
```

### Orage Approche (Storm Dramatic)
```
Type: Storm
Coverage: 0.9
Density: 1.0
Erosion: 0.6 (breaks dramatiques)
FBM Octaves: 6
Sharpness: 0.7
Layer2 Speed: 2.0
```

## 💡 Tips Rapides

### Pour des nuages plus réalistes:
- ✅ Mettre Layer1 et Layer2 dans des **directions différentes**
- ✅ Layer2 Speed **plus rapide** que Layer1
- ✅ Layer2 Scale **2-3x plus grand** que Layer1
- ✅ Erosion **0.2-0.4** pour breaks naturels

### Pour du morphing visible:
- ✅ Morph Speed **0.7-1.0**
- ✅ Layer directions **opposées** (ex: 90° et -90°)
- ✅ Layer2 Speed **1.5-2x** Layer1 Speed

### Pour des détails fins:
- ✅ FBM Octaves **5-6**
- ✅ Detail Strength **0.6-0.8**
- ✅ Layer2 Scale **3.0-4.0**

### Pour des déchirures (comme dans les photos):
- ✅ Erosion **0.4-0.7**
- ✅ Sharpness **0.6-0.8**
- ✅ FBM Strength **0.7-0.9**

## 🐛 Problèmes Communs

**Nuages ne bougent pas?**
→ Vérifier Animation Speed > 0 et Morph Speed > 0

**Pas de morphing visible?**
→ Augmenter différence entre Layer1 et Layer2 directions/speeds

**Nuages trop uniformes?**
→ Augmenter FBM Octaves et Erosion

**Performance lente?**
→ Réduire FBM Octaves à 3-4

## 📖 Documentation Complète

Voir **CLOUD_SYSTEM_REFACTOR_2025.md** pour:
- Explication détaillée de tous les paramètres
- Configurations avancées
- Architecture technique
- Troubleshooting complet
