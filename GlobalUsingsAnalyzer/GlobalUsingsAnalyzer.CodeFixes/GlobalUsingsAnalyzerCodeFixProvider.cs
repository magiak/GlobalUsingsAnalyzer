using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace GlobalUsingsAnalyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GlobalUsingsAnalyzerCodeFixProvider)), Shared]
    public class GlobalUsingsAnalyzerCodeFixProvider : CodeFixProvider
    {
        public override sealed ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(GlobalUsingsAnalyzerAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            //return WellKnownFixAllProviders.BatchFixer;
            return GlobalUsingsFixAllProvider.Instance;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            foreach(var diagnostic in context.Diagnostics)
            {
                var diagnosticSpan = diagnostic.Location.SourceSpan;

                // Find the type declaration identified by the diagnostic.
                var usingItem = (UsingDirectiveSyntax)root.FindNode(diagnosticSpan);

                // Register a code action that will invoke the fix.
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: CodeFixResources.CodeFixTitle,
                        createChangedSolution: c => ReplaceUsingWithGlobalAsync(context.Document, usingItem, c),
                        equivalenceKey: $"{nameof(CodeFixResources.CodeFixTitle)}-{diagnosticSpan.Start}"
                        ),
                    diagnostic);
            }
        }

        private async Task<Solution> ReplaceUsingWithGlobalAsync(Document document, UsingDirectiveSyntax usingItem, CancellationToken cancellationToken)
        {
            CompilationUnitSyntax root = (CompilationUnitSyntax)await document.GetSyntaxRootAsync(cancellationToken);

            // Get and remove using from the original file
            root = root.RemoveNode(usingItem, SyntaxRemoveOptions.KeepNoTrivia);
            //root = root.WithAdditionalAnnotations(Formatter.Annotation);

            // Create the Using.cs file
            var usingFileName = "Usings.cs";
            var usingsDocument = document.Project.Documents.FirstOrDefault(x => x.Name == usingFileName);
            if(usingsDocument == null)
            {
                var projectGenerator = SyntaxGenerator.GetGenerator(document.Project);
                var usingsSyntaxNode = projectGenerator.CompilationUnit();
                usingsDocument = document.Project.AddDocument(usingFileName, usingsSyntaxNode);
            }

            // Add the using to the Using.cs file
            var usingsDocumentRoot = (CompilationUnitSyntax)await usingsDocument.GetSyntaxRootAsync();
            var globalToken = SyntaxFactory.Token(SyntaxKind.GlobalKeyword);
            var globalUsingItem = usingItem.WithGlobalKeyword(globalToken);
            usingsDocumentRoot = usingsDocumentRoot.AddUsings(globalUsingItem);
            //usingsDocumentRoot = usingsDocumentRoot.WithAdditionalAnnotations(Formatter.Annotation);
            usingsDocument = usingsDocument.WithSyntaxRoot(usingsDocumentRoot);

            var solution = usingsDocument.Project.Solution;

            // Update original document - remove all usings
            solution = solution.WithDocumentSyntaxRoot(document.Id, root);

            // Update the new document
            solution = solution.WithDocumentSyntaxRoot(usingsDocument.Id, usingsDocumentRoot);

            return solution;
        }
    }
}