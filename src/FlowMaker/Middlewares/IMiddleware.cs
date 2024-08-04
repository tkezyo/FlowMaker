using Microsoft.Extensions.DependencyInjection;

namespace FlowMaker.Middlewares;

public delegate Task MiddlewareDelegate<TContext>(TContext context, CancellationToken cancellationToken);
public interface IMiddleware<TContext>
{
    static string Name { get; } = string.Empty;
    /// <summary>
    /// 执行中间件
    /// </summary>
    /// <param name="next">下一个中间件</param>
    /// <param name="context">上下文</param>
    /// <returns></returns>
    Task InvokeAsync(MiddlewareDelegate<TContext> next, TContext context, CancellationToken cancellationToken);
}

/// <summary>
/// 应用程序创建者
/// </summary>
/// <param name="appServices"></param>
/// <param name="fallbackHandler">回退处理者</param>
public class MiddlewareBuilder<TContext>(IServiceProvider appServices, MiddlewareDelegate<TContext> fallbackHandler)
{
    private readonly MiddlewareDelegate<TContext> fallbackHandler = fallbackHandler;
    private readonly List<Func<MiddlewareDelegate<TContext>, MiddlewareDelegate<TContext>>> middlewares = [];

    /// <summary>
    /// 获取服务提供者
    /// </summary>
    public IServiceProvider ApplicationServices { get; } = appServices;

    /// <summary>
    /// 应用程序创建者
    /// </summary>
    /// <param name="appServices"></param>
    public MiddlewareBuilder(IServiceProvider appServices)
        : this(appServices, (context, ct) => Task.CompletedTask)
    {
    }

    /// <summary>
    /// 创建处理应用请求的委托
    /// </summary>
    /// <returns></returns>
    public MiddlewareDelegate<TContext> Build()
    {
        var handler = fallbackHandler;
        for (var i = middlewares.Count - 1; i >= 0; i--)
        {
            handler = middlewares[i](handler);
        }
        return handler;
    }


    /// <summary>
    /// 使用默认配制创建新的PipelineBuilder
    /// </summary>
    /// <returns></returns>
    public MiddlewareBuilder<TContext> New()
    {
        return new MiddlewareBuilder<TContext>(ApplicationServices, fallbackHandler);
    }

    /// <summary>
    /// 条件中间件
    /// </summary>
    /// <param name="predicate"></param>
    /// <param name="handler"></param> 
    /// <returns></returns>
    public MiddlewareBuilder<TContext> When(Func<TContext, bool> predicate, MiddlewareDelegate<TContext> handler)
    {
        return Use(next => async (context, ct) =>
        {
            if (predicate(context))
            {
                await handler(context, ct);
            }
            else
            {
                await next(context, ct);
            }
        });
    }


    /// <summary>
    /// 条件中间件
    /// </summary>
    /// <param name="predicate"></param>
    /// <param name="configureAction"></param>
    /// <returns></returns>
    public MiddlewareBuilder<TContext> When(Func<TContext, bool> predicate, Action<MiddlewareBuilder<TContext>> configureAction)
    {
        return Use(next => async (context, ct) =>
        {
            if (predicate(context))
            {
                var branchBuilder = New();
                configureAction(branchBuilder);
                await branchBuilder.Build().Invoke(context, ct);
            }
            else
            {
                await next(context, ct);
            }
        });
    }
    /// <summary>
    /// 使用中间件
    /// </summary>
    /// <typeparam name="name"></typeparam>
    /// <returns></returns>
    public MiddlewareBuilder<TContext> Use(string name)
    {
        var middleware = ApplicationServices.GetKeyedService<IMiddleware<TContext>>(name);
        if (middleware == null)
        {
            return this;
        }
        return Use(middleware);
    }
    /// <summary>
    /// 使用中间件
    /// </summary>
    /// <typeparam name="TMiddleware"></typeparam>
    /// <returns></returns>
    public MiddlewareBuilder<TContext> Use<TMiddleware>()
        where TMiddleware : IMiddleware<TContext>
    {
        var middleware = ActivatorUtilities.GetServiceOrCreateInstance<TMiddleware>(ApplicationServices);
        return Use(middleware);
    }

    /// <summary>
    /// 使用中间件
    /// </summary> 
    /// <typeparam name="TMiddleware"></typeparam> 
    /// <param name="middleware"></param>
    /// <returns></returns>
    public MiddlewareBuilder<TContext> Use<TMiddleware>(TMiddleware middleware)
        where TMiddleware : IMiddleware<TContext>
    {
        return Use(middleware.InvokeAsync);
    }

    /// <summary>
    /// 使用中间件
    /// </summary>  
    /// <param name="middleware"></param>
    /// <returns></returns>
    public MiddlewareBuilder<TContext> Use(Func<MiddlewareDelegate<TContext>, TContext, CancellationToken, Task> middleware)
    {
        return Use(next => (context, ct) => middleware(next, context, ct));
    }

    /// <summary>
    /// 使用中间件
    /// </summary>
    /// <param name="middleware"></param>
    /// <returns></returns>
    public MiddlewareBuilder<TContext> Use(Func<MiddlewareDelegate<TContext>, MiddlewareDelegate<TContext>> middleware)
    {
        middlewares.Add(middleware);
        return this;
    }
}