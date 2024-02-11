using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace Jinaga.SourceGenerator
{

    public class FactSyntaxReceiver : ISyntaxReceiver
    {
        public List<ClassDeclarationSyntax> CandidateClasses { get; } = new List<ClassDeclarationSyntax>();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            // Any time we encounter a class declaration, we'll want to check if it's partial
            // and if it has the FactType attribute.
            if (syntaxNode is ClassDeclarationSyntax classDeclarationSyntax
                && classDeclarationSyntax.Modifiers.Any(SyntaxKind.PartialKeyword)
                && classDeclarationSyntax.AttributeLists.Count > 0)
            {
                var hasFactTypeAttribute = false;

                // Check all attributes of the class.
                foreach (var attributeList in classDeclarationSyntax.AttributeLists)
                {
                    foreach (var attribute in attributeList.Attributes)
                    {
                        // Check if the attribute is FactType.
                        if (attribute.Name.ToString() == "FactType")
                        {
                            hasFactTypeAttribute = true;
                            break;
                        }
                    }

                    if (hasFactTypeAttribute)
                    {
                        break;
                    }
                }

                if (hasFactTypeAttribute)
                {
                    CandidateClasses.Add(classDeclarationSyntax);
                }
            }
        }
    }
}