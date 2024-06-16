using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using System;
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
                var attires = item.Option.GetAttributes();
                var flowStep = item.Option.Interfaces.Any(c => c.Name == "IStep");
                var flowConverter = item.Option.Interfaces.Any(c => c.Name == "IDataConverter");

                if (flowStep)
                {
                    //var category = flowStep.ConstructorArguments[0].Value.ToString();
                    //var name = flowStep.ConstructorArguments[1].Value.ToString();

                    StringBuilder inputStringBuilder = new();
                    StringBuilder outputStringBuilder = new();

                    StringBuilder defStringBuilder = new();
                    List<string> props = [];
                    foreach (var member in item.Option.GetMembers())
                    {
                        if (member is IPropertySymbol property)
                        {
                            if (property.IsStatic)
                            {
                                continue;
                            }
                            var memberName = member.Name;

                            var propAttires = property.GetAttributes();
                            var displayNameAttr = propAttires.FirstOrDefault(c => c.AttributeClass.Name == "DescriptionAttribute");
                            var input = propAttires.FirstOrDefault(c => c.AttributeClass.Name == "InputAttribute");

                            var output = propAttires.FirstOrDefault(c => c.AttributeClass.Name == "OutputAttribute");
                            if (input is null && output is null)
                            {
                                continue;
                            }
                            var displayName = memberName;

                            if (displayNameAttr is not null)
                            {
                                displayName = displayNameAttr.ConstructorArguments[0].Value.ToString();
                            }

                            var options = propAttires.Where(c => c.AttributeClass.Name == "OptionAttribute").ToList();
                            var optionProviderAttr = propAttires.FirstOrDefault(c => c.AttributeClass.Name == "OptionProviderAttribute");
                            var defaultValue = propAttires.FirstOrDefault(c => c.AttributeClass.Name == "DefaultValueAttribute");
                            string defaultValueValue = string.Empty;
                            if (defaultValue is not null)
                            {
                                defaultValueValue = defaultValue.ConstructorArguments[0].Value.ToString();
                            }
                            defStringBuilder.AppendLine($$"""
        var {{property.Name}}Prop = new DataDefinition("{{property.Name}}", "{{displayName}}", "{{property.Type.ToDisplayString().Trim('?')}}", "{{defaultValueValue}}");
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
                            bool isArray = false;
                            string subType = string.Empty;
                            int rank = 0;

                            if (property.Type.TypeKind == TypeKind.Array && property.Type is IArrayTypeSymbol arrayTypeSymbol)
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
                                isArray = true;
                                subType = GetSubType(arrayTypeSymbol);
                                rank = arrayTypeSymbol.ToDisplayString().Count(c => c == '[');
                                defStringBuilder.AppendLine($$"""
        {{property.Name}}Prop.IsArray = true;        
        {{property.Name}}Prop.Rank = {{rank}};        
        {{property.Name}}Prop.SubType = "{{subType}}";
""");
                            }

                            if (property.Type is INamedTypeSymbol typeSymbol && typeSymbol.TypeKind == TypeKind.Enum)
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
        {{property.Name}}Prop.Options.Add(new OptionDefinition("{{enumDisplayName}}", $"{(int){{typeSymbol.Name}}.{{enumValue.Name}}}"));
""");
                                }
                            }
                            if (property.Type.SpecialType == SpecialType.System_Boolean)
                            {
                                defStringBuilder.AppendLine($$"""
        {{property.Name}}Prop.Options.Add(new OptionDefinition("是", "true"));
        {{property.Name}}Prop.Options.Add(new OptionDefinition("否", "false"));
""");
                            }

                            if (optionProviderAttr is not null && optionProviderAttr.AttributeClass.TypeArguments.Length > 0)
                            {
                                if (optionProviderAttr.AttributeClass.TypeArguments[0] is INamedTypeSymbol namedType)
                                {
                                    defStringBuilder.AppendLine($$"""
        {{property.Name}}Prop.OptionProviderName = {{namedType}}.Type + ":" + {{namedType}}.Name;
""");
                                }

                            }
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
                                if (isArray)
                                {
                                    inputStringBuilder.AppendLine($$"""
        var {{memberName}}Input = stepContext.Step.Inputs.First(v=> v.Name == nameof({{memberName}}));
        if ({{memberName}}Input.Mode == InputMode.Array)
        {
            {{memberName}} = ({{property.Type.ToDisplayString()}})IDataConverterInject.Reshape<{{subType}}>({{memberName}}Input.Dims, await IDataConverterInject.GetArrayValue<{{subType}}>({{memberName}}Input, serviceProvider, stepContext.FlowContext, s => JsonSerializer.Deserialize<{{subType}}>(s), cancellationToken));
        }
        else
        {
            {{memberName}} = await IDataConverterInject.GetValue<{{property.Type.ToDisplayString()}}>({{memberName}}Input, serviceProvider, stepContext.FlowContext, s => JsonSerializer.Deserialize<{{property.Type.ToDisplayString()}}>(s), cancellationToken);
        }
        stepContext.StepOnceStatus.Inputs.Add(new NameValue(nameof({{memberName}}), JsonSerializer.Serialize({{memberName}})));
""");
                                }
                                else
                                {
                                    if (property.Type.SpecialType == SpecialType.System_String)
                                    {
                                        inputStringBuilder.AppendLine($$"""
        {{memberName}} = await IDataConverterInject.GetValue<{{property.Type.ToDisplayString()}}>(stepContext.Step.Inputs.First(v=> v.Name == nameof({{memberName}})), serviceProvider, stepContext.FlowContext, s => s?.ToString(), cancellationToken);
        stepContext.StepOnceStatus.Inputs.Add(new NameValue(nameof({{memberName}}), JsonSerializer.Serialize({{memberName}})));
""");
                                    }
                                    else
                                    {
                                        inputStringBuilder.AppendLine($$"""
        {{memberName}} = await IDataConverterInject.GetValue<{{property.Type.ToDisplayString()}}>(stepContext.Step.Inputs.First(v=> v.Name == nameof({{memberName}})), serviceProvider, stepContext.FlowContext, s => JsonSerializer.Deserialize<{{property.Type.ToDisplayString()}}>(s), cancellationToken);
        stepContext.StepOnceStatus.Inputs.Add(new NameValue(nameof({{memberName}}), JsonSerializer.Serialize({{memberName}})));
""");
                                    }

                                }
                            }


                            if (output is not null)
                            {
                                outputStringBuilder.AppendLine($$"""
        await IDataConverterInject.SetValue(stepContext.Step.Outputs.First(v=> v.Name == nameof({{memberName}})), {{memberName}}, serviceProvider, stepContext.FlowContext, cancellationToken);
        stepContext.StepOnceStatus.Outputs.Add(new NameValue(nameof({{memberName}}), JsonSerializer.Serialize({{memberName}})));
""");
                            }
                        }
                    }

                    string baseStr = $@"using FlowMaker;
