# Guide de Test - Améliorations de Lisibilité 🎯

## Comment Tester les Améliorations

### 1. Lancer l'Éditeur
```bash
dotnet run --project Editor/Editor.csproj
```

### 2. Ouvrir le Sélecteur de Thèmes
- **Menu**: `View > Theme Selector`
- **Ou raccourci**: (si configuré)

### 3. Tester Différents Thèmes

#### Thèmes à Tester Prioritairement

**🎨 Purple Dream**
- **Avant**: Texte blanc sur transparents → parfois illisible
- **Après**: Contraste automatiquement ajusté → toujours lisible

**🌿 Mint Fresh**
- **Avant**: Texte très clair sur verts transparents
- **Après**: Ajustement selon luminance du fond

**💙 Cyber Blue**
- **Avant**: Texte bleu clair sur bleus transparents
- **Après**: Contraste optimisé

**🌸 Pink Passion**
- **Avant**: Texte rose/blanc sur transparents
- **Après**: Lisibilité garantie

### 4. Éléments à Vérifier

#### ✅ Dans les Panneaux
- [ ] **Inspector**: Labels et valeurs lisibles
- [ ] **Hierarchy**: Noms des objets
- [ ] **Project**: Noms des fichiers
- [ ] **Console**: Messages d'erreur/avertissement

#### ✅ Dans les Fenêtres
- [ ] **Popups/Menus**: Texte contrasté
- [ ] **Tooltips**: Lisibles sur tous fonds
- [ ] **Dialogues modaux**: Texte clair

#### ✅ États Interactifs
- [ ] **Boutons**: Texte lisible au repos et hover
- [ ] **Champs de saisie**: Placeholder et texte
- [ ] **Onglets**: Actifs et inactifs
- [ ] **Headers pliables**: Titres et contenu

### 5. Scénarios de Test

#### 🌞 Environnements Clairs
- **Bureau clair** avec thèmes sombres
- **Éclairage naturel** intense
- **Moniteurs IPS** avec gamut étendu

#### 🌙 Environnements Sombres
- **Bureau sombre** avec thèmes clairs
- **Nuit/éclairage artificiel**
- **Moniteurs OLED** avec contrastes élevés

#### 👥 Accessibilité
- [ ] **Utilisateurs daltoniens**: Contraste élevé aide
- [ ] **Vision réduite**: Texte plus grand reste lisible
- [ ] **Fatigue oculaire**: Moins d'effort pour lire

### 6. Validation Technique

#### Contraste Minimum Respecté
- **Texte normal**: ≥ 4.5:1 ✅
- **Texte large**: ≥ 3:1 ✅
- **Texte désactivé**: ≥ 3:1 ✅

#### Performance
- [ ] **Changement de thème**: Instantané
- [ ] **Pas de lag** pendant l'utilisation
- [ ] **Mémoire stable**

### 7. Problèmes à Signaler

#### Si vous trouvez un texte illisible :
1. **Notez le thème** et l'élément exact
2. **Capture d'écran** si possible
3. **Décrivez le contexte** (fond, état, etc.)

#### Format de rapport :
```
Thème: [nom]
Élément: [panneau.champ]
Problème: [description]
Contexte: [fond, état, etc.]
```

### 8. Comparaison Avant/Après

#### Avant les Améliorations ❌
- Certains thèmes avaient du texte blanc sur blanc
- Contraste aléatoire selon les transparences
- Lisibilité dépendante du thème et de l'environnement

#### Après les Améliorations ✅
- **Tous les thèmes** automatiquement lisibles
- **Contraste garanti** selon standards WCAG
- **Adaptation intelligente** au contexte visuel

## 🎉 Résultat Attendu

**100% des thèmes** devraient maintenant être **parfaitement lisibles** dans **tous les contextes** !

Si vous trouvez le moindre problème de lisibilité, c'est un bug à corriger immédiatement.

---

**Happy Testing! 🧪✨**