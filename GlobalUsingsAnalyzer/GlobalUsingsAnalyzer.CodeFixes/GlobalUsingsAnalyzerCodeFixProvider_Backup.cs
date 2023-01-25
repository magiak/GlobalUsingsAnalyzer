//using Microsoft.CodeAnalysis;
//using Microsoft.CodeAnalysis.CodeActions;
//using Microsoft.CodeAnalysis.CodeFixes;
//using Microsoft.CodeAnalysis.CSharp;
//using Microsoft.CodeAnalysis.CSharp.Syntax;
//using Microsoft.CodeAnalysis.Editing;
//using Microsoft.CodeAnalysis.Formatting;
//using Microsoft.CodeAnalysis.Rename;
//using Microsoft.CodeAnalysis.Text;
//using System.Collections.Generic;
//using System.Collections.Immutable;
//using System.Composition;
//using System.Threading;
//using System.Threading.Tasks;

//namespace GlobalUsingsAnalyzer
//{
//    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GlobalUsingsAnalyzerCodeFixProvider)), Shared]
//    public class GlobalUsingsAnalyzerCodeFixProvider : CodeFixProvider
//    {
//        public override sealed ImmutableArray<string> FixableDiagnosticIds
//        {
//            get { return ImmutableArray.Create(GlobalUsingsAnalyzerAnalyzer.DiagnosticId); }
//        }

//        public sealed override FixAllProvider GetFixAllProvider()
//        {
//            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
//            return WellKnownFixAllProviders.BatchFixer;
//        }

//        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
//        {
//            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

//            // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
//            var diagnostic = context.Diagnostics.First();
//            var diagnosticSpan = diagnostic.Location.SourceSpan;

//            // Find the type declaration identified by the diagnostic.
//            //var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().First();
//            var usingItem = (UsingDirectiveSyntax)root.FindNode(diagnosticSpan);

//            // Register a code action that will invoke the fix.
//            context.RegisterCodeFix(
//                CodeAction.Create(
//                    title: CodeFixResources.CodeFixTitle,
//                    //createChangedSolution: c => MakeUppercaseAsync(context.Document, declaration, c),
//                    //createChangedDocument: c => AddUsingSystemLinqAsync(context.Document, declaration, c),
//                    //createChangedDocument: c => AddStaticAsync(context.Document, declaration, c),
//                    //createChangedSolution: c => ReplaceAllUsingsWithGlobalAsync(context.Document, c),
//                    createChangedSolution: c => ReplaceUsingWithGlobalAsync(context.Document, usingItem, c)
//                    //equivalenceKey: $"{nameof(CodeFixResources.CodeFixTitle)}-{diagnosticSpan.Start}"
//                    ),
//                diagnostic);
//        }

//        private async Task<Solution> MakeUppercaseAsync(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
//        {
//            // Compute new uppercase name.
//            var identifierToken = typeDecl.Identifier;
//            var newName = identifierToken.Text.ToUpperInvariant();

//            // Get the symbol representing the type to be renamed.
//            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
//            var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);

//            // Produce a new solution that has all references to that type renamed, including the declaration.
//            var originalSolution = document.Project.Solution;
//            var optionSet = originalSolution.Workspace.Options;
//            var newSolution = await Renamer.RenameSymbolAsync(document.Project.Solution, typeSymbol, newName, optionSet, cancellationToken).ConfigureAwait(false);

//            // Return the new solution with the now-uppercase type name.
//            return newSolution;
//        }

//        private async Task<Document> AddStaticAsync(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
//        {
//            SyntaxToken staticKeywordToken = SyntaxFactory.Token(SyntaxKind.StaticKeyword);
//            TypeDeclarationSyntax newClassDeclaration = typeDecl.AddModifiers(new SyntaxToken[] { staticKeywordToken });

//            SyntaxNode root = await document.GetSyntaxRootAsync();
//            SyntaxNode newRoot = root.ReplaceNode(typeDecl, newClassDeclaration);

