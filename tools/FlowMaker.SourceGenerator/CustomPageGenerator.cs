using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace FlowMaker.SourceGenerator
{
    [Generator]
    public class CustomPageGenerator : IIncrementalGenerator
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
                        if (fff.Identifier.Text == "ICustomPageViewModel")
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


                //var category = flowStep.ConstructorArguments[0].Value.ToString();
                //var name = flowStep.ConstructorArguments[1].Value.ToString();

                StringBuilder inputStringBuilder = new();

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
        {{property.Name}}Prop.Options.Add(new OptionDefinition("{{enumDisplayName}}", "{{enumValue.Name}}"));
""");
                            }
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
        var {{memberName}}Input = inputs.First(v=> v.Name == "{{memberName}}");
        if ({{memberName}}Input.Mode == InputMode.Array)
        {
            {{memberName}} = ({{property.Type.ToDisplayString()}})IDataConverter.Reshape<{{subType}}>({{memberName}}Input.Dims, await IDataConverter.GetArrayValue<{{subType}}>({{memberName}}Input, serviceProvider, null, s => JsonSerializer.Deserialize<{{subType}}>(s), cancellationToken));
        }
        else
        {
            {{memberName}} = await IDataConverter.GetValue<{{property.Type.ToDisplayString()}}>({{memberName}}Input, serviceProvider, null, s => JsonSerializer.Deserialize<{{property.Type.ToDisplayString()}}>(s), cancellationToken);
        }
""");
                            }
                            else
                            {
                                if (property.Type.SpecialType == SpecialType.System_String)
                                {
                                    inputStringBuilder.AppendLine($$"""
        {{memberName}} = await IDataConverter.GetValue<{{property.Type.ToDisplayString()}}>(inputs.First(v=> v.Name == "{{memberName}}"), serviceProvider, null, s => s?.ToString(), cancellationToken);
""");
                                }
                                else
                                {
                                    inputStringBuilder.AppendLine($$"""
        {{memberName}} = await IDataConverter.GetValue<{{property.Type.ToDisplayString()}}>(inputs.First(v=> v.Name == "{{memberName}}"), serviceProvider, null, s => JsonSerializer.Deserialize<{{property.Type.ToDisplayString()}}>(s), cancellationToken);
""");
                                }
                            }
                        }



                    }
                }

                string baseStr = $@"using FlowMaker;
using FlowMaker.Models;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System;
using System.Threading.Tasks;

namespace {item.Option.ContainingNamespace};

#nullable enable

public partial class {item.Option.MetadataName}
{{
    public async Task WrapAsync(List<FlowInput> inputs, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {{
{inputStringBuilder}
        await Load();
    }}

    public static CustomViewDefinition GetDefinition()
    {{
{defStringBuilder}
        return new CustomViewDefinition
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

            });

        }
    }
}
