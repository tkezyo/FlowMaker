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
                if (ids.AttributeLists.Any(v => v.Attributes.Any(c => c.Name is IdentifierNameSyntax ff && ff.Identifier.Text == "FlowStep")))
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

                var group = flowStep.ConstructorArguments[0].Value.ToString();
                var name = flowStep.ConstructorArguments[1].Value.ToString();
                var isCheck = flowStep.ConstructorArguments[2].Value.ToString() == "true";

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
        var key{{memberName}} = step.Inputs["{{memberName}}"].Value;
        if (step.Inputs["{{memberName}}"].UseGlobeData)
        {
            key{{memberName}} = context.Data[step.Inputs["{{memberName}}"].Value];
        }
        {{memberName}} = Convert.ToInt32(key{{memberName}});
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
        context.Data[step.Outputs["{{memberName}}"]] = {{memberName}}.ToString();
""");
                        }
                    }
                }

                if (!isCheck)
                {
                    string baseStr = $@"using FlowMaker;
using FlowMaker.Models;

namespace {item.Option.ContainingNamespace};

public partial class {item.Option.MetadataName} : IStep
{{
    public async Task WrapAsync(RunningContext context, Step step, CancellationToken cancellationToken)
    {{
{inputStringBuilder}
        await Run(context, step, cancellationToken);
{outputStringBuilder}
    }}
}}
";

                    c.AddSource($"{item.Option.MetadataName}.g.cs", SourceText.From(baseStr, Encoding.UTF8));
                }
                else
                {
                    string baseStr = $@"using FlowMaker;
using FlowMaker.Models;

namespace {item.Option.ContainingNamespace};

public partial class {item.Option.MetadataName} : ICheckStep
{{
    public async Task<bool> WrapAsync(RunningContext context, Step step, CancellationToken cancellationToken)
    {{
{inputStringBuilder}
        var result = await Run(context, step, cancellationToken);
{outputStringBuilder}
        return result;
    }}
}}
";

                    c.AddSource($"{item.Option.MetadataName}.g.cs", SourceText.From(baseStr, Encoding.UTF8));
                }
            });

        }
    }
    public class SyntaxModel
    {
        public ITypeSymbol Option { get; set; }
    }
}
