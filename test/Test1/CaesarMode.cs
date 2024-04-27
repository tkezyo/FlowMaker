using FlowMaker;
using Microsoft.Extensions.Options;
using System.ComponentModel;

namespace Test1
{
    [Steps("FFF")]
    public interface ITestStep
    {
        int Test(StepContext stepContext, int ss = 2);
    }

    public class TestStep1 : ITestStep
    {
        public int Test(StepContext stepContext, int ss = 2)
        {
            stepContext.AddLog("sdfw");
            return 1;
        }
    }

    public class TestStep2 : ITestStep
    {
        public int Test(StepContext stepContext, int ss = 2)
        {
            stepContext.AddLog("sdfw");
            return 2;
        }
    }

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
        public (int, string) Test(int ss = 3, CancellationToken cancellationToken = default)
        {
            return (ss, "ss");
        }

        public int Test2(StepContext stepContext, DayOfWeek ss, CancellationToken cancellationToken)
        {
            return 1;
        }

        public void Test3(StepContext stepContext, int ss)
        {

        }

        public Task Test4(int[][] ss)
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
