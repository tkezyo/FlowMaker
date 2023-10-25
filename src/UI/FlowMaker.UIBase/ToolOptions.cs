using System.Diagnostics.CodeAnalysis;
using System.Drawing;

namespace FlowMaker
{
    public class ToolOptions
    {
        public bool ShowThemeToggle { get; set; } = true;
        public List<ToolInfo> Tools { get; set; } = new();
    }
    public class ToolInfo
    {
        public required string DisplayName { get; set; }
        [SetsRequiredMembers]
        public ToolInfo(string displayName, string name, string? icon = null)
        {
            DisplayName = displayName;
            Name = name;
            Icon = icon;
        }
        public IObservable<bool>? Enable { get; set; }
        public IObservable<bool>? Show { get; set; }
        public IObservable<Color?>? ChangeColor { get; set; }
        public IObservable<string>? ChangeIcon { get; set; }
        public IObservable<string>? ChangeDisplayName { get; set; }
        public required string Name { get; set; }
        public string? Icon { get; set; }
        public Color? Color { get; set; }
        public List<ToolInfo> Children { get; set; } = new List<ToolInfo>();
    }
}
