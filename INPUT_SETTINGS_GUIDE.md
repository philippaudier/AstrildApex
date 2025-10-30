# Guide d'utilisation - Input Settings Panel

## 🎮 Comment créer de nouvelles actions d'entrée

### Méthode 1 : Bouton "New Action"

1. **Ouvrir Input Settings**
   - Lance l'éditeur et ouvre le panel Input Settings

2. **Créer une nouvelle action**
   - Clique sur le bouton **"➕ New Action"** en haut à droite (à côté de la barre de recherche)
   - Une fenêtre de dialogue s'ouvre

3. **Configurer l'action**
   - **Action Name** : Entre un nom unique (ex: "Fire", "Reload", "Crouch")
   - **Category** : Choisis une catégorie (Movement, Camera, General, etc.)
   - Le système vérifie automatiquement si le nom existe déjà

4. **Créer**
   - Clique sur **"✓ Create"**
   - L'éditeur de binding s'ouvre automatiquement pour ajouter la première touche

5. **Configurer le premier binding**
   - Choisis le type (Key / MouseButton / MouseAxis)
   - Sélectionne la touche dans le menu déroulant ou clique **"⌨️ Capture"**
   - Ajoute des modificateurs (Ctrl, Alt, Shift) si nécessaire
   - Clique **"✓ Apply"**

### Exemple : Créer une action "Sprint"

```
1. Clique "➕ New Action"
2. Action Name: "Sprint"
3. Category: "Movement"
4. Clique "✓ Create"
5. Type: Key
6. Key: Left Shift
7. Clique "✓ Apply"
8. Clique "💾 Apply" en bas pour sauvegarder
```

## 🗑️ Supprimer une action

- Clique sur l'icône **🗑️** à droite du nom de l'action
- L'action et tous ses bindings sont supprimés immédiatement
- N'oublie pas de cliquer **"💾 Apply"** pour sauvegarder

## ➕ Ajouter des bindings supplémentaires

Une action peut avoir plusieurs bindings (ex: Jump sur Space ET Gamepad Button A)

1. Trouve l'action dans la liste
2. Clique sur **"➕ Add Binding"** en dessous des bindings existants
3. Configure le nouveau binding dans l'éditeur
4. Clique **"✓ Apply"**

## ✏️ Modifier un binding existant

1. Clique sur le bouton du binding (affiche la touche actuelle)
2. L'éditeur s'ouvre avec les valeurs actuelles pré-remplies
3. Modifie ce que tu veux (type, touche, modificateurs, sensibilité)
4. Clique **"✓ Apply"**

## 🎯 Organisation par Action Maps

Les actions sont organisées en contextes (Action Maps) :

- **Player** : Contrôles à pied (déplacement, saut, actions)
- **Vehicle** : Contrôles de véhicule (accélérer, freiner, tourner)
- **Menu** : Navigation dans les menus (confirmer, annuler, naviguer)

Tu peux créer des actions dans n'importe quel Action Map en le sélectionnant d'abord en haut.

## 💡 Astuces

### Noms d'actions recommandés
- Utilise des noms clairs et en anglais : "Jump", "Fire", "Reload"
- Évite les espaces : utilise "MoveForward" au lieu de "Move Forward"
- Sois cohérent avec la casse : PascalCase recommandé

### Éviter les conflits
- Le panel affiche automatiquement les conflits (⚠️)
- Deux actions ne peuvent pas utiliser exactement la même touche/bouton
- Utilise des modificateurs (Ctrl, Alt, Shift) pour différencier

### Bindings composés
- Exemple : "Ctrl + S" pour sauvegarder
- Active les modificateurs avec les checkboxes
- Le preview montre le résultat final

### Mouse Axis
- Utilise pour la caméra ou le vol
- Ajuste la sensibilité avec le slider (0.1x - 10x)
- X = horizontal, Y = vertical, ScrollX/Y = molette

## 🔧 Utilisation en code

Une fois tes actions créées, utilise-les dans ton code :

```csharp
// Dans ton script de joueur
var playerMap = InputManager.Instance.FindActionMap("Player");

if (playerMap.GetKeyDown("Sprint"))
{
    StartSprinting();
}

if (playerMap.GetKey("Sprint"))
{
    isSprinting = true;
}

if (playerMap.GetKeyUp("Sprint"))
{
    StopSprinting();
}
```

## 🔄 Workflow complet

1. **Créer** une nouvelle action avec "➕ New Action"
2. **Configurer** le premier binding (s'ouvre automatiquement)
3. **Ajouter** d'autres bindings si nécessaire
4. **Tester** dans le jeu
5. **Ajuster** si besoin (modifier sensibilité, changer touches)
6. **Sauvegarder** avec "💾 Apply"

Les changements sont sauvegardés dans `ProjectSettings/InputSettings.json` et persistent entre les sessions.

## ⚠️ Important

- **Toujours cliquer "💾 Apply"** en bas pour sauvegarder tes modifications
- **"Reset to Defaults"** restaure les bindings par défaut (attention : perte des modifications)
- Les actions supprimées ne peuvent pas être récupérées (pense à sauvegarder une copie du fichier JSON si besoin)

## 🎮 Actions par défaut disponibles

### Player (On-foot)
- MoveForward, MoveBackward, MoveLeft, MoveRight
- Jump
- LookX, LookY

### Vehicle
- Accelerate, Brake
- SteerLeft, SteerRight
- ExitVehicle

### Menu
- Navigate
- Confirm, Cancel
- Pause

Tu peux maintenant créer tes propres actions personnalisées ! 🚀
