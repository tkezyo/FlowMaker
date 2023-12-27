using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Collections.ObjectModel;

namespace Ty.ViewModels
{
    public class PagerViewModel : ReactiveObject
    {
        /// <summary>
        /// 跳过多少信息
        /// </summary>
        [Reactive]
        public int SkipCount { get; set; } = 0;
        /// <summary>
        /// 总信息数
        /// </summary>
        [Reactive]
        public long TotalCount { get; set; }
        /// <summary>
        /// 显示的页面个数
        /// </summary>
        [Reactive]
        public int DisplayPageCount { get; set; } = 2;
        /// <summary>
        /// 每页数量
        /// </summary>
        [Reactive]
        public int PageSize { get; set; } = 10;
        [Reactive]
        public ObservableCollection<PageInfo> PageInfos { get; set; } = [];
        /// <summary>
        /// 重置分页信息
        /// </summary>
        /// <param name="skipCount"></param>
        public virtual void SetSkipCount(int? skipCount = null)
        {
            if (skipCount.HasValue)
            {
                SkipCount = skipCount.Value;
            }
            else
            {
                SkipCount = 0;
            }
        }
        /// <summary>
        /// 设置分页按钮信息
        /// </summary>
        /// <param name="totalCount"></param>
        public virtual void SetPages(long totalCount)
        {
            PageInfos.Clear();

            var pages = GetPage(SkipCount, totalCount, PageSize);
            foreach (var item in pages)
            {
                PageInfos.Add(item);
            }

            TotalCount = totalCount;
        }
        /// <summary>
        /// 设置每页数量，并重置分页按钮信息
        /// </summary>
        /// <param name="pageSize"></param>
        public virtual void SetPageSize(int pageSize)
        {
            PageSize = pageSize;
            SetPages(TotalCount);
        }
        protected virtual List<PageInfo> GetPage(int skipCount, long totalCount, int pageSize)
        {
            List<PageInfo> pageInfos = [];
            int skipCountTemp = 0;
            int totalPage = (int)(totalCount / pageSize + (totalCount % pageSize == 0 ? 0 : 1));

            //当前页面索引
            int currentPageIndex = skipCount / pageSize + (skipCount % pageSize == 0 ? 0 : 1) + 1;

            for (int i = 1; i <= totalPage; i++)
            {
                //第一页或最后一页或当前页附近的页码
                if (i == 1 || i == totalPage || Math.Abs(currentPageIndex - i) <= DisplayPageCount)
                {
                    pageInfos.Add(new PageInfo(i.ToString(), skipCountTemp) { CurrentIndex = skipCount == skipCountTemp });
                }
                else
                {
                    //如果最后一个元素不是...
                    if (pageInfos.Last().Display != "...")
                    {
                        pageInfos.Add(new PageInfo("...", -1) { CurrentIndex = false });
                    }
                }

                skipCountTemp += pageSize;
            }

            return pageInfos;
        }

        public string? Sorting { get; private set; }

        public void SetSorting(string? newSorting)
        {
            if (string.IsNullOrWhiteSpace(newSorting))
            {
                Sorting = string.Empty;
                return;
            }
            if (string.IsNullOrWhiteSpace(Sorting))
            {
                Sorting = newSorting + " asc";
            }
            else
            {
                if (Sorting!.Contains(newSorting))
                {
                    if (Sorting.Contains("asc"))
                    {
                        Sorting = newSorting + " desc";
                    }
                    else
                    {
                        Sorting = newSorting + " asc";
                    }
                }
                else
                {
                    Sorting = newSorting + " asc";
                }
            }
        }
    }
    public class PageInfo : ReactiveObject
    {
        private int skipCount;

        [Reactive]
        public string Display { get; set; }

        public PageInfo(string display, int skipCount)
        {
            Display = display;
            SkipCount = skipCount;
        }

        public bool CanExcute
        {
            get
            {
                return SkipCount >= 0;
            }
        }

        public int SkipCount
        {
            get => skipCount; set
            {
                this.RaiseAndSetIfChanged(ref skipCount, value);
                this.RaisePropertyChanged(nameof(CanExcute));
            }
        }

        [Reactive]
        public bool CurrentIndex { get; set; }
    }
}
