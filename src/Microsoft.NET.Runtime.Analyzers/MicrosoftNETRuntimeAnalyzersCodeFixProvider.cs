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
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.NET.Runtime.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MicrosoftNETRuntimeAnalyzersCodeFixProvider)), Shared]
    public class MicrosoftNETRuntimeAnalyzersCodeFixProvider : CodeFixProvider
    {
        private const string Title = "Add [PreserveDependency(...)]";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(MicrosoftNETRuntimeAnalyzersAnalyzer.ApiIsNotLinkerFriendlyAddPreserveDependency.Id); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            foreach (var diagnostic in context.Diagnostics)
            {
                var method = root.FindToken(diagnostic.Location.SourceSpan.Start).Parent.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().First();

                // Register a code action that will invoke the fix.
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: Title,
                        createChangedDocument: c => MakeUppercaseAsync(context.Document, method, diagnostic.Properties["type"], diagnostic.Properties["assembly"], c),
                        equivalenceKey: Title),
                    diagnostic);
            }
        }

        private async Task<Document> MakeUppercaseAsync(Document document, MethodDeclarationSyntax method, string typeName, string assemblyName, CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken);
            editor.AddAttribute(
                method,
                SyntaxFactory.Attribute(
                    SyntaxFactory.QualifiedName(
                        SyntaxFactory.QualifiedName(
                            SyntaxFactory.QualifiedName(
                                SyntaxFactory.IdentifierName("System"),
                                SyntaxFactory.IdentifierName("Runtime")),
                            SyntaxFactory.IdentifierName("CompilerServices")),
                        SyntaxFactory.IdentifierName("PreserveDependency")))
                .WithArgumentList(
                    SyntaxFactory.AttributeArgumentList(
                        SyntaxFactory.SeparatedList<AttributeArgumentSyntax>(
                            new SyntaxNodeOrToken[]
                            {
                                SyntaxFactory.AttributeArgument(
                                    SyntaxFactory.LiteralExpression(
                                        SyntaxKind.NullLiteralExpression)),
                                SyntaxFactory.Token(SyntaxKind.CommaToken),
                                SyntaxFactory.AttributeArgument(
                                    SyntaxFactory.LiteralExpression(
                                        SyntaxKind.StringLiteralExpression,
                                        SyntaxFactory.Literal(typeName))),
                                SyntaxFactory.Token(SyntaxKind.CommaToken),
                                SyntaxFactory.AttributeArgument(
                                    SyntaxFactory.LiteralExpression(
                                        SyntaxKind.StringLiteralExpression,
                                        SyntaxFactory.Literal(assemblyName))),
                            }))));

            return editor.GetChangedDocument();
        }
    }
}
