using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GlobalUsingsAnalyzer
{
    public class GlobalUsingsFixAllProvider : FixAllProvider
    {
        public static readonly FixAllProvider Instance = new GlobalUsingsFixAllProvider();

        public override IEnumerable<FixAllScope> GetSupportedFixAllScopes()
        {
            return new FixAllScope[] { FixAllScope.Document };
            //return new[] { FixAllScope.Document, FixAllScope.Project, FixAllScope.Solution }; // TODO add others :)
        }

        public override async Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
        {
            Solution solution = fixAllContext.Scope switch
            {
                FixAllScope.Document => await ReplaceAllUsingsWithGlobalAsync(fixAllContext, fixAllContext.Document).ConfigureAwait(false),
                _ => throw new NotSupportedException($"Scope {fixAllContext.Scope} is not supported by this fix all provider."),
            };

            if(solution == null)
                return null;

            return CodeAction.Create(
                "Move to Global Usings", c => Task.FromResult(solution));
        }

        private async Task<Solution> ReplaceAllUsingsWithGlobalAsync(FixAllContext fixAllContext, Document document)
        {
            var diagnostics = await fixAllContext.GetDocumentDiagnosticsAsync(document);
            var fileName = diagnostics.First().Properties.ContainsKey("FileName") ? diagnostics.First().Properties["FileName"] : "Usings.cs";

            CompilationUnitSyntax root = (CompilationUnitSyntax)await document.GetSyntaxRootAsync();

            // Get and remove usings from the original file
            var newUsings = new List<UsingDirectiveSyntax>();
            while(root.Usings.Any()) // can not use foreach! TODO refactor to two steps 1. remove usings 2. create global usings
            {
                var usingItem = root.Usings.First();
                var globalToken = SyntaxFactory.Token(SyntaxKind.GlobalKeyword);
                var globalUsingItem = usingItem.WithGlobalKeyword(globalToken);
                root = root.RemoveNode(usingItem, SyntaxRemoveOptions.KeepNoTrivia);
                newUsings.Add(globalUsingItem);
            }

            root = root.WithAdditionalAnnotations(Formatter.Annotation);

            // Create the Using.cs file
            var usingsDocument = document.Project.Documents.FirstOrDefault(x => x.Name == fileName);
            if(usingsDocument == null)
            {
                var projectGenerator = SyntaxGenerator.GetGenerator(document.Project);
                var usingsSyntaxNode = projectGenerator.CompilationUnit();
                usingsDocument = document.Project.AddDocument(fileName, usingsSyntaxNode);
            }

            // Add the usings to the Using.cs file
            var usingsDocumentRoot = (CompilationUnitSyntax)await usingsDocument.GetSyntaxRootAsync();

            usingsDocumentRoot = GlobalUsingCodeAction.AddUsings(fixAllContext.Project.AnalyzerOptions, usingsDocumentRoot, newUsings);
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