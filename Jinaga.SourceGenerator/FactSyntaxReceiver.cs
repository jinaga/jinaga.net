using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Jinaga.SourceGenerator
{

    public class FactSyntaxReceiver : ISyntaxReceiver
    {
        public List<RecordDeclarationSyntax> CandidateRecords { get; } = new List<RecordDeclarationSyntax>();
        public List<Diagnostic> Diagnostics { get; } = new List<Diagnostic>();
 
        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            // Any time we encounter a record declaration, we'll want to check if it's partial
            // and if it has the FactType attribute.
            if (syntaxNode is RecordDeclarationSyntax recordDeclarationSyntax
                && recordDeclarationSyntax.AttributeLists.Count > 0)
            {
                // Check all attributes of the record.
                var hasFactTypeAttribute = recordDeclarationSyntax.AttributeLists
                    .SelectMany(attributeList => attributeList.Attributes)
                    .Any(attribute => attribute.Name.ToString() == "FactType");

                if (hasFactTypeAttribute)
                {
                    // Check if the record is marked partial.
                    if (!recordDeclarationSyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
                    {
                        // Issue an error if the record is not marked partial.
                        Diagnostics.Add(Diagnostic.Create(
                            new DiagnosticDescriptor(
                                id: "JIN001",
                                title: "Add the partial keyword to the record",
                                messageFormat: "The record '{0}' has the FactType attribute but is not marked partial",
                                category: "Jinaga",
                                DiagnosticSeverity.Error,
                                isEnabledByDefault: true),
                            recordDeclarationSyntax.GetLocation(),
                            recordDeclarationSyntax.Identifier.Text));
                    }
                    else
                    {
                        CandidateRecords.Add(recordDeclarationSyntax);
                    }
                }
            }
        }
    }
}