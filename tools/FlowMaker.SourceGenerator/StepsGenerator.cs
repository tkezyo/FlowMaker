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
            if (node is InterfaceDeclarationSyntax iids)
            {
                if (iids.AttributeLists.Any(v => v.Attributes.Any(c =>
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

                StringBuilder stringBuilder = new();
                StringBuilder extensionBuilder = new();
                //item.Option是否为接口
                var isInterface = item.Option.TypeKind == TypeKind.Interface;
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

                        //获取Description标签的描述
                        var description = methodSymbol.GetAttributes().FirstOrDefault(v => v.AttributeClass?.Name == "DescriptionAttribute")?.ConstructorArguments.FirstOrDefault().Value?.ToString();

                        if (string.IsNullOrEmpty(description))
                        {
                            description = methodSymbol.Name;
                        }
                        string stepContext = string.Empty;
                        string cancellationToken = string.Empty;

                        if (parameters.Length > 0)
                        {
                            if (parameters[0].Type.ToDisplayString() == "FlowMaker.StepContext")
                            {
                                stepContext = parameters[0].Name;
                            }

                            //如果最后一个参数是 CancellationToken，则认为是 CancellationToken
                            if (parameters[parameters.Length - 1].Type.ToDisplayString() == "System.Threading.CancellationToken")
                            {
                                cancellationToken = parameters[parameters.Length - 1].Name;
                            }
                        }

                        foreach (var parameter in parameters)
                        {
                            //如果是 StepContext 或者 CancellationToken，则跳过
                            if (parameter.Type.ToDisplayString() == "FlowMaker.StepContext" || parameter.Type.ToDisplayString() == "System.Threading.CancellationToken")
                            {
                                continue;
                            }

                            var input = new Input
                            {
                                Name = parameter.Name,
                                Type = parameter.Type,
                                DefaultValue = parameter.HasExplicitDefaultValue ? parameter.ExplicitDefaultValue?.ToString() : null,
                                Description = parameter.GetAttributes().FirstOrDefault(v => v.AttributeClass?.Name == "DescriptionAttribute")?.ConstructorArguments.FirstOrDefault().Value?.ToString()
                            };
                            if (string.IsNullOrEmpty(input.Description))
                            {
                                input.Description = input.Name;
                            }
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
                                    Type = tupleElement.Type
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
                                            Type = tupleElement.Type
                                        };
                                        outputs.Add(output);
                                    }
                                }
                                else
                                {
                                    var output = new Output
                                    {
                                        Name = "Result",
                                        Type = returnType
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
                                Type = returnType
                            };
                            outputs.Add(output);
                        }
                        var textInfo = new CultureInfo("en-US", false).TextInfo;


                        StringBuilder inputPropStringBuilder = new();
                        StringBuilder outputPropStringBuilder = new();

                        StringBuilder inputStringBuilder = new();
                        StringBuilder outputStringBuilder = new();

                        StringBuilder defStringBuilder = new();
                        StringBuilder outputDefStringBuilder = new();
                        List<string> props = [];

                        if (isInterface)
                        {
                            inputPropStringBuilder.AppendLine("    [Input]");
                            inputPropStringBuilder.AppendLine($"    [OptionProvider({item.Option.Name}InstanceProvider.FullName)]");
                            inputPropStringBuilder.AppendLine($"    public string InstanceProvider {{ get; set; }}");
                            defStringBuilder.AppendLine($$"""
        var InstanceProviderProp = new DataDefinition("InstanceProvider", "实例", "string", "");
        InstanceProviderProp.IsInput = true;
        InstanceProviderProp.OptionProviderName = {{item.Option.Name}}InstanceProvider.Type + ":" + {{item.Option.Name}}InstanceProvider.Name;;
""");

                            inputStringBuilder.AppendLine($$"""
        InstanceProvider = await IDataConverterInject.GetValue<string>(stepContext.Step.Inputs.First(v=> v.Name == nameof(InstanceProvider)), serviceProvider, stepContext.FlowContext, s => s?.ToString(), cancellationToken);
        stepContext.StepOnceStatus.Inputs.Add(new NameValue(nameof(InstanceProvider), JsonSerializer.Serialize(InstanceProvider)));
""");
                            props.Add("InstanceProviderProp");
                        }

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

                            inputPropStringBuilder.AppendLine($"    public {input.Type.ToDisplayString()} {input.Name} {{ get; set; }}");


                            defStringBuilder.AppendLine($$"""
        var {{input.Name}}Prop = new DataDefinition("{{input.Name}}", "{{input.Description}}", "{{input.Type.ToDisplayString()}}", "{{input.DefaultValue}}");
        {{input.Name}}Prop.IsInput = true;
""");

                            bool isArray = false;
                            string subType = string.Empty;


                            if (input.Type.TypeKind == TypeKind.Array && input.Type is IArrayTypeSymbol arrayTypeSymbol)
                            {
                                string GetSubType(IArrayTypeSymbol arrayType)
                                {
                                    if (arrayType.ElementType is IArrayTypeSymbol subArrayType)
                                    {
                                        return GetSubType(subArrayType);
                                    }
                                    else
                                    {
                                        return arrayType.ElementType.ToDisplayString();
                                    }
                                }
                                subType = GetSubType(arrayTypeSymbol);
                                var rank = arrayTypeSymbol.ToDisplayString().Count(c => c == '[');
                                defStringBuilder.AppendLine($$"""
        {{input.Name}}Prop.IsArray = true;        
        {{input.Name}}Prop.Rank = {{rank}};        
        {{input.Name}}Prop.SubType = "{{subType}}";
""");
                            }//如果是布尔类型
                            if (input.Type.SpecialType == SpecialType.System_Boolean)
                            {
                                defStringBuilder.AppendLine($$"""
        {{input.Name}}Prop.Options.Add(new OptionDefinition("是", "true"));
        {{input.Name}}Prop.Options.Add(new OptionDefinition("否", "false"));
""");
                            }


                            if (isArray)
                            {
                                inputStringBuilder.AppendLine($$"""
        var {{input.Name}}Input = stepContext.Step.Inputs.First(v=> v.Name == nameof({{input.Name}}));
        if ({{input.Name}}Input.Mode == InputMode.Array)
        {
            {{input.Name}} = ({{input.Type.ToDisplayString()}})IDataConverterInject.Reshape<{{subType}}>({{input.Name}}Input.Dims, await IDataConverterInject.GetArrayValue<{{subType}}>({{input.Name}}Input, serviceProvider, stepContext.FlowContext, s => JsonSerializer.Deserialize<{{subType}}>(s), cancellationToken));
        }
        else
        {
            {{input.Name}} = await IDataConverterInject.GetValue<{{input.Type.ToDisplayString()}}>({{input.Name}}Input, serviceProvider, stepContext.FlowContext, s => JsonSerializer.Deserialize<{{input.Type.ToDisplayString()}}>(s), cancellationToken);
        }
        stepContext.StepOnceStatus.Inputs.Add(new NameValue(nameof({{input.Name}}), JsonSerializer.Serialize({{input.Name}})));
""");
                            }
                            else
                            {
                                if (input.Type.SpecialType == SpecialType.System_String)
                                {
                                    inputStringBuilder.AppendLine($$"""
        {{input.Name}} = await IDataConverterInject.GetValue<{{input.Type.ToDisplayString()}}>(stepContext.Step.Inputs.First(v=> v.Name == nameof({{input.Name}})), serviceProvider, stepContext.FlowContext, s => s?.ToString(), cancellationToken);
        stepContext.StepOnceStatus.Inputs.Add(new NameValue(nameof({{input.Name}}), JsonSerializer.Serialize({{input.Name}})));
""");
                                }
                                else
                                {
                                    inputStringBuilder.AppendLine($$"""
        {{input.Name}} = await IDataConverterInject.GetValue<{{input.Type.ToDisplayString()}}>(stepContext.Step.Inputs.First(v=> v.Name == nameof({{input.Name}})), serviceProvider, stepContext.FlowContext, s => JsonSerializer.Deserialize<{{input.Type.ToDisplayString()}}>(s), cancellationToken);
        stepContext.StepOnceStatus.Inputs.Add(new NameValue(nameof({{input.Name}}), JsonSerializer.Serialize({{input.Name}})));
""");
                                }

                            }

                            if (input.Type is INamedTypeSymbol typeSymbol && typeSymbol.TypeKind == TypeKind.Enum)
                            {
                                var enumValues = typeSymbol.GetMembers().Where(c => c.Kind == SymbolKind.Field).ToList();
                                foreach (var enumValue in enumValues)
                                {
                                    var enumAttires = enumValue.GetAttributes();
                                    var enumDisplayNameAttr = enumAttires.FirstOrDefault(c => c.AttributeClass.Name == "DescriptionAttribute");
                                    var enumDisplayName = enumValue.Name;
                                    if (enumDisplayNameAttr is not null)
                                    {
                                        enumDisplayName = enumDisplayNameAttr.ConstructorArguments[0].Value.ToString();
                                    }
                                    defStringBuilder.AppendLine($$"""
        {{input.Name}}Prop.Options.Add(new OptionDefinition("{{enumDisplayName}}", $"{(int){{typeSymbol.Name}}.{{enumValue.Name}}}"));
""");
                                }
                            }

                            props.Add($"{input.Name}Prop");
                        }

                        foreach (var output in outputs)
                        {
                            output.Name = textInfo.ToTitleCase(output.Name.ToLower());

                            outputPropStringBuilder.AppendLine("    [Output]");
                            outputPropStringBuilder.AppendLine($"    public {output.Type} {output.Name} {{ get; set; }}");

                            defStringBuilder.AppendLine($$"""
        var {{output.Name}}Prop = new DataDefinition("{{output.Name}}", "{{output.Name}}", "{{output.Type}}", "");
        {{output.Name}}Prop.IsOutput = true;
""");
                            if (output.Type.TypeKind == TypeKind.Array && output.Type is IArrayTypeSymbol arrayTypeSymbol)
                            {
                                string GetSubType(IArrayTypeSymbol arrayType)
                                {
                                    if (arrayType.ElementType is IArrayTypeSymbol subArrayType)
                                    {
                                        return GetSubType(subArrayType);
                                    }
                                    else
                                    {
                                        return arrayType.ElementType.ToDisplayString();
                                    }
                                }
                                var subType = GetSubType(arrayTypeSymbol);
                                var rank = arrayTypeSymbol.ToDisplayString().Count(c => c == '[');
                                defStringBuilder.AppendLine($$"""
        {{output.Name}}Prop.IsArray = true;        
        {{output.Name}}Prop.Rank = {{rank}};        
        {{output.Name}}Prop.SubType = "{{subType}}";
""");
                            }


                            outputStringBuilder.AppendLine($$"""
        await IDataConverterInject.SetValue(stepContext.Step.Outputs.First(v=> v.Name == nameof({{output.Name}})), {{output.Name}}, serviceProvider, stepContext.FlowContext, cancellationToken);
        stepContext.StepOnceStatus.Outputs.Add(new NameValue(nameof({{output.Name}}), JsonSerializer.Serialize({{output.Name}})));
""");

                            props.Add($"{output.Name}Prop");
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

                        List<string> inputStrings = new();
                        if (!string.IsNullOrEmpty(stepContext))
                        {
                            inputStrings.Add(stepContext);
                        }
                        foreach (var input in inputs)
                        {
                            inputStrings.Add(input.Name);
                        }
                        if (!string.IsNullOrEmpty(cancellationToken))
                        {
                            inputStrings.Add(cancellationToken);
                        }


                        string inputString = string.Join(", ", inputStrings);



                        extensionBuilder.AppendLine($$"""
                                    serviceDescriptors.AddFlowStep<{{item.Option.Name}}_{{methodSymbol.Name}}>();
                            """);
                       

                        if (!isInterface)
                        {
                            stringBuilder.AppendLine($$"""
public partial class {{item.Option.Name}}_{{methodSymbol.Name}}({{item.Option.Name}} _service): IStep
{
    public static string Category => "{{category}}";
    
    public static string Name => "{{description}}";
{{inputPropStringBuilder}}
{{outputPropStringBuilder}}
    public async Task Run(StepContext stepContext, CancellationToken cancellationToken)
    {
       {{(isVoid ? string.Empty : "var result = ")}}{{(isTask ? "await " : "await Task.Run(() => ")}}_service.{{methodSymbol.Name}}({{inputString}}){{(!isTask ? ")" : "")}};
{{outputString}}
        await Task.CompletedTask;
    }

    public async Task WrapAsync(StepContext stepContext, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
{{inputStringBuilder}}
        await Run(stepContext, cancellationToken);

{{outputStringBuilder}}
    }

    public static StepDefinition GetDefinition()
    {
{{defStringBuilder}}
        return new StepDefinition
        {
            Category = {{item.Option.Name}}_{{methodSymbol.Name}}.Category,
            Name = {{item.Option.Name}}_{{methodSymbol.Name}}.Name,
            Data = [ {{string.Join(", ", props)}} ]
        };
    }
}

""");
                        }
                        else
                        {
                            stringBuilder.AppendLine($$"""
public partial class {{item.Option.Name}}_{{methodSymbol.Name}}(IServiceProvider _serviceProvider): IStep
{
    public static string Category => "{{category}}";
    
    public static string Name => "{{description}}";
{{inputPropStringBuilder}}
{{outputPropStringBuilder}}
    public async Task Run(StepContext stepContext, CancellationToken cancellationToken)
    {
        {{item.Option.Name}} _service = null;
        if (string.IsNullOrEmpty(InstanceProvider))
        {
            _service = _serviceProvider.GetRequiredService<{{item.Option.Name}}>();
        }
        else
        {
            _service = _serviceProvider.GetRequiredKeyedService<{{item.Option.Name}}>(InstanceProvider);
        }
        {{(isVoid ? string.Empty : "var result = ")}}{{(isTask ? "await " : "await Task.Run(() => ")}}_service.{{methodSymbol.Name}}({{inputString}}){{(!isTask ? ")" : "")}};
{{outputString}}
        await Task.CompletedTask;
    }

    public async Task WrapAsync(StepContext stepContext, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
{{inputStringBuilder}}
        await Run(stepContext, cancellationToken);

{{outputStringBuilder}}
    }

    public static StepDefinition GetDefinition()
    {
{{defStringBuilder}}
        return new StepDefinition
        {
            Category = {{item.Option.Name}}_{{methodSymbol.Name}}.Category,
            Name = {{item.Option.Name}}_{{methodSymbol.Name}}.Name,
            Data = [ {{string.Join(", ", props)}} ]
        };
    }
}

""");
                        }



                    }
                }
                string optionString = null;
                if (isInterface)
                {

                    extensionBuilder.AppendLine($$"""
                                    serviceDescriptors.AddConfigOptionProvider<{{item.Option.Name}}InstanceProvider>();
                            """);
                }
                if (isInterface)
                {
                    optionString = $$"""
                public class {{item.Option.Name}}InstanceOption
                {
                    public List<NameValue> Instances { get; set; } = [];
                }
                public partial class {{item.Option.Name}}InstanceProvider(IOptions<{{item.Option.Name}}InstanceOption> {{item.Option.Name}}InstanceOption) : IOptionProvider<string>
                {
                    public static string DisplayName => "{{category}}";
                
                    public const string FullName = "string:{{item.Option.ContainingNamespace}}.{{item.Option.Name}}InstanceProvider";
                    public static string Name => typeof({{item.Option.Name}}InstanceProvider).FullName ?? string.Empty;
                    public static string Type => "string";
                
                    public async Task<IEnumerable<NameValue>> GetOptions()
                    {
                        await Task.CompletedTask;
                        return {{item.Option.Name}}InstanceOption.Value.Instances;
                    }
                }
                """;
                }
             
                

                var source = new StringBuilder();
                source.AppendLine($$"""
using FlowMaker;
using System.Text.Json;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ty.Module.Configs;
using Ty;

namespace {{item.Option.ContainingNamespace}};

#nullable disable

{{stringBuilder}}


public static partial class FlowMakerExtension
{
    public static void Add{{item.Option.Name}}FlowStep(this IServiceCollection serviceDescriptors)
    {
{{extensionBuilder}}
    }
}

{{optionString}}

#nullable restore
""");

                c.AddSource($"{item.Option.Name}.s.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
            });

        }
    }

    public class Input
    {
        public string Name { get; set; }
        public ITypeSymbol Type { get; set; }
        public string DefaultValue { get; set; }
        public string Description { get; set; }


    }

    public class Output
    {
        public string Name { get; set; }
        public ITypeSymbol Type { get; set; }
    }
}
