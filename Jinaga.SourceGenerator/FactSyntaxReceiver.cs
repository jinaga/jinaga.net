using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Jinaga.SourceGenerator
{

    public class FactSyntaxReceiver : ISyntaxReceiver
    {
        public List<ClassDeclarationSyntax> CandidateClasses { get; } = new List<ClassDeclarationSyntax>();
        public List<RecordDeclarationSyntax> CandidateRecords { get; } = new List<RecordDeclarationSyntax>();
        public List<Diagnostic> Diagnostics { get; } = new List<Diagnostic>();
 
        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            // Any time we encounter a class declaration with the FactType attribute,
            // we'll want to check if it's partial.
            if (syntaxNode is ClassDeclarationSyntax classDeclarationSyntax
                && classDeclarationSyntax.AttributeLists.Count > 0)
            {
                // Check all attributes of the class.
                if (HasFactTypeAttribute(classDeclarationSyntax.AttributeLists))
                {
                    // Check if the class is marked partial.
                    if (!classDeclarationSyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
                    {
                        // Issue an error if the class is not marked partial.
                        Diagnostics.Add(Diagnostic.Create(
                            new DiagnosticDescriptor(
                                id: "JIN001",
                                title: "Add the partial keyword to the class",
                                messageFormat: "The class '{0}' has the FactType attribute but is not marked partial",
                                category: "Jinaga",
                                DiagnosticSeverity.Error,
                                isEnabledByDefault: true),
                            classDeclarationSyntax.GetLocation(),
                            classDeclarationSyntax.Identifier.Text));
                    }
                    else
                    {
                        CandidateClasses.Add(classDeclarationSyntax);
                    }
                }
            }

            // Any time we encounter a record declaration with the FactType attribute,
            // we'll want to check if it's partial.
            else if (syntaxNode is RecordDeclarationSyntax recordDeclarationSyntax
                && recordDeclarationSyntax.AttributeLists.Count > 0)
            {
                // Check all attributes of the record.
                if (HasFactTypeAttribute(recordDeclarationSyntax.AttributeLists))
                {
                    // Check if the record is marked partial.
                    if (!recordDeclarationSyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
                    {
                        // Issue an error if the record is not marked partial.
                        Diagnostics.Add(Diagnostic.Create(
                            new DiagnosticDescriptor(
                                id: "JIN002",
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

        private static bool HasFactTypeAttribute(SyntaxList<AttributeListSyntax> attributeLists)
        {
            return attributeLists
                .SelectMany(attributeList => attributeList.Attributes)
                .Any(attribute => attribute.Name.ToString() == "FactType");
        }
    }
}