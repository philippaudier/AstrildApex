# 🛠️ External Tools - Unity-Style Script Editor Integration

## 📋 Vue d'ensemble

Le système **External Tools** permet de configurer un éditeur de script externe (VS Code, Rider, Visual Studio) pour ouvrir automatiquement les fichiers `.cs` depuis l'éditeur AstrildApex.

## ✨ Fonctionnalités

### 1. Auto-détection de VS Code
- Détection automatique au premier lancement
- Recherche dans les emplacements standards Windows :
  - `%LOCALAPPDATA%\Programs\Microsoft VS Code\Code.exe`
  - `C:\Program Files\Microsoft VS Code\Code.exe`
  - `C:\Program Files (x86)\Microsoft VS Code\Code.exe`

### 2. Configuration Manuelle
- Chemin personnalisé vers n'importe quel éditeur
- Arguments configurables avec placeholders
- Presets pour les éditeurs populaires

### 3. Placeholders d'Arguments
- `$(File)` - Chemin complet du fichier
- `$(Line)` - Numéro de ligne
- `$(Column)` - Numéro de colonne

## 🎮 Utilisation

### Accéder aux Préférences

**Menu :** `Edit > Preferences...` (ou `Ctrl+,`)

**Catégorie :** `External Tools`

### Configuration de Base

1. **Auto-détection de VS Code** :
   - Cliquer sur **"Auto-detect VS Code"**
   - Le chemin sera automatiquement détecté et configuré
   - Arguments par défaut : `"$(File)" -g "$(File):$(Line)"`

2. **Configuration Manuelle** :
   - **Editor Application** : Chemin vers l'exécutable de l'éditeur
   - **External Script Editor Args** : Arguments avec placeholders
   - Cliquer sur **"Browse..."** pour sélectionner un fichier

3. **Tester la Configuration** :
   - Cliquer sur **"Test Editor"**
   - Ouvre `README.md` dans l'éditeur configuré

### Presets Disponibles

#### VS Code (Standard)
```
Arguments: "$(File)" -g "$(File):$(Line)"
```
- Ouvre le fichier et va directement à la ligne spécifiée
- Utilise le flag `-g` (goto)

#### Visual Studio
```
Arguments: "$(File)" /Edit
```
- Ouvre le fichier en mode édition
- Pas de support natif de goto line via arguments

#### JetBrains Rider
```
Arguments: "$(File)" --line $(Line)
```
- Ouvre le fichier et va à la ligne
- Utilise le flag `--line`

## 🔧 Implémentation Technique

### Architecture

**Fichiers Modifiés** :
1. `Editor/State/EditorSettings.cs` - Sauvegarde/chargement des settings
2. `Editor/UI/PreferencesWindow.cs` - Interface utilisateur
3. `ProjectSettings/EditorSettings.json` - Stockage persistant

### EditorSettings.cs

**Classe de Configuration** :
```csharp
public class ExternalToolsData
{
    public string ScriptEditor { get; set; } = "";
    public string ScriptEditorArgs { get; set; } = "\"$(File)\" -g \"$(File):$(Line)\"";
    public bool AutoDetectEditor { get; set; } = true;
}
```

**API Publique** :
```csharp
// Obtenir/Définir le chemin de l'éditeur
public static string ScriptEditor { get; set; }

// Obtenir/Définir les arguments
public static string ScriptEditorArgs { get; set; }

// Auto-détection de VS Code
private static string DetectVSCode()

// Ouvrir un script dans l'éditeur externe
public static void OpenScript(string filePath, int line = 1)
```

### Exemple d'Utilisation dans le Code

```csharp
// Ouvrir un fichier C# à la ligne 42
EditorSettings.OpenScript("C:\\Path\\To\\Script.cs", 42);

// Ouvrir un fichier à la ligne 1
EditorSettings.OpenScript("C:\\Path\\To\\Script.cs");
```

### Format des Arguments

