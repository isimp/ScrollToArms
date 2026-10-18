using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace ScrollToArms.Standards
{
    /// <summary>
    /// Text compares and sorts one way whatever language the PC is set to: by its characters,
    /// ordinally, with or without case. A comparison, a change of case or a sort that takes the
    /// PC's language orders a list differently on a Swedish PC than on an English one, and an
    /// invariant one still follows a language's rules; both are left out, implicit ones too
    /// (an <c>OrderBy</c> by a name with no comparer, a <c>StartsWith</c> with no comparison).
    /// </summary>
    public class StringStandardTests
    {
        private static readonly HashSet<string> Comparing = new HashSet<string> { "StartsWith", "EndsWith", "IndexOf", "LastIndexOf", "Compare", "CompareTo", "Equals" };
        private static readonly HashSet<string> Sorting = new HashSet<string> { "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending", "Min", "Max" };

        [Fact]
        public void TextComparesAndSortsByItsCharacters()
        {
            var found = new List<string>();
            void Add(SyntaxNode node, string how) => found.Add($"{ScrollToArmsSource.Where(node)} {how}");

            foreach (var (call, model) in ScrollToArmsSource.All<InvocationExpressionSyntax>())
            {
                if (!(model.GetSymbolInfo(call).Symbol is IMethodSymbol method)) continue;
                var owner = method.ContainingType?.ToDisplayString();
                var parameters = method.Parameters;

                if (owner == "string" && Comparing.Contains(method.Name)
                    && parameters.Any(p => p.Type.SpecialType == SpecialType.System_String)
                    && !parameters.Any(p => p.Type.Name == "StringComparison"))
                {
                    // Equals of two strings is ordinal; the others take the PC's language unless told.
                    if (method.Name != "Equals") Add(call, $"string.{method.Name} without a StringComparison");
                }

                if (owner == "string" && (method.Name == "ToLower" || method.Name == "ToUpper")) Add(call, $"{method.Name} in the PC's language");
                if (owner == "char" && (method.Name == "ToLower" || method.Name == "ToUpper")) Add(call, $"char.{method.Name} in the PC's language");

                var sortsText = owner == "System.Linq.Enumerable" && Sorting.Contains(method.Name)
                                && method.TypeArguments.Length == 2 && method.TypeArguments[1].SpecialType == SpecialType.System_String
                                && !parameters.Any(p => p.Type.Name == "IComparer");
                if (sortsText) Add(call, $"{method.Name} by text without a comparer");

                if (method.Name == "Sort" && parameters.Length == 0 && method.ContainingType is INamedTypeSymbol list
                    && list.TypeArguments.Length == 1 && list.TypeArguments[0].SpecialType == SpecialType.System_String)
                    Add(call, "a list of text sorted without a comparer");
                if (owner == "System.Array" && method.Name == "Sort" && parameters.Length == 1) Add(call, "an array sorted without a comparer");
            }

            foreach (var (access, model) in ScrollToArmsSource.All<MemberAccessExpressionSyntax>())
            {
                var name = access.Name.Identifier.Text;
                if ((name == "CurrentCulture" || name == "CurrentCultureIgnoreCase" || name == "InvariantCulture" || name == "InvariantCultureIgnoreCase")
                    && model.GetSymbolInfo(access).Symbol?.ContainingType?.Name is string type && (type == "StringComparer" || type == "StringComparison"))
                    Add(access, $"{type}.{name}");
                if (name == "Default" && model.GetTypeInfo(access).Type is INamedTypeSymbol comparer && comparer.Name == "Comparer"
                    && comparer.TypeArguments.Length == 1 && comparer.TypeArguments[0].SpecialType == SpecialType.System_String)
                    Add(access, "Comparer<string>.Default");
            }

            foreach (var (creation, model) in ScrollToArmsSource.All<ObjectCreationExpressionSyntax>())
            {
                if (!(model.GetTypeInfo(creation).Type is INamedTypeSymbol made) || !(made.Name == "SortedSet" || made.Name == "SortedDictionary" || made.Name == "SortedList")) continue;
                if (made.TypeArguments.Length == 0 || made.TypeArguments[0].SpecialType != SpecialType.System_String) continue;
                var comparer = model.GetSymbolInfo(creation).Symbol is IMethodSymbol ctor && ctor.Parameters.Any(p => p.Type.Name == "IComparer");
                if (!comparer) Add(creation, $"a {made.Name} of text without a comparer");
            }

            Violations.None("compare or sort text in a language's way", found);
        }
    }
}
