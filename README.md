# FlowMaker

FlowMaker是一个基于.NET8的流程编辑执行框架

为您提供简洁API, 方便的中间件开发体验

- [x] 单步执行时将状态显示在Debug页面
- [x] 单步执行子步骤时需要重新赋值上下文
- [x] 停止单步执行
- [x] 简化调试页面
- [x] 优化流程菜单/调整流程与流程配置的编辑方式
- [ ] ~~添加历史日志~~

## 流程

FlowManager，可进行初始化，执行，停止，发送事件等操作。

IFlowProvider，可以对流程序列进行保存和读取。

## 步骤

1. 无参数步骤

```c#
public partial class MyClass : IStep
{
    public static string Category => "类别";

    public static string Name => "名称";

    public Task Run(StepContext stepContext, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
```

```c#
	//注册相关步骤
	hostApplicationBuilder.Services.AddFlowStep<MyClass>();
```



2. 有输入输出的步骤

```c#
public partial class MyClass : IStep
{
    public static string Category => "Test1";

    public static string Name => "输入输出各一个";

    [Input]
    public int Input { get; set; }
    [Output]
    public int Output { get; set; }
    
    public Task Run( StepContext stepContext, CancellationToken cancellationToken)
    {
        Output = Input * 2;
        return Task.CompletedTask;
    }
}
```

输入参数可以添加多种特性标签

```c#
    [DefaultValue("3")]
    [Description("属性2")]
    [Option("三", "3")]
    [Option("四", "4")]
    public int Prop2 { get; set; }
```

3. 将整个类转换为步骤，上下文和取消令牌分别为第一个参数与最后一个参数，如果不需要可以不填写。

```c#
    [Steps("Category")]
    public class AllStepMode
    {
        [Description("算法A")]//通过特性标签改变显示名称
        public (int, string) Test(int ss = 3, CancellationToken cancellationToken = default)
        {
            return (ss, "ss");
        }

        public int Test2(StepContext stepContext, DayOfWeek ss, CancellationToken cancellationToken)
        {
            return 1;
        }
    }
```

```c#
	//注册相关步骤
	hostApplicationBuilder.Services.AddScoped<AllStepMode>();
	hostApplicationBuilder.Services.AddAllStepModeFlowStep();
```



4. 将接口转换为步骤，与将整个类转换为步骤类似，但是输入中会多出配置具体实现的参数。

```c#
 [Steps("FFF")]
 public interface ITestStep
 {
     Task<int> Test(StepContext stepContext, int ss = 2);
 }
```

```c#
    //注册相关步骤
	hostApplicationBuilder.Services.AddITestStepFlowStep();
    hostApplicationBuilder.Services.AddKeyedScoped<ITestStep, TestStep1>("Test1");
    hostApplicationBuilder.Services.AddKeyedScoped<ITestStep, TestStep2>("Test2");
	//配置选项
    hostApplicationBuilder.Services.Configure<ITestStepInstanceOption>(c =>
    {
        c.Instances.Add(new FlowMaker.NameValue("Test1", "Test1"));
        c.Instances.Add(new FlowMaker.NameValue("Test2", "Test2"));
    });
```



5. 选项提供类，提供可变化的参数选项下拉框

```c#
    public partial class PortProvider : IOptionProvider<string>
    {
        public static string DisplayName => "串口";

        public async Task<IEnumerable<NameValue>> GetOptions()
        {
            await Task.CompletedTask;
            return [new("oo", "22"), new("oo22", "2211")];
        }
    }
```

```
	//注册选项提供类
	hostApplicationBuilder.Services.AddFlowOption<PortProvider>();
```

## 转换器

配置流程时可以对输入输出的信息进行加工，此时需要用到转换器，实现方式于步骤类似，但是根据转换器的类型有固定的返回类型

```c#
public partial class ValueConverter : IDataConverter<int>
{
    public static string Category => "Test1";

    public static string Name => "转换器1";

    [Input]
    public int Prop1 { get; set; }
    [Input]
    public int Prop2 { get; set; }

    public Task<int> Convert(FlowContext? context, CancellationToken cancellationToken)
    {
        return Task.FromResult(Prop1 + Prop2);
    }
}
```

```c#
	//注册转换器
	hostApplicationBuilder.Services.AddFlowConverter<ValueConverter>();
```



## 中间件

提供了5种中间件

1. IFlowMiddleware 流程的开始与结束
2. IStepMiddleware 步骤的开始与结束
3. IStepOnceMiddleware 步骤的单次执行时的开始与结束（如重复与重试）
4. IEventMiddleware 事件触发时
5. ILogMiddleware 记录日志时

```c#
public interface IFlowMiddleware
{
    Task OnExecuting(FlowContext flowContext, FlowState runnerState, CancellationToken cancellationToken);
    Task OnExecuted(FlowContext flowContext, FlowState runnerState, Exception? exception, CancellationToken cancellationToken);
}
public interface IStepMiddleware
{
    Task OnExecuting(FlowContext flowContext, FlowStep flowStep, StepStatus step, CancellationToken cancellationToken);
    Task OnExecuted(FlowContext flowContext, FlowStep flowStep, StepStatus step, Exception? exception, CancellationToken cancellationToken);
}
public interface IStepOnceMiddleware
{
    Task OnExecuting(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, CancellationToken cancellationToken);
    Task OnExecuted(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, Exception? exception, CancellationToken cancellationToken);
}
public interface IEventMiddleware
{
    Task OnExecuting(FlowContext flowContext, string eventName, string? eventData, CancellationToken cancellationToken);
}

public interface ILogMiddleware
{
    Task OnLog(FlowContext flowContext, FlowStep flowStep, StepStatus step, StepOnceStatus stepOnceStatus, LogInfo logInfo, CancellationToken cancellationToken);
}
```

## 待完善内容

- [x] 对一个类中的方法生成为步骤，可以使用Scope域实现一个流程过程中的状态保持。自动解析输入输出参数
  - [x] 方法生成步骤是需要匹配参数, 将FlowContext等加进去. 
  - [ ] 从代码备注中获取更多信息
  - [x] 对接口生成的步骤添加可选实例的参数
- [x] 实现流程内的分组功能
- [x] 子流程的日志问题
- [x] 对日志进行分类显示与保存
- [ ] 优化流程编辑方式
- [ ] 优化代码编辑流程的API（需要设置项过多，延后）
- [ ] 完善文档
- [ ] 测试
