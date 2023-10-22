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
                if (ids.BaseList is null)
                {
                    return false;
                }
                return ids.BaseList.Types.Any(c => c.Type is IdentifierNameSyntax ff && ff.Identifier.Text == "IStep");
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
                StringBuilder stringBuilder = new StringBuilder();
                foreach (var member in item.Option.GetMembers())
                {
                    if (member is IPropertySymbol property)
                    {
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
                            var name =  member.ToDisplayString() ;
                            stringBuilder.AppendLine($$"""
                        var key{{name}} = step.Inputs["{{name}}"].Value;
                        if (step.Inputs["{{name}}"].UseGlobeData)
                        {
                            key{{name}} = context.Data[step.Inputs["{{name}}"].Value];
                        }
                        {{name}} = Convert.ToInt32(key{{name}});
""");
                        }


                        var output = propAttrs.FirstOrDefault(c => c.AttributeClass.Name == "OutputAttribute");
                        if (output is not null)
                        {
                            var outputName = output.ConstructorArguments[0].Value.ToString();
                            var defaultValue = propAttrs.FirstOrDefault(c => c.AttributeClass.Name == "DefaultValueAttribute");
                        }
                    }
                }


                string baseStr = $@"using FlowMaker;
using FlowMaker.Models;

namespace {item.Option.ContainingNamespace};

public partial class {item.Option.MetadataName}
{{
    public async Task WrapAsync(RunningContext context, Step step, CancellationToken cancellationToken)
    {{
        await Run(context, step, cancellationToken);
    }}
}}
";



                c.AddSource($"{item.Option.MetadataName.Substring(1)}CommandInfoProvider.g.cs", SourceText.From(baseStr, Encoding.UTF8));
            });

        }
    }
    public class SyntaxModel
    {
        public ITypeSymbol Option { get; set; }
    }
}
