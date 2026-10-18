using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace ScrollToArms.Standards
{
    /// <summary>
    /// The version BepInEx reads from the plugin, the one the build stamps on the DLL and the one
    /// the package's manifest.json gives the stores are one version. The release workflow checks
    /// the same three against the version typed in, but only once a release is started; this
    /// says so on every push, while the change is still fresh.
    /// </summary>
    public class VersionStandardTests
    {
        [Fact]
        public void ThePluginTheBuildAndTheManifestNameOneVersion()
        {
            var versions = new List<string>();
            versions.AddRange(PluginVersions().Select(v => "BepInPlugin " + v));
            versions.AddRange(BuildVersions().Select(v => v.File + " <Version> " + v.Version));
            versions.Add("packaging/manifest.json version_number " + ManifestVersion());

            var distinct = versions.Select(v => v.Substring(v.LastIndexOf(' ') + 1)).Distinct().ToList();
            Assert.True(versions.Count >= 3 && distinct.Count == 1, "the versions differ or one is missing:\n" + string.Join("\n", versions));
        }

        /// <summary>The version in each BepInPlugin attribute of what a release ships, read as the compiler reads it, so a constant counts as surely as a literal.</summary>
        private static IEnumerable<string> PluginVersions()
        {
            foreach (var compilation in ScrollToArmsSource.Released)
            {
                foreach (var tree in compilation.SyntaxTrees)
                {
                    var model = compilation.GetSemanticModel(tree);
                    foreach (var attribute in tree.GetRoot().DescendantNodes().OfType<AttributeSyntax>())
                    {
                        if (model.GetTypeInfo(attribute).Type?.Name != "BepInPlugin" || attribute.ArgumentList?.Arguments.Count != 3) continue;
                        yield return model.GetConstantValue(attribute.ArgumentList.Arguments[2].Expression).Value as string ?? "?";
                    }
                }
            }
        }

        /// <summary>Every &lt;Version&gt; the build reads: Directory.Build.props and the project files at the root and under src/.</summary>
        private static IEnumerable<(string File, string Version)> BuildVersions()
        {
            var root = ScrollToArmsSource.Root();
            var files = new[] { Path.Combine(root, "Directory.Build.props") }
                .Concat(Directory.EnumerateFiles(root, "*.csproj"))
                .Concat(ScrollToArmsSource.Files("src", "*.csproj"))
                .Where(File.Exists);
            foreach (var file in files)
            {
                foreach (var version in XDocument.Load(file).Descendants("Version"))
                    yield return (ScrollToArmsSource.Relative(file), version.Value.Trim());
            }
        }

        private static string ManifestVersion()
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(ScrollToArmsSource.Root(), "packaging", "manifest.json")));
            return manifest.RootElement.GetProperty("version_number").GetString();
        }
    }
}
