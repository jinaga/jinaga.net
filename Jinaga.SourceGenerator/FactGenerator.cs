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
                var namespaceDeclaration = string.IsNullOrEmpty(namespaceName) ? "" : $"namespace {namespaceName}\n{{\n";
                var closingBrace = string.IsNullOrEmpty(namespaceName) ? "" : "\n}";

                var source = $@"
using Jinaga.Facts;
using Jinaga.Serialization;

{namespaceDeclaration}
    public partial class {className} : IFactProxy
    {{
        FactGraph IFactProxy.Graph {{ get; set; }}
    }}
{closingBrace}";

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
                var namespaceDeclaration = string.IsNullOrEmpty(namespaceName) ? "" : $"namespace {namespaceName}\n{{\n";
                var closingBrace = string.IsNullOrEmpty(namespaceName) ? "" : "\n}";

                var source = $@"
using Jinaga.Facts;
using Jinaga.Serialization;

{namespaceDeclaration}
    public partial record {recordName} : IFactProxy
    {{
        FactGraph IFactProxy.Graph {{ get; set; }}
    }}
{closingBrace}";

                // Add the source code to the compilation
                context.AddSource($"{recordName}.g.cs", SourceText.From(source, Encoding.UTF8));
            }
        }

        // determine the namespace the class/enum/struct is declared in, if any
        private static string GetNamespaceName(BaseTypeDeclarationSyntax syntax)
        {
            // If we don't have a namespace at all we'll return an empty string
            // This accounts for the "default namespace" case
            string nameSpace = string.Empty;

            // Get the containing syntax node for the type declaration
            // (could be a nested type, for example)
            SyntaxNode potentialNamespaceParent = syntax.Parent;

            // Keep moving "out" of nested classes etc until we get to a namespace
            // or until we run out of parents
            while (potentialNamespaceParent != null)
            {
                if (potentialNamespaceParent is NamespaceDeclarationSyntax || potentialNamespaceParent is FileScopedNamespaceDeclarationSyntax)
                {
                    break;
                }

                potentialNamespaceParent = potentialNamespaceParent.Parent;
            }

            // Build up the final namespace by looping until we no longer have a namespace declaration
            if (potentialNamespaceParent is BaseNamespaceDeclarationSyntax namespaceParent)
            {
                // We have a namespace. Use that as the type
                nameSpace = namespaceParent.Name.ToString();

                // Keep moving "out" of the namespace declarations until we 
                // run out of nested namespace declarations
                while (true)
                {
                    var parent = namespaceParent.Parent as NamespaceDeclarationSyntax;
                    if (parent == null)
                    {
                        break;
                    }

                    // Add the outer namespace as a prefix to the final namespace
                    nameSpace = $"{parent.Name}.{nameSpace}";
                    namespaceParent = parent;
                }
            }

            // return the final namespace
            return nameSpace;
        }
    }
}
