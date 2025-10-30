using System;
using System.Collections.Generic;
using System.Numerics;
using System.IO;
using System.Text.Json;

namespace Editor.Themes
{
    /// <summary>
    /// Built-in themes for AstrildApex Editor
    /// </summary>
    public static class BuiltInThemes
    {
        // Cache the built-in themes so we don't recreate all theme objects on every call.
        private static readonly List<EditorTheme> _cachedThemes = CreateThemeList();

        private static List<EditorTheme> CreateThemeList()
        {
            return new List<EditorTheme>
            {
                // Glassmorphism originals
                PurpleDream(),
                PinkPassion(),
                CyberBlue(),
                MintFresh(),
                SunsetGlow(),
                OceanDeep(),
                PastelDream(),
                WarmCoral(),

                // Retro & Space
                RetroWave(),
                SpaceOdyssey(),
                NeonNights(),

                // Chromatic
                ForestCanopy(),
                LavenderFields(),
                AutumnLeaves(),

                // Complementary & Bi-tone
                FireAndIce(),
                DayAndNight(),

                // Classic
                DarkUnity(),
                MonokaiPro(),
                NordAurora()
            };
        }
        /// <summary>
        /// Purple Dream - Glassmorphism theme with purple gradients (from your HTML design)
        /// </summary>
        public static EditorTheme PurpleDream()
        {
            return new EditorTheme
            {
                Name = "Purple Dream",
                Description = "Glassmorphism design with vibrant purple gradients and modern aesthetics",
                
                // Window & Background - Glass effect with purple tint
                WindowBackground = new Vector4(0.1f, 0.1f, 0.15f, 0.95f),
                ChildBackground = new Vector4(0.15f, 0.15f, 0.2f, 0.9f),
                PopupBackground = new Vector4(0.12f, 0.12f, 0.18f, 0.98f),
                Border = new Vector4(1f, 1f, 1f, 0.2f),
                
                // Text - White with good contrast
                Text = new Vector4(1f, 1f, 1f, 1f),
                TextDisabled = new Vector4(1f, 1f, 1f, 0.5f),
                TextSelectedBg = new Vector4(0.4f, 0.5f, 0.92f, 0.35f),
                
                // Frames - Glass input fields
                FrameBg = new Vector4(1f, 1f, 1f, 0.1f),
                FrameBgHovered = new Vector4(1f, 1f, 1f, 0.15f),
                FrameBgActive = new Vector4(1f, 1f, 1f, 0.2f),
                
                // Title Bar - Purple gradient
                TitleBg = new Vector4(0.4f, 0.49f, 0.92f, 0.8f),
                TitleBgActive = new Vector4(0.4f, 0.49f, 0.92f, 1f),
                TitleBgCollapsed = new Vector4(0.4f, 0.49f, 0.92f, 0.5f),
                
                // Menu Bar
                MenuBarBg = new Vector4(0.15f, 0.15f, 0.2f, 0.9f),
                
                // Scrollbar
                ScrollbarBg = new Vector4(0.1f, 0.1f, 0.15f, 0.6f),
                ScrollbarGrab = new Vector4(1f, 1f, 1f, 0.3f),
                ScrollbarGrabHovered = new Vector4(1f, 1f, 1f, 0.4f),
                ScrollbarGrabActive = new Vector4(1f, 1f, 1f, 0.5f),
                
                // CheckMark - Accent color
                CheckMark = new Vector4(0.94f, 0.58f, 0.99f, 1f),
                
                // Slider - Pink-purple gradient
                SliderGrab = new Vector4(0.94f, 0.58f, 0.99f, 0.8f),
                SliderGrabActive = new Vector4(0.96f, 0.34f, 0.42f, 1f),
                
                // Buttons - Glass with gradient on hover
                Button = new Vector4(1f, 1f, 1f, 0.1f),
                ButtonHovered = new Vector4(1f, 1f, 1f, 0.2f),
                ButtonActive = new Vector4(0.94f, 0.58f, 0.99f, 0.4f),
                
                // Header - Section headers (from your design)
                Header = new Vector4(0.3f, 0.5f, 0.8f, 0.8f),
                HeaderHovered = new Vector4(0.4f, 0.6f, 0.9f, 0.9f),
                HeaderActive = new Vector4(0.5f, 0.7f, 1.0f, 1f),
                
                // Separator
                Separator = new Vector4(1f, 1f, 1f, 0.2f),
                SeparatorHovered = new Vector4(1f, 1f, 1f, 0.3f),
                SeparatorActive = new Vector4(1f, 1f, 1f, 0.4f),
                
                // Resize Grip
                ResizeGrip = new Vector4(1f, 1f, 1f, 0.2f),
                ResizeGripHovered = new Vector4(1f, 1f, 1f, 0.3f),
                ResizeGripActive = new Vector4(1f, 1f, 1f, 0.5f),
                
                // Tabs - Glass effect
                Tab = new Vector4(1f, 1f, 1f, 0.1f),
                TabHovered = new Vector4(1f, 1f, 1f, 0.2f),
                TabActive = new Vector4(0.4f, 0.49f, 0.92f, 0.8f),
                TabUnfocused = new Vector4(1f, 1f, 1f, 0.05f),
                TabUnfocusedActive = new Vector4(0.4f, 0.49f, 0.92f, 0.5f),
                
                // Docking
                DockingPreview = new Vector4(0.94f, 0.58f, 0.99f, 0.3f),
                DockingEmptyBg = new Vector4(0.1f, 0.1f, 0.15f, 0.5f),
                
                // Table
                TableHeaderBg = new Vector4(0.2f, 0.2f, 0.25f, 0.9f),
                TableBorderStrong = new Vector4(1f, 1f, 1f, 0.3f),
                TableBorderLight = new Vector4(1f, 1f, 1f, 0.15f),
                TableRowBg = new Vector4(0f, 0f, 0f, 0f),
                TableRowBgAlt = new Vector4(1f, 1f, 1f, 0.05f),
                
                // Drag Drop
                DragDropTarget = new Vector4(0.2f, 0.6f, 1.0f, 0.5f),
                
                // Nav
                NavHighlight = new Vector4(0.94f, 0.58f, 0.99f, 1f),
                NavWindowingHighlight = new Vector4(1f, 1f, 1f, 0.7f),
                NavWindowingDimBg = new Vector4(0.8f, 0.8f, 0.8f, 0.2f),
                
                // Modal
                ModalWindowDimBg = new Vector4(0.1f, 0.1f, 0.15f, 0.7f),
                
                // Inspector Custom Colors
                InspectorLabel = new Vector4(0.8f, 0.8f, 0.8f, 1f),
                InspectorValue = new Vector4(1.0f, 1.0f, 1.0f, 1f),
                InspectorWarning = new Vector4(1.0f, 0.8f, 0.2f, 1f),
                InspectorError = new Vector4(1.0f, 0.3f, 0.3f, 1f),
                InspectorSuccess = new Vector4(0.3f, 1.0f, 0.3f, 1f),
                InspectorInfo = new Vector4(0.4f, 0.7f, 1.0f, 1f),
                InspectorSection = new Vector4(0.3f, 0.5f, 0.8f, 1f),
                
                // Gradients
                GradientStart = new Vector4(0.4f, 0.49f, 0.92f, 1f),  // #667eea
                GradientEnd = new Vector4(0.46f, 0.29f, 0.64f, 1f),   // #764ba2
                AccentColor = new Vector4(0.94f, 0.58f, 0.99f, 1f),   // #f093fb
                
                // Rounding (glassmorphism style)
                WindowRounding = 20.0f,
                ChildRounding = 15.0f,
                FrameRounding = 10.0f,
                PopupRounding = 15.0f,
                ScrollbarRounding = 12.0f,
                GrabRounding = 10.0f,
                TabRounding = 12.0f,
            };
        }
        
        /// <summary>
        /// Cyber Blue - Cool blue glassmorphism theme
        /// </summary>
        public static EditorTheme CyberBlue()
        {
            return new EditorTheme
            {
                Name = "Cyber Blue",
                Description = "Cool blue glassmorphism with cyan accents",
                
                WindowBackground = new Vector4(0.05f, 0.1f, 0.15f, 0.95f),
                ChildBackground = new Vector4(0.08f, 0.13f, 0.18f, 0.9f),
                PopupBackground = new Vector4(0.06f, 0.11f, 0.16f, 0.98f),
                Border = new Vector4(0.3f, 0.7f, 1f, 0.3f),
                
                Text = new Vector4(0.9f, 0.95f, 1f, 1f),
                TextDisabled = new Vector4(0.5f, 0.6f, 0.7f, 1f),
                TextSelectedBg = new Vector4(0.31f, 0.67f, 1f, 0.35f),
                
                FrameBg = new Vector4(0.2f, 0.4f, 0.6f, 0.2f),
                FrameBgHovered = new Vector4(0.2f, 0.4f, 0.6f, 0.3f),
                FrameBgActive = new Vector4(0.2f, 0.5f, 0.8f, 0.4f),
                
                TitleBg = new Vector4(0.31f, 0.67f, 1f, 0.8f),
                TitleBgActive = new Vector4(0.0f, 0.95f, 1f, 1f),
                TitleBgCollapsed = new Vector4(0.31f, 0.67f, 1f, 0.5f),
                
                MenuBarBg = new Vector4(0.08f, 0.13f, 0.18f, 0.95f),
                
                ScrollbarBg = new Vector4(0.05f, 0.1f, 0.15f, 0.6f),
                ScrollbarGrab = new Vector4(0.31f, 0.67f, 1f, 0.5f),
                ScrollbarGrabHovered = new Vector4(0.31f, 0.67f, 1f, 0.7f),
                ScrollbarGrabActive = new Vector4(0.0f, 0.95f, 1f, 0.9f),
                
                CheckMark = new Vector4(0.0f, 0.95f, 1f, 1f),
                
                SliderGrab = new Vector4(0.31f, 0.67f, 1f, 0.8f),
                SliderGrabActive = new Vector4(0.0f, 0.95f, 1f, 1f),
                
                Button = new Vector4(0.2f, 0.4f, 0.6f, 0.3f),
                ButtonHovered = new Vector4(0.31f, 0.67f, 1f, 0.4f),
                ButtonActive = new Vector4(0.0f, 0.95f, 1f, 0.6f),
                
                Header = new Vector4(0.2f, 0.45f, 0.7f, 0.8f),
                HeaderHovered = new Vector4(0.31f, 0.67f, 1f, 0.9f),
                HeaderActive = new Vector4(0.0f, 0.95f, 1f, 1f),
                
                Separator = new Vector4(0.3f, 0.6f, 0.9f, 0.3f),
                SeparatorHovered = new Vector4(0.3f, 0.7f, 1f, 0.5f),
                SeparatorActive = new Vector4(0.0f, 0.95f, 1f, 0.7f),
                
                ResizeGrip = new Vector4(0.31f, 0.67f, 1f, 0.25f),
                ResizeGripHovered = new Vector4(0.31f, 0.67f, 1f, 0.5f),
                ResizeGripActive = new Vector4(0.0f, 0.95f, 1f, 0.8f),
                
                Tab = new Vector4(0.2f, 0.4f, 0.6f, 0.3f),
                TabHovered = new Vector4(0.31f, 0.67f, 1f, 0.5f),
                TabActive = new Vector4(0.31f, 0.67f, 1f, 0.8f),
                TabUnfocused = new Vector4(0.1f, 0.2f, 0.3f, 0.5f),
                TabUnfocusedActive = new Vector4(0.2f, 0.4f, 0.6f, 0.6f),
                
                DockingPreview = new Vector4(0.0f, 0.95f, 1f, 0.3f),
                DockingEmptyBg = new Vector4(0.05f, 0.1f, 0.15f, 0.5f),
                
                TableHeaderBg = new Vector4(0.1f, 0.2f, 0.3f, 0.9f),
                TableBorderStrong = new Vector4(0.3f, 0.6f, 0.9f, 0.5f),
                TableBorderLight = new Vector4(0.2f, 0.4f, 0.6f, 0.3f),
                TableRowBg = new Vector4(0f, 0f, 0f, 0f),
                TableRowBgAlt = new Vector4(0.2f, 0.4f, 0.6f, 0.1f),
                
                DragDropTarget = new Vector4(0.0f, 0.95f, 1f, 0.5f),
                
                NavHighlight = new Vector4(0.0f, 0.95f, 1f, 1f),
                NavWindowingHighlight = new Vector4(1f, 1f, 1f, 0.7f),
                NavWindowingDimBg = new Vector4(0.2f, 0.4f, 0.6f, 0.2f),
                
                ModalWindowDimBg = new Vector4(0.05f, 0.1f, 0.15f, 0.75f),
                
                InspectorLabel = new Vector4(0.8f, 0.9f, 1f, 1f),
                InspectorValue = new Vector4(0.9f, 0.95f, 1f, 1f),
                InspectorWarning = new Vector4(1.0f, 0.8f, 0.2f, 1f),
                InspectorError = new Vector4(1.0f, 0.3f, 0.4f, 1f),
                InspectorSuccess = new Vector4(0.2f, 1.0f, 0.8f, 1f),
                InspectorInfo = new Vector4(0.0f, 0.95f, 1f, 1f),
                InspectorSection = new Vector4(0.2f, 0.45f, 0.7f, 1f),
                
                GradientStart = new Vector4(0.31f, 0.67f, 1f, 1f),
                GradientEnd = new Vector4(0.0f, 0.95f, 1f, 1f),
                AccentColor = new Vector4(0.0f, 0.95f, 1f, 1f),
                
                WindowRounding = 15.0f,
                ChildRounding = 12.0f,
                FrameRounding = 8.0f,
                PopupRounding = 12.0f,
                ScrollbarRounding = 10.0f,
                GrabRounding = 8.0f,
                TabRounding = 10.0f,
            };
        }
        
