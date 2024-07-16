using ReactiveUI;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using System;
using System.IO;
using System.Reflection;
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

            // 获取当前程序集
            Assembly assembly = Assembly.GetAssembly(typeof(Test1UIModule));

            // 构建资源名称
            string resourceName = "Test1.PURE.svg"; // 注意: 这里的路径需要根据实际情况调整
                                                    // 从嵌入的资源中读取SVG文件
            using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null) throw new InvalidOperationException("无法找到嵌入的资源.");

                WpfDrawingSettings settings = new WpfDrawingSettings();
                // 使用FileSvgReader从Stream中读取SVG
                FileSvgReader reader = new FileSvgReader(settings);
                DrawingGroup drawing = reader.Read(stream);

                // 创建一个DrawingImage并将DrawingGroup设置为其源
                DrawingImage drawingImage = new DrawingImage(drawing);


                // 创建一个Image控件并将DrawingImage设置为其源
                img.Source = drawingImage;
                //image.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                //image.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
            }
            this.WhenActivated(d => { });
        }
    }
}
