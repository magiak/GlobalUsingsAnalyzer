using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace GlobalUsingsAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class GlobalUsingsAnalyzerAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "global_usings";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));

        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.UsingDirective);
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            // https://www.mytechramblings.com/posts/configure-roslyn-analyzers-using-editorconfig/
            var config = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
            config.TryGetValue($"dotnet_diagnostic.{DiagnosticId}.file_name", out var fileName);

            var usingNode = (UsingDirectiveSyntax)context.Node;
            if(usingNode.GlobalKeyword.IsKind(SyntaxKind.None))
            {
                var properties = new Dictionary<string, string>
                {
                    { "FileName", fileName }
                };

                var diagnostic = Diagnostic.Create(Rule, usingNode.GetLocation(), properties.ToImmutableDictionary(), usingNode.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}