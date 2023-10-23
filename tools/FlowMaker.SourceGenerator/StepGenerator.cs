using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
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
                    var group = flowStep.ConstructorArguments[0].Value.ToString();
                    var name = flowStep.ConstructorArguments[1].Value.ToString();

                    StringBuilder inputStringBuilder = new();
                    StringBuilder outputStringBuilder = new();
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

                                if (property.Type.SpecialType == SpecialType.System_Boolean)
                                {

                                }
                                
                                inputStringBuilder.AppendLine($$"""
        {{memberName}} = await IFlowValueConverter<{{property.Type.ToDisplayString()}}>.GetValue("{{memberName}}", serviceProvider, context, step.Inputs, s => System.Text.Json.JsonSerializer.Deserialize<{{property.Type.ToDisplayString()}}>(s), cancellationToken);
""");
                            }


                            var output = propAttrs.FirstOrDefault(c => c.AttributeClass.Name == "OutputAttribute");
                            if (output is not null)
                            {
                                var outputName = output.ConstructorArguments[0].Value.ToString();
                                var defaultValue = propAttrs.FirstOrDefault(c => c.AttributeClass.Name == "DefaultValueAttribute");
                                if (defaultValue is not null)
                                {
                                    var defaultValueValue = defaultValue.ConstructorArguments[0].Value.ToString();
                                    inputStringBuilder.AppendLine($$"""
        {{memberName}} = {{defaultValueValue}};
""");
                                }

                                outputStringBuilder.AppendLine($$"""
        context.Data[step.Outputs["{{memberName}}"]] = System.Text.Json.JsonSerializer.Serialize({{memberName}});
""");
                            }
                        }
                    }


                    string baseStr = $@"using FlowMaker;
using FlowMaker.Models;

namespace {item.Option.ContainingNamespace};

public partial class {item.Option.MetadataName} : IStep
{{
    public static string GroupName => ""{group}"";

    public static string Name => ""{name}"";

    public async Task WrapAsync(RunningContext context, FlowStep step, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {{
{inputStringBuilder}
        await Run(context, step, cancellationToken);
{outputStringBuilder}
    }}

    public static StepDefinition GetDefinition()
    {{
        throw new NotImplementedException();
    }}
}}
";

                    c.AddSource($"{item.Option.MetadataName}.s.g.cs", SourceText.From(baseStr, Encoding.UTF8));
                }
                if (flowConverter is not null)
                {
                    //获取flowConverter中的泛型参数
                    var type = flowConverter.AttributeClass.TypeArguments[0] as INamedTypeSymbol;

                    var group = flowConverter.ConstructorArguments[0].Value.ToString();
                    var name = flowConverter.ConstructorArguments[1].Value.ToString();

                    StringBuilder inputStringBuilder = new();

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

                                if (property.Type.SpecialType == SpecialType.System_Boolean)
                                {

                                }
                                inputStringBuilder.AppendLine($$"""
        {{memberName}} = IFlowValueConverter<{{property.Type.ToDisplayString()}}>.GetValue("{{memberName}}", context, input, s=> System.Text.Json.JsonSerializer.Deserialize<{{property.Type.ToDisplayString()}}>(s));
""");
                            }
                        }
                    }


                    string baseStr = $@"using FlowMaker;
using FlowMaker.Models;

namespace {item.Option.ContainingNamespace};

public partial class {item.Option.MetadataName} : IFlowValueConverter<{type.ToDisplayString()}>
{{
    public static string GroupName => ""{group}"";

    public static string Name => ""{name}"";

    public async Task<{type.ToDisplayString()}> WrapAsync(RunningContext context, FlowInput input, CancellationToken cancellationToken)
    {{
{inputStringBuilder}
        return await Convert(context, input, cancellationToken);
    }}

    public static ConverterDefinition GetDefinition()
    {{
        throw new NotImplementedException();
    }}
}}
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