        /// <summary>
        /// Mint Fresh - Green/cyan refreshing theme
        /// </summary>
        public static EditorTheme MintFresh()
        {
            return new EditorTheme
            {
                Name = "Mint Fresh",
                Description = "Refreshing mint green with cyan highlights",
                
                WindowBackground = new Vector4(0.05f, 0.15f, 0.12f, 0.95f),
                ChildBackground = new Vector4(0.08f, 0.18f, 0.15f, 0.9f),
                PopupBackground = new Vector4(0.06f, 0.16f, 0.13f, 0.98f),
                Border = new Vector4(0.3f, 1f, 0.7f, 0.25f),
                
                Text = new Vector4(0.9f, 1f, 0.95f, 1f),
                TextDisabled = new Vector4(0.5f, 0.7f, 0.6f, 1f),
                TextSelectedBg = new Vector4(0.26f, 0.91f, 0.48f, 0.35f),
                
                FrameBg = new Vector4(0.2f, 0.6f, 0.4f, 0.2f),
                FrameBgHovered = new Vector4(0.2f, 0.6f, 0.4f, 0.3f),
                FrameBgActive = new Vector4(0.26f, 0.91f, 0.48f, 0.4f),
                
                TitleBg = new Vector4(0.26f, 0.91f, 0.48f, 0.8f),
                TitleBgActive = new Vector4(0.22f, 0.97f, 0.84f, 1f),
                TitleBgCollapsed = new Vector4(0.26f, 0.91f, 0.48f, 0.5f),
                
                MenuBarBg = new Vector4(0.08f, 0.18f, 0.15f, 0.95f),
                
                ScrollbarBg = new Vector4(0.05f, 0.15f, 0.12f, 0.6f),
                ScrollbarGrab = new Vector4(0.26f, 0.91f, 0.48f, 0.5f),
                ScrollbarGrabHovered = new Vector4(0.26f, 0.91f, 0.48f, 0.7f),
                ScrollbarGrabActive = new Vector4(0.22f, 0.97f, 0.84f, 0.9f),
                
                CheckMark = new Vector4(0.22f, 0.97f, 0.84f, 1f),
                
                SliderGrab = new Vector4(0.26f, 0.91f, 0.48f, 0.8f),
                SliderGrabActive = new Vector4(0.22f, 0.97f, 0.84f, 1f),
                
                Button = new Vector4(0.2f, 0.6f, 0.4f, 0.3f),
                ButtonHovered = new Vector4(0.26f, 0.91f, 0.48f, 0.5f),
                ButtonActive = new Vector4(0.22f, 0.97f, 0.84f, 0.7f),
                
                Header = new Vector4(0.2f, 0.7f, 0.5f, 0.8f),
                HeaderHovered = new Vector4(0.26f, 0.91f, 0.48f, 0.9f),
                HeaderActive = new Vector4(0.22f, 0.97f, 0.84f, 1f),
                
                Separator = new Vector4(0.3f, 0.9f, 0.6f, 0.3f),
                SeparatorHovered = new Vector4(0.3f, 1f, 0.7f, 0.5f),
                SeparatorActive = new Vector4(0.22f, 0.97f, 0.84f, 0.7f),
                
                ResizeGrip = new Vector4(0.26f, 0.91f, 0.48f, 0.25f),
                ResizeGripHovered = new Vector4(0.26f, 0.91f, 0.48f, 0.5f),
                ResizeGripActive = new Vector4(0.22f, 0.97f, 0.84f, 0.8f),
                
                Tab = new Vector4(0.2f, 0.6f, 0.4f, 0.3f),
                TabHovered = new Vector4(0.26f, 0.91f, 0.48f, 0.5f),
                TabActive = new Vector4(0.26f, 0.91f, 0.48f, 0.8f),
                TabUnfocused = new Vector4(0.1f, 0.3f, 0.2f, 0.5f),
                TabUnfocusedActive = new Vector4(0.15f, 0.45f, 0.3f, 0.6f),
                
                DockingPreview = new Vector4(0.22f, 0.97f, 0.84f, 0.3f),
                DockingEmptyBg = new Vector4(0.05f, 0.15f, 0.12f, 0.5f),
                
                TableHeaderBg = new Vector4(0.1f, 0.3f, 0.2f, 0.9f),
                TableBorderStrong = new Vector4(0.3f, 0.9f, 0.6f, 0.5f),
                TableBorderLight = new Vector4(0.2f, 0.6f, 0.4f, 0.3f),
                TableRowBg = new Vector4(0f, 0f, 0f, 0f),
                TableRowBgAlt = new Vector4(0.2f, 0.6f, 0.4f, 0.1f),
                
                DragDropTarget = new Vector4(0.22f, 0.97f, 0.84f, 0.5f),
                
                NavHighlight = new Vector4(0.22f, 0.97f, 0.84f, 1f),
                NavWindowingHighlight = new Vector4(1f, 1f, 1f, 0.7f),
                NavWindowingDimBg = new Vector4(0.2f, 0.6f, 0.4f, 0.2f),
                
                ModalWindowDimBg = new Vector4(0.05f, 0.15f, 0.12f, 0.75f),
                
                InspectorLabel = new Vector4(0.8f, 1f, 0.9f, 1f),
                InspectorValue = new Vector4(0.9f, 1f, 0.95f, 1f),
                InspectorWarning = new Vector4(1.0f, 0.85f, 0.2f, 1f),
                InspectorError = new Vector4(1.0f, 0.35f, 0.4f, 1f),
                InspectorSuccess = new Vector4(0.22f, 0.97f, 0.84f, 1f),
                InspectorInfo = new Vector4(0.2f, 0.8f, 1f, 1f),
                InspectorSection = new Vector4(0.2f, 0.7f, 0.5f, 1f),
                
                GradientStart = new Vector4(0.26f, 0.91f, 0.48f, 1f),
                GradientEnd = new Vector4(0.22f, 0.97f, 0.84f, 1f),
                AccentColor = new Vector4(0.22f, 0.97f, 0.84f, 1f),
                
                WindowRounding = 18.0f,
                ChildRounding = 14.0f,
                FrameRounding = 9.0f,
                PopupRounding = 14.0f,
                ScrollbarRounding = 11.0f,
                GrabRounding = 9.0f,
                TabRounding = 11.0f,
            };
        }
        
        /// <summary>
        /// Pink Passion - Vibrant pink glassmorphism
        /// </summary>
        public static EditorTheme PinkPassion()
        {
            return new EditorTheme
            {
                Name = "Pink Passion",
                Description = "Vibrant pink to red gradient with passionate energy",
                
                WindowBackground = new Vector4(0.15f, 0.1f, 0.13f, 0.95f),
                ChildBackground = new Vector4(0.18f, 0.12f, 0.16f, 0.9f),
                PopupBackground = new Vector4(0.16f, 0.11f, 0.14f, 0.98f),
                Border = new Vector4(1f, 0.5f, 0.8f, 0.3f),
                
                Text = new Vector4(1f, 0.95f, 0.98f, 1f),
                TextDisabled = new Vector4(1f, 0.7f, 0.85f, 0.6f),
                TextSelectedBg = new Vector4(0.94f, 0.58f, 0.99f, 0.35f),
                
                FrameBg = new Vector4(1f, 0.4f, 0.7f, 0.15f),
                FrameBgHovered = new Vector4(1f, 0.4f, 0.7f, 0.25f),
                FrameBgActive = new Vector4(0.96f, 0.34f, 0.42f, 0.35f),
                
                TitleBg = new Vector4(0.94f, 0.58f, 0.99f, 0.8f),
                TitleBgActive = new Vector4(0.96f, 0.34f, 0.42f, 1f),
                TitleBgCollapsed = new Vector4(0.94f, 0.58f, 0.99f, 0.5f),
                
                MenuBarBg = new Vector4(0.18f, 0.12f, 0.16f, 0.95f),
                
                ScrollbarBg = new Vector4(0.15f, 0.1f, 0.13f, 0.6f),
                ScrollbarGrab = new Vector4(0.94f, 0.58f, 0.99f, 0.5f),
                ScrollbarGrabHovered = new Vector4(0.94f, 0.58f, 0.99f, 0.7f),
                ScrollbarGrabActive = new Vector4(0.96f, 0.34f, 0.42f, 0.9f),
                
                CheckMark = new Vector4(0.96f, 0.34f, 0.42f, 1f),
                
                SliderGrab = new Vector4(0.94f, 0.58f, 0.99f, 0.8f),
                SliderGrabActive = new Vector4(0.96f, 0.34f, 0.42f, 1f),
                
                Button = new Vector4(1f, 0.4f, 0.7f, 0.3f),
                ButtonHovered = new Vector4(0.94f, 0.58f, 0.99f, 0.5f),
                ButtonActive = new Vector4(0.96f, 0.34f, 0.42f, 0.7f),
                
                Header = new Vector4(0.8f, 0.3f, 0.6f, 0.8f),
                HeaderHovered = new Vector4(0.94f, 0.58f, 0.99f, 0.9f),
                HeaderActive = new Vector4(0.96f, 0.34f, 0.42f, 1f),
                
                Separator = new Vector4(1f, 0.5f, 0.8f, 0.3f),
                SeparatorHovered = new Vector4(1f, 0.6f, 0.85f, 0.5f),
                SeparatorActive = new Vector4(0.96f, 0.34f, 0.42f, 0.7f),
                
                ResizeGrip = new Vector4(0.94f, 0.58f, 0.99f, 0.25f),
                ResizeGripHovered = new Vector4(0.94f, 0.58f, 0.99f, 0.5f),
                ResizeGripActive = new Vector4(0.96f, 0.34f, 0.42f, 0.8f),
                
                Tab = new Vector4(1f, 0.4f, 0.7f, 0.3f),
                TabHovered = new Vector4(0.94f, 0.58f, 0.99f, 0.5f),
                TabActive = new Vector4(0.94f, 0.58f, 0.99f, 0.8f),
                TabUnfocused = new Vector4(0.6f, 0.2f, 0.4f, 0.5f),
                TabUnfocusedActive = new Vector4(0.7f, 0.3f, 0.5f, 0.6f),
                
                DockingPreview = new Vector4(0.96f, 0.34f, 0.42f, 0.3f),
                DockingEmptyBg = new Vector4(0.15f, 0.1f, 0.13f, 0.5f),
                
                TableHeaderBg = new Vector4(0.25f, 0.15f, 0.2f, 0.9f),
                TableBorderStrong = new Vector4(1f, 0.5f, 0.8f, 0.5f),
                TableBorderLight = new Vector4(1f, 0.5f, 0.8f, 0.25f),
                TableRowBg = new Vector4(0f, 0f, 0f, 0f),
                TableRowBgAlt = new Vector4(1f, 0.4f, 0.7f, 0.08f),
                
                DragDropTarget = new Vector4(0.96f, 0.34f, 0.42f, 0.5f),
                
                NavHighlight = new Vector4(0.96f, 0.34f, 0.42f, 1f),
                NavWindowingHighlight = new Vector4(1f, 0.9f, 0.95f, 0.7f),
                NavWindowingDimBg = new Vector4(0.8f, 0.4f, 0.6f, 0.2f),
                
                ModalWindowDimBg = new Vector4(0.15f, 0.1f, 0.13f, 0.75f),
                
                InspectorLabel = new Vector4(1f, 0.9f, 0.95f, 1f),
                InspectorValue = new Vector4(1f, 0.95f, 0.98f, 1f),
                InspectorWarning = new Vector4(1.0f, 0.7f, 0.2f, 1f),
                InspectorError = new Vector4(1.0f, 0.2f, 0.3f, 1f),
                InspectorSuccess = new Vector4(1.0f, 0.5f, 0.8f, 1f),
                InspectorInfo = new Vector4(0.94f, 0.58f, 0.99f, 1f),
                InspectorSection = new Vector4(0.8f, 0.3f, 0.6f, 1f),
                
                GradientStart = new Vector4(0.94f, 0.58f, 0.99f, 1f),  // #f093fb
                GradientEnd = new Vector4(0.96f, 0.34f, 0.42f, 1f),    // #f5576c
                AccentColor = new Vector4(0.96f, 0.34f, 0.42f, 1f),
                
                WindowRounding = 20.0f,
                ChildRounding = 15.0f,
                FrameRounding = 10.0f,
                PopupRounding = 15.0f,
                ScrollbarRounding = 12.0f,
                GrabRounding = 10.0f,
                TabRounding = 12.0f,
            };
        }
        
        /// <summary>
        /// Sunset Glow - Warm orange to yellow gradient
        /// </summary>
        public static EditorTheme SunsetGlow()
        {
            return new EditorTheme
            {
                Name = "Sunset Glow",
                Description = "Warm sunset colors from orange to golden yellow",
                
                WindowBackground = new Vector4(0.15f, 0.1f, 0.08f, 0.95f),
                ChildBackground = new Vector4(0.18f, 0.13f, 0.1f, 0.9f),
                PopupBackground = new Vector4(0.16f, 0.11f, 0.09f, 0.98f),
                Border = new Vector4(1f, 0.7f, 0.3f, 0.3f),
                
                Text = new Vector4(1f, 0.95f, 0.85f, 1f),
                TextDisabled = new Vector4(0.8f, 0.6f, 0.4f, 1f),
                TextSelectedBg = new Vector4(0.98f, 0.56f, 0.34f, 0.35f),
                
                FrameBg = new Vector4(1f, 0.6f, 0.2f, 0.2f),
                FrameBgHovered = new Vector4(1f, 0.6f, 0.2f, 0.3f),
                FrameBgActive = new Vector4(1f, 0.88f, 0.25f, 0.4f),
                
                TitleBg = new Vector4(0.98f, 0.56f, 0.34f, 0.8f),
                TitleBgActive = new Vector4(1f, 0.88f, 0.25f, 1f),
                TitleBgCollapsed = new Vector4(0.98f, 0.56f, 0.34f, 0.5f),
                
                MenuBarBg = new Vector4(0.18f, 0.13f, 0.1f, 0.95f),
                
                ScrollbarBg = new Vector4(0.15f, 0.1f, 0.08f, 0.6f),
                ScrollbarGrab = new Vector4(0.98f, 0.56f, 0.34f, 0.5f),
                ScrollbarGrabHovered = new Vector4(0.98f, 0.56f, 0.34f, 0.7f),
                ScrollbarGrabActive = new Vector4(1f, 0.88f, 0.25f, 0.9f),
                
                CheckMark = new Vector4(1f, 0.88f, 0.25f, 1f),
                
                SliderGrab = new Vector4(0.98f, 0.56f, 0.34f, 0.8f),
                SliderGrabActive = new Vector4(1f, 0.88f, 0.25f, 1f),
                
                Button = new Vector4(1f, 0.6f, 0.2f, 0.3f),
                ButtonHovered = new Vector4(0.98f, 0.56f, 0.34f, 0.5f),
                ButtonActive = new Vector4(1f, 0.88f, 0.25f, 0.7f),
                
                Header = new Vector4(0.9f, 0.5f, 0.2f, 0.8f),
                HeaderHovered = new Vector4(0.98f, 0.56f, 0.34f, 0.9f),
                HeaderActive = new Vector4(1f, 0.88f, 0.25f, 1f),
                
                Separator = new Vector4(1f, 0.7f, 0.3f, 0.3f),
                SeparatorHovered = new Vector4(1f, 0.8f, 0.4f, 0.5f),
                SeparatorActive = new Vector4(1f, 0.88f, 0.25f, 0.7f),
                
                ResizeGrip = new Vector4(0.98f, 0.56f, 0.34f, 0.25f),
                ResizeGripHovered = new Vector4(0.98f, 0.56f, 0.34f, 0.5f),
                ResizeGripActive = new Vector4(1f, 0.88f, 0.25f, 0.8f),
                
                Tab = new Vector4(1f, 0.6f, 0.2f, 0.3f),
                TabHovered = new Vector4(0.98f, 0.56f, 0.34f, 0.5f),
                TabActive = new Vector4(0.98f, 0.56f, 0.34f, 0.8f),
                TabUnfocused = new Vector4(0.5f, 0.3f, 0.15f, 0.5f),
                TabUnfocusedActive = new Vector4(0.7f, 0.4f, 0.2f, 0.6f),
                
                DockingPreview = new Vector4(1f, 0.88f, 0.25f, 0.3f),
                DockingEmptyBg = new Vector4(0.15f, 0.1f, 0.08f, 0.5f),
                
                TableHeaderBg = new Vector4(0.25f, 0.18f, 0.12f, 0.9f),
                TableBorderStrong = new Vector4(1f, 0.7f, 0.3f, 0.5f),
                TableBorderLight = new Vector4(1f, 0.7f, 0.3f, 0.25f),
                TableRowBg = new Vector4(0f, 0f, 0f, 0f),
                TableRowBgAlt = new Vector4(1f, 0.6f, 0.2f, 0.08f),
                
                DragDropTarget = new Vector4(1f, 0.88f, 0.25f, 0.5f),
                
                NavHighlight = new Vector4(1f, 0.88f, 0.25f, 1f),
                NavWindowingHighlight = new Vector4(1f, 0.95f, 0.8f, 0.7f),
                NavWindowingDimBg = new Vector4(0.8f, 0.5f, 0.2f, 0.2f),
                
                ModalWindowDimBg = new Vector4(0.15f, 0.1f, 0.08f, 0.75f),
                
                InspectorLabel = new Vector4(1f, 0.9f, 0.75f, 1f),
                InspectorValue = new Vector4(1f, 0.95f, 0.85f, 1f),
                InspectorWarning = new Vector4(1.0f, 0.88f, 0.25f, 1f),
                InspectorError = new Vector4(1.0f, 0.3f, 0.2f, 1f),
                InspectorSuccess = new Vector4(1.0f, 0.85f, 0.4f, 1f),
                InspectorInfo = new Vector4(0.98f, 0.75f, 0.5f, 1f),
                InspectorSection = new Vector4(0.9f, 0.5f, 0.2f, 1f),
                
                GradientStart = new Vector4(0.98f, 0.56f, 0.34f, 1f),  // #fa709a
                GradientEnd = new Vector4(1f, 0.88f, 0.25f, 1f),       // #fee140
                AccentColor = new Vector4(1f, 0.88f, 0.25f, 1f),
                
                WindowRounding = 18.0f,
                ChildRounding = 14.0f,
                FrameRounding = 9.0f,
                PopupRounding = 14.0f,
                ScrollbarRounding = 11.0f,
                GrabRounding = 9.0f,
                TabRounding = 11.0f,
            };
        }
        
