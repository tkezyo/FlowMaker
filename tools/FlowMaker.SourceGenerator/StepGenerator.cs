using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace FlowMaker.SourceGenerator
{
    [Generator]
    public class StepGenerator : IIncrementalGenerator
    {
        private bool Condition(SyntaxNode node, CancellationToken cancellationToken)
        {
            if (node is ClassDeclarationSyntax ids)
            {
                //判断ids是否继承了IStep
                //if (ids.BaseList is null)
                //{
                //    return false;
                //}
                //return ids.BaseList.Types.Any(c => c.Type is IdentifierNameSyntax ff && (ff.Identifier.Text == "IStep" || ff.Identifier.Text == "ICheckStep"));
                if (ids.AttributeLists.Any(v => v.Attributes.Any(c =>
                {
                    if (c.Name is IdentifierNameSyntax ff && ff.Identifier.Text == "FlowStep" || (c.Name is GenericNameSyntax fc && fc.Identifier.Text == "FlowConverter"))
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
                var attrs = item.Option.GetAttributes();
                var flowStep = attrs.FirstOrDefault(c => c.AttributeClass.Name == "FlowStepAttribute");
                var flowConverter = attrs.FirstOrDefault(c => c.AttributeClass.Name == "FlowConverterAttribute");

                if (flowStep is not null)
                {
                    var category = flowStep.ConstructorArguments[0].Value.ToString();
                    var name = flowStep.ConstructorArguments[1].Value.ToString();

                    StringBuilder inputStringBuilder = new();
                    StringBuilder outputStringBuilder = new();

                    StringBuilder inputDefStringBuilder = new();
                    StringBuilder outputDefStringBuilder = new();
                    List<string> inputs = new List<string>();
                    List<string> outputs = new List<string>();
                    foreach (var member in item.Option.GetMembers())
                    {
                        if (member is IPropertySymbol property)
                        {
                            var memberName = member.Name;
                            var propAttrs = property.GetAttributes();
                            var input = propAttrs.FirstOrDefault(c => c.AttributeClass.Name == "InputAttribute");
                            if (input is not null)
                            {
                                var inputName = input.ConstructorArguments[0].Value.ToString();

                                var options = propAttrs.Where(c => c.AttributeClass.Name == "OptionAttribute").ToList();
                                var defaultValue = propAttrs.FirstOrDefault(c => c.AttributeClass.Name == "DefaultValueAttribute");
                                string defaultValueValue = string.Empty;
                                if (defaultValue is not null)
                                {
                                    defaultValueValue = defaultValue.ConstructorArguments[0].Value.ToString();
                                }

                                inputStringBuilder.AppendLine($$"""
        {{memberName}} = await IFlowValueConverter<{{property.Type.ToDisplayString()}}>.GetValue(step.Inputs.First(v=> v.Name == "{{memberName}}"), serviceProvider, context, s => JsonSerializer.Deserialize<{{property.Type.ToDisplayString()}}>(s), cancellationToken);
""");
                                inputDefStringBuilder.AppendLine($$"""
        var {{property.Name}}InputProp = new StepInputDefinition("{{property.Name}}", "{{inputName}}", "{{property.Type.ToDisplayString().Trim()}}", "{{defaultValueValue}}");
""");
                                inputs.Add($"{property.Name}InputProp");

                                if (options.Any())
                                {
                                    foreach (var option in options)
                                    {
                                        inputDefStringBuilder.AppendLine($$"""
        {{property.Name}}InputProp.Options.Add(new OptionDefinition("{{option.ConstructorArguments[0].Value}}", "{{option.ConstructorArguments[1].Value}}"));
""");
                                    }
                                }
                            }


                            var output = propAttrs.FirstOrDefault(c => c.AttributeClass.Name == "OutputAttribute");
                            if (output is not null)
                            {
                                var outputName = output.ConstructorArguments[0].Value.ToString();

                                outputStringBuilder.AppendLine($$"""
        await IFlowValueConverter.SetValue(step.Outputs.First(v=> v.Name == "{{memberName}}"), {{memberName}}, serviceProvider, context, cancellationToken);
""");

                                outputDefStringBuilder.AppendLine($$"""
        var {{property.Name}}OutputProp = new StepOutputDefinition("{{property.Name}}", "{{outputName}}", "{{property.Type.ToDisplayString()}}");
""");
                                outputs.Add($"{property.Name}OutputProp");
                            }
                        }
                    }

                    string baseStr = $@"using FlowMaker;
using FlowMaker.Models;
using System.Text.Json;

namespace {item.Option.ContainingNamespace};

#nullable enable

public partial class {item.Option.MetadataName} : IStep
{{
    public static string Category => ""{category}"";

    public static string Name => ""{name}"";

    public async Task WrapAsync(FlowContext context, StepContext stepContext, FlowStep step, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {{
{inputStringBuilder}
        await Run(context, stepContext, step, cancellationToken);

{outputStringBuilder}
    }}

    public static StepDefinition GetDefinition()
    {{
{inputDefStringBuilder}
{outputDefStringBuilder}
        return new StepDefinition
        {{
            Category = ""{category}"",
            DisplayName = ""{name}"",
            Name = ""{item.Option.ContainingNamespace}.{item.Option.Name}"",
            Type = typeof({item.Option.Name}),
            Inputs = new List<StepInputDefinition>
            {{
                {string.Join(", ", inputs)}
            }},
            Outputs = new List<StepOutputDefinition>
            {{
                {string.Join(", ", outputs)}
            }}
        }};
    }}
}}
#nullable restore
";

                    c.AddSource($"{item.Option.MetadataName}.s.g.cs", SourceText.From(baseStr, Encoding.UTF8));
                }
                if (flowConverter is not null)
                {
                    //获取flowConverter中的泛型参数
                    var type = flowConverter.AttributeClass.TypeArguments[0] as INamedTypeSymbol;

                    var category = flowConverter.ConstructorArguments[0].Value.ToString();
                    var name = flowConverter.ConstructorArguments[1].Value.ToString();

                    StringBuilder inputStringBuilder = new();

                    StringBuilder inputDefStringBuilder = new();
                    List<string> inputs = new List<string>();
                    foreach (var member in item.Option.GetMembers())
                    {
                        if (member is IPropertySymbol property)
                        {
                            var memberName = member.Name;
                            var propAttrs = property.GetAttributes();
                            var input = propAttrs.FirstOrDefault(c => c.AttributeClass.Name == "InputAttribute");
                            if (input is not null)
                            {
                                var inputName = input.ConstructorArguments[0].Value.ToString();

                                var options = propAttrs.Where(c => c.AttributeClass.Name == "OptionAttribute").ToList();
                                var defaultValue = propAttrs.FirstOrDefault(c => c.AttributeClass.Name == "DefaultValueAttribute");

                               
                                inputStringBuilder.AppendLine($$"""
        {{memberName}} = await IFlowValueConverter<{{property.Type.ToDisplayString()}}>.GetValue(inputs.First(v=> v.Name == "{{memberName}}"), serviceProvider, context, s => System.Text.Json.JsonSerializer.Deserialize<{{property.Type.ToDisplayString()}}>(s), cancellationToken);
""");
                                inputDefStringBuilder.AppendLine($$"""
        var {{property.Name}}InputProp = new StepInputDefinition("{{property.Name}}", "{{inputName}}", "{{property.Type.ToDisplayString().Trim()}}", "{{defaultValue}}");
""");
                                inputs.Add($"{property.Name}InputProp");

                                if (options.Any())
                                {
                                    foreach (var option in options)
                                    {
                                        inputDefStringBuilder.AppendLine($$"""
        {{property.Name}}InputProp.Options.Add(new OptionDefinition("{{option.ConstructorArguments[0].Value}}", "{{option.ConstructorArguments[1].Value}}"));
""");
                                    }
                                }
                            }
                        }
                    }


                    string baseStr = $@"using FlowMaker;
using FlowMaker.Models;
using System.Text.Json;

namespace {item.Option.ContainingNamespace};

#nullable enable

partial class {item.Option.MetadataName} : IFlowValueConverter<{type.ToDisplayString()}>
{{
    public static string Category => ""{category}"";

    public static string Name => ""{name}"";

    public async Task<{type.ToDisplayString()}> WrapAsync(FlowContext context, IReadOnlyList<FlowInput> inputs, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {{
{inputStringBuilder}
        return await Convert(context, inputs, cancellationToken);
    }}

    public async Task<string> GetStringResultAsync(FlowContext context, IReadOnlyList<FlowInput> inputs, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {{
        var result  = await WrapAsync(context, inputs, serviceProvider, cancellationToken);
        return JsonSerializer.Serialize(result);
    }}


    public static ConverterDefinition GetDefinition()
    {{
{inputDefStringBuilder}
        return new ConverterDefinition
        {{
            Category = ""{category}"",
            DisplayName = ""{name}"",
            Name = ""{item.Option.ContainingNamespace}.{item.Option.Name}"",
            Type = typeof({item.Option.Name}),
            Output = ""{type.ToDisplayString()}"",
            Inputs = new List<StepInputDefinition>
            {{
                {string.Join(", ", inputs)}
            }}
        }};
    }}
}}
#nullable restore
";

                    c.AddSource($"{item.Option.MetadataName}.c.g.cs", SourceText.From(baseStr, Encoding.UTF8));
                }
            });

        }
    }
    public class SyntaxModel
    {
        public ITypeSymbol Option { get; set; }
    }
}
