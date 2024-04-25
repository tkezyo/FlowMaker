using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace FlowMaker.SourceGenerator
{
    [Generator]
    public class StepsGenerator : IIncrementalGenerator
    {
        private bool Condition(SyntaxNode node, CancellationToken cancellationToken)
        {
            if (node is ClassDeclarationSyntax ids)
            {
                if (ids.AttributeLists.Any(v => v.Attributes.Any(c =>
                {
                    if (c.Name is IdentifierNameSyntax ff && ff.Identifier.Text == "Steps")
                    {
                        return true;
                    }
                    return false;
                })))
                {
                    return true;
                }
            }
            return false;
        }
        private SyntaxModel Transform(GeneratorSyntaxContext context, CancellationToken cancellationToken)
        {
            var step = context.SemanticModel.GetDeclaredSymbol(context.Node) as INamedTypeSymbol;

            return new SyntaxModel
            {
                Option = step
            };
        }
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterSourceOutput(context.SyntaxProvider.CreateSyntaxProvider<SyntaxModel>(Condition, Transform), (c, item) =>
            {
                var attires = item.Option.GetAttributes();
                var category = attires.FirstOrDefault(v => v.AttributeClass?.Name == "StepsAttribute")?.ConstructorArguments.FirstOrDefault().Value?.ToString();

                foreach (var member in item.Option.GetMembers())
                {
                    if (member is IMethodSymbol methodSymbol)
                    {
                        //确保是 public
                        if (methodSymbol.DeclaredAccessibility != Accessibility.Public)
                        {
                            continue;
                        }
                        //确保是实例方法
                        if (methodSymbol.IsStatic)
                        {
                            continue;
                        }
                        //确保是方法
                        if (methodSymbol.MethodKind != MethodKind.Ordinary)
                        {
                            continue;
                        }
                        //获取所有参数
                        var parameters = methodSymbol.Parameters;

                        List<Input> inputs = new List<Input>();
                        List<Output> outputs = new List<Output>();

                        foreach (var parameter in parameters)
                        {
                            var input = new Input
                            {
                                Name = parameter.Name,
                                Type = parameter.Type.ToDisplayString(),
                                DefaultValue = parameter.HasExplicitDefaultValue ? parameter.ExplicitDefaultValue?.ToString() : null,
                                Description = parameter.GetAttributes().FirstOrDefault(v => v.AttributeClass?.Name == "DescriptionAttribute")?.ConstructorArguments.FirstOrDefault().Value?.ToString()
                            };
                            inputs.Add(input);
                        }
                        bool isVoid = methodSymbol.ReturnsVoid;
                        bool isTask = methodSymbol.ReturnType.Name == "Task";
                        //获取返回值，如果返回值是元组，则获取元组的所有元素
                        var returnType = methodSymbol.ReturnType;
                        if (returnType is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsTupleType)
                        {
                            var tupleElements = namedTypeSymbol.TupleElements;
                            foreach (var tupleElement in tupleElements)
                            {
                                var output = new Output
                                {
                                    Name = tupleElement.Name,
                                    Type = tupleElement.Type.ToDisplayString()
                                };
                                outputs.Add(output);
                            }
                        }
                        //如果是 void，则不需要返回值
                        else if (methodSymbol.ReturnsVoid)
                        {

                        }
                        //如果是没有泛型参数的 Task，则不需要返回值
                        else if (returnType is INamedTypeSymbol namedTypeSymbol1 && namedTypeSymbol1.Name == "Task")
                        {
                            //如果返回值是 Task，则获取 Task 的泛型参数
                            if (namedTypeSymbol1.TypeArguments.Length > 0)
                            {
                                returnType = namedTypeSymbol1.TypeArguments[0];
                                if (returnType is INamedTypeSymbol namedTypeSymbol3 && namedTypeSymbol3.IsTupleType)
                                {
                                    var tupleElements = namedTypeSymbol3.TupleElements;
                                    foreach (var tupleElement in tupleElements)
                                    {
                                        var output = new Output
                                        {
                                            Name = tupleElement.Name,
                                            Type = tupleElement.Type.ToDisplayString()
                                        };
                                        outputs.Add(output);
                                    }
                                }
                                else
                                {
                                    var output = new Output
                                    {
                                        Name = "Result",
                                        Type = returnType.ToDisplayString()
                                    };
                                    outputs.Add(output);
                                }
                            }
                            else
                            {
                                isVoid = true;
                            }
                        }
                        else
                        {
                            var output = new Output
                            {
                                Name = "Result",
                                Type = returnType.ToDisplayString()
                            };
                            outputs.Add(output);
                        }
                        var textInfo = new CultureInfo("en-US", false).TextInfo;


                        StringBuilder inputPropStringBuilder = new StringBuilder();
                        foreach (var input in inputs)
                        {
                            input.Name = textInfo.ToTitleCase(input.Name.ToLower());

                            inputPropStringBuilder.AppendLine("    [Input]");
                            //defaultValue
                            if (!string.IsNullOrEmpty(input.DefaultValue))
                            {
                                inputPropStringBuilder.AppendLine($"    [DefaultValue(\"{input.DefaultValue}\")]");
                            }
                            //description
                            if (!string.IsNullOrEmpty(input.Description))
                            {
                                inputPropStringBuilder.AppendLine($"    [Description(\"{input.Description}\")]");
                            }

                            inputPropStringBuilder.AppendLine($"    public {input.Type} {input.Name} {{ get; set; }}");
                        }

                        StringBuilder outputPropStringBuilder = new StringBuilder();
                        foreach (var output in outputs)
                        {
                            output.Name = textInfo.ToTitleCase(output.Name.ToLower());

                            outputPropStringBuilder.AppendLine("    [Output]");
                            outputPropStringBuilder.AppendLine($"    public {output.Type} {output.Name} {{ get; set; }}");
                        }


                        string outputString = string.Empty;
                        if (outputs.Count > 1)
                        {
                            outputString = string.Join("\n", outputs.Select(v => $"        {v.Name} = result.{v.Name};"));
                        }
                        else if (outputs.Count == 1)
                        {
                            outputString = $"        Result = result;";
                        }
                        else
                        {

                        }

                        string inputString = string.Empty;
                        if (inputs.Count > 1)
                        {
                            inputString = string.Join(",", inputs.Select(v => v.Name));
                        }
                        else if (inputs.Count == 1)
                        {
                            inputString = inputs[0].Name;
                        }




                        var source = new StringBuilder();
                        source.AppendLine($$"""
using FlowMaker;
using System.Text.Json;

namespace {{item.Option.ContainingNamespace}};

#nullable enable

public partial class {{item.Option.Name}}_{{methodSymbol.Name}}({{item.Option.Name}} _service): IStep
{
    public static string Category => "{{category}}";
    
    public static string Name => "{{methodSymbol.Name}}";
{{inputPropStringBuilder}}
{{outputPropStringBuilder}}
    public async Task Run(FlowContext context, StepContext stepContext, CancellationToken cancellationToken)
    {
        {{(isVoid ? string.Empty : "var result = ")}}{{(isTask ? "await " : "")}}_service.{{methodSymbol.Name}}({{inputString}});
{{outputString}}
        await Task.CompletedTask;
    }

    public async Task WrapAsync(FlowContext context, StepContext stepContext, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        await Run(context, stepContext, cancellationToken);

    }

    public static StepDefinition GetDefinition()
    {
        return new StepDefinition
        {
            Category = {{item.Option.Name}}_{{methodSymbol.Name}}.Category,
            Name = {{item.Option.Name}}_{{methodSymbol.Name}}.Name,
            Data = []
        };
    }
}

#nullable restore
""");

                        c.AddSource($"{item.Option.Name}_{methodSymbol.Name}.s.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));

                    }
                }
            });

        }
    }

    public class Input
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string DefaultValue { get; set; }
        public string Description { get; set; }
    }

    public class Output
    {
        public string Name { get; set; }
        public string Type { get; set; }
    }
}
