using System;
using ImGuiNET;
using Engine.Components;
using Editor.UI;
using Editor.Themes;

namespace Editor.Inspector
{
    /// <summary>
    /// Inspector UI for TimeComponent - provides intuitive controls for time of day,
    /// seasons, and celestial mechanics.
    /// </summary>
    public static class TimeInspector
    {
        private static UITheme UI => ThemeManager.UI;

        public static void Draw(Engine.Scene.Entity entity, TimeComponent time)
        {
            if (time == null) return;

            DrawTimeOfDaySection(entity, time);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawSeasonSection(entity, time);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawCelestialSection(entity, time);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawLinkingSection(entity, time);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawSkyOverridesSection(entity, time);
            ImGui.Spacing();
            DrawSunMoonSection(entity, time);
        }

        private static void DrawSkyOverridesSection(Engine.Scene.Entity entity, TimeComponent time)
        {
            if (!InspectorWidgets.Section("🌤 Sky Overrides (Time)", defaultOpen: false)) return;

            uint entityId = time.Entity?.Id ?? 0;

            // Sky tints
            var dayTint = new OpenTK.Mathematics.Vector3(time.DaySkyTint.X, time.DaySkyTint.Y, time.DaySkyTint.Z);
            if (InspectorWidgets.ColorFieldOTK("Day Sky Tint", ref dayTint, entityId, "DaySkyTint", tooltip: "Sky tint used during daytime"))
            {
                time.DaySkyTint = new System.Numerics.Vector3(dayTint.X, dayTint.Y, dayTint.Z);
            }

            var nightTint = new OpenTK.Mathematics.Vector3(time.NightSkyTint.X, time.NightSkyTint.Y, time.NightSkyTint.Z);
            if (InspectorWidgets.ColorFieldOTK("Night Sky Tint", ref nightTint, entityId, "NightSkyTint", tooltip: "Sky tint used during night"))
            {
                time.NightSkyTint = new System.Numerics.Vector3(nightTint.X, nightTint.Y, nightTint.Z);
            }

            var dawnTint = new OpenTK.Mathematics.Vector3(time.DawnSkyTint.X, time.DawnSkyTint.Y, time.DawnSkyTint.Z);
            if (InspectorWidgets.ColorFieldOTK("Dawn Sky Tint", ref dawnTint, entityId, "DawnSkyTint", tooltip: "Sky tint used during dawn (golden hour)"))
            {
                time.DawnSkyTint = new System.Numerics.Vector3(dawnTint.X, dawnTint.Y, dawnTint.Z);
            }

            var duskTint = new OpenTK.Mathematics.Vector3(time.DuskSkyTint.X, time.DuskSkyTint.Y, time.DuskSkyTint.Z);
            if (InspectorWidgets.ColorFieldOTK("Dusk Sky Tint", ref duskTint, entityId, "DuskSkyTint", tooltip: "Sky tint used during dusk (golden hour)"))
            {
                time.DuskSkyTint = new System.Numerics.Vector3(duskTint.X, duskTint.Y, duskTint.Z);
            }

            ImGui.Spacing();

            // Ground colors
            var dayGround = new OpenTK.Mathematics.Vector3(time.DayGroundColor.X, time.DayGroundColor.Y, time.DayGroundColor.Z);
            if (InspectorWidgets.ColorFieldOTK("Day Ground Color", ref dayGround, entityId, "DayGroundColor", tooltip: "Ground color used during daytime"))
            {
                time.DayGroundColor = new System.Numerics.Vector3(dayGround.X, dayGround.Y, dayGround.Z);
            }

            var nightGround = new OpenTK.Mathematics.Vector3(time.NightGroundColor.X, time.NightGroundColor.Y, time.NightGroundColor.Z);
            if (InspectorWidgets.ColorFieldOTK("Night Ground Color", ref nightGround, entityId, "NightGroundColor", tooltip: "Ground color used during night"))
            {
                time.NightGroundColor = new System.Numerics.Vector3(nightGround.X, nightGround.Y, nightGround.Z);
            }

            ImGui.Spacing();

            // Atmosphere thickness
            float dayAt = time.DayAtmosphereThickness;
            InspectorWidgets.FloatField("Day Atmosphere Thickness", ref dayAt, entityId, "DayAtmosphereThickness", speed: 0.01f, min: 0.1f, max: 5f, tooltip: "Atmosphere thickness for daytime");
            time.DayAtmosphereThickness = dayAt;

            float nightAt = time.NightAtmosphereThickness;
            InspectorWidgets.FloatField("Night Atmosphere Thickness", ref nightAt, entityId, "NightAtmosphereThickness", speed: 0.01f, min: 0.1f, max: 5f, tooltip: "Atmosphere thickness for nighttime");
            time.NightAtmosphereThickness = nightAt;

            float dawnAt = time.DawnDuskAtmosphereThickness;
            InspectorWidgets.FloatField("Dawn/Dusk Atmosphere Thickness", ref dawnAt, entityId, "DawnDuskAtmosphereThickness", speed: 0.01f, min: 0.1f, max: 5f, tooltip: "Atmosphere thickness for dawn/dusk (golden hour)");
            time.DawnDuskAtmosphereThickness = dawnAt;

            InspectorWidgets.EndSection();
        }