        /// <summary>
        /// Ocean Deep - Deep blue ocean theme
        /// </summary>
        public static EditorTheme OceanDeep()
        {
            return new EditorTheme
            {
                Name = "Ocean Deep",
                Description = "Deep ocean blue gradient from cyan to dark navy",
                
                WindowBackground = new Vector4(0.05f, 0.08f, 0.15f, 0.95f),
                ChildBackground = new Vector4(0.07f, 0.1f, 0.18f, 0.9f),
                PopupBackground = new Vector4(0.06f, 0.09f, 0.16f, 0.98f),
                Border = new Vector4(0.2f, 0.5f, 0.8f, 0.3f),
                
                Text = new Vector4(0.85f, 0.95f, 1f, 1f),
                TextDisabled = new Vector4(0.4f, 0.6f, 0.8f, 1f),
                TextSelectedBg = new Vector4(0.19f, 0.81f, 0.82f, 0.35f),
                
                FrameBg = new Vector4(0.1f, 0.4f, 0.7f, 0.2f),
                FrameBgHovered = new Vector4(0.1f, 0.4f, 0.7f, 0.3f),
                FrameBgActive = new Vector4(0.19f, 0.81f, 0.82f, 0.4f),
                
                TitleBg = new Vector4(0.19f, 0.81f, 0.82f, 0.8f),
                TitleBgActive = new Vector4(0.2f, 0.03f, 0.4f, 1f),
                TitleBgCollapsed = new Vector4(0.19f, 0.81f, 0.82f, 0.5f),
                
                MenuBarBg = new Vector4(0.07f, 0.1f, 0.18f, 0.95f),
                
                ScrollbarBg = new Vector4(0.05f, 0.08f, 0.15f, 0.6f),
                ScrollbarGrab = new Vector4(0.19f, 0.81f, 0.82f, 0.5f),
                ScrollbarGrabHovered = new Vector4(0.19f, 0.81f, 0.82f, 0.7f),
                ScrollbarGrabActive = new Vector4(0.2f, 0.03f, 0.4f, 0.9f),
                
                CheckMark = new Vector4(0.19f, 0.81f, 0.82f, 1f),
                
                SliderGrab = new Vector4(0.19f, 0.81f, 0.82f, 0.8f),
                SliderGrabActive = new Vector4(0.2f, 0.03f, 0.4f, 1f),
                
                Button = new Vector4(0.1f, 0.4f, 0.7f, 0.3f),
                ButtonHovered = new Vector4(0.19f, 0.81f, 0.82f, 0.5f),
                ButtonActive = new Vector4(0.2f, 0.03f, 0.4f, 0.7f),
                
                Header = new Vector4(0.1f, 0.3f, 0.6f, 0.8f),
                HeaderHovered = new Vector4(0.19f, 0.81f, 0.82f, 0.9f),
                HeaderActive = new Vector4(0.2f, 0.03f, 0.4f, 1f),
                
                Separator = new Vector4(0.2f, 0.5f, 0.8f, 0.3f),
                SeparatorHovered = new Vector4(0.3f, 0.7f, 0.9f, 0.5f),
                SeparatorActive = new Vector4(0.19f, 0.81f, 0.82f, 0.7f),
                
                ResizeGrip = new Vector4(0.19f, 0.81f, 0.82f, 0.25f),
                ResizeGripHovered = new Vector4(0.19f, 0.81f, 0.82f, 0.5f),
                ResizeGripActive = new Vector4(0.2f, 0.03f, 0.4f, 0.8f),
                
                Tab = new Vector4(0.1f, 0.4f, 0.7f, 0.3f),
                TabHovered = new Vector4(0.19f, 0.81f, 0.82f, 0.5f),
                TabActive = new Vector4(0.19f, 0.81f, 0.82f, 0.8f),
                TabUnfocused = new Vector4(0.05f, 0.15f, 0.3f, 0.5f),
                TabUnfocusedActive = new Vector4(0.1f, 0.25f, 0.45f, 0.6f),
                
                DockingPreview = new Vector4(0.19f, 0.81f, 0.82f, 0.3f),
                DockingEmptyBg = new Vector4(0.05f, 0.08f, 0.15f, 0.5f),
                
                TableHeaderBg = new Vector4(0.08f, 0.12f, 0.22f, 0.9f),
                TableBorderStrong = new Vector4(0.2f, 0.5f, 0.8f, 0.5f),
                TableBorderLight = new Vector4(0.1f, 0.4f, 0.7f, 0.25f),
                TableRowBg = new Vector4(0f, 0f, 0f, 0f),
                TableRowBgAlt = new Vector4(0.1f, 0.4f, 0.7f, 0.08f),
                
                DragDropTarget = new Vector4(0.19f, 0.81f, 0.82f, 0.5f),
                
                NavHighlight = new Vector4(0.19f, 0.81f, 0.82f, 1f),
                NavWindowingHighlight = new Vector4(0.8f, 0.95f, 1f, 0.7f),
                NavWindowingDimBg = new Vector4(0.1f, 0.3f, 0.6f, 0.2f),
                
                ModalWindowDimBg = new Vector4(0.05f, 0.08f, 0.15f, 0.75f),
                
                InspectorLabel = new Vector4(0.75f, 0.9f, 1f, 1f),
                InspectorValue = new Vector4(0.85f, 0.95f, 1f, 1f),
                InspectorWarning = new Vector4(1.0f, 0.85f, 0.3f, 1f),
                InspectorError = new Vector4(1.0f, 0.35f, 0.4f, 1f),
                InspectorSuccess = new Vector4(0.3f, 1.0f, 0.9f, 1f),
                InspectorInfo = new Vector4(0.19f, 0.81f, 0.82f, 1f),
                InspectorSection = new Vector4(0.1f, 0.3f, 0.6f, 1f),
                
                GradientStart = new Vector4(0.19f, 0.81f, 0.82f, 1f),  // #30cfd0
                GradientEnd = new Vector4(0.2f, 0.03f, 0.4f, 1f),      // #330867
                AccentColor = new Vector4(0.19f, 0.81f, 0.82f, 1f),
                
                WindowRounding = 16.0f,
                ChildRounding = 12.0f,
                FrameRounding = 8.0f,
                PopupRounding = 12.0f,
                ScrollbarRounding = 10.0f,
                GrabRounding = 8.0f,
                TabRounding = 10.0f,
            };
        }
        
        /// <summary>
        /// Pastel Dream - Soft pastel colors
        /// </summary>
        public static EditorTheme PastelDream()
        {
            return new EditorTheme
            {
                Name = "Pastel Dream",
                Description = "Soft dreamy pastel colors from mint to pink",
                
                WindowBackground = new Vector4(0.18f, 0.15f, 0.16f, 0.95f),
                ChildBackground = new Vector4(0.2f, 0.17f, 0.18f, 0.9f),
                PopupBackground = new Vector4(0.19f, 0.16f, 0.17f, 0.98f),
                Border = new Vector4(0.8f, 0.7f, 0.8f, 0.3f),
                
                Text = new Vector4(0.95f, 0.92f, 0.95f, 1f),
                TextDisabled = new Vector4(0.7f, 0.65f, 0.7f, 1f),
                TextSelectedBg = new Vector4(0.66f, 0.93f, 0.92f, 0.35f),
                
                FrameBg = new Vector4(0.7f, 0.6f, 0.7f, 0.15f),
                FrameBgHovered = new Vector4(0.7f, 0.6f, 0.7f, 0.25f),
                FrameBgActive = new Vector4(1f, 0.84f, 0.89f, 0.35f),
                
                TitleBg = new Vector4(0.66f, 0.93f, 0.92f, 0.8f),
                TitleBgActive = new Vector4(1f, 0.84f, 0.89f, 1f),
                TitleBgCollapsed = new Vector4(0.66f, 0.93f, 0.92f, 0.5f),
                
                MenuBarBg = new Vector4(0.2f, 0.17f, 0.18f, 0.95f),
                
                ScrollbarBg = new Vector4(0.18f, 0.15f, 0.16f, 0.6f),
                ScrollbarGrab = new Vector4(0.66f, 0.93f, 0.92f, 0.5f),
                ScrollbarGrabHovered = new Vector4(0.66f, 0.93f, 0.92f, 0.7f),
                ScrollbarGrabActive = new Vector4(1f, 0.84f, 0.89f, 0.9f),
                
                CheckMark = new Vector4(1f, 0.84f, 0.89f, 1f),
                
                SliderGrab = new Vector4(0.66f, 0.93f, 0.92f, 0.8f),
                SliderGrabActive = new Vector4(1f, 0.84f, 0.89f, 1f),
                
                Button = new Vector4(0.7f, 0.6f, 0.7f, 0.3f),
                ButtonHovered = new Vector4(0.66f, 0.93f, 0.92f, 0.5f),
                ButtonActive = new Vector4(1f, 0.84f, 0.89f, 0.7f),
                
                Header = new Vector4(0.6f, 0.75f, 0.75f, 0.8f),
                HeaderHovered = new Vector4(0.66f, 0.93f, 0.92f, 0.9f),
                HeaderActive = new Vector4(1f, 0.84f, 0.89f, 1f),
                
                Separator = new Vector4(0.8f, 0.7f, 0.8f, 0.3f),
                SeparatorHovered = new Vector4(0.85f, 0.8f, 0.85f, 0.5f),
                SeparatorActive = new Vector4(1f, 0.84f, 0.89f, 0.7f),
                
                ResizeGrip = new Vector4(0.66f, 0.93f, 0.92f, 0.25f),
                ResizeGripHovered = new Vector4(0.66f, 0.93f, 0.92f, 0.5f),
                ResizeGripActive = new Vector4(1f, 0.84f, 0.89f, 0.8f),
                
                Tab = new Vector4(0.7f, 0.6f, 0.7f, 0.3f),
                TabHovered = new Vector4(0.66f, 0.93f, 0.92f, 0.5f),
                TabActive = new Vector4(0.66f, 0.93f, 0.92f, 0.8f),
                TabUnfocused = new Vector4(0.4f, 0.35f, 0.4f, 0.5f),
                TabUnfocusedActive = new Vector4(0.5f, 0.45f, 0.5f, 0.6f),
                
                DockingPreview = new Vector4(1f, 0.84f, 0.89f, 0.3f),
                DockingEmptyBg = new Vector4(0.18f, 0.15f, 0.16f, 0.5f),
                
                TableHeaderBg = new Vector4(0.25f, 0.22f, 0.24f, 0.9f),
                TableBorderStrong = new Vector4(0.8f, 0.7f, 0.8f, 0.5f),
                TableBorderLight = new Vector4(0.7f, 0.6f, 0.7f, 0.25f),
                TableRowBg = new Vector4(0f, 0f, 0f, 0f),
                TableRowBgAlt = new Vector4(0.7f, 0.6f, 0.7f, 0.05f),
                
                DragDropTarget = new Vector4(1f, 0.84f, 0.89f, 0.5f),
                
                NavHighlight = new Vector4(1f, 0.84f, 0.89f, 1f),
                NavWindowingHighlight = new Vector4(0.95f, 0.92f, 0.95f, 0.7f),
                NavWindowingDimBg = new Vector4(0.7f, 0.6f, 0.7f, 0.2f),
                
                ModalWindowDimBg = new Vector4(0.18f, 0.15f, 0.16f, 0.75f),
                
                InspectorLabel = new Vector4(0.9f, 0.85f, 0.9f, 1f),
                InspectorValue = new Vector4(0.95f, 0.92f, 0.95f, 1f),
                InspectorWarning = new Vector4(1.0f, 0.9f, 0.6f, 1f),
                InspectorError = new Vector4(1.0f, 0.7f, 0.75f, 1f),
                InspectorSuccess = new Vector4(0.7f, 1.0f, 0.95f, 1f),
                InspectorInfo = new Vector4(0.75f, 0.9f, 1.0f, 1f),
                InspectorSection = new Vector4(0.6f, 0.75f, 0.75f, 1f),
                
                GradientStart = new Vector4(0.66f, 0.93f, 0.92f, 1f),  // #a8edea
                GradientEnd = new Vector4(1f, 0.84f, 0.89f, 1f),       // #fed6e3
                AccentColor = new Vector4(1f, 0.84f, 0.89f, 1f),
                
                WindowRounding = 20.0f,
                ChildRounding = 16.0f,
                FrameRounding = 12.0f,
                PopupRounding = 16.0f,
                ScrollbarRounding = 14.0f,
                GrabRounding = 12.0f,
                TabRounding = 14.0f,
            };
        }
        
