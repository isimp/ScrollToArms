using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace ScrollToArms.Standards
{
    /// <summary>
    /// Every file under src/ is compiled into the unit tests, or is named in GameOnlyFiles.cs as one
    /// that needs the game, with why. A file on neither would go untested without anyone noticing:
    /// the unit tests would never see it, and nothing would say so.
    /// </summary>
    public class UnitTestReachStandardTests
    {
        private const string UnitTests = "tests/ScrollToArms.Tests/ScrollToArms.Tests.csproj";

        [Fact]
        public void EverySourceFileIsInTheUnitTestsOrNamedAsGameOnly()
        {
            var compiled = Compiled();
            var found = SourceFiles()
                .Where(file => !compiled.Contains(file) && !GameOnly(file))
                .Select(file => file + " (compile it into " + UnitTests + ", or name it in GameOnlyFiles.cs with why it needs the game)");
            Violations.None("are neither in the unit tests nor named as game-only", found);
        }

        [Fact]
        public void AFileNamedAsGameOnlyIsLeftOutOfTheUnitTests()
        {
            var compiled = Compiled();
            var found = SourceFiles().Where(file => compiled.Contains(file) && GameOnly(file));
            Violations.None("are named as game-only but compiled into the unit tests", found);
        }

        [Fact]
        public void EachGameOnlyNameStandsForAFile()
        {
            var files = SourceFiles().ToList();
            var found = GameOnlyFiles.List.Keys.Where(name => !files.Any(file => Glob(name).IsMatch(file)));
            Violations.None("in GameOnlyFiles.cs name no file under src/", found);
        }

        [Fact]
        public void EachGameOnlyFileSaysWhy()
        {
            var found = GameOnlyFiles.List.Where(entry => string.IsNullOrWhiteSpace(entry.Value)).Select(entry => entry.Key);
            Violations.None("in GameOnlyFiles.cs give no reason", found);
        }

        private static IEnumerable<string> SourceFiles() => ScrollToArmsSource.Files("src").Select(ScrollToArmsSource.Relative);

        private static bool GameOnly(string file) => GameOnlyFiles.List.Keys.Any(name => Glob(name).IsMatch(file));

        /// <summary>
        /// The files under src/ the unit test project compiles: its Compile items, and those of a
        /// Directory.Build.props at the root, which every project below it takes in too. An Exclude
        /// or a Remove takes files back out.
        /// </summary>
        private static HashSet<string> Compiled()
        {
            var root = ScrollToArmsSource.Root();
            var included = new List<Regex>();
            var excluded = new List<Regex>();
            foreach (var project in new[] { "Directory.Build.props", UnitTests })
            {
                var path = Path.Combine(root, project);
                if (!File.Exists(path)) continue;

                var folder = Path.GetDirectoryName(project)?.Replace('\\', '/') ?? "";
                foreach (var item in XDocument.Load(path).Descendants("Compile"))
                {
                    included.AddRange(Patterns(item.Attribute("Include")?.Value, folder));
                    excluded.AddRange(Patterns(item.Attribute("Exclude")?.Value, folder));
                    excluded.AddRange(Patterns(item.Attribute("Remove")?.Value, folder));
                }
            }

            return new HashSet<string>(SourceFiles().Where(file => included.Any(p => p.IsMatch(file)) && !excluded.Any(p => p.IsMatch(file))));
        }

        /// <summary>An item's paths, each as a pattern from the repository's root.</summary>
        private static IEnumerable<Regex> Patterns(string value, string folder)
        {
            if (string.IsNullOrEmpty(value)) yield break;

            foreach (var raw in value.Split(';'))
            {
                var path = raw.Trim().Replace('\\', '/');
                if (path.Length == 0) continue;

                // The file's own folder, which a path without it is relative to as well.
                const string here = "$(MSBuildThisFileDirectory)";
                if (path.StartsWith(here, System.StringComparison.Ordinal)) path = path.Substring(here.Length);
                if (path.Contains("$(")) continue;

                yield return Glob(Normalized(folder.Length == 0 ? path : folder + "/" + path));
            }
        }

        /// <summary>A path with its "." and ".." steps walked: "tests/ScrollToArms.Tests/../../src/**/*.cs" is "src/**/*.cs".</summary>
        private static string Normalized(string path)
        {
            var steps = new List<string>();
            foreach (var step in path.Split('/'))
            {
                if (step == "." || step.Length == 0) continue;
                if (step == ".." && steps.Count > 0) steps.RemoveAt(steps.Count - 1);
                else steps.Add(step);
            }
            return string.Join("/", steps);
        }

        /// <summary>A path with MSBuild's wildcards as a pattern: ** for any folders, * for any part of one name.</summary>
        private static Regex Glob(string pattern) =>
            new Regex("^" + Regex.Escape(pattern).Replace(@"\*\*/", "(.*/)?").Replace(@"\*\*", ".*").Replace(@"\*", "[^/]*") + "$");
    }
}