using System.Text.Json;
using Ty;

namespace {item.Option.ContainingNamespace};

#nullable enable

public partial class {item.Option.MetadataName}
{{
    public async Task WrapAsync(StepContext stepContext, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {{
{inputStringBuilder}
        await Run(stepContext, cancellationToken);

{outputStringBuilder}
    }}

    public static StepDefinition GetDefinition()
    {{
{defStringBuilder}
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
                    //获取 flowConverter中的泛型参数
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
                    List<string> inputs = [];
                    foreach (var member in item.Option.GetMembers())
                    {
                        if (member is IPropertySymbol property)
                        {
                            if (property.IsStatic)
                            {
                                continue;
                            }
                            var memberName = member.Name;
                            var propAttires = property.GetAttributes();
                            var displayNameAttr = propAttires.FirstOrDefault(c => c.AttributeClass.Name == "DescriptionAttribute");
                            var input = propAttires.FirstOrDefault(c => c.AttributeClass.Name == "InputAttribute");

                            var output = propAttires.FirstOrDefault(c => c.AttributeClass.Name == "OutputAttribute");
                            if (input is null && output is null)
                            {
                                continue;
                            }
                            var displayName = memberName;

                            if (displayNameAttr is not null)
                            {
                                displayName = displayNameAttr.ConstructorArguments[0].Value.ToString();
                            }

                            var options = propAttires.Where(c => c.AttributeClass.Name == "OptionAttribute").ToList();
                            var defaultValue = propAttires.FirstOrDefault(c => c.AttributeClass.Name == "DefaultValueAttribute");
                            string defaultValueValue = string.Empty;
                            if (defaultValue is not null)
                            {
                                defaultValueValue = defaultValue.ConstructorArguments[0].Value.ToString();
                            }
                            defStringBuilder.AppendLine($$"""
        var {{property.Name}}Prop = new DataDefinition("{{property.Name}}", "{{displayName}}", "{{property.Type.ToDisplayString().Trim('?')}}", "{{defaultValueValue}}");
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
using System.Text.Json;
using Ty;
using Ty.Module.Configs;

namespace {item.Option.ContainingNamespace};

#nullable enable

partial class {item.Option.MetadataName}
{{
    public async Task<{type.ToDisplayString()}> WrapAsync(FlowContext? context, IReadOnlyList<FlowInput> inputs, IServiceProvider serviceProvider, CancellationToken cancellationToken)
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