        /// <summary>
        /// Warm Coral - Coral pink to orange
        /// </summary>
        public static EditorTheme WarmCoral()
        {
            return new EditorTheme
            {
                Name = "Warm Coral",
                Description = "Warm coral and peach tones",
                
                WindowBackground = new Vector4(0.15f, 0.11f, 0.1f, 0.95f),
                ChildBackground = new Vector4(0.18f, 0.13f, 0.12f, 0.9f),
                PopupBackground = new Vector4(0.16f, 0.12f, 0.11f, 0.98f),
                Border = new Vector4(1f, 0.6f, 0.5f, 0.3f),
                
                Text = new Vector4(1f, 0.95f, 0.9f, 1f),
                TextDisabled = new Vector4(0.8f, 0.6f, 0.5f, 1f),
                TextSelectedBg = new Vector4(1f, 0.6f, 0.34f, 0.35f),
                
                FrameBg = new Vector4(1f, 0.5f, 0.3f, 0.15f),
                FrameBgHovered = new Vector4(1f, 0.5f, 0.3f, 0.25f),
                FrameBgActive = new Vector4(1f, 0.41f, 0.53f, 0.35f),
                
                TitleBg = new Vector4(1f, 0.6f, 0.34f, 0.8f),
                TitleBgActive = new Vector4(1f, 0.41f, 0.53f, 1f),
                TitleBgCollapsed = new Vector4(1f, 0.6f, 0.34f, 0.5f),
                
                MenuBarBg = new Vector4(0.18f, 0.13f, 0.12f, 0.95f),
                
                ScrollbarBg = new Vector4(0.15f, 0.11f, 0.1f, 0.6f),
                ScrollbarGrab = new Vector4(1f, 0.6f, 0.34f, 0.5f),
                ScrollbarGrabHovered = new Vector4(1f, 0.6f, 0.34f, 0.7f),
                ScrollbarGrabActive = new Vector4(1f, 0.41f, 0.53f, 0.9f),
                
                CheckMark = new Vector4(1f, 0.41f, 0.53f, 1f),
                
                SliderGrab = new Vector4(1f, 0.6f, 0.34f, 0.8f),
                SliderGrabActive = new Vector4(1f, 0.41f, 0.53f, 1f),
                
                Button = new Vector4(1f, 0.5f, 0.3f, 0.3f),
                ButtonHovered = new Vector4(1f, 0.6f, 0.34f, 0.5f),
                ButtonActive = new Vector4(1f, 0.41f, 0.53f, 0.7f),
                
                Header = new Vector4(0.9f, 0.45f, 0.35f, 0.8f),
                HeaderHovered = new Vector4(1f, 0.6f, 0.34f, 0.9f),
                HeaderActive = new Vector4(1f, 0.41f, 0.53f, 1f),
                
                Separator = new Vector4(1f, 0.6f, 0.5f, 0.3f),
                SeparatorHovered = new Vector4(1f, 0.7f, 0.6f, 0.5f),
                SeparatorActive = new Vector4(1f, 0.41f, 0.53f, 0.7f),
                
                ResizeGrip = new Vector4(1f, 0.6f, 0.34f, 0.25f),
                ResizeGripHovered = new Vector4(1f, 0.6f, 0.34f, 0.5f),
                ResizeGripActive = new Vector4(1f, 0.41f, 0.53f, 0.8f),
                
                Tab = new Vector4(1f, 0.5f, 0.3f, 0.3f),
                TabHovered = new Vector4(1f, 0.6f, 0.34f, 0.5f),
                TabActive = new Vector4(1f, 0.6f, 0.34f, 0.8f),
                TabUnfocused = new Vector4(0.5f, 0.3f, 0.2f, 0.5f),
                TabUnfocusedActive = new Vector4(0.7f, 0.4f, 0.3f, 0.6f),
                
                DockingPreview = new Vector4(1f, 0.41f, 0.53f, 0.3f),
                DockingEmptyBg = new Vector4(0.15f, 0.11f, 0.1f, 0.5f),
                
                TableHeaderBg = new Vector4(0.25f, 0.18f, 0.15f, 0.9f),
                TableBorderStrong = new Vector4(1f, 0.6f, 0.5f, 0.5f),
                TableBorderLight = new Vector4(1f, 0.5f, 0.3f, 0.25f),
                TableRowBg = new Vector4(0f, 0f, 0f, 0f),
                TableRowBgAlt = new Vector4(1f, 0.5f, 0.3f, 0.05f),
                
                DragDropTarget = new Vector4(1f, 0.41f, 0.53f, 0.5f),
                
                NavHighlight = new Vector4(1f, 0.41f, 0.53f, 1f),
                NavWindowingHighlight = new Vector4(1f, 0.9f, 0.85f, 0.7f),
                NavWindowingDimBg = new Vector4(0.8f, 0.5f, 0.4f, 0.2f),
                
                ModalWindowDimBg = new Vector4(0.15f, 0.11f, 0.1f, 0.75f),
                
                InspectorLabel = new Vector4(1f, 0.9f, 0.85f, 1f),
                InspectorValue = new Vector4(1f, 0.95f, 0.9f, 1f),
                InspectorWarning = new Vector4(1.0f, 0.85f, 0.4f, 1f),
                InspectorError = new Vector4(1.0f, 0.3f, 0.3f, 1f),
                InspectorSuccess = new Vector4(1.0f, 0.75f, 0.6f, 1f),
                InspectorInfo = new Vector4(1f, 0.7f, 0.5f, 1f),
                InspectorSection = new Vector4(0.9f, 0.45f, 0.35f, 1f),
                
                GradientStart = new Vector4(1f, 0.6f, 0.34f, 1f),   // #ff9a56
                GradientEnd = new Vector4(1f, 0.41f, 0.53f, 1f),    // #ff6a88
                AccentColor = new Vector4(1f, 0.41f, 0.53f, 1f),
                
                WindowRounding = 18.0f,
                ChildRounding = 14.0f,
                FrameRounding = 10.0f,
                PopupRounding = 14.0f,
                ScrollbarRounding = 12.0f,
                GrabRounding = 10.0f,
                TabRounding = 12.0f,
            };
        }
        
        /// <summary>
        /// Dark Unity - Classic Unity Dark theme
        /// </summary>
        public static EditorTheme DarkUnity()
        {
            return new EditorTheme
            {
                Name = "Dark Unity",
                Description = "Classic Unity dark editor theme",
                
                WindowBackground = new Vector4(0.15f, 0.15f, 0.15f, 1f),
                ChildBackground = new Vector4(0.18f, 0.18f, 0.18f, 1f),
                PopupBackground = new Vector4(0.14f, 0.14f, 0.14f, 1f),
                Border = new Vector4(0.25f, 0.25f, 0.25f, 1f),
                
                Text = new Vector4(0.86f, 0.86f, 0.86f, 1f),
                TextDisabled = new Vector4(0.5f, 0.5f, 0.5f, 1f),
                TextSelectedBg = new Vector4(0.26f, 0.59f, 0.98f, 0.35f),
                
                FrameBg = new Vector4(0.2f, 0.2f, 0.2f, 1f),
                FrameBgHovered = new Vector4(0.26f, 0.26f, 0.26f, 1f),
                FrameBgActive = new Vector4(0.32f, 0.32f, 0.32f, 1f),
                
                TitleBg = new Vector4(0.10f, 0.10f, 0.10f, 1f),
                TitleBgActive = new Vector4(0.14f, 0.14f, 0.14f, 1f),
                TitleBgCollapsed = new Vector4(0.10f, 0.10f, 0.10f, 1f),
                
                MenuBarBg = new Vector4(0.14f, 0.14f, 0.14f, 1f),
                
                ScrollbarBg = new Vector4(0.10f, 0.10f, 0.10f, 0.8f),
                ScrollbarGrab = new Vector4(0.35f, 0.35f, 0.35f, 1f),
                ScrollbarGrabHovered = new Vector4(0.45f, 0.45f, 0.45f, 1f),
                ScrollbarGrabActive = new Vector4(0.55f, 0.55f, 0.55f, 1f),
                
                CheckMark = new Vector4(0.26f, 0.59f, 0.98f, 1f),
                
                SliderGrab = new Vector4(0.24f, 0.52f, 0.88f, 1f),
                SliderGrabActive = new Vector4(0.26f, 0.59f, 0.98f, 1f),
                
                Button = new Vector4(0.26f, 0.59f, 0.98f, 0.4f),
                ButtonHovered = new Vector4(0.26f, 0.59f, 0.98f, 1f),
                ButtonActive = new Vector4(0.06f, 0.53f, 0.98f, 1f),
                
                Header = new Vector4(0.22f, 0.22f, 0.22f, 1f),
                HeaderHovered = new Vector4(0.26f, 0.59f, 0.98f, 0.8f),
                HeaderActive = new Vector4(0.26f, 0.59f, 0.98f, 1f),
                
                Separator = new Vector4(0.28f, 0.28f, 0.28f, 1f),
                SeparatorHovered = new Vector4(0.26f, 0.59f, 0.98f, 0.78f),
                SeparatorActive = new Vector4(0.26f, 0.59f, 0.98f, 1f),
                
                ResizeGrip = new Vector4(0.26f, 0.59f, 0.98f, 0.25f),
                ResizeGripHovered = new Vector4(0.26f, 0.59f, 0.98f, 0.67f),
                ResizeGripActive = new Vector4(0.26f, 0.59f, 0.98f, 0.95f),
                
                Tab = new Vector4(0.18f, 0.18f, 0.18f, 1f),
                TabHovered = new Vector4(0.26f, 0.59f, 0.98f, 0.8f),
                TabActive = new Vector4(0.20f, 0.20f, 0.20f, 1f),
                TabUnfocused = new Vector4(0.15f, 0.15f, 0.15f, 1f),
                TabUnfocusedActive = new Vector4(0.18f, 0.18f, 0.18f, 1f),
                
                DockingPreview = new Vector4(0.26f, 0.59f, 0.98f, 0.7f),
                DockingEmptyBg = new Vector4(0.20f, 0.20f, 0.20f, 1f),
                
                TableHeaderBg = new Vector4(0.19f, 0.19f, 0.19f, 1f),
                TableBorderStrong = new Vector4(0.31f, 0.31f, 0.31f, 1f),
                TableBorderLight = new Vector4(0.23f, 0.23f, 0.23f, 1f),
                TableRowBg = new Vector4(0f, 0f, 0f, 0f),
                TableRowBgAlt = new Vector4(1f, 1f, 1f, 0.06f),
                
                DragDropTarget = new Vector4(1f, 1f, 0f, 0.9f),
                
                NavHighlight = new Vector4(0.26f, 0.59f, 0.98f, 1f),
                NavWindowingHighlight = new Vector4(1f, 1f, 1f, 0.7f),
                NavWindowingDimBg = new Vector4(0.8f, 0.8f, 0.8f, 0.2f),
                
                ModalWindowDimBg = new Vector4(0.8f, 0.8f, 0.8f, 0.35f),
                
                InspectorLabel = new Vector4(0.75f, 0.75f, 0.75f, 1f),
                InspectorValue = new Vector4(0.86f, 0.86f, 0.86f, 1f),
                InspectorWarning = new Vector4(1.0f, 0.7f, 0.0f, 1f),
                InspectorError = new Vector4(1.0f, 0.2f, 0.2f, 1f),
                InspectorSuccess = new Vector4(0.2f, 1.0f, 0.2f, 1f),
                InspectorInfo = new Vector4(0.4f, 0.7f, 1.0f, 1f),
                InspectorSection = new Vector4(0.22f, 0.22f, 0.22f, 1f),
                
                GradientStart = new Vector4(0.26f, 0.59f, 0.98f, 1f),
                GradientEnd = new Vector4(0.06f, 0.53f, 0.98f, 1f),
                AccentColor = new Vector4(0.26f, 0.59f, 0.98f, 1f),
                
                WindowRounding = 0.0f,
                ChildRounding = 0.0f,
                FrameRounding = 3.0f,
                PopupRounding = 0.0f,
                ScrollbarRounding = 9.0f,
                GrabRounding = 3.0f,
                TabRounding = 4.0f,
            };
        }
        