        // Add UI for sun/moon size and convergence
        private static void DrawSunMoonSection(Engine.Scene.Entity entity, TimeComponent time)
        {
            if (!InspectorWidgets.Section("☀️ Sun/Moon Visuals", defaultOpen: false)) return;
            uint entityId = time.Entity?.Id ?? 0;

            float daySunSize = time.DaySunSize;
            InspectorWidgets.FloatField("Day Sun Size", ref daySunSize, entityId, "DaySunSize", speed: 0.001f, min: 0.001f, max: 1.0f, tooltip: "Visual size of sun during day (used by procedural sky)");
            time.DaySunSize = daySunSize;

            float nightMoonSize = time.NightMoonSize;
            InspectorWidgets.FloatField("Night Moon Size", ref nightMoonSize, entityId, "NightMoonSize", speed: 0.001f, min: 0.001f, max: 1.0f, tooltip: "Visual size of moon during night (used by procedural sky)");
            time.NightMoonSize = nightMoonSize;

            float dawnSunSize = time.DawnDuskSunSize;
            InspectorWidgets.FloatField("Dawn/Dusk Sun Size", ref dawnSunSize, entityId, "DawnDuskSunSize", speed: 0.001f, min: 0.001f, max: 1.0f, tooltip: "Sun size during golden hour");
            time.DawnDuskSunSize = dawnSunSize;

            ImGui.Spacing();

            float dayConv = time.DaySunConvergence;
            InspectorWidgets.FloatField("Day Sun Convergence", ref dayConv, entityId, "DaySunConvergence", speed: 0.1f, min: 0.1f, max: 50f, tooltip: "Convergence/falloff multiplier for sun glow during day");
            time.DaySunConvergence = dayConv;

            float nightConv = time.NightMoonConvergence;
            InspectorWidgets.FloatField("Night Moon Convergence", ref nightConv, entityId, "NightMoonConvergence", speed: 0.1f, min: 0.1f, max: 50f, tooltip: "Convergence/falloff multiplier for moon glow at night");
            time.NightMoonConvergence = nightConv;

            float dawnConv = time.DawnDuskSunConvergence;
            InspectorWidgets.FloatField("Dawn/Dusk Convergence", ref dawnConv, entityId, "DawnDuskSunConvergence", speed: 0.1f, min: 0.1f, max: 100f, tooltip: "Convergence during golden hour");
            time.DawnDuskSunConvergence = dawnConv;

            InspectorWidgets.EndSection();
        }

