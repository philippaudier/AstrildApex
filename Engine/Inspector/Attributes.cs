using System;

namespace Engine.Inspector
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class EditableAttribute : Attribute
    {
        public string? DisplayName;
        public EditableAttribute(string? displayName = null) { DisplayName = displayName; }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class RangeAttribute : Attribute
    {
        public float Min, Max;
        public RangeAttribute(float min, float max) { Min = min; Max = max; }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class StepAttribute : Attribute
    {
        public float Step;
        public StepAttribute(float step) { Step = step; }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class ColorAttribute : Attribute
    {
        public bool HDR;
        public ColorAttribute(bool hdr = false) { HDR = hdr; }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class ReadOnlyAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TooltipAttribute : Attribute
    {
        public string Text;
        public TooltipAttribute(string text) { Text = text; }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class MultilineAttribute : Attribute
    {
        public int Lines;
        public MultilineAttribute(int lines = 3) { Lines = Math.Max(1, lines); }
    }
}
