using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using Microsoft.CodeAnalysis.FindSymbols;

namespace GlobalUsingsAnalyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CSharpAddGlobalImportCodeFixProvider)), Shared]
    public class CSharpAddGlobalImportCodeFixProvider : CodeFixProvider
    {
        public override sealed ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create("CS0246"); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            //return WellKnownFixAllProviders.BatchFixer;
            return null; // Fix All is not supported
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if(root == null)
            {
                return;
            }

            var semanticModel = await context.Document.GetSemanticModelAsync();

            foreach(var diagnostic in context.Diagnostics)
            {
                var diagnosticSpan = diagnostic.Location.SourceSpan;
                var node = root.FindNode(diagnosticSpan);
                if(node == null)
                {
                    continue;
                }
                
                // Known note types - SimpleBaseTypeSyntax and IdentifierNameSyntax
                string? identifier = node.GetText().ToString();

                if(identifier == null)
                {
                    continue;
                }

                // "DbContext\r\n" => "DbContext"
                identifier = identifier.Replace("\r\n", string.Empty);

                var possibleDeclarations = await SymbolFinder.FindDeclarationsAsync(context.Document.Project, identifier, false);

                if(possibleDeclarations.Count() != 1)
                {
                    return;
                }

                var declaration = possibleDeclarations.First();
                var namespaceSymbol = declaration.ContainingNamespace;
                var namespaceName = namespaceSymbol.ToDisplayString();
                var usingItem = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(namespaceName));

                usingItem = usingItem.WithGlobalKeyword(SyntaxFactory.Token(SyntaxKind.GlobalKeyword));

                var fileName = diagnostic.Properties.ContainsKey("FileName") ? diagnostic.Properties["FileName"] : "Usings.cs";

                // Register a code action that will invoke the fix.
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"global using {namespaceName}",
                        createChangedSolution: c => ReplaceUsingWithGlobalAsync(context.Document, usingItem, fileName, c),
                        equivalenceKey: $"{nameof(CodeFixResources.CodeFixTitle)}-{diagnosticSpan.Start}"
                        ),
                    diagnostic);
            }
        }

        private async Task<Solution> ReplaceUsingWithGlobalAsync(Microsoft.CodeAnalysis.Document document, UsingDirectiveSyntax usingItem, string usingFileName, CancellationToken cancellationToken)
        {
            CompilationUnitSyntax root = (CompilationUnitSyntax)await document.GetSyntaxRootAsync(cancellationToken);

            // Create the Using.cs file
            var usingsDocument = document.Project.Documents.FirstOrDefault(x => x.Name == usingFileName);
            if(usingsDocument == null)
            {
                var projectGenerator = SyntaxGenerator.GetGenerator(document.Project);
                var usingsSyntaxNode = projectGenerator.CompilationUnit();
                usingsDocument = document.Project.AddDocument(usingFileName, usingsSyntaxNode);
            }

            // Add the using to the Using.cs file
            var usingsDocumentRoot = (CompilationUnitSyntax)await usingsDocument.GetSyntaxRootAsync();
            usingsDocumentRoot = usingsDocumentRoot.AddUsings(usingItem);
            usingsDocument = usingsDocument.WithSyntaxRoot(usingsDocumentRoot);

            var solution = usingsDocument.Project.Solution;

            // Update the Using.cs document
            solution = solution.WithDocumentSyntaxRoot(usingsDocument.Id, usingsDocumentRoot);

            return solution;
        }
    }
}