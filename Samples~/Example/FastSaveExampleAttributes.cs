#if !ODIN_INSPECTOR
using System;

namespace Vtcong.Core.Example
{
    // Stub để example vẫn biên dịch được khi project chưa cài Odin Inspector.
    // Khi có Odin, file này bị loại khỏi build và attribute thật của Sirenix được dùng.
    // Không có Odin thì FastSaveExampleEditor sẽ vẽ nút bằng IMGUI.

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ButtonAttribute : Attribute
    {
        public ButtonAttribute()
        {
        }

        public ButtonAttribute(string name)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class BoxGroupAttribute : Attribute
    {
        public BoxGroupAttribute(string group)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class ButtonGroupAttribute : Attribute
    {
        public ButtonGroupAttribute(string group)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class ShowInInspectorAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class ReadOnlyAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class TitleAttribute : Attribute
    {
        public TitleAttribute(string title)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class InfoBoxAttribute : Attribute
    {
        public InfoBoxAttribute(string message)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class LabelTextAttribute : Attribute
    {
        public LabelTextAttribute(string label)
        {
        }
    }
}
#endif
