using Avalonia;
using Avalonia.Controls;

namespace FlowMaker.Controls
{
    public partial class FlowInputView : UserControl
    {
        public static readonly StyledProperty<bool> EditModeProperty =
          AvaloniaProperty.Register<FlowInputView, bool>(nameof(EditMode));

        public bool EditMode
        {
            get { return GetValue(EditModeProperty); }
            set { SetValue(EditModeProperty, value); }
        }
        public FlowInputView()
        {
            InitializeComponent();
        }
    }
}
