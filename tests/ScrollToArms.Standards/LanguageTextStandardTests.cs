using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ScrollToArms.Standards
{
    /// <summary>
    /// A number with a fraction, a point in time or a span of time becomes text one way whatever
    /// the PC's language: with the invariant culture named. Left to the PC, 0.5 is written 0,5 on
    /// a German one, which a file read back elsewhere misreads and the panel shows unlike the
    /// game's own numbers. The analysis already holds <c>ToString</c> and <c>string.Format</c> to
    /// it; this holds what it cannot see, a value put into an interpolated string or added to
    /// text, unless the whole string is made with <c>FormattableString.Invariant</c>.
    /// </summary>
    public class LanguageTextStandardTests
    {
        [Xunit.Fact]
        public void FractionsAndTimesBecomeTextTheSameWayEverywhere()
        {
            var found = new List<string>();
            foreach (var (hole, model) in ScrollToArmsSource.All<InterpolationSyntax>())
            {
                if (!Violations.WrittenByLanguage(model.GetTypeInfo(hole.Expression).Type)) continue;
                if (Invariant(hole, model)) continue;
                found.Add($"{ScrollToArmsSource.Where(hole)} {{{hole.Expression}}} in {ScrollToArmsSource.TopType(hole)}.{ScrollToArmsSource.Member(hole)}");
            }
            foreach (var (add, model) in ScrollToArmsSource.All<BinaryExpressionSyntax>())
            {
                if (!add.IsKind(SyntaxKind.AddExpression) || model.GetTypeInfo(add).Type?.SpecialType != SpecialType.System_String) continue;
                foreach (var side in new[] { add.Left, add.Right })
                {
                    if (Violations.WrittenByLanguage(model.GetTypeInfo(side).Type))
                        found.Add($"{ScrollToArmsSource.Where(side)} \"...\" + {side} in {ScrollToArmsSource.TopType(add)}.{ScrollToArmsSource.Member(add)}");
                }
            }
            Violations.None("write a fraction or a time in the PC's language", found);
        }

        /// <summary>Whether the interpolated string is handed whole to FormattableString.Invariant.</summary>
        private static bool Invariant(InterpolationSyntax hole, SemanticModel model)
        {
            var text = hole.Parent as InterpolatedStringExpressionSyntax;
            if (!(text?.Parent is ArgumentSyntax argument) || !(argument.Parent?.Parent is InvocationExpressionSyntax call)) return false;
            return model.GetSymbolInfo(call).Symbol is IMethodSymbol method
                   && method.Name == "Invariant" && method.ContainingType?.ToDisplayString() == "System.FormattableString";
        }
    }
}
