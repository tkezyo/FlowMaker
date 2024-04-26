using FlowMaker;
using System.ComponentModel;

namespace Test1
{
    [Steps("算法")]
    public interface ICaesarMode
    {
        [Description("算法A")]
        (int, string) Test(int ss = 2);
        int Test2(DayOfWeek ss);
        void Test3(int ss);
        Task Test4(int ss);
        Task<int> Test5(int ss);
    }


    [Steps("CaesarModeKK")]
    public class CaesarMode
    {
        public (int, string) Test(int ss = 2)
        {
            return (ss, "ss");
        }

        public int Test2(DayOfWeek ss)
        {
            return 1;
        }

        public void Test3(int ss)
        {

        }

        public Task Test4(int ss)
        {
            return Task.CompletedTask;
        }

        public async Task<int> Test5(int ss)
        {
            await Task.CompletedTask;
            return ss;
        }

        public async Task<int> Test6(bool ss)
        {
            await Task.CompletedTask;
            return 12;
        }

        public void Error()
        {
            throw new Exception("错误了");
        }
    }
}
