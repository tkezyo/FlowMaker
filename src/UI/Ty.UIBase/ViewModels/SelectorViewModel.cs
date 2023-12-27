using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Collections.ObjectModel;
using System.Reactive.Linq;

namespace Ty.ViewModels
{
    public interface ICanSelector
    {
        Guid Id { get; }
        bool IsSelected { get; set; }
    }
    /// <summary>
    ///     SelectorViewModel = new SelectorViewModel<PatientViewModel>(Entities);
    ///     // 加载页面订阅勾选事件
    ///     LoadCommand.Subscribe(c =>
    ///     {
    ///         SelectorViewModel.IsSelectAllValueChanged(true);
    ///     });
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SelectorViewModel<T> : ReactiveObject
        where T : ReactiveObject, ICanSelector
    {
        public ObservableCollection<T> Entities { get; set; }
        public SelectorViewModel(ObservableCollection<T> entities)
        {
            Entities = entities;
            AllSelectorDisposable = this.WhenAnyValue(c => c.IsSelectAll).Subscribe(c => SelectAllOrNot());
            IsSelectedValueChanged();
        }
        public IDisposable? AllSelectorDisposable { get; set; }
        public IDisposable? EntitySelectorDisposable { get; set; }

        /// <summary>
        /// 全选框值绑定
        /// </summary>
        [Reactive]
        public bool IsSelectAll { get; set; }
        /// <summary>
        /// 储存勾选的信息的Guid
        /// </summary>
        public List<Guid> Seleted { get; set; } = new();

        /// <summary>
        /// 全选；全不选
        /// </summary>
        public void SelectAllOrNot()
        {
            // 全选:设置值为true，加入勾选列表
            if (IsSelectAll)
            {
                foreach (var patientItem in Entities)
                {
                    patientItem.IsSelected = true;
                    if (!Seleted.Contains(patientItem.Id))
                    {
                        Seleted.Add(patientItem.Id);
                    }
                }
            }
            // 全不选
            else
            {
                foreach (var patientItem in Entities)
                {
                    patientItem.IsSelected = false;
                    Seleted.Remove(patientItem.Id);
                }
            }
            // 单选
            // 绑定数据源中的单选框值发生改变事件
            IsSelectedValueChanged();
        }

        /// <summary>
        /// 单选框值发生改变时
        /// </summary>
        public void IsSelectedValueChanged()
        {
            // 取消订阅
            EntitySelectorDisposable?.Dispose();
            EntitySelectorDisposable = Entities.ToObservableChangeSet().SubscribeMany(c =>
            {
                return c.WhenValueChanged(v => v.IsSelected, notifyOnInitialValue: false).Subscribe(v =>
                {
                    // 勾选
                    if (v)
                    {
                        if (!Seleted.Contains(c.Id))
                        {
                            Seleted.Add(c.Id);
                        }
                    }
                    // 不勾选
                    else
                    {
                        Seleted.Remove(c.Id);
                    }
                    IsSelectAllValueChanged(true);
                });
            }).Subscribe();
        }

        /// <summary>
        /// 单选框值发生改变时,全选框值的变化
        /// </summary>
        /// <param name="isSkipFirst"></param>
        public void IsSelectAllValueChanged(bool isSkipFirst = false)
        {
            AllSelectorDisposable?.Dispose();
            IsSelectAll = Entities.Count != 0 && Entities.All(x => x.IsSelected);
            if (isSkipFirst)
            {
                AllSelectorDisposable = this.WhenAnyValue(c => c.IsSelectAll).Skip(1).Subscribe(c => SelectAllOrNot());
            }
            else
            {
                AllSelectorDisposable = this.WhenAnyValue(c => c.IsSelectAll).Subscribe(c => SelectAllOrNot());
            }
        }
    }
}