Les placeholders sont remplacés avant l'exécution :
```csharp
var args = ScriptEditorArgs
    .Replace("$(File)", filePath)
    .Replace("$(Line)", line.ToString())
    .Replace("$(Column)", "1");

// Exemple : "C:\Path\To\Script.cs" -g "C:\Path\To\Script.cs:42"
```

### Lancement du Processus

```csharp
var processInfo = new ProcessStartInfo
{
    FileName = editorPath,      // Chemin vers Code.exe
    Arguments = args,            // Arguments avec placeholders remplacés
    UseShellExecute = true,     // Utilise le shell Windows
    CreateNoWindow = true       // Pas de fenêtre console
};

Process.Start(processInfo);
```

## 📊 Stockage des Settings

**Fichier** : `ProjectSettings/EditorSettings.json`

**Structure** :
```json
{
  "ExternalTools": {
    "ScriptEditor": "C:\\Users\\Username\\AppData\\Local\\Programs\\Microsoft VS Code\\Code.exe",
    "ScriptEditorArgs": "\"$(File)\" -g \"$(File):$(Line)\"",
    "AutoDetectEditor": true
  }
}
```

## 🎯 Workflows Utilisateur

### Workflow 1 : Premier Lancement (Auto-détection)

```
1. Lancer AstrildApex Editor
2. Ouvrir Edit > Preferences > External Tools
3. Cliquer sur "Auto-detect VS Code"
   → Chemin détecté automatiquement
   → Arguments par défaut configurés
4. Cliquer sur "Test Editor"
   → README.md s'ouvre dans VS Code
5. Fermer la fenêtre de préférences
   → Settings sauvegardés automatiquement
```

### Workflow 2 : Configuration Manuelle (Rider)

```
1. Ouvrir Edit > Preferences > External Tools
2. Cliquer sur "Browse..."
3. Sélectionner rider64.exe
4. Cliquer sur preset "JetBrains Rider"
   → Arguments: "$(File)" --line $(Line)
5. Cliquer sur "Test Editor"
   → README.md s'ouvre dans Rider
```

### Workflow 3 : Ouverture de Script (Future)

```
1. Clic droit sur un fichier .cs dans Assets
2. Sélectionner "Open C# Project" (TODO)
   → EditorSettings.OpenScript(filePath, 1)
   → Fichier s'ouvre dans l'éditeur configuré
```

## 🚀 Intégrations Futures

### TODO 1 : Context Menu dans Assets
```csharp
// Dans AssetsPanel.cs
if (ImGui.BeginPopupContextItem())
{
    if (file.EndsWith(".cs"))
    {
        if (ImGui.MenuItem("Open C# Script"))
        {
            EditorSettings.OpenScript(fullPath, 1);
        }
    }
    ImGui.EndPopup();
}
```

### TODO 2 : Double-clic sur Script
```csharp
// Dans AssetsPanel.cs
if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(0))
{
    if (file.EndsWith(".cs"))
    {
        EditorSettings.OpenScript(fullPath, 1);
    }
}
```

### TODO 3 : Goto Error Line
```csharp
// Dans Console Panel
if (ImGui.Selectable(errorLine))
{
    // Parse file path and line number from error
    var (file, line) = ParseErrorLocation(errorLine);
    EditorSettings.OpenScript(file, line);
}
```

### TODO 4 : Inspector Script Reference
```csharp
// Dans ComponentInspector
if (ImGui.Button("Edit Script"))
{
    var scriptPath = GetScriptPath(component.GetType());
    EditorSettings.OpenScript(scriptPath, 1);
}
```

## 🎨 Interface Utilisateur

### Preferences Window - External Tools Tab