        /// <summary>
        /// Retro Wave - 80s synthwave aesthetic
        /// </summary>
        public static EditorTheme RetroWave()
        {
            return new EditorTheme
            {
                Name = "Retro Wave",
                Description = "80s synthwave vibe with neon pink and cyan",
                
                WindowBackground = new Vector4(0.08f, 0.05f, 0.12f, 0.95f),
                ChildBackground = new Vector4(0.1f, 0.06f, 0.14f, 0.9f),
                PopupBackground = new Vector4(0.09f, 0.055f, 0.13f, 0.98f),
                Border = new Vector4(1f, 0f, 0.5f, 0.4f),
                
                Text = new Vector4(1f, 0.2f, 0.6f, 1f),
                TextDisabled = new Vector4(0.6f, 0.3f, 0.5f, 1f),
                TextSelectedBg = new Vector4(1f, 0f, 0.5f, 0.4f),
                
                FrameBg = new Vector4(0.3f, 0f, 0.4f, 0.3f),
                FrameBgHovered = new Vector4(0.4f, 0f, 0.5f, 0.4f),
                FrameBgActive = new Vector4(1f, 0f, 0.5f, 0.5f),
                
                TitleBg = new Vector4(0.2f, 0f, 0.35f, 0.9f),
                TitleBgActive = new Vector4(1f, 0f, 0.5f, 1f),
                TitleBgCollapsed = new Vector4(0.2f, 0f, 0.35f, 0.6f),
                
                MenuBarBg = new Vector4(0.1f, 0.06f, 0.14f, 0.98f),
                
                ScrollbarBg = new Vector4(0.08f, 0.05f, 0.12f, 0.7f),
                ScrollbarGrab = new Vector4(1f, 0f, 0.5f, 0.6f),
                ScrollbarGrabHovered = new Vector4(1f, 0f, 0.5f, 0.8f),
                ScrollbarGrabActive = new Vector4(0f, 1f, 1f, 1f),
                
                CheckMark = new Vector4(0f, 1f, 1f, 1f),
                
                SliderGrab = new Vector4(1f, 0f, 0.5f, 0.9f),
                SliderGrabActive = new Vector4(0f, 1f, 1f, 1f),
                
                Button = new Vector4(0.3f, 0f, 0.4f, 0.5f),
                ButtonHovered = new Vector4(1f, 0f, 0.5f, 0.7f),
                ButtonActive = new Vector4(0f, 1f, 1f, 0.8f),
                
                Header = new Vector4(0.25f, 0f, 0.4f, 0.85f),
                HeaderHovered = new Vector4(1f, 0f, 0.5f, 0.9f),
                HeaderActive = new Vector4(0f, 1f, 1f, 1f),
                
                Separator = new Vector4(1f, 0f, 0.5f, 0.4f),
                SeparatorHovered = new Vector4(1f, 0f, 0.5f, 0.6f),
                SeparatorActive = new Vector4(0f, 1f, 1f, 0.8f),
                
                ResizeGrip = new Vector4(1f, 0f, 0.5f, 0.3f),
                ResizeGripHovered = new Vector4(1f, 0f, 0.5f, 0.6f),
                ResizeGripActive = new Vector4(0f, 1f, 1f, 0.9f),
                
                Tab = new Vector4(0.3f, 0f, 0.4f, 0.5f),
                TabHovered = new Vector4(1f, 0f, 0.5f, 0.7f),
                TabActive = new Vector4(1f, 0f, 0.5f, 0.9f),
                TabUnfocused = new Vector4(0.15f, 0f, 0.2f, 0.6f),
                TabUnfocusedActive = new Vector4(0.25f, 0f, 0.35f, 0.7f),
                
                DockingPreview = new Vector4(0f, 1f, 1f, 0.4f),
                DockingEmptyBg = new Vector4(0.08f, 0.05f, 0.12f, 0.6f),
                
                TableHeaderBg = new Vector4(0.15f, 0.08f, 0.2f, 0.95f),
                TableBorderStrong = new Vector4(1f, 0f, 0.5f, 0.6f),
                TableBorderLight = new Vector4(1f, 0f, 0.5f, 0.3f),
                TableRowBg = new Vector4(0f, 0f, 0f, 0f),
                TableRowBgAlt = new Vector4(0.3f, 0f, 0.4f, 0.15f),
                
                DragDropTarget = new Vector4(0f, 1f, 1f, 0.6f),
                
                NavHighlight = new Vector4(0f, 1f, 1f, 1f),
                NavWindowingHighlight = new Vector4(1f, 0.2f, 0.6f, 0.8f),
                NavWindowingDimBg = new Vector4(0.3f, 0f, 0.4f, 0.3f),
                
                ModalWindowDimBg = new Vector4(0.08f, 0.05f, 0.12f, 0.8f),
                
                InspectorLabel = new Vector4(1f, 0.2f, 0.6f, 1f),
                InspectorValue = new Vector4(0f, 1f, 1f, 1f),
                InspectorWarning = new Vector4(1f, 0.8f, 0f, 1f),
                InspectorError = new Vector4(1f, 0f, 0.3f, 1f),
                InspectorSuccess = new Vector4(0f, 1f, 0.5f, 1f),
                InspectorInfo = new Vector4(0f, 0.8f, 1f, 1f),
                InspectorSection = new Vector4(1f, 0f, 0.5f, 1f),
                
                GradientStart = new Vector4(1f, 0f, 0.5f, 1f),
                GradientEnd = new Vector4(0f, 1f, 1f, 1f),
                AccentColor = new Vector4(0f, 1f, 1f, 1f),
                
                WindowRounding = 0.0f,
                ChildRounding = 0.0f,
                FrameRounding = 0.0f,
                PopupRounding = 0.0f,
                ScrollbarRounding = 0.0f,
                GrabRounding = 0.0f,
                TabRounding = 0.0f,
            };
        }
        
        /// <summary>
        /// Space Odyssey - Deep space with stars
        /// </summary>
        public static EditorTheme SpaceOdyssey()
        {
            return new EditorTheme
            {
                Name = "Space Odyssey",
                Description = "Deep space theme with cosmic purple and blue",
                
                WindowBackground = new Vector4(0.02f, 0.02f, 0.08f, 0.98f),
                ChildBackground = new Vector4(0.03f, 0.03f, 0.1f, 0.95f),
                PopupBackground = new Vector4(0.025f, 0.025f, 0.09f, 0.99f),
                Border = new Vector4(0.3f, 0.2f, 0.6f, 0.4f),
                
                Text = new Vector4(0.9f, 0.9f, 1f, 1f),
                TextDisabled = new Vector4(0.4f, 0.4f, 0.6f, 1f),
                TextSelectedBg = new Vector4(0.5f, 0.3f, 0.9f, 0.4f),
                
                FrameBg = new Vector4(0.1f, 0.1f, 0.3f, 0.3f),
                FrameBgHovered = new Vector4(0.15f, 0.15f, 0.4f, 0.4f),
                FrameBgActive = new Vector4(0.3f, 0.2f, 0.7f, 0.5f),
                
                TitleBg = new Vector4(0.1f, 0.05f, 0.3f, 0.9f),
                TitleBgActive = new Vector4(0.3f, 0.2f, 0.7f, 1f),
                TitleBgCollapsed = new Vector4(0.1f, 0.05f, 0.3f, 0.6f),
                
                MenuBarBg = new Vector4(0.03f, 0.03f, 0.1f, 0.98f),
                
                ScrollbarBg = new Vector4(0.02f, 0.02f, 0.08f, 0.7f),
                ScrollbarGrab = new Vector4(0.3f, 0.2f, 0.6f, 0.6f),
                ScrollbarGrabHovered = new Vector4(0.4f, 0.3f, 0.8f, 0.8f),
                ScrollbarGrabActive = new Vector4(0.6f, 0.5f, 1f, 1f),
                
                CheckMark = new Vector4(0.6f, 0.5f, 1f, 1f),
                
                SliderGrab = new Vector4(0.4f, 0.3f, 0.8f, 0.9f),
                SliderGrabActive = new Vector4(0.6f, 0.5f, 1f, 1f),
                
                Button = new Vector4(0.15f, 0.1f, 0.4f, 0.5f),
                ButtonHovered = new Vector4(0.3f, 0.2f, 0.7f, 0.7f),
                ButtonActive = new Vector4(0.5f, 0.4f, 0.9f, 0.9f),
                
                Header = new Vector4(0.15f, 0.1f, 0.4f, 0.85f),
                HeaderHovered = new Vector4(0.3f, 0.2f, 0.7f, 0.95f),
                HeaderActive = new Vector4(0.5f, 0.4f, 0.9f, 1f),
                
                Separator = new Vector4(0.3f, 0.2f, 0.6f, 0.4f),
                SeparatorHovered = new Vector4(0.4f, 0.3f, 0.8f, 0.6f),
                SeparatorActive = new Vector4(0.6f, 0.5f, 1f, 0.8f),
                
                ResizeGrip = new Vector4(0.3f, 0.2f, 0.6f, 0.3f),
                ResizeGripHovered = new Vector4(0.4f, 0.3f, 0.8f, 0.6f),
                ResizeGripActive = new Vector4(0.6f, 0.5f, 1f, 0.9f),
                
                Tab = new Vector4(0.15f, 0.1f, 0.4f, 0.5f),
                TabHovered = new Vector4(0.3f, 0.2f, 0.7f, 0.7f),
                TabActive = new Vector4(0.3f, 0.2f, 0.7f, 0.9f),
                TabUnfocused = new Vector4(0.08f, 0.05f, 0.2f, 0.6f),
                TabUnfocusedActive = new Vector4(0.12f, 0.08f, 0.3f, 0.7f),
                
                DockingPreview = new Vector4(0.5f, 0.4f, 0.9f, 0.4f),
                DockingEmptyBg = new Vector4(0.02f, 0.02f, 0.08f, 0.6f),
                
                TableHeaderBg = new Vector4(0.08f, 0.06f, 0.2f, 0.95f),
                TableBorderStrong = new Vector4(0.3f, 0.2f, 0.6f, 0.6f),
                TableBorderLight = new Vector4(0.2f, 0.15f, 0.4f, 0.3f),
                TableRowBg = new Vector4(0f, 0f, 0f, 0f),
                TableRowBgAlt = new Vector4(0.1f, 0.1f, 0.3f, 0.1f),
                
                DragDropTarget = new Vector4(0.6f, 0.5f, 1f, 0.6f),
                
                NavHighlight = new Vector4(0.6f, 0.5f, 1f, 1f),
                NavWindowingHighlight = new Vector4(0.9f, 0.9f, 1f, 0.8f),
                NavWindowingDimBg = new Vector4(0.1f, 0.1f, 0.3f, 0.3f),
                
                ModalWindowDimBg = new Vector4(0.02f, 0.02f, 0.08f, 0.85f),
                
                InspectorLabel = new Vector4(0.8f, 0.8f, 1f, 1f),
                InspectorValue = new Vector4(0.9f, 0.9f, 1f, 1f),
                InspectorWarning = new Vector4(1f, 0.9f, 0.3f, 1f),
                InspectorError = new Vector4(1f, 0.3f, 0.5f, 1f),
                InspectorSuccess = new Vector4(0.4f, 1f, 0.8f, 1f),
                InspectorInfo = new Vector4(0.5f, 0.7f, 1f, 1f),
                InspectorSection = new Vector4(0.4f, 0.3f, 0.8f, 1f),
                
                GradientStart = new Vector4(0.3f, 0.2f, 0.7f, 1f),
                GradientEnd = new Vector4(0.6f, 0.5f, 1f, 1f),
                AccentColor = new Vector4(0.6f, 0.5f, 1f, 1f),
                
                WindowRounding = 8.0f,
                ChildRounding = 6.0f,
                FrameRounding = 4.0f,
                PopupRounding = 6.0f,
                ScrollbarRounding = 8.0f,
                GrabRounding = 4.0f,
                TabRounding = 6.0f,
            };
        }
        
        /// <summary>
        /// Neon Nights - Vibrant neon colors
        /// </summary>
        public static EditorTheme NeonNights()
        {
            return new EditorTheme
            {
                Name = "Neon Nights",
                Description = "Vibrant neon green and magenta",
                
                WindowBackground = new Vector4(0.05f, 0.05f, 0.05f, 0.98f),
                ChildBackground = new Vector4(0.08f, 0.08f, 0.08f, 0.95f),
                PopupBackground = new Vector4(0.06f, 0.06f, 0.06f, 0.99f),
                Border = new Vector4(0f, 1f, 0.5f, 0.5f),
                
                Text = new Vector4(0f, 1f, 0.5f, 1f),
                TextDisabled = new Vector4(0f, 0.5f, 0.25f, 1f),
                TextSelectedBg = new Vector4(1f, 0f, 1f, 0.4f),
                
                FrameBg = new Vector4(0f, 0.3f, 0.15f, 0.3f),
                FrameBgHovered = new Vector4(0f, 0.5f, 0.25f, 0.4f),
                FrameBgActive = new Vector4(0f, 1f, 0.5f, 0.5f),
                
                TitleBg = new Vector4(0.1f, 0.1f, 0.1f, 0.95f),
                TitleBgActive = new Vector4(0f, 1f, 0.5f, 1f),
                TitleBgCollapsed = new Vector4(0.1f, 0.1f, 0.1f, 0.7f),
                
                MenuBarBg = new Vector4(0.08f, 0.08f, 0.08f, 0.98f),
                
                ScrollbarBg = new Vector4(0.05f, 0.05f, 0.05f, 0.7f),
                ScrollbarGrab = new Vector4(0f, 1f, 0.5f, 0.6f),
                ScrollbarGrabHovered = new Vector4(0f, 1f, 0.5f, 0.8f),
                ScrollbarGrabActive = new Vector4(1f, 0f, 1f, 1f),
                
                CheckMark = new Vector4(1f, 0f, 1f, 1f),
                
                SliderGrab = new Vector4(0f, 1f, 0.5f, 0.9f),
                SliderGrabActive = new Vector4(1f, 0f, 1f, 1f),
                
                Button = new Vector4(0f, 0.3f, 0.15f, 0.5f),
                ButtonHovered = new Vector4(0f, 1f, 0.5f, 0.7f),
                ButtonActive = new Vector4(1f, 0f, 1f, 0.9f),
                
                Header = new Vector4(0f, 0.4f, 0.2f, 0.85f),
                HeaderHovered = new Vector4(0f, 1f, 0.5f, 0.95f),
                HeaderActive = new Vector4(1f, 0f, 1f, 1f),
                
                Separator = new Vector4(0f, 1f, 0.5f, 0.5f),
                SeparatorHovered = new Vector4(0f, 1f, 0.5f, 0.7f),
                SeparatorActive = new Vector4(1f, 0f, 1f, 0.9f),
                
                ResizeGrip = new Vector4(0f, 1f, 0.5f, 0.3f),
                ResizeGripHovered = new Vector4(0f, 1f, 0.5f, 0.6f),
                ResizeGripActive = new Vector4(1f, 0f, 1f, 0.9f),
                
                Tab = new Vector4(0f, 0.3f, 0.15f, 0.5f),
                TabHovered = new Vector4(0f, 1f, 0.5f, 0.7f),
                TabActive = new Vector4(0f, 1f, 0.5f, 0.9f),
                TabUnfocused = new Vector4(0f, 0.15f, 0.08f, 0.6f),
                TabUnfocusedActive = new Vector4(0f, 0.25f, 0.12f, 0.7f),
                
                DockingPreview = new Vector4(1f, 0f, 1f, 0.4f),
                DockingEmptyBg = new Vector4(0.05f, 0.05f, 0.05f, 0.6f),
                
                TableHeaderBg = new Vector4(0.1f, 0.1f, 0.1f, 0.95f),
                TableBorderStrong = new Vector4(0f, 1f, 0.5f, 0.6f),
                TableBorderLight = new Vector4(0f, 1f, 0.5f, 0.3f),
                TableRowBg = new Vector4(0f, 0f, 0f, 0f),
                TableRowBgAlt = new Vector4(0f, 0.3f, 0.15f, 0.15f),
                
                DragDropTarget = new Vector4(1f, 0f, 1f, 0.6f),
                
                NavHighlight = new Vector4(1f, 0f, 1f, 1f),
                NavWindowingHighlight = new Vector4(0f, 1f, 0.5f, 0.8f),
                NavWindowingDimBg = new Vector4(0f, 0.3f, 0.15f, 0.3f),
                
                ModalWindowDimBg = new Vector4(0.05f, 0.05f, 0.05f, 0.85f),
                
                InspectorLabel = new Vector4(0f, 1f, 0.5f, 1f),
                InspectorValue = new Vector4(1f, 0f, 1f, 1f),
                InspectorWarning = new Vector4(1f, 1f, 0f, 1f),
                InspectorError = new Vector4(1f, 0f, 0f, 1f),
                InspectorSuccess = new Vector4(0f, 1f, 0f, 1f),
                InspectorInfo = new Vector4(0f, 1f, 1f, 1f),
                InspectorSection = new Vector4(0f, 0.8f, 0.4f, 1f),
                
                GradientStart = new Vector4(0f, 1f, 0.5f, 1f),
                GradientEnd = new Vector4(1f, 0f, 1f, 1f),
                AccentColor = new Vector4(1f, 0f, 1f, 1f),
                
                WindowRounding = 2.0f,
                ChildRounding = 2.0f,
                FrameRounding = 2.0f,
                PopupRounding = 2.0f,
                ScrollbarRounding = 4.0f,
                GrabRounding = 2.0f,
                TabRounding = 2.0f,
            };
        }
        
