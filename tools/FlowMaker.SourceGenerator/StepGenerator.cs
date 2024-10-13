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
        private static readonly DiagnosticDescriptor unsupportedTypeDiagnosticDescriptor = new(
                id: "FM0001",
                title: "不支持的参数类型",
                messageFormat: "不支持类型:{0}",
                category: "FlowMaker",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "支持的类型有Number,String,Boolean,DateTime,DateOnly,TimeOnly."
            );
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
                    //if (c.Type is GenericNameSyntax ff)
                    //{
                    //    if (ff.Identifier.Text == "IDataConverter" && ff.TypeArgumentList.Arguments.Any())
                    //    {
                    //        return true;
                    //    }
                    //}

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

                            ITypeSymbol GetRealType(ITypeSymbol type)
                            {
                                if (type is IArrayTypeSymbol arrayType)
                                {
                                    if (arrayType.ElementType is IArrayTypeSymbol subArrayType)
                                    {
                                        return GetRealType(subArrayType);
                                    }
                                    else
                                    {
                                        return arrayType.ElementType;
                                    }
                                }
                                return type;
                            }
                            var realType = GetRealType(property.Type);
                            var flowDataType = "FlowDataType.Number";
                            //验证是否是字符串,数字,布尔,枚举转换为数字,时间,日期
                            if (realType.SpecialType == SpecialType.System_String)
                            {
                                flowDataType = "FlowDataType.String";
                            }
                            else if (realType.SpecialType == SpecialType.System_Int32 ||
                                 realType.SpecialType == SpecialType.System_Int64 ||
                                 realType.SpecialType == SpecialType.System_Double ||
                                 realType.SpecialType == SpecialType.System_Byte ||
                                 realType.SpecialType == SpecialType.System_Single ||
                                 realType.SpecialType == SpecialType.System_Decimal ||
                                 realType.SpecialType == SpecialType.System_Int16 ||
                                 realType.SpecialType == SpecialType.System_UInt16 ||
                                 realType.SpecialType == SpecialType.System_UInt32 ||
                                 realType.SpecialType == SpecialType.System_UInt64 ||
                                 realType.SpecialType == SpecialType.System_SByte ||
                                 //或者是枚举
                                 realType.TypeKind == TypeKind.Enum)
                            {
                                flowDataType = "FlowDataType.Number";
                            }
                            else if (realType.SpecialType == SpecialType.System_Boolean)
                            {
                                flowDataType = "FlowDataType.Boolean";
                            }
                            else if (realType.SpecialType == SpecialType.System_DateTime)
                            {
                                flowDataType = "FlowDataType.DateTime";
                            }
                            else
                            {
                                c.ReportDiagnostic(Diagnostic.Create(unsupportedTypeDiagnosticDescriptor, property.Locations[0], property.Type.ToDisplayString()));
                                //类型错误
                            }

                            defStringBuilder.AppendLine($$"""
        var {{property.Name}}Prop = new DataDefinition("{{property.Name}}", "{{displayName}}", {{flowDataType}}, "{{defaultValueValue}}");
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
                            int rank = 0;

                            if (property.Type.TypeKind == TypeKind.Array && property.Type is IArrayTypeSymbol arrayTypeSymbol)
                            {

                                isArray = true;
                                rank = arrayTypeSymbol.ToDisplayString().Count(c => c == '[');
                                defStringBuilder.AppendLine($$"""
        {{property.Name}}Prop.IsArray = true;        
        {{property.Name}}Prop.Rank = {{rank}};        
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
        {{property.Name}}Prop.Options.Add(new OptionDefinition("{{enumDisplayName}}", $"{({{typeSymbol.EnumUnderlyingType.ToDisplayString()}}){{typeSymbol.Name}}.{{enumValue.Name}}}"));
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
            {{memberName}} = IStepInject.ConvertToArray<{{property.Type.ToDisplayString()}}>(IStepInject.Reshape<{{realType.ToDisplayString()}}>({{memberName}}Input.Dims, IStepInject.GetArrayValue<{{realType.ToDisplayString()}}>({{memberName}}Input, stepContext.FlowContext, s => JsonSerializer.Deserialize<{{realType.ToDisplayString()}}>(s))));
        }
        else
        {
            {{memberName}} = IStepInject.GetValue<{{property.Type.ToDisplayString()}}>({{memberName}}Input, stepContext.FlowContext, s => JsonSerializer.Deserialize<{{property.Type.ToDisplayString()}}>(s), nameof({{memberName}}));
        }
        stepContext.StepStatus.Inputs.Add(new NameValue(nameof({{memberName}}), JsonSerializer.Serialize({{memberName}})));
""");
                                }
                                else
                                {
                                    if (property.Type.SpecialType == SpecialType.System_String)
                                    {
                                        inputStringBuilder.AppendLine($$"""
        {{memberName}} = IStepInject.GetValue<{{property.Type.ToDisplayString()}}>(stepContext.Step.Inputs.First(v=> v.Name == nameof({{memberName}})), stepContext.FlowContext, s => s?.ToString(), nameof({{memberName}}));
        stepContext.StepStatus.Inputs.Add(new NameValue(nameof({{memberName}}), JsonSerializer.Serialize({{memberName}})));
""");
                                    }
                                    else
                                    {
                                        inputStringBuilder.AppendLine($$"""
        {{memberName}} = IStepInject.GetValue<{{property.Type.ToDisplayString()}}>(stepContext.Step.Inputs.First(v=> v.Name == nameof({{memberName}})), stepContext.FlowContext, s => JsonSerializer.Deserialize<{{property.Type.ToDisplayString()}}>(s), nameof({{memberName}}));
        stepContext.StepStatus.Inputs.Add(new NameValue(nameof({{memberName}}), JsonSerializer.Serialize({{memberName}})));
""");
                                    }

                                }
                            }


                            if (output is not null)
                            {
                                outputStringBuilder.AppendLine($$"""
        IStepInject.SetValue(stepContext.Step.Outputs.First(v=> v.Name == nameof({{memberName}})), {{memberName}}, stepContext.FlowContext);
        stepContext.StepStatus.Outputs.Add(new NameValue(nameof({{memberName}}), JsonSerializer.Serialize({{memberName}})));
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
            });
        }
    }
    public class SyntaxModel
    {
        public ITypeSymbol Option { get; set; }
    }
}