//            Document newDocument = document.WithSyntaxRoot(newRoot);
//            return newDocument;
//        }

//        private async Task<Solution> ReplaceUsingWithGlobalAsync(Document document, UsingDirectiveSyntax usingItem, CancellationToken cancellationToken)
//        {
//            CompilationUnitSyntax root = (CompilationUnitSyntax)await document.GetSyntaxRootAsync(cancellationToken);

//            // Get and remove using from the original file
//            root = root.RemoveNode(usingItem, SyntaxRemoveOptions.KeepNoTrivia);
//            //root = root.WithAdditionalAnnotations(Formatter.Annotation);

//            // Create the Using.cs file
//            var usingFileName = "Usings.cs";
//            var usingsDocument = document.Project.Documents.FirstOrDefault(x => x.Name == usingFileName);
//            if(usingsDocument == null)
//            {
//                var projectGenerator = SyntaxGenerator.GetGenerator(document.Project);
//                var usingsSyntaxNode = projectGenerator.CompilationUnit();
//                usingsDocument = document.Project.AddDocument(usingFileName, usingsSyntaxNode);
//            }

//            // Add the using to the Using.cs file
//            var usingsDocumentRoot = (CompilationUnitSyntax)await usingsDocument.GetSyntaxRootAsync();
//            var globalToken = SyntaxFactory.Token(SyntaxKind.GlobalKeyword);
//            var globalUsingItem = usingItem.WithGlobalKeyword(globalToken);
//            usingsDocumentRoot = usingsDocumentRoot.AddUsings(globalUsingItem);
//            //usingsDocumentRoot = usingsDocumentRoot.WithAdditionalAnnotations(Formatter.Annotation);
//            usingsDocument = usingsDocument.WithSyntaxRoot(usingsDocumentRoot);

//            var solution = usingsDocument.Project.Solution;

//            // Update original document - remove all usings
//            solution = solution.WithDocumentSyntaxRoot(document.Id, root);

//            // Update the new document
//            solution = solution.WithDocumentSyntaxRoot(usingsDocument.Id, usingsDocumentRoot);

//            return solution;
//        }

//        private async Task<Solution> ReplaceAllUsingsWithGlobalAsync(Document document, CancellationToken cancellationToken)
//        {
//            CompilationUnitSyntax root = (CompilationUnitSyntax)await document.GetSyntaxRootAsync(cancellationToken);

//            // Get and remove usings from the original file
//            var newUsings = new List<UsingDirectiveSyntax>();
//            foreach(var usingItem in root.Usings)
//            {
//                var globalToken = SyntaxFactory.Token(SyntaxKind.GlobalKeyword);
//                var globalUsingItem = usingItem.WithGlobalKeyword(globalToken);
//                root = root.RemoveNode(usingItem, SyntaxRemoveOptions.KeepNoTrivia);
//                newUsings.Add(globalUsingItem);
//            }
//            root = root.WithAdditionalAnnotations(Formatter.Annotation);

//            // Create the Using.cs file
//            var usingFileName = "Usings.cs";
//            var usingsDocument = document.Project.Documents.FirstOrDefault(x => x.Name == usingFileName);
//            if(usingsDocument == null)
//            {
//                var projectGenerator = SyntaxGenerator.GetGenerator(document.Project);
//                var usingsSyntaxNode = projectGenerator.CompilationUnit();
//                usingsDocument = document.Project.AddDocument(usingFileName, usingsSyntaxNode);
//            }

//            // Add the usings to the Using.cs file
//            var usingsDocumentRoot = (CompilationUnitSyntax)await usingsDocument.GetSyntaxRootAsync();
//            usingsDocumentRoot = usingsDocumentRoot.AddUsings(newUsings.ToArray());
//            usingsDocumentRoot = usingsDocumentRoot.WithAdditionalAnnotations(Formatter.Annotation);
//            usingsDocument = usingsDocument.WithSyntaxRoot(usingsDocumentRoot);

//            var solution = usingsDocument.Project.Solution;