```
╔═══════════════════════════════════════════════════════╗
║ 🛠️ External Tools                                     ║
╠═══════════════════════════════════════════════════════╣
║ External Script Editor                                ║
║ ─────────────────────────────────────────────────────║
║                                                       ║
║ Editor Application:                                   ║
║ [C:\...\Microsoft VS Code\Code.exe              ]    ║
║                                                       ║
║ [Browse...] [Auto-detect VS Code] [Test Editor]     ║
║                                                       ║
║ ─────────────────────────────────────────────────────║
║                                                       ║
║ External Script Editor Args:                          ║
║ ["$(File)" -g "$(File):$(Line)"                 ]    ║
║                                                       ║
║ Argument Placeholders:                                ║
║   $(File) - Full file path                           ║
║   $(Line) - Line number                              ║
║   $(Column) - Column number                          ║
║                                                       ║
║ ─────────────────────────────────────────────────────║
║                                                       ║
║ Argument Presets:                                     ║
║                                                       ║
║ [VS Code (Standard)] [Visual Studio] [JetBrains Rider]║
║                                                       ║
║ ─────────────────────────────────────────────────────║
║                                                       ║
║ Current Configuration:                                ║
║ ┌───────────────────────────────────────────────┐   ║
║ │ Editor: C:\...\Code.exe                       │   ║
║ │ Arguments: "$(File)" -g "$(File):$(Line)"     │   ║
║ └───────────────────────────────────────────────┘   ║
╚═══════════════════════════════════════════════════════╝
```

## ✅ Tests de Validation

### Test 1 : Auto-détection
1. Ouvrir Preferences > External Tools
2. Vérifier que VS Code est détecté (si installé)
3. **Résultat attendu** : Chemin affiché automatiquement

### Test 2 : Test Editor
1. Configurer un éditeur
2. Cliquer sur "Test Editor"
3. **Résultat attendu** : README.md s'ouvre dans l'éditeur

### Test 3 : Preset Arguments
1. Sélectionner "JetBrains Rider" preset
2. Vérifier les arguments
3. **Résultat attendu** : `"$(File)" --line $(Line)`

### Test 4 : Persistance
1. Configurer un éditeur
2. Fermer l'éditeur AstrildApex
3. Relancer l'éditeur
4. **Résultat attendu** : Configuration conservée

### Test 5 : Goto Line
1. Appeler `EditorSettings.OpenScript("test.cs", 42)`
2. **Résultat attendu** : VS Code ouvre test.cs et va à la ligne 42

## 🐛 Dépannage

### Problème 1 : VS Code non détecté
**Cause** : Installation non standard  
**Solution** : Utiliser "Browse..." pour sélectionner manuellement Code.exe

### Problème 2 : Goto line ne fonctionne pas
**Cause** : Arguments incorrects  
**Solution** : Utiliser le preset correspondant à votre éditeur

### Problème 3 : Fichier ne s'ouvre pas
**Cause** : Chemin d'éditeur invalide  
**Solution** : Vérifier que le fichier existe avec "Test Editor"

## 📝 Notes de Développement

### Chemins Standards VS Code

```
Windows 10/11:
- User Install: %LOCALAPPDATA%\Programs\Microsoft VS Code\Code.exe
- System Install: C:\Program Files\Microsoft VS Code\Code.exe
- System Install (x86): C:\Program Files (x86)\Microsoft VS Code\Code.exe
```

### Format des Arguments par Éditeur

| Éditeur | Arguments | Goto Line |
|---------|-----------|-----------|
| VS Code | `"$(File)" -g "$(File):$(Line)"` | ✅ Oui |
| Rider | `"$(File)" --line $(Line)` | ✅ Oui |
| Visual Studio | `"$(File)" /Edit` | ❌ Non |
| Notepad++ | `"$(File)" -n$(Line)` | ✅ Oui |
| Sublime Text | `"$(File):$(Line)` | ✅ Oui |

## ✅ Status Final

**Version** : 1.0  
**Date** : 18 octobre 2025  
**Build** : ✅ Compilé sans erreurs (0 warnings, 0 errors)  
**Tests** : À effectuer par l'utilisateur  
**Documentation** : Complète

---

**Prêt à utiliser !** 🚀

Ouvre `Edit > Preferences > External Tools` et configure ton éditeur favori !