        private static void DrawTimeOfDaySection(Engine.Scene.Entity entity, TimeComponent time)
        {
            if (ThemedImGui.CollapsingHeader("🕐 Time of Day", ImGuiTreeNodeFlags.DefaultOpen))
            {
                uint entityId = time.Entity?.Id ?? 0;

                // Time of day slider (0-24)
                float timeOfDay = time.TimeOfDay;
                InspectorWidgets.FloatField("Time (Hours)", ref timeOfDay, entityId, "TimeOfDay",
                    speed: 0.1f, min: 0f, max: 24f,
                    tooltip: "Current time of day (0 = midnight, 12 = noon, 24 = midnight)",
                    helpText: "Sunrise: ~6h, Noon: 12h, Sunset: ~18h, Midnight: 0h/24h");
                if (Math.Abs(time.TimeOfDay - timeOfDay) > 0.001f)
                {
                    time.TimeOfDay = timeOfDay;
                    time.UpdateLinkedEntitiesInEditMode(Panels.EditorUI.MainViewport.Renderer?.Scene); // Apply to EnvironmentSettings in Edit mode
                }

                // Quick time presets
                InspectorWidgets.DisabledLabel("Presets:");
                int preset = InspectorWidgets.PresetButtonRow(
                    ("Dawn", "6:00 AM"),
                    ("Noon", "12:00 PM"),
                    ("Dusk", "18:00 PM"),
                    ("Night", "0:00 AM"));

                if (preset == 0) { time.TimeOfDay = 6.0f; time.UpdateLinkedEntitiesInEditMode(Panels.EditorUI.MainViewport.Renderer?.Scene); }
                else if (preset == 1) { time.TimeOfDay = 12.0f; time.UpdateLinkedEntitiesInEditMode(Panels.EditorUI.MainViewport.Renderer?.Scene); }
                else if (preset == 2) { time.TimeOfDay = 18.0f; time.UpdateLinkedEntitiesInEditMode(Panels.EditorUI.MainViewport.Renderer?.Scene); }
                else if (preset == 3) { time.TimeOfDay = 0.0f; time.UpdateLinkedEntitiesInEditMode(Panels.EditorUI.MainViewport.Renderer?.Scene); }

                ImGui.Spacing();

                // Auto-advance settings
                bool autoAdvance = time.AutoAdvance;
                InspectorWidgets.Checkbox("Auto Advance", ref autoAdvance, entityId, "AutoAdvance",
                    tooltip: "Automatically advance time at a specified rate");
                time.AutoAdvance = autoAdvance;

                if (autoAdvance)
                {
                    float dayLength = time.DayLengthMinutes;
                    InspectorWidgets.FloatField("Day Length (min)", ref dayLength, entityId, "DayLength",
                        speed: 0.5f, min: 0.1f, max: 1440f,
                        tooltip: "Real-world minutes for one full day cycle",
                        helpText: "1 min = fast, 10 min = normal, 1440 min = real-time");
                    time.DayLengthMinutes = dayLength;

                    float timeScale = time.TimeScale;
                    InspectorWidgets.FloatField("Time Scale", ref timeScale, entityId, "TimeScale",
                        speed: 0.1f, min: 0f, max: 100f,
                        tooltip: "Additional time speed multiplier (1.0 = normal, 2.0 = 2x speed)",
                        helpText: "Use for dramatic effects or debugging");
                    time.TimeScale = timeScale;
                }

                ImGui.Spacing();

                // Display current time
                int hours = (int)time.TimeOfDay;
                int minutes = (int)((time.TimeOfDay - hours) * 60);
                string timeString = $"{hours:D2}:{minutes:D2}";
                float dayNightBlend = time.GetDayNightBlend();
                string period = dayNightBlend > 0.5f ? "Day" : "Night";

                InspectorWidgets.DisabledLabel($"Current Time: {timeString} ({period})");

                // Day/Night indicator bar
                ImGui.ProgressBar(dayNightBlend, new System.Numerics.Vector2(-1, 0), $"Day/Night: {dayNightBlend:P0}");
            }
        }

