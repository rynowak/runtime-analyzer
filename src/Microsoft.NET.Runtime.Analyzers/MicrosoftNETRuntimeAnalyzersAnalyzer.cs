using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NET.Runtime.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MicrosoftNETRuntimeAnalyzersAnalyzer : DiagnosticAnalyzer
    {
        public static DiagnosticDescriptor ApiIsNotLinkerFriendlyNoMitigation = new DiagnosticDescriptor(
            "LINKER001",
            "API is not linker-friendly",
            "The usage of {0} is not linker-friendly. Avoid using this API or choose an alternative that is strongly-typed.",
            "Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static DiagnosticDescriptor ApiIsNotLinkerFriendlyAddPreserveDependency = new DiagnosticDescriptor(
            "LINKER002",
            "API is not linker-friendly",
            "The usage of {0} is not linker-friendly. Add [PreserveDependency(...)] to manually include members used by reflection.",
            "Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static DiagnosticDescriptor MemberIsNotLinkerFriendlyMarkAsUnfriendly = new DiagnosticDescriptor(
            "LINKER002",
            "API is not linker-friendly",
            "This member is not linker-friendly because the type parameter {1} is passed to a linker-unfriendly API {0}. Add [LinkerUnfriendly] to the declaration.",
            "Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private ImmutableDictionary<string, Entry> _apis;

        public MicrosoftNETRuntimeAnalyzersAnalyzer()
        {
            _apis = ReadApis();
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(
                    ApiIsNotLinkerFriendlyNoMitigation,
                    ApiIsNotLinkerFriendlyAddPreserveDependency,
                    MemberIsNotLinkerFriendlyMarkAsUnfriendly);
            }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterOperationAction(
                AnalyzeOperation,
                OperationKind.Invocation);
        }

        private void AnalyzeOperation(OperationAnalysisContext context)
        {
            var operation = (IInvocationOperation)context.Operation;
            var docId = operation.TargetMethod.GetDocumentationCommentId();

            if (!_apis.TryGetValue(docId, out var entry))
            {
                return;
            }

            var attributes = context.ContainingSymbol.GetAttributes();
            for (var i = 0; i < attributes.Length; i++)
            {
                // API is already marked as unfriendly.
                if (string.Equals(attributes[i].AttributeClass.MetadataName, "LinkerUnfriendlyAttribute"))
                {
                    return;
                }
            }

            if (entry.Actions.Length == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ApiIsNotLinkerFriendlyNoMitigation,
                    operation.Syntax.GetLocation(),
                    operation.TargetMethod.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)));
                return;
            }

            var types = new List<INamedTypeSymbol>();
            for (var i = 0; i < entry.Actions.Length; i++)
            {
                var action = entry.Actions[i];
                for (var j = 0; j < operation.TargetMethod.TypeParameters.Length; j++)
                {
                    var parameterDocId = operation.TargetMethod.TypeParameters[j].GetDocumentationCommentId();
                    if (!string.Equals(action.Parameter, parameterDocId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var typeArgument = operation.TargetMethod.TypeArguments[j];
                    if (typeArgument is INamedTypeSymbol namedType)
                    {
                        // TODO: visit for type parameters recursively.

                        if (!PreserveDependencyFacts.HasPreserveDependencyAttribute(attributes, namedType))
                        {
                            types.Add(namedType);
                        }
                    }
                    else if (typeArgument is ITypeParameterSymbol typeParameter)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            MemberIsNotLinkerFriendlyMarkAsUnfriendly,
                            operation.Syntax.GetLocation(),
                            operation.TargetMethod.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat),
                            typeParameter.Name));
                        return;
                    }
                }
            }

            for (var i = 0; i < types.Count; i++)
            {
                var type = types[i];

                var properties = new[]
                {
                    new KeyValuePair<string, string>("type", type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)),
                    new KeyValuePair<string, string>("assembly", type.ContainingAssembly.Name),
                }.ToImmutableDictionary();

                context.ReportDiagnostic(Diagnostic.Create(
                    ApiIsNotLinkerFriendlyAddPreserveDependency,
                    operation.Syntax.GetLocation(),
                    properties,
                    operation.TargetMethod.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)));
            }
        }

        private static ImmutableDictionary<string, Entry> ReadApis()
        {
            using var stream = typeof(MicrosoftNETRuntimeAnalyzersAnalyzer).Assembly.GetManifestResourceStream("Microsoft.NET.Runtime.Analyzers.Apis.txt");
            using var reader = new StreamReader(stream);

            var builder = ImmutableDictionary.CreateBuilder<string, Entry>();
            while (true)
            {
                var line = reader.ReadLine();
                if (line == null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var split = line.Split('|');
                var entry = split.Length switch
                {
                    1 => new Entry() { Actions = ImmutableArray<ParameterAction>.Empty, },
                    3 => new Entry() { Actions = ImmutableArray.Create(new ParameterAction() { Parameter = split[1], Action = split[2], }), },
                    _ => throw new InvalidOperationException("Invalid YO!"),
                };

                // TODO ERROR LOL
                builder.Add(split[0], entry);
            }

            return builder.ToImmutable();
        }

        private class Entry
        {
            public ImmutableArray<ParameterAction> Actions { get; set; }
        }

        private class ParameterAction
        {
            public string Parameter { get; set; } = default!;
            public string Action { get; set; } = default!;
        }
    }
}
