using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace ScrollToArms.Standards
{
    /// <summary>What the standards tests share: the types they ask about and how they say what breaks a standard.</summary>
    internal static class Violations
    {
        /// <summary>Whether a type is a number with a fraction or a point in time (or a nullable one): what the PC's language writes its own way.</summary>
        public static bool WrittenByLanguage(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T) type = named.TypeArguments[0];
            switch (type?.SpecialType)
            {
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_Decimal:
                case SpecialType.System_DateTime:
                    return true;
            }
            var name = type?.ToDisplayString();
            return name == "System.DateTimeOffset" || name == "System.TimeSpan";
        }

        /// <summary>Whether a type is UnityEngine.Object or derives from it.</summary>
        public static bool IsUnityObject(ITypeSymbol type)
        {
            for (var each = type; each != null; each = each.BaseType)
            {
                if (each.ToDisplayString() == "UnityEngine.Object") return true;
            }
            return false;
        }

        /// <summary>Whether a type is System.Exception or derives from it.</summary>
        public static bool IsException(ITypeSymbol type)
        {
            for (var each = type; each != null; each = each.BaseType)
            {
                if (each.ToDisplayString() == "System.Exception") return true;
            }
            return false;
        }

        /// <summary>Whether a node stands in one of these files, by its name: "TextStore.cs".</summary>
        public static bool In(SyntaxNode node, params string[] files) =>
            files.Contains(System.IO.Path.GetFileName(node.SyntaxTree.FilePath));

        /// <summary>
        /// Fails with every place that breaks the standard, one a line, sorted; the message shows
        /// the first 400, and with SCROLLTOARMS_VIOLATIONS naming a folder every one is written to a
        /// file there named for the standard.
        /// </summary>
        public static void None(string standard, IEnumerable<string> found)
        {
            var list = found.Distinct().OrderBy(s => s, System.StringComparer.Ordinal).ToList();
            var folder = System.Environment.GetEnvironmentVariable("SCROLLTOARMS_VIOLATIONS");
            if (list.Count > 0 && !string.IsNullOrEmpty(folder))
                System.IO.File.WriteAllLines(System.IO.Path.Combine(folder, System.Text.RegularExpressions.Regex.Replace(standard, @"\W+", "-") + ".txt"), list);
            Assert.True(list.Count == 0, $"{list.Count} place(s) {standard}:\n" + string.Join("\n", list.Take(400)));
        }
    }
}
