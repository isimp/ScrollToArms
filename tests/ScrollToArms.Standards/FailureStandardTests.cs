using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace ScrollToArms.Standards
{
    /// <summary>
    /// A failure in ScrollToArms never takes the game or another mod down with it, and is never lost
    /// either. A catch that takes every failure (<c>catch</c>, <c>catch (Exception)</c>) tells
    /// it: through one of <c>Plugin.Log</c>'s levels, or a <c>WarnOnce</c>, once for each place
    /// however often it fails, where code that runs every frame gets one; or it passes the failure
    /// on. Where a failure only means a value cannot be read, a member of the game that
    /// reflection cannot find after an update, the catch begins with a
    /// comment saying so, and that is the whole of what it means.
    /// </summary>
    public class FailureStandardTests
    {
        [Fact]
        public void ACatchOfEveryFailureTellsItOrSaysWhatItMeans()
        {
            var found = new List<string>();
            foreach (var (clause, model) in ScrollToArmsSource.All<CatchClauseSyntax>())
            {
                if (!CatchesEverything(clause, model)) continue;
                if (Tells(clause.Block, model) || PassesOn(clause.Block) || Explains(clause)) continue;
                found.Add($"{ScrollToArmsSource.Where(clause)} in {ScrollToArmsSource.TopType(clause)}.{ScrollToArmsSource.Member(clause)}");
            }
            Violations.None("catch every failure without telling it or saying what it means", found);
        }

        [Fact]
        public void AFailureIsHandedToWarnOnceRatherThanSpelledIntoItsMessage()
        {
            // WarnOnce writes the stack of the first only; a failure spelled into the message
            // writes it every time, which in code run each frame floods the log.
            var found = new List<string>();
            foreach (var (call, model) in ScrollToArmsSource.All<InvocationExpressionSyntax>())
            {
                if (!(model.GetSymbolInfo(call).Symbol is IMethodSymbol method) || method.Name != "WarnOnce") continue;
                var message = call.ArgumentList.Arguments.FirstOrDefault()?.Expression;
                if (message == null) continue;
                var spelled = message.DescendantNodesAndSelf().OfType<ExpressionSyntax>()
                    .Any(e => !(e.Parent is MemberAccessExpressionSyntax) && Violations.IsException(model.GetTypeInfo(e).Type));
                if (spelled) found.Add($"{ScrollToArmsSource.Where(call)} in {ScrollToArmsSource.TopType(call)}.{ScrollToArmsSource.Member(call)}");
            }
            Violations.None("spell a whole failure into a WarnOnce message", found);
        }

        private static bool CatchesEverything(CatchClauseSyntax clause, SemanticModel model) =>
            clause.Declaration == null || model.GetTypeInfo(clause.Declaration.Type).Type?.ToDisplayString() == "System.Exception";

        /// <summary>Whether the block reports through WarnOnce or the plugin's log.</summary>
        private static bool Tells(BlockSyntax block, SemanticModel model) =>
            block.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call =>
            {
                if (!(model.GetSymbolInfo(call).Symbol is IMethodSymbol method)) return false;
                if (method.Name == "WarnOnce") return true;
                return method.ContainingType?.ToDisplayString() == "BepInEx.Logging.ManualLogSource";
            });

        private static bool PassesOn(BlockSyntax block) =>
            block.DescendantNodes().OfType<ThrowStatementSyntax>().Any() || block.DescendantNodes().OfType<ThrowExpressionSyntax>().Any();

        /// <summary>Whether the catch's block begins with a comment, the first thing inside its braces or on its own line.</summary>
        private static bool Explains(CatchClauseSyntax clause)
        {
            var inside = clause.Block.OpenBraceToken.TrailingTrivia.Concat(
                clause.Block.Statements.Count > 0 ? clause.Block.Statements[0].GetLeadingTrivia() : clause.Block.CloseBraceToken.LeadingTrivia);
            return inside.Any(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia));
        }
    }
}
