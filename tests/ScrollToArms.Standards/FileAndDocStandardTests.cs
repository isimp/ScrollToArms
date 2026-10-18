using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace ScrollToArms.Standards
{
    /// <summary>
    /// Every file of the repository's own reads as it was written: a sign a tool could not read
    /// (U+FFFD, the replacement character) is a letter lost on the way, and stays lost unless a
    /// test finds it.
    /// </summary>
    public class FileStandardTests
    {
        private static readonly string[] Folders = { "src", "selftest", "tests", "docs", "tools", "packaging", ".github", ".githooks" };
        private static readonly string[] Kinds = { ".cs", ".md", ".json", ".csproj", ".props", ".globalconfig", ".py", ".ps1", ".yml", ".txt", ".keys", ".pins", ".sln", "" };

        /// <summary>Written by its number, so this file does not hold it.</summary>
        private const char LostCharacter = (char)0xFFFD;

        [Fact]
        public void NoFileHoldsACharacterLostOnTheWay()
        {
            var root = ScrollToArmsSource.Root();
            var files = Folders
                .SelectMany(folder => ScrollToArmsSource.Files(folder, "*"))
                .Concat(Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
                .Where(file => Kinds.Contains(Path.GetExtension(file)))
                .ToList();
            Assert.NotEmpty(files);

            var found = new List<string>();
            foreach (var file in files)
            {
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].IndexOf(LostCharacter) >= 0) found.Add($"{ScrollToArmsSource.Relative(file)}:{i + 1}");
                }
            }
            Violations.None("put back the character lost on the way", found);
        }
    }

    /// <summary>
    /// Each doc comment belongs to the member it stands over: one summary to a member, and none
    /// left behind where a member moved away, which would otherwise read as the next one's.
    /// </summary>
    public class DocStandardTests
    {
        [Fact]
        public void EachMemberHasOneSummaryOfItsOwn()
        {
            var found = new List<string>();
            var trees = ScrollToArmsSource.Compilations.SelectMany(c => c.SyntaxTrees).Where(t => !string.IsNullOrEmpty(t.FilePath)).GroupBy(t => t.FilePath).Select(g => g.First());
            foreach (var tree in trees)
            {
                foreach (var token in tree.GetRoot().DescendantTokens())
                {
                    var docs = token.LeadingTrivia.Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)).ToList();
                    if (docs.Count == 0) continue;

                    var where = $"{ScrollToArmsSource.Relative(tree)}:{docs[0].GetLocation().GetLineSpan().StartLinePosition.Line + 1}";
                    var summaries = docs.Sum(d => d.GetStructure().DescendantNodes().OfType<XmlElementSyntax>().Count(e => e.StartTag.Name.LocalName.Text == "summary"));
                    if (summaries > 1) found.Add($"{where} {summaries} summaries over one member");
                    if (!StartsADeclaration(token)) found.Add($"{where} a doc comment over no member");
                }
            }
            Violations.None("leave a doc comment that is not one member's own", found);
        }

        /// <summary>Whether a token is the first of a member's, an enum value's or a local function's declaration.</summary>
        private static bool StartsADeclaration(SyntaxToken token) =>
            token.Parent.AncestorsAndSelf().Any(n => (n is MemberDeclarationSyntax || n is LocalFunctionStatementSyntax || n is AccessorDeclarationSyntax) && n.GetFirstToken() == token);
    }
}
