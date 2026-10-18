using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace ScrollToArms.Standards
{
    /// <summary>
    /// Comments and notes describe the code as it is and why it works so: what is there, the rule
    /// it follows and the reason behind it. They carry no dates or clock times, which tell when
    /// something changed rather than what it is. Nor do comments or notes use a term from the
    /// repository's local list, kept where git never commits it (<c>.git/info/forbidden-terms</c>,
    /// a term a line, <c>#</c> for a note), so what is kept out of the published code is never
    /// written into it, this test included; where the list is not there, as on the build server,
    /// that check has nothing to hold.
    /// </summary>
    public class CommentStandardTests
    {
        /// <summary>A day written as year, month and day, or a time of day as hours and minutes.</summary>
        private static readonly Regex DateOrTime = new Regex(@"\b20\d\d-\d\d-\d\d\b|\b\d\d:\d\d\b", RegexOptions.CultureInvariant);

        [Fact]
        public void CommentsCarryNoDatesOrClockTimes()
        {
            var found = new List<string>();
            foreach (var (where, text) in Comments())
            {
                foreach (Match date in DateOrTime.Matches(text)) found.Add($"{where} {date.Value}");
            }
            Violations.None("tell no dates or clock times in comments", found);
        }

        [Fact]
        public void CommentsAndNotesUseNoTermOfTheLocalList()
        {
            var terms = LocalTerms();
            if (terms.Count == 0) return;

            var found = new List<string>();
            foreach (var (where, text) in Comments().Concat(Notes()))
            {
                foreach (var term in terms)
                {
                    if (Regex.IsMatch(text, @"(?<!\w)" + Regex.Escape(term) + @"(?!\w)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) found.Add($"{where} uses a term of the local list");
                }
            }
            Violations.None("use no term of the local list in comments and notes", found);
        }

        [Theory]
        [InlineData("as the run at 19:03 told")]
        [InlineData("the rules before 2026-10-05")]
        public void ADateOrTimeIsFound(string comment) => Assert.Matches(DateOrTime, comment);

        [Theory]
        [InlineData("Ctrl+F1 frees the mouse")]
        [InlineData("Home opens the panel")]
        [InlineData("version 0.5.1, a 4:3 screen")]
        public void WhatIsNoDateOrTimeIsLeft(string comment) => Assert.DoesNotMatch(DateOrTime, comment);

        /// <summary>The local list's terms, none where it is not there.</summary>
        private static List<string> LocalTerms()
        {
            var list = Path.Combine(ScrollToArmsSource.Root(), ".git", "info", "forbidden-terms");
            if (!File.Exists(list)) return new List<string>();
            return File.ReadAllLines(list).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#", StringComparison.Ordinal)).ToList();
        }

        /// <summary>Every comment the standards read, the released code's, the self-test's and the tests', with where it is.</summary>
        private static IEnumerable<(string Where, string Text)> Comments()
        {
            foreach (var tree in ScrollToArmsSource.WrittenTrees())
            {
                foreach (var trivia in tree.GetRoot().DescendantTrivia(descendIntoTrivia: true))
                {
                    if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                        && !trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) && !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)) continue;
                    yield return ($"{ScrollToArmsSource.Relative(tree)}:{tree.GetLineSpan(trivia.Span).StartLinePosition.Line + 1}", trivia.ToFullString());
                }
            }
        }

        /// <summary>The README, the changelog and every note under docs, a line at a time.</summary>
        private static IEnumerable<(string Where, string Text)> Notes()
        {
            var root = ScrollToArmsSource.Root();
            var files = ScrollToArmsSource.Files("docs", "*.md")
                .Concat(new[] { "README.md", "CHANGELOG.md" }.Select(name => Path.Combine(root, name)).Where(File.Exists));
            foreach (var file in files)
            {
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++) yield return ($"{ScrollToArmsSource.Relative(file)}:{i + 1}", lines[i]);
            }
        }
    }
}
