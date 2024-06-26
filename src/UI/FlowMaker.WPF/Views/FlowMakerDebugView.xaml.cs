﻿using DynamicData;
using DynamicData.Binding;
using FlowMaker.ViewModels;
using ReactiveUI;
using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Windows.UI.WebUI;

namespace FlowMaker.Views
{
    /// <summary>
    /// FlowMakerDebugView.xaml 的交互逻辑
    /// </summary>
    public partial class FlowMakerDebugView : ReactiveUserControl<FlowMakerDebugViewModel>
    {
        //public CompositeDisposable? LogDisplay { get; set; }
        public FlowMakerDebugView()
        {
            InitializeComponent();
            //this.WhenActivated(d =>
            //{
            //    ViewModel = (FlowMakerDebugViewModel)DataContext;
            //    FlowDocument doc = new FlowDocument();

            //    doc.LineHeight = 10;
            //    doc.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            //    rtb.Document = doc;
            //    ViewModel.WhenAnyValue(c => c.DataDisplay).Subscribe(data =>
            //    {
            //        if (data is null)
            //        {
            //            return;
            //        }
            //        LogDisplay?.Dispose();
            //        LogDisplay = [];
            //        // 将FlowDocument设置为 rtb的Document
            //        RxApp.MainThreadScheduler.Schedule(() =>
            //        {
            //            // 创建一个新的FlowDocument
            //            // 订阅 data.Log的变化
            //            data.Log.ToObservableChangeSet().ObserveOn(RxApp.MainThreadScheduler).Subscribe(changeSet =>
            //              {
            //                  // 对于每个变化，创建一个新的Paragraph，并将其添加到FlowDocument中
            //                  foreach (var change in changeSet)
            //                  {
            //                      if (change.Reason == ListChangeReason.Clear || change.Reason == ListChangeReason.Remove || change.Reason == ListChangeReason.RemoveRange)
            //                      {
            //                          doc.Blocks.Clear();
            //                      }
            //                      if (change.Reason == ListChangeReason.Add || change.Reason == ListChangeReason.AddRange)
            //                      {
            //                          if (change.Type == ChangeType.Item)
            //                          {
            //                              if (change.Item.Current.Level > Microsoft.Extensions.Logging.LogLevel.Warning)
            //                              {
            //                                  Paragraph para = new Paragraph(new Run(change.Item.Current.Log) { Foreground = new SolidColorBrush(Colors.Red) });
            //                                  doc.Blocks.Add(para);
            //                              }
            //                              else
            //                              {
            //                                  Paragraph para = new Paragraph(new Run(change.Item.Current.Log) { });
            //                                  doc.Blocks.Add(para);
            //                              }

            //                              // 将光标移动到文档的末尾
            //                              //rtb.CaretPosition = rtb.Document.ContentEnd;
            //                          }
            //                          else if (change.Type == ChangeType.Range)
            //                          {
            //                              foreach (var item in change.Range)
            //                              {
            //                                  if (item.Level > Microsoft.Extensions.Logging.LogLevel.Warning)
            //                                  {
            //                                      Paragraph para = new Paragraph(new Run(item.Log) { Foreground = new SolidColorBrush(Colors.Red) });
            //                                      doc.Blocks.Add(para);
            //                                  }
            //                                  else
            //                                  {
            //                                      Paragraph para = new Paragraph(new Run(item.Log) { });
            //                                      doc.Blocks.Add(para);
            //                                  }
            //                              }

            //                              // 将光标移动到文档的末尾
            //                              //rtb.CaretPosition = rtb.Document.ContentEnd;
            //                          }
            //                      }
            //                  }
            //              }).DisposeWith(LogDisplay);
            //            //data.Log.ToObservableChangeSet().Sample(TimeSpan.FromSeconds(1)).ObserveOn(RxApp.MainThreadScheduler).Subscribe(c =>
            //            //{
            //            //    rtb.CaretPosition = rtb.Document.ContentEnd;
            //            //}).DisposeWith(LogDisplay);
            //        });
            //    }).DisposeWith(d);
            //});
        }



    }
}