        private static void DrawSeasonSection(Engine.Scene.Entity entity, TimeComponent time)
        {
            if (ThemedImGui.CollapsingHeader("📅 Calendar", ImGuiTreeNodeFlags.None))
            {
                uint entityId = time.Entity?.Id ?? 0;

                int dayOfYear = time.DayOfYear;
                InspectorWidgets.IntField("Day of Year", ref dayOfYear, entityId, "DayOfYear",
                    speed: 1, min: 0, max: 365,
                    tooltip: "Current day of the year (0-365, where 180 = summer solstice)");
                time.DayOfYear = dayOfYear;

                int year = time.Year;
                InspectorWidgets.IntField("Year", ref year, entityId, "Year",
                    speed: 1, min: 1, max: 9999,
                    tooltip: "Current year");
                time.Year = year;

                ImGui.Spacing();

                // Display current season
                var season = time.GetSeason();
                string seasonName = season.ToString();
                float yearProgress = (float)time.DayOfYear / 365.0f;

                InspectorWidgets.DisabledLabel($"Current Season: {seasonName}");
                ImGui.ProgressBar(yearProgress, new System.Numerics.Vector2(-1, 0), $"Year Progress: {yearProgress:P0}");

                ImGui.Spacing();
                InspectorWidgets.InfoBox("Seasons are calculated from DayOfYear and affect celestial mechanics and environmental parameters");
            }
        }

        private static void DrawCelestialSection(Engine.Scene.Entity entity, TimeComponent time)
        {
            if (ThemedImGui.CollapsingHeader("🌅 Time Blends", ImGuiTreeNodeFlags.None))
            {
                // Display day/night and golden hour blends
                float dayNightBlend = time.GetDayNightBlend();
                ImGui.Text("Day/Night Blend:");
                ImGui.ProgressBar(dayNightBlend, new System.Numerics.Vector2(-1, 0), $"{dayNightBlend:P0}");
                ImGui.SameLine();
                ImGui.TextDisabled("(0 = night, 1 = day)");

                ImGui.Spacing();

                float goldenHour = time.GetGoldenHourBlend();
                ImGui.Text("Golden Hour Blend:");
                ImGui.ProgressBar(goldenHour, new System.Numerics.Vector2(-1, 0), $"{goldenHour:P0}");
                if (goldenHour > 0.01f)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(new System.Numerics.Vector4(1.0f, 0.7f, 0.3f, 1.0f), "Active!");
                }

                ImGui.Spacing();
                InspectorWidgets.InfoBox("These blends drive EnvironmentSettings (sun/moon) and post-process effects (color grading, tonemapping)");
            }
        }

        private static void DrawLinkingSection(Engine.Scene.Entity entity, TimeComponent time)
        {
            if (ThemedImGui.CollapsingHeader("🔗 Linking", ImGuiTreeNodeFlags.None))
            {
                // CRITICAL: Auto-detect linked entities (Update() is not called in Edit mode!)
                time.UpdateLinkedEntitiesInEditMode(Panels.EditorUI.MainViewport.Renderer?.Scene);

                InspectorWidgets.DisabledLabel("Linked Entities:");

                // Display current links (read-only for now)
                var envEntity = time.EnvironmentEntity;
                string envName = envEntity != null ? envEntity.Name : "None (Auto-detect)";
                ImGui.BulletText($"Environment: {envName}");

                var effectsEntity = time.GlobalEffectsEntity;
                string effectsName = effectsEntity != null ? effectsEntity.Name : "None (Auto-detect)";
                ImGui.BulletText($"Global Effects: {effectsName}");

                ImGui.Spacing();
                InspectorWidgets.InfoBox("TimeComponent automatically finds and updates linked EnvironmentSettings and GlobalEffects components in the scene.");
            }
        }
    }
}
