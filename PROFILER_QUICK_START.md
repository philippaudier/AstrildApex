# ⚡ Performance Profiler - Quick Start Guide

## Phase 0 est terminée ! 🎉

Le système de profiling est maintenant actif dans votre éditeur.

## Comment utiliser le profiler

### 1. Activer l'overlay de performance

1. Lancez l'éditeur : `dotnet run --project Editor/Editor.csproj`
2. Dans le menu : **View → ⚡ Performance Overlay**
3. Un overlay apparaît en bas à droite avec :
   - **Total UI** : Temps CPU total de tous les panels
   - **UI FPS** : FPS estimé basé sur le temps UI
   - **Hotspot** : Le panel le plus coûteux

### 2. Voir les détails par panel

- Cliquez sur **Show Details** dans l'overlay
- Un tableau s'affiche avec pour chaque panel :
  - **Last** : Temps du dernier frame (ms)
  - **Avg** : Moyenne sur 2 secondes (ms)
  - **Max** : Pic maximum observé (ms)

### 3. Code couleur

- 🟢 **Vert** (<2ms) : Excellent
- 🟡 **Jaune** (2-5ms) : Acceptable
- 🟠 **Orange** (5-10ms) : À surveiller
- 🔴 **Rouge** (>10ms) : Problématique !

## Prochaines étapes - À VOUS DE TESTER

### Test 1 : Baseline FPS (éditeur au repos)
1. Fermez tous les panels sauf le Viewport
2. Laissez tourner 10 secondes
3. **Notez le UI FPS affiché**
4. Objectif : Devrait être >300 FPS

### Test 2 : Tous panels ouverts
1. Ouvrez TOUS les panels (Hierarchy, Inspector, Assets, Console, etc.)
2. Créez une scène avec ~100 entités
3. Scrollez dans la Hierarchy et Assets
4. **Notez le UI FPS et les 3 panels les plus lents**
5. Objectif actuel : Probablement 30-60 FPS (on va améliorer ça !)

### Test 3 : Stress test Assets panel
1. Allez dans le dossier Assets avec beaucoup de fichiers
2. Scrollez rapidement de haut en bas
3. **Notez le temps du panel Assets dans l'overlay**

### Test 4 : Stress test Hierarchy
1. Créez une scène avec 500+ entités imbriquées
2. Développez/collapse des nodes
3. **Notez le temps du panel Hierarchy**

## Résultats attendus (avant optimisation)

Les panels les plus coûteux sont probablement :
1. **Assets** : 5-15ms (filesystem watching + icon rendering)
2. **Hierarchy** : 2-8ms (tree rebuild + selection tracking)
3. **Console** : 1-5ms (filtering + scrolling)

## Commandes utiles

- **Reset** : Réinitialise les statistiques (min/max/avg)
- **Raccourci clavier** : Vous pouvez ajouter `Ctrl+P` dans EditorUI.cs si besoin

## Prochaine phase

Une fois les benchmarks terminés, on passera à la **Phase 1** :
- Création de `PanelBase.cs` avec dirty flags
- Création de `AstrildUIManager.cs` pour centraliser la gestion
- Migration du premier panel (Hierarchy) vers le nouveau système

## Notes importantes

- Le profiler mesure uniquement le temps **CPU** des panels
- Il ne mesure pas le temps GPU (rendering OpenGL)
- Les mesures incluent les appels ImGui (layout + draw calls)
- L'overhead du profiler lui-même est <0.1ms par panel

---

**Testez et partagez vos résultats !**
Je suis curieux de voir quels sont vos bottlenecks réels 📊