//            // Update original document - remove all usings
//            solution = solution.WithDocumentSyntaxRoot(document.Id, root);

//            // Update the new document
//            solution = solution.WithDocumentSyntaxRoot(usingsDocument.Id, usingsDocumentRoot);

//            return solution;
//        }

//        private async Task<Document> AddUsingSystemLinqAsync(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
//        {
//            //https://github.com/dotnet/roslyn/issues/3082
//            CompilationUnitSyntax root = (CompilationUnitSyntax)await document.GetSyntaxRootAsync(cancellationToken); // CompilationUnitSyntax CompilationUnit

//            // Create Using.cs file if not exists
//            var usingFileName = "Usings.cs";
//            var usingsDocument = document.Project.Documents.FirstOrDefault(x => x.Name == usingFileName);
//            if(usingsDocument == null)
//            {
//                // create the using file
//                var projectGenerator = SyntaxGenerator.GetGenerator(document.Project);
//                var usingsSyntaxNode = projectGenerator.CompilationUnit();
//                usingsDocument = document.Project.AddDocument(usingFileName, usingsSyntaxNode);
//            }

//            // Build new file
//            var usingsRoot = await usingsDocument.GetSyntaxRootAsync(cancellationToken);
//            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(usingsDocument);
//            var newUsingsRoot = generator.AddNamespaceImports(usingsRoot, generator.IdentifierName("System.Linq"));
//            var newUsingsFile = usingsDocument.WithSyntaxRoot(newUsingsRoot);

//            var solution = newUsingsFile.Project.Solution;
//            solution = solution.WithDocumentSyntaxRoot(newUsingsFile.Id, newUsingsRoot);
//            return solution.GetDocument(newUsingsFile.Id);
//        }

//        // OBSOLETE !!!!!!!!!!!!!!!!!!!!!!!!!!!!
//        //List<SyntaxNode> globalUsingNodeChildren = globalUsingNode.ChildNodes().ToList();
//        //SyntaxNode globalUsingNodeChild = globalUsingNodeChildren.First(); // IdentifierNameSyntax IdentifierName System

//        //var globalToken = SyntaxFactory.Token(SyntaxKind.GlobalKeyword);
//        //SyntaxToken firstToken = globalUsingNode.GetFirstToken();
//        //usingSyntaxNode = usingSyntaxNode.InsertTokensBefore(usingSyntaxNode.GetFirstToken(), new[] { globalToken });
//        //SyntaxNode root2 = await usingsDocument.GetSyntaxRootAsync(cancellationToken); // CompilationUnitSyntax CompilationUnit

//        //var newUsingsRoot = generator.AddGlobalImport(usingsRoot, generator.IdentifierName("System.Linq"));
//        //var newUsingsRoot = generator.AddNamespaceImports(root2, usingSyntaxNode);

//        //var globalToken = SyntaxFactory.Token(SyntaxKind.GlobalKeyword);
//        //var jahoda = newUsingsRoot.DescendantNodes().ToList();
//        //var newUsingsRoot = generator.AddGlobalImport(usingsRoot, generator.IdentifierName("System.Linq"));

//        //var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Linq"))
//        //    .With(SyntaxFactory.TriviaList(SyntaxFactory.Token(SyntaxKind.GlobalKeyword)));
//        //.WithLeadingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.Token(SyntaxKind.GlobalKeyword)));

//        //var globalKeyword = SyntaxFactory.IdentifierName("global");
//        //var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Linq"));
//        //usingDirective = usingDirective.WithDescendantNodesAndTokens(usingDirective.DescendantNodesAndTokens().Prepend(globalKeyword));

//        //var globalKeyword = SyntaxFactory.Trivia(SyntaxFactory.Token(SyntaxKind.GlobalKeyword));

//        //var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Linq")).DescendantNodesAndTokens.
//        //usingSyntaxNode.ReplaceToken(SyntaxFactory.Token(SyntaxKind.NamespaceDeclaration), SyntaxFactory.Token(SyntaxKind.GlobalStatement));
//    }
//}