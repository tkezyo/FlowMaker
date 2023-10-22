using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FlowMaker.SourceGenerator
{
    [Generator]
    public class StepProviderGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxContextReceiver is not ClientSyntax receiver)
            {
                return;
            }
            try
            {

                foreach (var item in receiver.SyntaxModels)
                {
                    Dictionary<string, (ISymbol, double, bool, bool)> actionList = new();
                    StringBuilder stringBuilder = new();
                    StringBuilder actionNameStringBuilder = new();
                    StringBuilder groupNameStringBuilder = new();
                    var classAttrs = item.Option.GetAttributes();
                    var ganttGroupAttrs = classAttrs.Where(c => c.AttributeClass.Name == "GanttGroupAttribute").ToList();

                    foreach (var action in item.Option.GetMembers())
                    {
                        var attrs = action.GetAttributes();
                        var ganttNameAttrs = attrs.Where(c => c.AttributeClass.Name == "GanttActionAttribute").ToList();
                        if (!ganttNameAttrs.Any())
                        {
                            continue;
                        }

                        bool needActionName = false;
                        bool needGroupName = false;

                        if (action is IMethodSymbol method)
                        {
                            if (method.Parameters.Length > 3 || method.Parameters.Length == 0)
                            {
                             
                                actionNameStringBuilder.AppendLine($@"        list.Add((""方法参数错误{method.Name}"", TimeSpan.FromSeconds(1)));");
                                continue;
                            }
                            else if (method.Parameters.Length == 1 && method.Parameters[0].Type.ToDisplayString() != "System.Threading.CancellationToken")
                            {
                            
                                actionNameStringBuilder.AppendLine($@"        list.Add((""方法参数错误{method.Name}"", TimeSpan.FromSeconds(1)));");
                                continue;
                            }
                            else if (method.Parameters.Length == 2 &&
                                (method.Parameters[0].Type.ToDisplayString() != "string" ||
                                method.Parameters[1].Type.ToDisplayString() != "System.Threading.CancellationToken"))
                            {
                             
                                actionNameStringBuilder.AppendLine($@"        list.Add((""方法参数错误{method.Name}"", TimeSpan.FromSeconds(1)));");
                                continue;
                            }
                            else if (method.Parameters.Length == 3 &&
                              (method.Parameters[0].Type.ToDisplayString() != "string" ||
                              method.Parameters[1].Type.ToDisplayString() != "string" ||
                              method.Parameters[2].Type.ToDisplayString() != "System.Threading.CancellationToken"))
                            {
                               
                                actionNameStringBuilder.AppendLine($@"        list.Add((""方法参数错误{method.Name}"", TimeSpan.FromSeconds(1)));");
                                continue;
                            }

                            if (method.Parameters.Length == 2)
                            {
                                needActionName = true;
                            }
                            else if (method.Parameters.Length == 3)
                            {
                                needActionName = true;
                                needGroupName = true;
                            }
                        }
                        foreach (var ganttNameAttr in ganttNameAttrs)
                        {
                            var ganttActionName = ganttNameAttr.ConstructorArguments[0].Value.ToString();
                            var waitTime = ganttNameAttr.ConstructorArguments[1].Value.ToString();

                            if (actionList.ContainsKey(ganttActionName))
                            {
                                
                                actionNameStringBuilder.AppendLine($@"        list.Add((""方法参数重复{ganttActionName}"", TimeSpan.FromSeconds(1)));");

                                continue;
                            }
                            actionList.Add(ganttActionName, (action, Convert.ToDouble(waitTime), needActionName, needGroupName));
                        }
                    }

                    foreach (var action in actionList)
                    {
                        stringBuilder.AppendLine($$"""
             case "{{action.Key}}":
                await {{action.Value.Item1.Name}}({{(action.Value.Item3 ? "name, " : "")}}{{(action.Value.Item4 ? "groupName, " : "")}}cancellationToken);
                break;
""");

                        actionNameStringBuilder.AppendLine($@"        list.Add((""{action.Key}"", TimeSpan.FromSeconds({action.Value.Item2})));");
                    }

                    foreach (var ganttGroupAttr in ganttGroupAttrs)
                    {
                        var ganttGroupName = ganttGroupAttr.ConstructorArguments[0].Value.ToString();
                        groupNameStringBuilder.AppendLine($"        list.Add(\"{ganttGroupName}\");");
                    }

                    string baseStr = $$"""
using Volo.Abp.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace {{item.Option.ContainingNamespace}};

public partial class {{item.Option.MetadataName}}
{

    /// <summary>
    ///
    /// </summary>
    public async Task RunService(string name, string? groupName, CancellationToken cancellationToken = default)
    {
        switch (name)
        {
{{stringBuilder}}
            default:
                break;
        }
    }
    public List<(string, TimeSpan)> GetActions()
    {
        List<(string, TimeSpan)> list = new();
        
{{actionNameStringBuilder}}
    
        return list;
    }
    public List<string> GetGroups()
    {
        List<string> list = new();
        
{{groupNameStringBuilder}}
    
        return list;
    }
}
""";

                    context.AddSource($"{item.Option.MetadataName}.g.cs", SourceText.From(baseStr, Encoding.UTF8));
                }
            }
            catch (Exception e)
            {

                throw;
            }

        }

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new ClientSyntax());
        }
        class ClientSyntax : ISyntaxContextReceiver
        {
            public List<SyntaxModel> SyntaxModels { get; set; } = new List<SyntaxModel>();

            public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
            {
                if (context.Node is ClassDeclarationSyntax ids)
                {
                    var commandOption = context.SemanticModel.GetDeclaredSymbol(ids) as INamedTypeSymbol;

                    var match = commandOption.AllInterfaces.Any(c => c.Name == "IStep");

                    if (match)
                    {
                        if (SyntaxModels.Any(c => c.Option.Name == commandOption.Name))
                        {
                            return;
                        }
                        SyntaxModels.Add(new SyntaxModel
                        {
                            Option = commandOption
                        });
                    }
                }
            }
        }
        class SyntaxModel
        {
            public ITypeSymbol Option { get; set; }
        }
    }
}
