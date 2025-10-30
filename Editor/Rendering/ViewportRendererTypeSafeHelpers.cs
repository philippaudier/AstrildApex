namespace Editor.Rendering
{
    // Small type-safe helpers used by renderers to coordinate one-shot debug prints.
    public static class ViewportRendererTypeSafeHelpers
    {
        private static bool _hasPrintedSSAODebug = false;
        public static bool HasPrintedSSAODebug => _hasPrintedSSAODebug;
        public static void MarkPrintedSSAODebug() => _hasPrintedSSAODebug = true;
    }
}