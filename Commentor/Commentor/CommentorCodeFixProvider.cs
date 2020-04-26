using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using System.IO;

namespace Commentor
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CommentorCodeFixProvider)), Shared]
    public class CommentorCodeFixProvider : CodeFixProvider
    {
        private const string title = "Add comments";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(CommentorAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
            var diagnostic = context.Diagnostics.First();

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedDocument: c => AddComments(context.Document, diagnostic, c),
                    equivalenceKey: title),
                diagnostic);
        }

        private async Task<Document> AddComments(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync();
            var methodDetails = root.FindNode(diagnostic.Location.SourceSpan);
            var columnOffset = methodDetails.GetLocation().GetLineSpan().StartLinePosition.Character;

            var newLeadingTrivia = this.CreateDocumentationNode(diagnostic, columnOffset);
            var tr = methodDetails.WithLeadingTrivia(newLeadingTrivia);

            var newRoot = root.ReplaceNode(methodDetails, tr);

            return document.WithSyntaxRoot(newRoot);
        }

        private SyntaxTrivia CreateDocumentationNode(Diagnostic diagnostic, int columnOffset)
        {
            var comments = this.GenerateComment(diagnostic, columnOffset);

            return SyntaxFactory.Comment(comments);
        }

        private string GenerateComment(Diagnostic diagnostic, int columnOffset)
        { 
            var props = diagnostic.Properties;
            StringBuilder comments = new StringBuilder();
            string columnOffsetString = new String(' ', columnOffset);

            comments.AppendLine();
            comments.AppendLine($"{columnOffsetString}/// <summary>");
            comments.AppendLine($"{columnOffsetString}/// {props["summary"]}");
            comments.AppendLine($"{columnOffsetString}/// </summary>");

            var keys = props.Keys.Where(x => x.Contains("Parameter")).ToList();
            keys.Sort();

            // add params
            foreach (var param in keys)
            {
                var paramDetails = props[param];
                var paramName = param.Split(':')[1];
                comments.AppendLine($"{columnOffsetString}/// <param name=\"{paramName}\">{paramDetails}</param>");
            }

            if (props.ContainsKey("returns"))
            { 
                comments.AppendLine($"{columnOffsetString}/// <returns>{props["returns"]}</returns>");
            }

            return $"{comments}{columnOffsetString}";
        }
    }
}
