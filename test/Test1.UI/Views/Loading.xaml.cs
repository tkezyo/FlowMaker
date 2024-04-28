using ReactiveUI;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using System.Windows.Controls;
using System.Windows.Media;
using Test1.ViewModels;

namespace Test1.Views
{
    /// <summary>
    /// Loading.xaml 的交互逻辑
    /// </summary>
    public partial class Loading : ReactiveUserControl<LoadingViewModel>
    {
        public Loading()
        {
            InitializeComponent();// 创建一个WpfDrawingSettings对象
            WpfDrawingSettings settings = new WpfDrawingSettings();

            // 创建一个FileSvgReader对象
            FileSvgReader reader = new FileSvgReader(settings);

            // 读取SVG文件并转换为WPF的DrawingGroup
            DrawingGroup drawing = reader.Read(".\\PURE.svg");

            // 创建一个DrawingImage并将DrawingGroup设置为其源
            DrawingImage drawingImage = new DrawingImage(drawing);

            // 创建一个Image控件并将DrawingImage设置为其源
            img.Source = drawingImage;
            this.WhenActivated(d => { });
        }
    }
}
