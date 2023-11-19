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
                if (ids.BaseList is null)
                {
                    return false;
                }
                return ids.BaseList.Types.Any(c =>
                {
                    if (c.Type is IdentifierNameSyntax fff)
                    {
                        if (fff.Identifier.Text == "IStep")
                        {
                            return true;
                        }
                    }
                    if (c.Type is GenericNameSyntax ff)
                    {
                        if (ff.Identifier.Text == "IDataConverter" && ff.TypeArgumentList.Arguments.Any())
                        {
                            return true;
                        }
                    }
                    return false;
                });
                //if (ids.AttributeLists.Any(v => v.Attributes.Any(c =>
                //{
                //    if (c.Name is IdentifierNameSyntax ff && ff.Identifier.Text == "FlowStep" || (c.Name is GenericNameSyntax fc && fc.Identifier.Text == "FlowConverter"))
                //    {
                //        return true;
                //    }
                //    return false;
                //})))
                //{
                //    return true;
                //}
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
                var flowStep = item.Option.Interfaces.Any(c => c.Name == "IStep");
                var flowConverter = item.Option.Interfaces.Any(c => c.Name == "IDataConverter");

                if (flowStep)
                {
                    //var category = flowStep.ConstructorArguments[0].Value.ToString();
                    //var name = flowStep.ConstructorArguments[1].Value.ToString();

                    StringBuilder inputStringBuilder = new();
                    StringBuilder outputStringBuilder = new();

                    StringBuilder defStringBuilder = new();
                    StringBuilder outputDefStringBuilder = new();
                    List<string> props = new List<string>();
                    foreach (var member in item.Option.GetMembers())
                    {
                        if (member is IPropertySymbol property)
                        {
                            if (property.IsStatic)
                            {
                                continue;
                            }
                            var memberName = member.Name;
                            var propAttrs = property.GetAttributes();
                            var displayNameAttr = propAttrs.FirstOrDefault(c => c.AttributeClass.Name == "DisplayNameAttribute");
                            var input = propAttrs.FirstOrDefault(c => c.AttributeClass.Name == "InputAttribute");

                            var output = propAttrs.FirstOrDefault(c => c.AttributeClass.Name == "OutputAttribute");
                            if (input is null && output is null)
                            {
                                continue;
                            }
                            var displayName = memberName;

                            if (displayNameAttr is not null)
                            {
                                displayName = displayNameAttr.ConstructorArguments[0].Value.ToString();
                            }

                            var options = propAttrs.Where(c => c.AttributeClass.Name == "OptionAttribute").ToList();
                            var defaultValue = propAttrs.FirstOrDefault(c => c.AttributeClass.Name == "DefaultValueAttribute");
                            string defaultValueValue = string.Empty;
                            if (defaultValue is not null)
                            {
                                defaultValueValue = defaultValue.ConstructorArguments[0].Value.ToString();
                            }
                            defStringBuilder.AppendLine($$"""
        var {{property.Name}}Prop = new StepDataDefinition("{{property.Name}}", "{{displayName}}", "{{property.Type.ToDisplayString().Trim('?')}}", "{{defaultValueValue}}");
""");
                            if (input is not null)
                            {
                                defStringBuilder.AppendLine($$"""
        {{property.Name}}Prop.IsInput = true;
""");
                            }
                            if (output is not null)
                            {
                                defStringBuilder.AppendLine($$"""
        {{property.Name}}Prop.IsOutput = true;
""");
                            }
                            props.Add($"{property.Name}Prop");

                            if (options.Any())
                            {
                                foreach (var option in options)
                                {
                                    defStringBuilder.AppendLine($$"""
        {{property.Name}}Prop.Options.Add(new OptionDefinition("{{option.ConstructorArguments[0].Value}}", "{{option.ConstructorArguments[1].Value}}"));
""");
                                }
                            }
                            if (input is not null)
                            {
                                inputStringBuilder.AppendLine($$"""
        {{memberName}} = await IDataConverter<{{property.Type.ToDisplayString()}}>.GetValue(step.Inputs.First(v=> v.Name == "{{memberName}}"), serviceProvider, context, s => JsonSerializer.Deserialize<{{property.Type.ToDisplayString()}}>(s), cancellationToken);
""");

                            }


                            if (output is not null)
                            {
                                outputStringBuilder.AppendLine($$"""
        await IDataConverter.SetValue(step.Outputs.First(v=> v.Name == "{{memberName}}"), {{memberName}}, serviceProvider, context, cancellationToken);
""");
                            }
                        }
                    }

                    string baseStr = $@"using FlowMaker;
using FlowMaker.Models;
using System.Text.Json;

namespace {item.Option.ContainingNamespace};

#nullable enable

public partial class {item.Option.MetadataName}
{{
    public async Task WrapAsync(FlowContext context, StepContext stepContext, FlowStep step, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {{
{inputStringBuilder}
        await Run(context, stepContext, step, cancellationToken);

{outputStringBuilder}
    }}

    public static StepDefinition GetDefinition()
    {{
{defStringBuilder}
{outputDefStringBuilder}
        return new StepDefinition
        {{
            Category = {item.Option.MetadataName}.Category,
            Name = {item.Option.MetadataName}.Name,
            Data = [ {string.Join(", ", props)} ]
        }};
    }}
}}
#nullable restore
";

                    c.AddSource($"{item.Option.MetadataName}.s.g.cs", SourceText.From(baseStr, Encoding.UTF8));
                }
                if (flowConverter)
                {
                    //获取flowConverter中的泛型参数
                    //var type = flowConverter.AttributeClass.TypeArguments[0] as INamedTypeSymbol;

                    //var category = flowConverter.ConstructorArguments[0].Value.ToString();
                    //var name = flowConverter.ConstructorArguments[1].Value.ToString();

                    var type = item.Option.AllInterfaces.FirstOrDefault(c => c.Name == "IDataConverter");
                    if (type.TypeArguments.Any())
                    {
                        type = type.TypeArguments[0] as INamedTypeSymbol;
                    }
                    StringBuilder inputStringBuilder = new();

                    StringBuilder defStringBuilder = new();
                    List<string> inputs = new List<string>();
                    foreach (var member in item.Option.GetMembers())
                    {
                        if (member is IPropertySymbol property)
                        {
                            if (property.IsStatic)
                            {
                                continue;
                            }
                            var memberName = member.Name;
                            var propAttrs = property.GetAttributes();
                            var displayNameAttr = propAttrs.FirstOrDefault(c => c.AttributeClass.Name == "DisplayNameAttribute");
                            var input = propAttrs.FirstOrDefault(c => c.AttributeClass.Name == "InputAttribute");

                            var output = propAttrs.FirstOrDefault(c => c.AttributeClass.Name == "OutputAttribute");
                            if (input is null && output is null)
                            {
                                continue;
                            }
                            var displayName = memberName;

                            if (displayNameAttr is not null)
                            {
                                displayName = displayNameAttr.ConstructorArguments[0].Value.ToString();
                            }

                            var options = propAttrs.Where(c => c.AttributeClass.Name == "OptionAttribute").ToList();
                            var defaultValue = propAttrs.FirstOrDefault(c => c.AttributeClass.Name == "DefaultValueAttribute");
                            string defaultValueValue = string.Empty;
                            if (defaultValue is not null)
                            {
                                defaultValueValue = defaultValue.ConstructorArguments[0].Value.ToString();
                            }
                            defStringBuilder.AppendLine($$"""
        var {{property.Name}}Prop = new StepDataDefinition("{{property.Name}}", "{{displayName}}", "{{property.Type.ToDisplayString().Trim('?')}}", "{{defaultValueValue}}");
""");
                            if (input is not null)
                            {
                                defStringBuilder.AppendLine($$"""
        {{property.Name}}Prop.IsInput = true;
""");
                            }
                            if (output is not null)
                            {
                                defStringBuilder.AppendLine($$"""
        {{property.Name}}Prop.IsOutput = true;
""");
                            }
                            inputs.Add($"{property.Name}Prop");

                            if (options.Any())
                            {
                                foreach (var option in options)
                                {
                                    defStringBuilder.AppendLine($$"""
        {{property.Name}}Prop.Options.Add(new OptionDefinition("{{option.ConstructorArguments[0].Value}}", "{{option.ConstructorArguments[1].Value}}"));
""");
                                }
                            }
                            if (input is not null)
                            {
                                inputStringBuilder.AppendLine($$"""
        {{memberName}} = await IDataConverter<{{property.Type.ToDisplayString()}}>.GetValue(inputs.First(v=> v.Name == "{{memberName}}"), serviceProvider, context, s => JsonSerializer.Deserialize<{{property.Type.ToDisplayString()}}>(s), cancellationToken);
""");

                            }
                        }
                    }


                    string baseStr = $@"using FlowMaker;
using FlowMaker.Models;
using System.Text.Json;

namespace {item.Option.ContainingNamespace};

#nullable enable

partial class {item.Option.MetadataName}
{{
    public async Task<{type.ToDisplayString()}> WrapAsync(FlowContext context, IReadOnlyList<FlowInput> inputs, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {{
{inputStringBuilder}
        return await Convert(context, cancellationToken);
    }}

    public async Task<string> GetStringResultAsync(FlowContext context, IReadOnlyList<FlowInput> inputs, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {{
        var result  = await WrapAsync(context, inputs, serviceProvider, cancellationToken);
        return JsonSerializer.Serialize(result);
    }}


    public static ConverterDefinition GetDefinition()
    {{
{defStringBuilder}
        return new ConverterDefinition
        {{
            Category = {item.Option.MetadataName}.Category,
            Name = {item.Option.MetadataName}.Name,
            Output = ""{type.ToDisplayString()}"",
            Inputs = [ {string.Join(", ", inputs)} ]
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
