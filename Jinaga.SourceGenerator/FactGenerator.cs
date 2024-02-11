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
                var namespaceDeclaration = classDeclaration.FirstAncestorOrSelf<NamespaceDeclarationSyntax>();
                var namespaceName = namespaceDeclaration.Name.ToString();

                // Get the name of the class
                var className = classDeclaration.Identifier.Text;

                // Generate the source code for the other half of the partial class
                var source = $@"
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
            foreach (var classDeclaration in receiver.CandidateRecords)
            {
                // Get the namespace of the record
                var namespaceDeclaration = classDeclaration.FirstAncestorOrSelf<NamespaceDeclarationSyntax>();
                var namespaceName = namespaceDeclaration.Name.ToString();

                // Get the name of the class
                var recordName = classDeclaration.Identifier.Text;

                // Generate the source code for the other half of the partial record
                var source = $@"
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
    }
}
