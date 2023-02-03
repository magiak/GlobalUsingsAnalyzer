using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace GlobalUsingsAnalyzer
{
    // TODO refactor to real code action
    public class GlobalUsingCodeAction
    {
        public static CompilationUnitSyntax AddUsings(AnalyzerOptions analyzerOptions, CompilationUnitSyntax compilationUnitSyntax, UsingDirectiveSyntax newUsing)
        {
            return AddUsings(analyzerOptions, compilationUnitSyntax, new[] { newUsing });
        }

        public static CompilationUnitSyntax AddUsings(AnalyzerOptions analyzerOptions, CompilationUnitSyntax compilationUnitSyntax, IEnumerable<UsingDirectiveSyntax> newUsings)
        {
            var allUsings = compilationUnitSyntax.Usings.ToList();
            allUsings.AddRange(newUsings);

            var config = analyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(compilationUnitSyntax.SyntaxTree);
            var sortGetResult = config.TryGetValue($"dotnet_diagnostic.global_usings.sort", out var sort);

            // Sort usings
            if(!sortGetResult || (sortGetResult && sort.ToLower() == "true"))
            {
                // Sort system directives first
                var systemGetResult = config.TryGetValue($"dotnet_sort_system_directives_first", out var systemDirectiveFirst);
                if(systemGetResult && systemDirectiveFirst.ToLower() == "true")
                {
                    allUsings = allUsings
                        .OrderBy(u => u.Name.GetText().ToString().StartsWith("System") ? 0 : 1)
                        .ThenBy(u => u.Name.GetText().ToString())
                        .ToList();
                }
                else
                {
                    allUsings = allUsings.OrderBy(u => u.Name.GetText().ToString()).ToList();
                }

                // dotnet_sort_system_directives_first is unnecessary
            }

            compilationUnitSyntax = compilationUnitSyntax.RemoveNodes(compilationUnitSyntax.Usings, SyntaxRemoveOptions.KeepNoTrivia);
            return compilationUnitSyntax.AddUsings(allUsings.ToArray());
        }
    }
}