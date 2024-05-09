using FlowMaker;
using Microsoft.Extensions.Options;
using System.ComponentModel;

namespace Test1
{
    [Steps("FFF")]
    public interface ITestStep
    {
        Task<int> Test(StepContext stepContext, int ss = 2);
    }

    public class TestStep1 : ITestStep
    {
        public async Task<int> Test(StepContext stepContext, int ss = 2)
        {
            for (int i = 0; i < 10; i++)
            {
                await stepContext.Log("sdfw+1" + i);
            }
            await stepContext.Log("sdfw");
            return 1;
        }
    }

    public class TestStep2 : ITestStep
    {
        public async Task<int> Test(StepContext stepContext, int ss = 2)
        {
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(1);
                await stepContext.Log("sdfw+2" + i);
            }
            await stepContext.Log("sdfw");
            return Random.Shared.Next(0, 100);
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
        public (int, string) Test(StepContext stepContext, int ss = 3, CancellationToken cancellationToken = default)
        {
            _ = stepContext.Log(ss.ToString());
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

        public void Error(StepContext stepContext)
        {
            Thread.Sleep(1);

            throw new Exception("错误了");
        }
    }
}
