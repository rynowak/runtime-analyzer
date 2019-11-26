using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Microsoft.NET.Runtime.Analyzers
{
    internal static class PreserveDependencyFacts
    {
        public static bool HasPreserveDependencyAttribute(IImmutableList<AttributeData> attributes, INamedTypeSymbol type)
        {
            for (var i = 0; i < attributes.Count; i++)
            {
                var attribute = attributes[i];
                if (attribute.AttributeClass.MetadataName != "PreserveDependencyAttribute" || attribute.ConstructorArguments.Length != 3)
                {
                    continue;
                }

                var memberName = attribute.ConstructorArguments[0].Value as string;
                var typeName = attribute.ConstructorArguments[1].Value as string;
                var assemblyName = attribute.ConstructorArguments[2].Value as string;

                if (memberName == null &&
                    (("global::" + typeName) == type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)) &&
                    assemblyName == type.ContainingAssembly.Name)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
