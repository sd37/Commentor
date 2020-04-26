using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Commentor
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CommentorAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "Commentor";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Comments";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            context.RegisterSymbolAction(AnalyzeMethodSymbol, SymbolKind.Method);
            context.RegisterSymbolAction(AnalyzeNamedSymbol, SymbolKind.NamedType);
            context.RegisterSymbolAction(AnalyzePropertySymbol, SymbolKind.Property);
        }

        private static void AnalyzePropertySymbol(SymbolAnalysisContext context)
        { 
            var propertyTypeSymbol = (IPropertySymbol)context.Symbol;
            var currentComments = propertyTypeSymbol.GetDocumentationCommentXml();

            if (string.IsNullOrWhiteSpace(currentComments))
            { 
                var properties = new Dictionary<string, string>();

                if (propertyTypeSymbol.OriginalDefinition.SetMethod != null)
                { 
                    properties.Add("summary", $"Gets or Sets the {SplitCamelCase(propertyTypeSymbol.Name)}.");
                }

                properties.Add("summary", $"Gets the {SplitCamelCase(propertyTypeSymbol.Name)}.");

                var props = properties.ToImmutableDictionary();
                var diagnostic = Diagnostic.Create(Rule, propertyTypeSymbol.Locations[0], props, propertyTypeSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static void AnalyzeNamedSymbol(SymbolAnalysisContext context)
        {
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
            var currentComments = namedTypeSymbol.GetDocumentationCommentXml();

            if (string.IsNullOrWhiteSpace(currentComments))
            { 
                var properties = new Dictionary<string, string>();
                properties.Add("summary", $"The {SplitCamelCase(namedTypeSymbol.Name)}.");
                var props = properties.ToImmutableDictionary();
                var diagnostic = Diagnostic.Create(Rule, namedTypeSymbol.Locations[0], props, namedTypeSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static void AnalyzeMethodSymbol(SymbolAnalysisContext context)
        {
            // TODO: Replace the following code with your own analysis, generating Diagnostic objects for any issues you find
            var methodTypeSymbol = (IMethodSymbol)context.Symbol;

            if (SkipCheck(methodTypeSymbol.Name))
            {
                return;
            }

            // Find just those named type symbols with names containing lowercase letters.

            var currentComments = methodTypeSymbol.GetDocumentationCommentXml();

            if (string.IsNullOrWhiteSpace(currentComments))
            {
                // For all such symbols, produce a diagnostic.
                var properties = new Dictionary<string, string>();

                properties.Add("summary", $"The {SplitCamelCase(methodTypeSymbol.Name)}.");

                if (!methodTypeSymbol.ReturnsVoid)
                {
                    properties.Add("returns", "The result.");
                }

                foreach (var param in methodTypeSymbol.Parameters)
                {
                    var key = $"Parameter:{param.Name}";
                    var value = $"The {SplitCamelCase(param.Name)}.";
                    properties.Add(key, value);
                }

                var props = properties.ToImmutableDictionary();
                var diagnostic = Diagnostic.Create(Rule, methodTypeSymbol.Locations[0], props, methodTypeSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
        private static string SplitCamelCase(string input)
        {
            return System.Text.RegularExpressions.Regex.Replace(input, "([A-Z])", " $1", System.Text.RegularExpressions.RegexOptions.Compiled).Trim()
                .ToLower();
        }

        private static bool SkipCheck(string methodName)
        {
            if (methodName.Contains("get_"))
            {
                return true;
            }

            if (methodName.Contains("set_"))
            {
                return true;
            }

            return false;
        }
    }
}
