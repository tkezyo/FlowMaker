using System.Drawing;

namespace Ty
{
    public class ToolOptions
    {
        public bool ShowThemeToggle { get; set; } = true;
        public List<ToolInfo> Tools { get; set; } = [];
    }
    public class ToolInfo(string displayName, string name, string? icon = null)
    {
        public IObservable<bool>? Enable { get; set; }
        public IObservable<bool>? Show { get; set; }
        public IObservable<Color?>? ChangeColor { get; set; }
        public IObservable<string>? ChangeIcon { get; set; }
        public IObservable<string>? ChangeDisplayName { get; set; }

        public string Name { get; set; } = name;
        public string DisplayName { get; set; } = displayName;
        public string? Icon { get; set; } = icon;
        public Color? Color { get; set; }
        public List<ToolInfo> Children { get; set; } = [];
    }
}
