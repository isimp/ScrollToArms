using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace ScrollToArms.Standards
{
    /// <summary>The standards tests read ScrollToArms's source with the compiler's types, which they can trust only while it compiles here as it does in the build.</summary>
    public class SourceCompilesTests
    {
        [Fact]
        public void ScrollToArmsAndItsSelfTestCompileHereAsInTheBuild()
        {
            var errors = ScrollToArmsSource.Compilations
                .SelectMany(c => c.GetDiagnostics())
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => $"{d.Location.GetLineSpan().Path}:{d.Location.GetLineSpan().StartLinePosition.Line + 1} {d.Id} {d.GetMessage()}")
                .Take(20)
                .ToList();
            Assert.True(errors.Count == 0, "ScrollToArms's source does not compile for the standards tests:\n" + string.Join("\n", errors));
        }

        [Fact]
        public void TheSourceIsThere()
        {
            Assert.Contains(ScrollToArmsSource.Compilation.SyntaxTrees, t => t.FilePath.EndsWith("Plugin.cs", System.StringComparison.Ordinal));
        }
    }
}
