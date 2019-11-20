using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        public const string DiagnosticId = "LINKER001";

        private const string Category = "Naming";

        private static DiagnosticDescriptor Diagnostic = new DiagnosticDescriptor(
            "LINKER001", 
            "API is not linker-friendly", 
            "The API {0} is not linker-friendly.", 
            Category, 
            DiagnosticSeverity.Warning, 
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Diagnostic); } }

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterOperationAction(
                AnalyzeOperation, 
                OperationKind.Invocation,
                OperationKind.PropertyReference);
        }

        private static void AnalyzeOperation(OperationAnalysisContext context)
        {

        }
    }
}
