using ImGuiNET;

namespace Editor.Utils;

/// <summary>
/// Helper class for checking keyboard shortcuts in ImGui
/// </summary>
public static class ShortcutHelper
{
    /// <summary>
    /// Check if a shortcut string matches current keyboard state
    /// </summary>
    /// <param name="shortcut">Format: "Ctrl+S", "Shift+Key", "Alt+Key", "Key"</param>
    /// <returns>True if the shortcut is pressed this frame</returns>
    public static bool IsShortcutPressed(string shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut))
            return false;

        var parts = shortcut.Split('+');
        if (parts.Length == 0)
            return false;

        bool needsCtrl = false;
        bool needsShift = false;
        bool needsAlt = false;
        string? keyName = null;

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                needsCtrl = true;
            }
            else if (trimmed.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                needsShift = true;
            }
            else if (trimmed.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                needsAlt = true;
            }
            else
            {
                keyName = trimmed;
            }
        }

        if (keyName == null)
            return false;

        // Check modifiers
        bool hasCtrl = ImGui.IsKeyDown(ImGuiKey.ModCtrl);
        bool hasShift = ImGui.IsKeyDown(ImGuiKey.ModShift);
        bool hasAlt = ImGui.IsKeyDown(ImGuiKey.ModAlt);

        // Modifiers must match exactly
        if (hasCtrl != needsCtrl || hasShift != needsShift || hasAlt != needsAlt)
            return false;

        // Parse key name and check if pressed
        ImGuiKey key = ParseKeyName(keyName);
        if (key == ImGuiKey.None)
            return false;

        return ImGui.IsKeyPressed(key);
    }

    /// <summary>
    /// Parse a key name string to ImGuiKey enum
    /// </summary>
    private static ImGuiKey ParseKeyName(string keyName)
    {
        // Single letter keys (A-Z)
        if (keyName.Length == 1 && char.IsLetter(keyName[0]))
        {
            char upper = char.ToUpper(keyName[0]);
            return upper switch
            {
                'A' => ImGuiKey.A,
                'B' => ImGuiKey.B,
                'C' => ImGuiKey.C,
                'D' => ImGuiKey.D,
                'E' => ImGuiKey.E,
                'F' => ImGuiKey.F,
                'G' => ImGuiKey.G,
                'H' => ImGuiKey.H,
                'I' => ImGuiKey.I,
                'J' => ImGuiKey.J,
                'K' => ImGuiKey.K,
                'L' => ImGuiKey.L,
                'M' => ImGuiKey.M,
                'N' => ImGuiKey.N,
                'O' => ImGuiKey.O,
                'P' => ImGuiKey.P,
                'Q' => ImGuiKey.Q,
                'R' => ImGuiKey.R,
                'S' => ImGuiKey.S,
                'T' => ImGuiKey.T,
                'U' => ImGuiKey.U,
                'V' => ImGuiKey.V,
                'W' => ImGuiKey.W,
                'X' => ImGuiKey.X,
                'Y' => ImGuiKey.Y,
                'Z' => ImGuiKey.Z,
                _ => ImGuiKey.None
            };
        }

        // Number keys
        if (keyName.Length == 1 && char.IsDigit(keyName[0]))
        {
            return keyName[0] switch
            {
                '0' => ImGuiKey._0,
                '1' => ImGuiKey._1,
                '2' => ImGuiKey._2,
                '3' => ImGuiKey._3,
                '4' => ImGuiKey._4,
                '5' => ImGuiKey._5,
                '6' => ImGuiKey._6,
                '7' => ImGuiKey._7,
                '8' => ImGuiKey._8,
                '9' => ImGuiKey._9,
                _ => ImGuiKey.None
            };
        }

        // Special keys
        return keyName.ToLower() switch
        {
            "f1" => ImGuiKey.F1,
            "f2" => ImGuiKey.F2,
            "f3" => ImGuiKey.F3,
            "f4" => ImGuiKey.F4,
            "f5" => ImGuiKey.F5,
            "f6" => ImGuiKey.F6,
            "f7" => ImGuiKey.F7,
            "f8" => ImGuiKey.F8,
            "f9" => ImGuiKey.F9,
            "f10" => ImGuiKey.F10,
            "f11" => ImGuiKey.F11,
            "f12" => ImGuiKey.F12,
            "space" => ImGuiKey.Space,
            "enter" => ImGuiKey.Enter,
            "return" => ImGuiKey.Enter,
            "escape" => ImGuiKey.Escape,
            "esc" => ImGuiKey.Escape,
            "tab" => ImGuiKey.Tab,
            "backspace" => ImGuiKey.Backspace,
            "delete" => ImGuiKey.Delete,
            "del" => ImGuiKey.Delete,
            "insert" => ImGuiKey.Insert,
            "ins" => ImGuiKey.Insert,
            "home" => ImGuiKey.Home,
            "end" => ImGuiKey.End,
            "pageup" => ImGuiKey.PageUp,
            "pagedown" => ImGuiKey.PageDown,
            "up" => ImGuiKey.UpArrow,
            "down" => ImGuiKey.DownArrow,
            "left" => ImGuiKey.LeftArrow,
            "right" => ImGuiKey.RightArrow,
            _ => ImGuiKey.None
        };
    }
}
