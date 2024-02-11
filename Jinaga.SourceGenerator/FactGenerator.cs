using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Jinaga.SourceGenerator
{
    [Generator]
    public class FactGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            // Register a factory that can create our custom syntax receiver
            context.RegisterForSyntaxNotifications(() => new FactSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            // Retrieve the populated receiver
            if (!(context.SyntaxReceiver is FactSyntaxReceiver receiver))
                return;

            // If there are any diagnostics, report them to the compiler
            foreach (var diagnostic in receiver.Diagnostics)
            {
                context.ReportDiagnostic(diagnostic);
            }

            // Iterate over all candidate classes
            foreach (var classDeclaration in receiver.CandidateClasses)
            {
                // Get the namespace of the class
                var namespaceName = GetNamespaceName(classDeclaration);

                // Get the name of the class
                var className = classDeclaration.Identifier.Text;

                // Generate the source code for the other half of the partial class
                var source = $@"
using Jinaga.Facts;
using Jinaga.Serialization;

namespace {namespaceName}
{{
    public partial class {className} : IFactProxy
    {{
        FactGraph IFactProxy.Graph {{ get; set; }}
    }}
}}";

                // Add the source code to the compilation
                context.AddSource($"{className}.g.cs", SourceText.From(source, Encoding.UTF8));
            }

            // Iterate over all candidate records
            foreach (var recordDeclaration in receiver.CandidateRecords)
            {
                // Get the namespace of the record
                string namespaceName = GetNamespaceName(recordDeclaration);

                // Get the name of the class
                var recordName = recordDeclaration.Identifier.Text;

                // Generate the source code for the other half of the partial record
                var source = $@"
using Jinaga.Facts;
using Jinaga.Serialization;

namespace {namespaceName}
{{
    public partial record {recordName} : IFactProxy
    {{
        FactGraph IFactProxy.Graph {{ get; set; }}
    }}
}}";

                // Add the source code to the compilation
                context.AddSource($"{recordName}.g.cs", SourceText.From(source, Encoding.UTF8));
            }
        }

        private static string GetNamespaceName(SyntaxNode syntaxNode)
        {
            var namespaceDeclaration = syntaxNode.Parent is NamespaceDeclarationSyntax
                ? (NamespaceDeclarationSyntax)syntaxNode.Parent
                : syntaxNode.SyntaxTree.GetRoot().DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();

            var namespaceName = namespaceDeclaration?.Name.ToString();
            return namespaceName;
        }
    }
}