        /// <summary>
        /// Forest Canopy - Green monochromatic
        /// </summary>
        public static EditorTheme ForestCanopy()
        {
            return new EditorTheme
            {
                Name = "Forest Canopy",
                Description = "Deep forest greens with nature tones",
                
                WindowBackground = new Vector4(0.08f, 0.12f, 0.08f, 0.95f),
                ChildBackground = new Vector4(0.1f, 0.15f, 0.1f, 0.9f),
                PopupBackground = new Vector4(0.09f, 0.13f, 0.09f, 0.98f),
                Border = new Vector4(0.3f, 0.6f, 0.3f, 0.4f),
                
                Text = new Vector4(0.8f, 1f, 0.8f, 1f),
                TextDisabled = new Vector4(0.4f, 0.6f, 0.4f, 1f),
                TextSelectedBg = new Vector4(0.3f, 0.7f, 0.3f, 0.4f),
                
                FrameBg = new Vector4(0.15f, 0.3f, 0.15f, 0.3f),
                FrameBgHovered = new Vector4(0.2f, 0.4f, 0.2f, 0.4f),
                FrameBgActive = new Vector4(0.3f, 0.6f, 0.3f, 0.5f),
                
                TitleBg = new Vector4(0.1f, 0.25f, 0.1f, 0.9f),
                TitleBgActive = new Vector4(0.2f, 0.6f, 0.2f, 1f),
                TitleBgCollapsed = new Vector4(0.1f, 0.25f, 0.1f, 0.6f),
                
                MenuBarBg = new Vector4(0.1f, 0.15f, 0.1f, 0.98f),
                
                ScrollbarBg = new Vector4(0.08f, 0.12f, 0.08f, 0.7f),
                ScrollbarGrab = new Vector4(0.3f, 0.6f, 0.3f, 0.6f),
                ScrollbarGrabHovered = new Vector4(0.4f, 0.8f, 0.4f, 0.8f),
                ScrollbarGrabActive = new Vector4(0.5f, 1f, 0.5f, 1f),
                
                CheckMark = new Vector4(0.5f, 1f, 0.5f, 1f),
                
                SliderGrab = new Vector4(0.3f, 0.7f, 0.3f, 0.9f),
                SliderGrabActive = new Vector4(0.5f, 1f, 0.5f, 1f),
                
                Button = new Vector4(0.15f, 0.35f, 0.15f, 0.5f),
                ButtonHovered = new Vector4(0.25f, 0.6f, 0.25f, 0.7f),
                ButtonActive = new Vector4(0.35f, 0.8f, 0.35f, 0.9f),
                
                Header = new Vector4(0.15f, 0.4f, 0.15f, 0.85f),
                HeaderHovered = new Vector4(0.25f, 0.7f, 0.25f, 0.95f),
                HeaderActive = new Vector4(0.35f, 0.9f, 0.35f, 1f),
                
                Separator = new Vector4(0.3f, 0.6f, 0.3f, 0.4f),
                SeparatorHovered = new Vector4(0.4f, 0.8f, 0.4f, 0.6f),
                SeparatorActive = new Vector4(0.5f, 1f, 0.5f, 0.8f),
                
                ResizeGrip = new Vector4(0.3f, 0.6f, 0.3f, 0.3f),
                ResizeGripHovered = new Vector4(0.4f, 0.8f, 0.4f, 0.6f),
                ResizeGripActive = new Vector4(0.5f, 1f, 0.5f, 0.9f),
                
                Tab = new Vector4(0.15f, 0.35f, 0.15f, 0.5f),
                TabHovered = new Vector4(0.25f, 0.6f, 0.25f, 0.7f),
                TabActive = new Vector4(0.25f, 0.6f, 0.25f, 0.9f),
                TabUnfocused = new Vector4(0.1f, 0.2f, 0.1f, 0.6f),
                TabUnfocusedActive = new Vector4(0.12f, 0.3f, 0.12f, 0.7f),
                
                DockingPreview = new Vector4(0.4f, 0.8f, 0.4f, 0.4f),
                DockingEmptyBg = new Vector4(0.08f, 0.12f, 0.08f, 0.6f),
                
                TableHeaderBg = new Vector4(0.12f, 0.25f, 0.12f, 0.95f),
                TableBorderStrong = new Vector4(0.3f, 0.6f, 0.3f, 0.6f),
                TableBorderLight = new Vector4(0.2f, 0.4f, 0.2f, 0.3f),
                TableRowBg = new Vector4(0f, 0f, 0f, 0f),
                TableRowBgAlt = new Vector4(0.15f, 0.3f, 0.15f, 0.1f),
                
                DragDropTarget = new Vector4(0.5f, 1f, 0.5f, 0.6f),
                
                NavHighlight = new Vector4(0.5f, 1f, 0.5f, 1f),
                NavWindowingHighlight = new Vector4(0.8f, 1f, 0.8f, 0.8f),
                NavWindowingDimBg = new Vector4(0.15f, 0.3f, 0.15f, 0.3f),
                
                ModalWindowDimBg = new Vector4(0.08f, 0.12f, 0.08f, 0.85f),
                
                InspectorLabel = new Vector4(0.7f, 0.9f, 0.7f, 1f),
                InspectorValue = new Vector4(0.8f, 1f, 0.8f, 1f),
                InspectorWarning = new Vector4(1f, 0.9f, 0.3f, 1f),
                InspectorError = new Vector4(1f, 0.4f, 0.3f, 1f),
                InspectorSuccess = new Vector4(0.5f, 1f, 0.5f, 1f),
                InspectorInfo = new Vector4(0.5f, 0.9f, 1f, 1f),
                InspectorSection = new Vector4(0.3f, 0.7f, 0.3f, 1f),
                
                GradientStart = new Vector4(0.2f, 0.5f, 0.2f, 1f),
                GradientEnd = new Vector4(0.4f, 1f, 0.4f, 1f),
                AccentColor = new Vector4(0.5f, 1f, 0.5f, 1f),
                
                WindowRounding = 12.0f,
                ChildRounding = 10.0f,
                FrameRounding = 6.0f,
                PopupRounding = 10.0f,
                ScrollbarRounding = 12.0f,
                GrabRounding = 6.0f,
                TabRounding = 8.0f,
            };
        }
        
        /// <summary>
        /// Lavender Fields - Purple monochromatic
        /// </summary>
        public static EditorTheme LavenderFields()
        {
            return new EditorTheme
            {
                Name = "Lavender Fields",
                Description = "Soft lavender and purple tones",
                
                WindowBackground = new Vector4(0.12f, 0.1f, 0.15f, 0.95f),
                ChildBackground = new Vector4(0.15f, 0.12f, 0.18f, 0.9f),
                PopupBackground = new Vector4(0.13f, 0.11f, 0.16f, 0.98f),
                Border = new Vector4(0.6f, 0.5f, 0.8f, 0.4f),
                
                Text = new Vector4(0.95f, 0.9f, 1f, 1f),
                TextDisabled = new Vector4(0.6f, 0.5f, 0.7f, 1f),
                TextSelectedBg = new Vector4(0.7f, 0.5f, 0.9f, 0.4f),
                
                FrameBg = new Vector4(0.3f, 0.2f, 0.4f, 0.3f),
                FrameBgHovered = new Vector4(0.4f, 0.3f, 0.5f, 0.4f),
                FrameBgActive = new Vector4(0.6f, 0.5f, 0.8f, 0.5f),
                
                TitleBg = new Vector4(0.25f, 0.2f, 0.35f, 0.9f),
                TitleBgActive = new Vector4(0.6f, 0.5f, 0.8f, 1f),
                TitleBgCollapsed = new Vector4(0.25f, 0.2f, 0.35f, 0.6f),
                
                MenuBarBg = new Vector4(0.15f, 0.12f, 0.18f, 0.98f),
                
                ScrollbarBg = new Vector4(0.12f, 0.1f, 0.15f, 0.7f),
                ScrollbarGrab = new Vector4(0.6f, 0.5f, 0.8f, 0.6f),
                ScrollbarGrabHovered = new Vector4(0.7f, 0.6f, 0.9f, 0.8f),
                ScrollbarGrabActive = new Vector4(0.8f, 0.7f, 1f, 1f),
                
                CheckMark = new Vector4(0.8f, 0.7f, 1f, 1f),
                
                SliderGrab = new Vector4(0.6f, 0.5f, 0.8f, 0.9f),
                SliderGrabActive = new Vector4(0.8f, 0.7f, 1f, 1f),
                
                Button = new Vector4(0.3f, 0.25f, 0.4f, 0.5f),
                ButtonHovered = new Vector4(0.6f, 0.5f, 0.8f, 0.7f),
                ButtonActive = new Vector4(0.7f, 0.6f, 0.9f, 0.9f),
                
                Header = new Vector4(0.35f, 0.3f, 0.5f, 0.85f),
                HeaderHovered = new Vector4(0.6f, 0.5f, 0.8f, 0.95f),
                HeaderActive = new Vector4(0.7f, 0.6f, 0.9f, 1f),
                
                Separator = new Vector4(0.6f, 0.5f, 0.8f, 0.4f),
                SeparatorHovered = new Vector4(0.7f, 0.6f, 0.9f, 0.6f),
                SeparatorActive = new Vector4(0.8f, 0.7f, 1f, 0.8f),
                
                ResizeGrip = new Vector4(0.6f, 0.5f, 0.8f, 0.3f),
                ResizeGripHovered = new Vector4(0.7f, 0.6f, 0.9f, 0.6f),
                ResizeGripActive = new Vector4(0.8f, 0.7f, 1f, 0.9f),
                
                Tab = new Vector4(0.3f, 0.25f, 0.4f, 0.5f),
                TabHovered = new Vector4(0.6f, 0.5f, 0.8f, 0.7f),
                TabActive = new Vector4(0.6f, 0.5f, 0.8f, 0.9f),
                TabUnfocused = new Vector4(0.18f, 0.15f, 0.25f, 0.6f),
                TabUnfocusedActive = new Vector4(0.25f, 0.2f, 0.35f, 0.7f),
                
                DockingPreview = new Vector4(0.7f, 0.6f, 0.9f, 0.4f),
                DockingEmptyBg = new Vector4(0.12f, 0.1f, 0.15f, 0.6f),
                
                TableHeaderBg = new Vector4(0.2f, 0.16f, 0.28f, 0.95f),
                TableBorderStrong = new Vector4(0.6f, 0.5f, 0.8f, 0.6f),
                TableBorderLight = new Vector4(0.4f, 0.3f, 0.5f, 0.3f),
                TableRowBg = new Vector4(0f, 0f, 0f, 0f),
                TableRowBgAlt = new Vector4(0.3f, 0.2f, 0.4f, 0.1f),
                
                DragDropTarget = new Vector4(0.8f, 0.7f, 1f, 0.6f),
                
                NavHighlight = new Vector4(0.8f, 0.7f, 1f, 1f),
                NavWindowingHighlight = new Vector4(0.95f, 0.9f, 1f, 0.8f),
                NavWindowingDimBg = new Vector4(0.3f, 0.2f, 0.4f, 0.3f),
                
                ModalWindowDimBg = new Vector4(0.12f, 0.1f, 0.15f, 0.85f),
                
                InspectorLabel = new Vector4(0.9f, 0.85f, 1f, 1f),
                InspectorValue = new Vector4(0.95f, 0.9f, 1f, 1f),
                InspectorWarning = new Vector4(1f, 0.9f, 0.5f, 1f),
                InspectorError = new Vector4(1f, 0.5f, 0.6f, 1f),
                InspectorSuccess = new Vector4(0.6f, 1f, 0.8f, 1f),
                InspectorInfo = new Vector4(0.6f, 0.8f, 1f, 1f),
                InspectorSection = new Vector4(0.6f, 0.5f, 0.8f, 1f),
                
                GradientStart = new Vector4(0.6f, 0.5f, 0.8f, 1f),
                GradientEnd = new Vector4(0.8f, 0.7f, 1f, 1f),
                AccentColor = new Vector4(0.8f, 0.7f, 1f, 1f),
                
                WindowRounding = 16.0f,
                ChildRounding = 14.0f,
                FrameRounding = 8.0f,
                PopupRounding = 14.0f,
                ScrollbarRounding = 16.0f,
                GrabRounding = 8.0f,
                TabRounding = 10.0f,
            };
        }
        
