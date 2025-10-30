using System;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Engine.Input
{
    // A Unity-like KeyCode enum covering common keys. This is a thin layer over OpenTK Keys
    // to provide a consistent API similar to Unity's KeyCode and allow future extension.
    public enum KeyCode
    {
        None = 0,
        // Letters
        A, B, C, D, E, F, G, H, I, J, K, L, M,
        N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        // Numbers
        Alpha0, Alpha1, Alpha2, Alpha3, Alpha4,
        Alpha5, Alpha6, Alpha7, Alpha8, Alpha9,
        // Numpad
        Keypad0, Keypad1, Keypad2, Keypad3, Keypad4,
        Keypad5, Keypad6, Keypad7, Keypad8, Keypad9,
        KeypadPeriod, KeypadDivide, KeypadMultiply, KeypadMinus, KeypadPlus, KeypadEnter,
        // Function keys
        F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12, F13, F14, F15,
        // Controls
        Escape, Space, Return, Enter, Backspace, Tab,
        LeftShift, RightShift, LeftControl, RightControl, LeftAlt, RightAlt,
        CapsLock, NumLock, ScrollLock,
        Insert, Delete, Home, End, PageUp, PageDown,
        // Arrows
        UpArrow, DownArrow, LeftArrow, RightArrow,
        // Misc
        Pause, PrintScreen, Menu,
        // OEM / punctuation keys
        Semicolon, Equals, Comma, Minus, Period, Slash, BackQuote,
        LeftBracket, Backslash, RightBracket, Quote
    }

    public static class KeyCodeExtensions
    {
        public static Keys ToOpenTK(this KeyCode kc)
        {
            return kc switch
            {
                KeyCode.A => Keys.A, KeyCode.B => Keys.B, KeyCode.C => Keys.C, KeyCode.D => Keys.D,
                KeyCode.E => Keys.E, KeyCode.F => Keys.F, KeyCode.G => Keys.G, KeyCode.H => Keys.H,
                KeyCode.I => Keys.I, KeyCode.J => Keys.J, KeyCode.K => Keys.K, KeyCode.L => Keys.L,
                KeyCode.M => Keys.M, KeyCode.N => Keys.N, KeyCode.O => Keys.O, KeyCode.P => Keys.P,
                KeyCode.Q => Keys.Q, KeyCode.R => Keys.R, KeyCode.S => Keys.S, KeyCode.T => Keys.T,
                KeyCode.U => Keys.U, KeyCode.V => Keys.V, KeyCode.W => Keys.W, KeyCode.X => Keys.X,
                KeyCode.Y => Keys.Y, KeyCode.Z => Keys.Z,

                KeyCode.Alpha0 => Keys.D0, KeyCode.Alpha1 => Keys.D1, KeyCode.Alpha2 => Keys.D2,
                KeyCode.Alpha3 => Keys.D3, KeyCode.Alpha4 => Keys.D4, KeyCode.Alpha5 => Keys.D5,
                KeyCode.Alpha6 => Keys.D6, KeyCode.Alpha7 => Keys.D7, KeyCode.Alpha8 => Keys.D8,
                KeyCode.Alpha9 => Keys.D9,

                // Keypad mappings are not present consistently across OpenTK versions; map to Unknown for now
                KeyCode.Keypad0 => Keys.Unknown, KeyCode.Keypad1 => Keys.Unknown, KeyCode.Keypad2 => Keys.Unknown,
                KeyCode.Keypad3 => Keys.Unknown, KeyCode.Keypad4 => Keys.Unknown, KeyCode.Keypad5 => Keys.Unknown,
                KeyCode.Keypad6 => Keys.Unknown, KeyCode.Keypad7 => Keys.Unknown, KeyCode.Keypad8 => Keys.Unknown,
                KeyCode.Keypad9 => Keys.Unknown, KeyCode.KeypadPeriod => Keys.Unknown, KeyCode.KeypadDivide => Keys.Unknown,
                KeyCode.KeypadMultiply => Keys.Unknown, KeyCode.KeypadMinus => Keys.Unknown, KeyCode.KeypadPlus => Keys.Unknown,
                KeyCode.KeypadEnter => Keys.Unknown,

                KeyCode.F1 => Keys.F1, KeyCode.F2 => Keys.F2, KeyCode.F3 => Keys.F3, KeyCode.F4 => Keys.F4,
                KeyCode.F5 => Keys.F5, KeyCode.F6 => Keys.F6, KeyCode.F7 => Keys.F7, KeyCode.F8 => Keys.F8,
                KeyCode.F9 => Keys.F9, KeyCode.F10 => Keys.F10, KeyCode.F11 => Keys.F11, KeyCode.F12 => Keys.F12,
                KeyCode.F13 => Keys.F13, KeyCode.F14 => Keys.F14, KeyCode.F15 => Keys.F15,

                KeyCode.Escape => Keys.Escape, KeyCode.Space => Keys.Space, KeyCode.Return => Keys.Enter,
                KeyCode.Enter => Keys.Enter, KeyCode.Backspace => Keys.Backspace, KeyCode.Tab => Keys.Tab,

                KeyCode.LeftShift => Keys.LeftShift, KeyCode.RightShift => Keys.RightShift,
                KeyCode.LeftControl => Keys.LeftControl, KeyCode.RightControl => Keys.RightControl,
                KeyCode.LeftAlt => Keys.LeftAlt, KeyCode.RightAlt => Keys.RightAlt,

                KeyCode.CapsLock => Keys.CapsLock, KeyCode.NumLock => Keys.NumLock, KeyCode.ScrollLock => Keys.ScrollLock,

                KeyCode.Insert => Keys.Insert, KeyCode.Delete => Keys.Delete, KeyCode.Home => Keys.Home,
                KeyCode.End => Keys.End, KeyCode.PageUp => Keys.PageUp, KeyCode.PageDown => Keys.PageDown,

                KeyCode.UpArrow => Keys.Up, KeyCode.DownArrow => Keys.Down, KeyCode.LeftArrow => Keys.Left, KeyCode.RightArrow => Keys.Right,

                KeyCode.Pause => Keys.Pause, KeyCode.PrintScreen => Keys.PrintScreen, KeyCode.Menu => Keys.Menu,

                KeyCode.Semicolon => Keys.Semicolon, KeyCode.Equals => Keys.Equal, KeyCode.Comma => Keys.Comma,
                KeyCode.Minus => Keys.Minus, KeyCode.Period => Keys.Period, KeyCode.Slash => Keys.Slash,
                KeyCode.BackQuote => Keys.GraveAccent, KeyCode.LeftBracket => Keys.Unknown, KeyCode.Backslash => Keys.Unknown,
                KeyCode.RightBracket => Keys.Unknown, KeyCode.Quote => Keys.Apostrophe,

                _ => Keys.Unknown
            };
        }

        public static KeyCode FromOpenTK(this Keys k)
        {
            return k switch
            {
                Keys.A => KeyCode.A, Keys.B => KeyCode.B, Keys.C => KeyCode.C, Keys.D => KeyCode.D,
                Keys.E => KeyCode.E, Keys.F => KeyCode.F, Keys.G => KeyCode.G, Keys.H => KeyCode.H,
                Keys.I => KeyCode.I, Keys.J => KeyCode.J, Keys.K => KeyCode.K, Keys.L => KeyCode.L,
                Keys.M => KeyCode.M, Keys.N => KeyCode.N, Keys.O => KeyCode.O, Keys.P => KeyCode.P,
                Keys.Q => KeyCode.Q, Keys.R => KeyCode.R, Keys.S => KeyCode.S, Keys.T => KeyCode.T,
                Keys.U => KeyCode.U, Keys.V => KeyCode.V, Keys.W => KeyCode.W, Keys.X => KeyCode.X,
                Keys.Y => KeyCode.Y, Keys.Z => KeyCode.Z,

                Keys.D0 => KeyCode.Alpha0, Keys.D1 => KeyCode.Alpha1, Keys.D2 => KeyCode.Alpha2,
                Keys.D3 => KeyCode.Alpha3, Keys.D4 => KeyCode.Alpha4, Keys.D5 => KeyCode.Alpha5,
                Keys.D6 => KeyCode.Alpha6, Keys.D7 => KeyCode.Alpha7, Keys.D8 => KeyCode.Alpha8,
                Keys.D9 => KeyCode.Alpha9,

                Keys.F1 => KeyCode.F1, Keys.F2 => KeyCode.F2, Keys.F3 => KeyCode.F3, Keys.F4 => KeyCode.F4,
                Keys.F5 => KeyCode.F5, Keys.F6 => KeyCode.F6, Keys.F7 => KeyCode.F7, Keys.F8 => KeyCode.F8,
                Keys.F9 => KeyCode.F9, Keys.F10 => KeyCode.F10, Keys.F11 => KeyCode.F11, Keys.F12 => KeyCode.F12,
                Keys.F13 => KeyCode.F13, Keys.F14 => KeyCode.F14, Keys.F15 => KeyCode.F15,

                Keys.Escape => KeyCode.Escape, Keys.Space => KeyCode.Space, Keys.Enter => KeyCode.Enter,
                Keys.Backspace => KeyCode.Backspace, Keys.Tab => KeyCode.Tab,

                Keys.LeftShift => KeyCode.LeftShift, Keys.RightShift => KeyCode.RightShift,
                Keys.LeftControl => KeyCode.LeftControl, Keys.RightControl => KeyCode.RightControl,
                Keys.LeftAlt => KeyCode.LeftAlt, Keys.RightAlt => KeyCode.RightAlt,

                Keys.CapsLock => KeyCode.CapsLock, Keys.NumLock => KeyCode.NumLock, Keys.ScrollLock => KeyCode.ScrollLock,

                Keys.Insert => KeyCode.Insert, Keys.Delete => KeyCode.Delete, Keys.Home => KeyCode.Home,
                Keys.End => KeyCode.End, Keys.PageUp => KeyCode.PageUp, Keys.PageDown => KeyCode.PageDown,

                Keys.Up => KeyCode.UpArrow, Keys.Down => KeyCode.DownArrow, Keys.Left => KeyCode.LeftArrow, Keys.Right => KeyCode.RightArrow,

                Keys.Pause => KeyCode.Pause, Keys.PrintScreen => KeyCode.PrintScreen, Keys.Menu => KeyCode.Menu,

                Keys.Semicolon => KeyCode.Semicolon, Keys.Equal => KeyCode.Equals, Keys.Comma => KeyCode.Comma,
                Keys.Minus => KeyCode.Minus, Keys.Period => KeyCode.Period, Keys.Slash => KeyCode.Slash,
                Keys.GraveAccent => KeyCode.BackQuote, Keys.Apostrophe => KeyCode.Quote,

                _ => KeyCode.None
            };
        }
    }
}
