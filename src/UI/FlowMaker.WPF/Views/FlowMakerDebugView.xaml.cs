using DynamicData;
using DynamicData.Binding;
using FlowMaker.ViewModels;
using ReactiveUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FlowMaker.Views
{
    /// <summary>
    /// FlowMakerDebugView.xaml 的交互逻辑
    /// </summary>
    public partial class FlowMakerDebugView : ReactiveUserControl<FlowMakerDebugViewModel>
    {
        public FlowMakerDebugView()
        {
            InitializeComponent();
            this.WhenActivated(d =>
            {
                ViewModel = (FlowMakerDebugViewModel)DataContext;
                ViewModel.WhenAnyValue(c => c.DataDisplay).Subscribe(data =>
                {
                    if (data is null)
                    {
                        return;
                    }
                    // 将FlowDocument设置为 rtb的Document
                    RxApp.MainThreadScheduler.Schedule(() =>
                    {
                        FlowDocument doc = new FlowDocument();
                        doc.LineHeight = 10;
                        doc.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
                        // 创建一个新的FlowDocument
                        // 订阅 data.Log的变化
                        data.Log.ToObservableChangeSet().ObserveOn(RxApp.MainThreadScheduler).Subscribe(changeSet =>
                        {
                            // 对于每个变化，创建一个新的Paragraph，并将其添加到FlowDocument中
                            foreach (var change in changeSet)
                            {
                                if (change.Reason == ListChangeReason.Clear)
                                {
                                    doc = new FlowDocument();
                                    doc.LineHeight = 10;
                                    doc.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
                                    rtb.Document = doc;
                                }
                                if (change.Reason == ListChangeReason.Add || change.Reason == ListChangeReason.AddRange)
                                {
                                    if (change.Type == ChangeType.Item)
                                    {
                                        if (change.Item.Current.Level > Microsoft.Extensions.Logging.LogLevel.Warning)
                                        {
                                            Paragraph para = new Paragraph(new Run(change.Item.Current.Log) { Foreground = new SolidColorBrush(Colors.Red) });
                                            doc.Blocks.Add(para);
                                        }
                                        else
                                        {
                                            Paragraph para = new Paragraph(new Run(change.Item.Current.Log) { });
                                            doc.Blocks.Add(para);
                                        }
                                        rtb.Document = doc;
                                        // 将光标移动到文档的末尾
                                        rtb.CaretPosition = rtb.Document.ContentEnd;
                                    }
                                    else if (change.Type == ChangeType.Range)
                                    {
                                        foreach (var item in change.Range)
                                        {
                                            if (item.Level > Microsoft.Extensions.Logging.LogLevel.Warning)
                                            {
                                                Paragraph para = new Paragraph(new Run(item.Log) { Foreground = new SolidColorBrush(Colors.Red) });
                                                doc.Blocks.Add(para);
                                            }
                                            else
                                            {
                                                Paragraph para = new Paragraph(new Run(item.Log) { });
                                                doc.Blocks.Add(para);
                                            }
                                        }
                                        rtb.Document = doc;
                                        rtb.Focus();

                                        // 将光标移动到文档的末尾
                                        rtb.CaretPosition = rtb.Document.ContentEnd;
                                    }
                                }

                            }

                        }).DisposeWith(d);


                    });



                }).DisposeWith(d);
            });
        }



    }
}
