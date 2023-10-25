using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace FlowMaker.ViewLocator.SourceGenerator
{
    [Generator]
    public class ViewLocatorGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxContextReceiver is not ClientSyntax receiver || !receiver.SyntaxModels.Any())
            {
                return;
            }
            try
            {
                StringBuilder stringBuilder = new StringBuilder();
                StringBuilder nameSpacestringBuilder = new StringBuilder();
                List<string> names = new List<string>();
                foreach (var item in receiver.SyntaxModels)
                {
                    stringBuilder.AppendLine($$"""
            {{item.Option.Name}} vm => ViewForMatch.GetAndSet(vm),
""");
                    names.Add(item.Option.ContainingNamespace.ToDisplayString());
                }
                foreach (var item in names.Distinct())
                {
                    nameSpacestringBuilder.AppendLine("using " + item + ";");
                }

                var containingAssembly = receiver.SyntaxModels.First().Option.ContainingAssembly;
                string baseStr = $$"""
using FlowMaker;
using ReactiveUI;
{{nameSpacestringBuilder}}
namespace {{containingAssembly.Name}};

#nullable enable
public static class {{containingAssembly.Name.Replace(".", "")}}ViewLocatorMatcher
{
    public static IViewFor? Match(object viewModel)
    {
        return viewModel switch
        {
{{stringBuilder}}
            _ => null
        };
    }
}
#nullable restore
""";

                context.AddSource($"{containingAssembly.Name.Replace(".", "")}ViewLocatorMatcher.g.cs", SourceText.From(baseStr, Encoding.UTF8));
            }
            catch (Exception e)
            {

                throw;
            }

        }

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new ClientSyntax());
        }
        class ClientSyntax : ISyntaxContextReceiver
        {
            public List<SyntaxModel> SyntaxModels { get; set; } = new List<SyntaxModel>();

            public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
            {
                if (context.Node is ClassDeclarationSyntax ids)
                {
                    var commandOption = context.SemanticModel.GetDeclaredSymbol(ids) as INamedTypeSymbol;

                    if (commandOption.IsAbstract)
                    {
                        return;
                    }
                    var match = commandOption.AllInterfaces.Any(c => c.Name == "IFlowMakerRoutableViewModel");

                    if (match)
                    {
                        if (SyntaxModels.Any(c => c.Option.Name == commandOption.Name))
                        {
                            return;
                        }
                        SyntaxModels.Add(new SyntaxModel
                        {
                            Option = commandOption
                        });
                    }
                }
            }
        }
        class SyntaxModel
        {
            public ITypeSymbol Option { get; set; }
        }
    }
}
