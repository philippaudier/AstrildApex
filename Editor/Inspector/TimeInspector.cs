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

        public static void Draw(TimeComponent time)
        {
            if (time == null) return;

            DrawTimeOfDaySection(time);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawSeasonSection(time);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawCelestialSection(time);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawLinkingSection(time);
        }

        private static void DrawTimeOfDaySection(TimeComponent time)
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
                time.TimeOfDay = timeOfDay;

                // Quick time presets
                InspectorWidgets.DisabledLabel("Presets:");
                int preset = InspectorWidgets.PresetButtonRow(
                    ("Dawn", "6:00 AM"),
                    ("Noon", "12:00 PM"),
                    ("Dusk", "18:00 PM"),
                    ("Night", "0:00 AM"));

                if (preset == 0) time.TimeOfDay = 6.0f;  // Dawn
                else if (preset == 1) time.TimeOfDay = 12.0f; // Noon
                else if (preset == 2) time.TimeOfDay = 18.0f; // Dusk
                else if (preset == 3) time.TimeOfDay = 0.0f;  // Midnight

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

        private static void DrawSeasonSection(TimeComponent time)
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

        private static void DrawCelestialSection(TimeComponent time)
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

        private static void DrawLinkingSection(TimeComponent time)
        {
            if (ThemedImGui.CollapsingHeader("🔗 Linking", ImGuiTreeNodeFlags.None))
            {
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