        /// <summary>
        /// Autumn Leaves - Orange and brown tones
        /// </summary>
        public static EditorTheme AutumnLeaves()
        {
            return new EditorTheme
            {
                Name = "Autumn Leaves",
                Description = "Warm autumn colors with orange and brown",
                
                WindowBackground = new Vector4(0.12f, 0.08f, 0.05f, 0.95f),
                ChildBackground = new Vector4(0.15f, 0.1f, 0.06f, 0.9f),
                PopupBackground = new Vector4(0.13f, 0.09f, 0.055f, 0.98f),
                Border = new Vector4(0.9f, 0.5f, 0.2f, 0.4f),
                
                Text = new Vector4(1f, 0.9f, 0.8f, 1f),
                TextDisabled = new Vector4(0.6f, 0.5f, 0.4f, 1f),
                TextSelectedBg = new Vector4(0.9f, 0.5f, 0.2f, 0.4f),
                
                FrameBg = new Vector4(0.3f, 0.2f, 0.1f, 0.3f),
                FrameBgHovered = new Vector4(0.5f, 0.3f, 0.15f, 0.4f),
                FrameBgActive = new Vector4(0.8f, 0.5f, 0.2f, 0.5f),
                
                TitleBg = new Vector4(0.25f, 0.15f, 0.08f, 0.9f),
                TitleBgActive = new Vector4(0.9f, 0.5f, 0.2f, 1f),
                TitleBgCollapsed = new Vector4(0.25f, 0.15f, 0.08f, 0.6f),
                
                MenuBarBg = new Vector4(0.15f, 0.1f, 0.06f, 0.98f),
                
                ScrollbarBg = new Vector4(0.12f, 0.08f, 0.05f, 0.7f),
                ScrollbarGrab = new Vector4(0.8f, 0.5f, 0.2f, 0.6f),
                ScrollbarGrabHovered = new Vector4(0.9f, 0.6f, 0.3f, 0.8f),
                ScrollbarGrabActive = new Vector4(1f, 0.7f, 0.4f, 1f),
                
                CheckMark = new Vector4(1f, 0.7f, 0.4f, 1f),
                
                SliderGrab = new Vector4(0.9f, 0.5f, 0.2f, 0.9f),
                SliderGrabActive = new Vector4(1f, 0.7f, 0.4f, 1f),
                
                Button = new Vector4(0.4f, 0.25f, 0.12f, 0.5f),
                ButtonHovered = new Vector4(0.8f, 0.5f, 0.2f, 0.7f),
                ButtonActive = new Vector4(1f, 0.6f, 0.3f, 0.9f),
                
                Header = new Vector4(0.5f, 0.3f, 0.15f, 0.85f),
                HeaderHovered = new Vector4(0.8f, 0.5f, 0.2f, 0.95f),
                HeaderActive = new Vector4(1f, 0.6f, 0.3f, 1f),
                
                Separator = new Vector4(0.8f, 0.5f, 0.2f, 0.4f),
                SeparatorHovered = new Vector4(0.9f, 0.6f, 0.3f, 0.6f),
                SeparatorActive = new Vector4(1f, 0.7f, 0.4f, 0.8f),
                
                ResizeGrip = new Vector4(0.8f, 0.5f, 0.2f, 0.3f),
                ResizeGripHovered = new Vector4(0.9f, 0.6f, 0.3f, 0.6f),
                ResizeGripActive = new Vector4(1f, 0.7f, 0.4f, 0.9f),
                
                Tab = new Vector4(0.4f, 0.25f, 0.12f, 0.5f),
                TabHovered = new Vector4(0.8f, 0.5f, 0.2f, 0.7f),
                TabActive = new Vector4(0.8f, 0.5f, 0.2f, 0.9f),
                TabUnfocused = new Vector4(0.2f, 0.12f, 0.06f, 0.6f),
                TabUnfocusedActive = new Vector4(0.3f, 0.18f, 0.09f, 0.7f),
                
                DockingPreview = new Vector4(1f, 0.6f, 0.3f, 0.4f),
                DockingEmptyBg = new Vector4(0.12f, 0.08f, 0.05f, 0.6f),
                
                TableHeaderBg = new Vector4(0.2f, 0.13f, 0.07f, 0.95f),
                TableBorderStrong = new Vector4(0.8f, 0.5f, 0.2f, 0.6f),
                TableBorderLight = new Vector4(0.5f, 0.3f, 0.15f, 0.3f),
                TableRowBg = new Vector4(0f, 0f, 0f, 0f),
                TableRowBgAlt = new Vector4(0.3f, 0.2f, 0.1f, 0.1f),
                
                DragDropTarget = new Vector4(1f, 0.7f, 0.4f, 0.6f),
                
                NavHighlight = new Vector4(1f, 0.7f, 0.4f, 1f),
                NavWindowingHighlight = new Vector4(1f, 0.9f, 0.8f, 0.8f),
                NavWindowingDimBg = new Vector4(0.3f, 0.2f, 0.1f, 0.3f),
                
                ModalWindowDimBg = new Vector4(0.12f, 0.08f, 0.05f, 0.85f),
                
                InspectorLabel = new Vector4(1f, 0.85f, 0.7f, 1f),
                InspectorValue = new Vector4(1f, 0.9f, 0.8f, 1f),
                InspectorWarning = new Vector4(1f, 0.9f, 0.3f, 1f),
                InspectorError = new Vector4(1f, 0.3f, 0.2f, 1f),
                InspectorSuccess = new Vector4(0.6f, 1f, 0.4f, 1f),
                InspectorInfo = new Vector4(0.5f, 0.8f, 1f, 1f),
                InspectorSection = new Vector4(0.9f, 0.5f, 0.2f, 1f),
                
                GradientStart = new Vector4(0.9f, 0.5f, 0.2f, 1f),
                GradientEnd = new Vector4(1f, 0.7f, 0.4f, 1f),
                AccentColor = new Vector4(1f, 0.7f, 0.4f, 1f),
                
                WindowRounding = 10.0f,
                ChildRounding = 8.0f,
                FrameRounding = 5.0f,
                PopupRounding = 8.0f,
                ScrollbarRounding = 10.0f,
                GrabRounding = 5.0f,
                TabRounding = 6.0f,
            };
        }
        
        /// <summary>
        /// Fire and Ice - Red and Cyan complementary
        /// </summary>
        public static EditorTheme FireAndIce()
        {
            return new EditorTheme
            {
                Name = "Fire and Ice",
                Description = "Dramatic contrast with fire red and ice cyan",
                
                WindowBackground = new Vector4(0.08f, 0.08f, 0.12f, 0.95f),
                ChildBackground = new Vector4(0.1f, 0.1f, 0.15f, 0.9f),
                PopupBackground = new Vector4(0.09f, 0.09f, 0.13f, 0.98f),
                Border = new Vector4(1f, 0.27f, 0.27f, 0.5f),
                
                Text = new Vector4(0.27f, 1f, 1f, 1f),
                TextDisabled = new Vector4(0.5f, 0.6f, 0.7f, 1f),
                TextSelectedBg = new Vector4(1f, 0.27f, 0.27f, 0.4f),
                
                FrameBg = new Vector4(0.15f, 0.3f, 0.35f, 0.3f),
                FrameBgHovered = new Vector4(0.2f, 0.5f, 0.6f, 0.4f),
                FrameBgActive = new Vector4(0.27f, 1f, 1f, 0.5f),
                
                TitleBg = new Vector4(0.3f, 0.08f, 0.08f, 0.9f),
                TitleBgActive = new Vector4(1f, 0.27f, 0.27f, 1f),
                TitleBgCollapsed = new Vector4(0.3f, 0.08f, 0.08f, 0.6f),
                
                MenuBarBg = new Vector4(0.1f, 0.1f, 0.15f, 0.98f),
                
                ScrollbarBg = new Vector4(0.08f, 0.08f, 0.12f, 0.7f),
                ScrollbarGrab = new Vector4(1f, 0.27f, 0.27f, 0.6f),
                ScrollbarGrabHovered = new Vector4(1f, 0.4f, 0.4f, 0.8f),
                ScrollbarGrabActive = new Vector4(0.27f, 1f, 1f, 1f),
                
                CheckMark = new Vector4(0.27f, 1f, 1f, 1f),
                
                SliderGrab = new Vector4(1f, 0.27f, 0.27f, 0.9f),
                SliderGrabActive = new Vector4(0.27f, 1f, 1f, 1f),
                
                Button = new Vector4(0.4f, 0.1f, 0.1f, 0.5f),
                ButtonHovered = new Vector4(1f, 0.27f, 0.27f, 0.7f),
                ButtonActive = new Vector4(0.27f, 1f, 1f, 0.9f),
                
                Header = new Vector4(0.35f, 0.1f, 0.1f, 0.85f),
                HeaderHovered = new Vector4(1f, 0.27f, 0.27f, 0.95f),
                HeaderActive = new Vector4(0.27f, 1f, 1f, 1f),
                
                Separator = new Vector4(1f, 0.27f, 0.27f, 0.5f),
                SeparatorHovered = new Vector4(1f, 0.4f, 0.4f, 0.7f),
                SeparatorActive = new Vector4(0.27f, 1f, 1f, 0.9f),
                
                ResizeGrip = new Vector4(1f, 0.27f, 0.27f, 0.3f),
                ResizeGripHovered = new Vector4(1f, 0.4f, 0.4f, 0.6f),
                ResizeGripActive = new Vector4(0.27f, 1f, 1f, 0.9f),
                
                Tab = new Vector4(0.4f, 0.1f, 0.1f, 0.5f),
                TabHovered = new Vector4(1f, 0.27f, 0.27f, 0.7f),
                TabActive = new Vector4(1f, 0.27f, 0.27f, 0.9f),
                TabUnfocused = new Vector4(0.2f, 0.05f, 0.05f, 0.6f),
                TabUnfocusedActive = new Vector4(0.3f, 0.08f, 0.08f, 0.7f),
                
                DockingPreview = new Vector4(0.27f, 1f, 1f, 0.4f),
                DockingEmptyBg = new Vector4(0.08f, 0.08f, 0.12f, 0.6f),
                
                TableHeaderBg = new Vector4(0.15f, 0.08f, 0.08f, 0.95f),
                TableBorderStrong = new Vector4(1f, 0.27f, 0.27f, 0.6f),
                TableBorderLight = new Vector4(1f, 0.27f, 0.27f, 0.3f),
                TableRowBg = new Vector4(0f, 0f, 0f, 0f),
                TableRowBgAlt = new Vector4(0.15f, 0.3f, 0.35f, 0.1f),
                
                DragDropTarget = new Vector4(0.27f, 1f, 1f, 0.6f),
                
                NavHighlight = new Vector4(0.27f, 1f, 1f, 1f),
                NavWindowingHighlight = new Vector4(1f, 0.5f, 0.5f, 0.8f),
                NavWindowingDimBg = new Vector4(0.15f, 0.3f, 0.35f, 0.3f),
                
                ModalWindowDimBg = new Vector4(0.08f, 0.08f, 0.12f, 0.85f),
                
                InspectorLabel = new Vector4(0.5f, 1f, 1f, 1f),
                InspectorValue = new Vector4(0.27f, 1f, 1f, 1f),
                InspectorWarning = new Vector4(1f, 0.9f, 0.3f, 1f),
                InspectorError = new Vector4(1f, 0.27f, 0.27f, 1f),
                InspectorSuccess = new Vector4(0.3f, 1f, 0.7f, 1f),
                InspectorInfo = new Vector4(0.27f, 1f, 1f, 1f),
                InspectorSection = new Vector4(1f, 0.4f, 0.4f, 1f),
                
                GradientStart = new Vector4(1f, 0.27f, 0.27f, 1f),
                GradientEnd = new Vector4(0.27f, 1f, 1f, 1f),
                AccentColor = new Vector4(0.27f, 1f, 1f, 1f),
                
                WindowRounding = 6.0f,
                ChildRounding = 5.0f,
                FrameRounding = 3.0f,
                PopupRounding = 5.0f,
                ScrollbarRounding = 6.0f,
                GrabRounding = 3.0f,
                TabRounding = 4.0f,
            };
        }
        
        /// <summary>
        /// Day and Night - Yellow and Blue bi-tone
        /// </summary>
        public static EditorTheme DayAndNight()
        {
            return new EditorTheme
            {
                Name = "Day and Night",
                Description = "Bi-tone theme with golden day and deep night blue",
                
                WindowBackground = new Vector4(0.06f, 0.08f, 0.15f, 0.95f),
                ChildBackground = new Vector4(0.08f, 0.1f, 0.18f, 0.9f),
                PopupBackground = new Vector4(0.07f, 0.09f, 0.16f, 0.98f),
                Border = new Vector4(1f, 0.84f, 0f, 0.4f),
                
                Text = new Vector4(1f, 0.95f, 0.7f, 1f),
                TextDisabled = new Vector4(0.6f, 0.6f, 0.7f, 1f),
                TextSelectedBg = new Vector4(1f, 0.84f, 0f, 0.4f),
                
                FrameBg = new Vector4(0.12f, 0.16f, 0.35f, 0.3f),
                FrameBgHovered = new Vector4(0.15f, 0.25f, 0.5f, 0.4f),
                FrameBgActive = new Vector4(1f, 0.84f, 0f, 0.5f),
                
                TitleBg = new Vector4(0.08f, 0.12f, 0.3f, 0.9f),
                TitleBgActive = new Vector4(1f, 0.84f, 0f, 1f),
                TitleBgCollapsed = new Vector4(0.08f, 0.12f, 0.3f, 0.6f),
                
                MenuBarBg = new Vector4(0.08f, 0.1f, 0.18f, 0.98f),
                
                ScrollbarBg = new Vector4(0.06f, 0.08f, 0.15f, 0.7f),
                ScrollbarGrab = new Vector4(1f, 0.84f, 0f, 0.6f),
                ScrollbarGrabHovered = new Vector4(1f, 0.9f, 0.3f, 0.8f),
                ScrollbarGrabActive = new Vector4(1f, 1f, 0.5f, 1f),
                
                CheckMark = new Vector4(1f, 1f, 0.5f, 1f),
                
                SliderGrab = new Vector4(1f, 0.84f, 0f, 0.9f),
                SliderGrabActive = new Vector4(1f, 1f, 0.5f, 1f),
                
                Button = new Vector4(0.12f, 0.16f, 0.35f, 0.5f),
                ButtonHovered = new Vector4(1f, 0.84f, 0f, 0.7f),
                ButtonActive = new Vector4(1f, 1f, 0.5f, 0.9f),
                
                Header = new Vector4(0.15f, 0.2f, 0.4f, 0.85f),
                HeaderHovered = new Vector4(1f, 0.84f, 0f, 0.95f),
                HeaderActive = new Vector4(1f, 1f, 0.5f, 1f),
                
                Separator = new Vector4(1f, 0.84f, 0f, 0.4f),
                SeparatorHovered = new Vector4(1f, 0.9f, 0.3f, 0.6f),
                SeparatorActive = new Vector4(1f, 1f, 0.5f, 0.8f),
                
                ResizeGrip = new Vector4(1f, 0.84f, 0f, 0.3f),
                ResizeGripHovered = new Vector4(1f, 0.9f, 0.3f, 0.6f),
                ResizeGripActive = new Vector4(1f, 1f, 0.5f, 0.9f),
                
                Tab = new Vector4(0.12f, 0.16f, 0.35f, 0.5f),
                TabHovered = new Vector4(1f, 0.84f, 0f, 0.7f),
                TabActive = new Vector4(1f, 0.84f, 0f, 0.9f),
                TabUnfocused = new Vector4(0.06f, 0.08f, 0.18f, 0.6f),
                TabUnfocusedActive = new Vector4(0.1f, 0.12f, 0.25f, 0.7f),
                
                DockingPreview = new Vector4(1f, 1f, 0.5f, 0.4f),
                DockingEmptyBg = new Vector4(0.06f, 0.08f, 0.15f, 0.6f),
                
                TableHeaderBg = new Vector4(0.1f, 0.13f, 0.25f, 0.95f),
                TableBorderStrong = new Vector4(1f, 0.84f, 0f, 0.6f),
                TableBorderLight = new Vector4(1f, 0.84f, 0f, 0.3f),
                TableRowBg = new Vector4(0f, 0f, 0f, 0f),
                TableRowBgAlt = new Vector4(0.12f, 0.16f, 0.35f, 0.1f),
                
                DragDropTarget = new Vector4(1f, 1f, 0.5f, 0.6f),
                
                NavHighlight = new Vector4(1f, 1f, 0.5f, 1f),
                NavWindowingHighlight = new Vector4(1f, 0.95f, 0.7f, 0.8f),
                NavWindowingDimBg = new Vector4(0.12f, 0.16f, 0.35f, 0.3f),
                
                ModalWindowDimBg = new Vector4(0.06f, 0.08f, 0.15f, 0.85f),
                
                InspectorLabel = new Vector4(1f, 0.9f, 0.6f, 1f),
                InspectorValue = new Vector4(1f, 0.95f, 0.7f, 1f),
                InspectorWarning = new Vector4(1f, 0.7f, 0f, 1f),
                InspectorError = new Vector4(1f, 0.3f, 0.3f, 1f),
                InspectorSuccess = new Vector4(0.5f, 1f, 0.5f, 1f),
                InspectorInfo = new Vector4(0.5f, 0.8f, 1f, 1f),
                InspectorSection = new Vector4(1f, 0.84f, 0f, 1f),
                
                GradientStart = new Vector4(1f, 0.84f, 0f, 1f),
                GradientEnd = new Vector4(0.12f, 0.25f, 0.56f, 1f),
                AccentColor = new Vector4(1f, 0.84f, 0f, 1f),
                
                WindowRounding = 14.0f,
                ChildRounding = 12.0f,
                FrameRounding = 7.0f,
                PopupRounding = 12.0f,
                ScrollbarRounding = 14.0f,
                GrabRounding = 7.0f,
                TabRounding = 10.0f,
            };
        }
        
