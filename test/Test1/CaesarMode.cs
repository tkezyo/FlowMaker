using FlowMaker;
using System.ComponentModel;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Text.Json;

namespace Test1
{
    [Steps("CaesarMode")]
    public class CaesarMode
    {
        public (int, string) Test(int ss)
        {
            return (ss, "ss");
        }

        public int Test2(int ss)
        {
            return ss;
        }

        public void Test3(int ss)
        {

        }

        public Task Test4(int ss)
        {
            return Task.CompletedTask;
        }
    }
}