        /// <summary>
        /// Monokai Pro - Classic dark theme
        /// </summary>
        public static EditorTheme MonokaiPro()
        {
            return new EditorTheme
            {
                Name = "Monokai Pro",
                Description = "Classic Monokai editor theme",
                
                WindowBackground = new Vector4(0.16f, 0.16f, 0.14f, 0.95f),
                ChildBackground = new Vector4(0.18f, 0.18f, 0.16f, 0.9f),
                PopupBackground = new Vector4(0.17f, 0.17f, 0.15f, 0.98f),
                Border = new Vector4(0.4f, 0.4f, 0.35f, 0.4f),
                
                Text = new Vector4(0.97f, 0.97f, 0.95f, 1f),
                TextDisabled = new Vector4(0.6f, 0.6f, 0.55f, 1f),
                TextSelectedBg = new Vector4(0.4f, 0.4f, 0.35f, 0.4f),
                
                FrameBg = new Vector4(0.25f, 0.25f, 0.22f, 0.3f),
                FrameBgHovered = new Vector4(0.3f, 0.3f, 0.27f, 0.4f),
                FrameBgActive = new Vector4(0.4f, 0.4f, 0.35f, 0.5f),
                
                TitleBg = new Vector4(0.12f, 0.12f, 0.1f, 0.9f),
                TitleBgActive = new Vector4(0.25f, 0.25f, 0.22f, 1f),
                TitleBgCollapsed = new Vector4(0.12f, 0.12f, 0.1f, 0.6f),
                
                MenuBarBg = new Vector4(0.18f, 0.18f, 0.16f, 0.98f),
                
                ScrollbarBg = new Vector4(0.16f, 0.16f, 0.14f, 0.7f),
                ScrollbarGrab = new Vector4(0.4f, 0.4f, 0.35f, 0.6f),
                ScrollbarGrabHovered = new Vector4(0.5f, 0.5f, 0.45f, 0.8f),
                ScrollbarGrabActive = new Vector4(0.6f, 0.6f, 0.55f, 1f),
                
                CheckMark = new Vector4(0.65f, 0.85f, 0.25f, 1f),
                
                SliderGrab = new Vector4(0.4f, 0.4f, 0.35f, 0.9f),
                SliderGrabActive = new Vector4(0.65f, 0.85f, 0.25f, 1f),
                
                Button = new Vector4(0.25f, 0.25f, 0.22f, 0.5f),
                ButtonHovered = new Vector4(0.35f, 0.35f, 0.3f, 0.7f),
                ButtonActive = new Vector4(0.4f, 0.4f, 0.35f, 0.9f),
                
                Header = new Vector4(0.25f, 0.25f, 0.22f, 0.85f),
                HeaderHovered = new Vector4(0.35f, 0.35f, 0.3f, 0.95f),
                HeaderActive = new Vector4(0.4f, 0.4f, 0.35f, 1f),
                
                Separator = new Vector4(0.4f, 0.4f, 0.35f, 0.4f),
                SeparatorHovered = new Vector4(0.5f, 0.5f, 0.45f, 0.6f),
                SeparatorActive = new Vector4(0.6f, 0.6f, 0.55f, 0.8f),
                
                ResizeGrip = new Vector4(0.4f, 0.4f, 0.35f, 0.3f),
                ResizeGripHovered = new Vector4(0.5f, 0.5f, 0.45f, 0.6f),
                ResizeGripActive = new Vector4(0.6f, 0.6f, 0.55f, 0.9f),
                
                Tab = new Vector4(0.25f, 0.25f, 0.22f, 0.5f),
                TabHovered = new Vector4(0.35f, 0.35f, 0.3f, 0.7f),
                TabActive = new Vector4(0.35f, 0.35f, 0.3f, 0.9f),
                TabUnfocused = new Vector4(0.18f, 0.18f, 0.16f, 0.6f),
                TabUnfocusedActive = new Vector4(0.22f, 0.22f, 0.19f, 0.7f),
                
                DockingPreview = new Vector4(0.65f, 0.85f, 0.25f, 0.4f),
                DockingEmptyBg = new Vector4(0.16f, 0.16f, 0.14f, 0.6f),
                
                TableHeaderBg = new Vector4(0.2f, 0.2f, 0.18f, 0.95f),
                TableBorderStrong = new Vector4(0.4f, 0.4f, 0.35f, 0.6f),
                TableBorderLight = new Vector4(0.3f, 0.3f, 0.27f, 0.3f),
                TableRowBg = new Vector4(0f, 0f, 0f, 0f),
                TableRowBgAlt = new Vector4(0.25f, 0.25f, 0.22f, 0.1f),
                
                DragDropTarget = new Vector4(0.65f, 0.85f, 0.25f, 0.6f),
                
                NavHighlight = new Vector4(0.65f, 0.85f, 0.25f, 1f),
                NavWindowingHighlight = new Vector4(0.97f, 0.97f, 0.95f, 0.8f),
                NavWindowingDimBg = new Vector4(0.25f, 0.25f, 0.22f, 0.3f),
                
                ModalWindowDimBg = new Vector4(0.16f, 0.16f, 0.14f, 0.85f),
                
                InspectorLabel = new Vector4(0.9f, 0.9f, 0.85f, 1f),
                InspectorValue = new Vector4(0.97f, 0.97f, 0.95f, 1f),
                InspectorWarning = new Vector4(1f, 0.8f, 0.2f, 1f),
                InspectorError = new Vector4(1f, 0.33f, 0.33f, 1f),
                InspectorSuccess = new Vector4(0.65f, 0.85f, 0.25f, 1f),
                InspectorInfo = new Vector4(0.4f, 0.75f, 1f, 1f),
                InspectorSection = new Vector4(0.7f, 0.7f, 0.65f, 1f),
                
                GradientStart = new Vector4(0.25f, 0.25f, 0.22f, 1f),
                GradientEnd = new Vector4(0.4f, 0.4f, 0.35f, 1f),
                AccentColor = new Vector4(0.65f, 0.85f, 0.25f, 1f),
                
                WindowRounding = 4.0f,
                ChildRounding = 3.0f,
                FrameRounding = 2.0f,
                PopupRounding = 3.0f,
                ScrollbarRounding = 4.0f,
                GrabRounding = 2.0f,
                TabRounding = 2.0f,
            };
        }
        
        /// <summary>
        /// Nord Aurora - Nordic color palette
        /// </summary>
        public static EditorTheme NordAurora()
        {
            return new EditorTheme
            {
                Name = "Nord Aurora",
                Description = "Nordic theme with aurora greens and cool grays",
                
                WindowBackground = new Vector4(0.18f, 0.2f, 0.25f, 0.95f),
                ChildBackground = new Vector4(0.2f, 0.22f, 0.27f, 0.9f),
                PopupBackground = new Vector4(0.19f, 0.21f, 0.26f, 0.98f),
                Border = new Vector4(0.5f, 0.75f, 0.65f, 0.4f),
                
                Text = new Vector4(0.93f, 0.94f, 0.96f, 1f),
                TextDisabled = new Vector4(0.6f, 0.65f, 0.7f, 1f),
                TextSelectedBg = new Vector4(0.5f, 0.75f, 0.65f, 0.4f),
                
                FrameBg = new Vector4(0.25f, 0.28f, 0.33f, 0.3f),
                FrameBgHovered = new Vector4(0.3f, 0.35f, 0.4f, 0.4f),
                FrameBgActive = new Vector4(0.5f, 0.75f, 0.65f, 0.5f),
                
                TitleBg = new Vector4(0.15f, 0.17f, 0.21f, 0.9f),
                TitleBgActive = new Vector4(0.5f, 0.75f, 0.65f, 1f),
                TitleBgCollapsed = new Vector4(0.15f, 0.17f, 0.21f, 0.6f),
                
                MenuBarBg = new Vector4(0.2f, 0.22f, 0.27f, 0.98f),
                
                ScrollbarBg = new Vector4(0.18f, 0.2f, 0.25f, 0.7f),
                ScrollbarGrab = new Vector4(0.5f, 0.75f, 0.65f, 0.6f),
                ScrollbarGrabHovered = new Vector4(0.6f, 0.85f, 0.75f, 0.8f),
                ScrollbarGrabActive = new Vector4(0.7f, 0.95f, 0.85f, 1f),
                
                CheckMark = new Vector4(0.7f, 0.95f, 0.85f, 1f),
                
                SliderGrab = new Vector4(0.5f, 0.75f, 0.65f, 0.9f),
                SliderGrabActive = new Vector4(0.7f, 0.95f, 0.85f, 1f),
                
                Button = new Vector4(0.25f, 0.3f, 0.35f, 0.5f),
                ButtonHovered = new Vector4(0.5f, 0.75f, 0.65f, 0.7f),
                ButtonActive = new Vector4(0.6f, 0.85f, 0.75f, 0.9f),
                
                Header = new Vector4(0.3f, 0.35f, 0.4f, 0.85f),
                HeaderHovered = new Vector4(0.5f, 0.75f, 0.65f, 0.95f),
                HeaderActive = new Vector4(0.6f, 0.85f, 0.75f, 1f),
                
                Separator = new Vector4(0.5f, 0.75f, 0.65f, 0.4f),
                SeparatorHovered = new Vector4(0.6f, 0.85f, 0.75f, 0.6f),
                SeparatorActive = new Vector4(0.7f, 0.95f, 0.85f, 0.8f),
                
                ResizeGrip = new Vector4(0.5f, 0.75f, 0.65f, 0.3f),
                ResizeGripHovered = new Vector4(0.6f, 0.85f, 0.75f, 0.6f),
                ResizeGripActive = new Vector4(0.7f, 0.95f, 0.85f, 0.9f),
                
                Tab = new Vector4(0.25f, 0.3f, 0.35f, 0.5f),
                TabHovered = new Vector4(0.5f, 0.75f, 0.65f, 0.7f),
                TabActive = new Vector4(0.5f, 0.75f, 0.65f, 0.9f),
                TabUnfocused = new Vector4(0.2f, 0.22f, 0.27f, 0.6f),
                TabUnfocusedActive = new Vector4(0.25f, 0.28f, 0.33f, 0.7f),
                
                DockingPreview = new Vector4(0.6f, 0.85f, 0.75f, 0.4f),
                DockingEmptyBg = new Vector4(0.18f, 0.2f, 0.25f, 0.6f),
                
                TableHeaderBg = new Vector4(0.22f, 0.25f, 0.3f, 0.95f),
                TableBorderStrong = new Vector4(0.5f, 0.75f, 0.65f, 0.6f),
                TableBorderLight = new Vector4(0.35f, 0.5f, 0.45f, 0.3f),
                TableRowBg = new Vector4(0f, 0f, 0f, 0f),
                TableRowBgAlt = new Vector4(0.25f, 0.28f, 0.33f, 0.1f),
                
                DragDropTarget = new Vector4(0.7f, 0.95f, 0.85f, 0.6f),
                
                NavHighlight = new Vector4(0.7f, 0.95f, 0.85f, 1f),
                NavWindowingHighlight = new Vector4(0.93f, 0.94f, 0.96f, 0.8f),
                NavWindowingDimBg = new Vector4(0.25f, 0.28f, 0.33f, 0.3f),
                
                ModalWindowDimBg = new Vector4(0.18f, 0.2f, 0.25f, 0.85f),
                
                InspectorLabel = new Vector4(0.85f, 0.88f, 0.92f, 1f),
                InspectorValue = new Vector4(0.93f, 0.94f, 0.96f, 1f),
                InspectorWarning = new Vector4(0.92f, 0.8f, 0.55f, 1f),
                InspectorError = new Vector4(0.75f, 0.38f, 0.42f, 1f),
                InspectorSuccess = new Vector4(0.64f, 0.88f, 0.55f, 1f),
                InspectorInfo = new Vector4(0.55f, 0.75f, 0.88f, 1f),
                InspectorSection = new Vector4(0.5f, 0.75f, 0.65f, 1f),
                
                GradientStart = new Vector4(0.5f, 0.75f, 0.65f, 1f),
                GradientEnd = new Vector4(0.35f, 0.5f, 0.68f, 1f),
                AccentColor = new Vector4(0.64f, 0.88f, 0.55f, 1f),
                
                WindowRounding = 8.0f,
                ChildRounding = 6.0f,
                FrameRounding = 4.0f,
                PopupRounding = 6.0f,
                ScrollbarRounding = 8.0f,
                GrabRounding = 4.0f,
                TabRounding = 6.0f,
            };
        }
        
        /// <summary>
        /// Get all built-in themes
        /// </summary>
        public static List<EditorTheme> GetAllThemes()
        {
            // Return a copy to avoid callers mutating the internal list, but avoid reconstructing theme objects.
            return new List<EditorTheme>(_cachedThemes);
        }
        
        /// <summary>
        /// Get theme by name
        /// </summary>
        public static EditorTheme? GetThemeByName(string name)
        {
            // Lookup in the cached list (case-sensitive matching as before)
            return _cachedThemes.Find(t => t.Name == name);
        }
    }
}
