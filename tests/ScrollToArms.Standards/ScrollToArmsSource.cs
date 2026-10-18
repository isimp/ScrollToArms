using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ScrollToArms.Standards
{
    /// <summary>
    /// ScrollToArms's source compiled as the build compiles it: every file under src/ as ScrollToArms, and
    /// every file under selftest/ as the self-test beside it, against the reference copies in lib/
    /// and the .NET Framework 4.7.2 reference assemblies. The standards tests read both with the
    /// compiler's own types, so a rule about text or numbers sees an implicit conversion as
    /// surely as a written one. Compiled once per test run.
    /// </summary>
    internal static class ScrollToArmsSource
    {
        private static readonly Lazy<CSharpCompilation> Built = new Lazy<CSharpCompilation>(Build);
        private static readonly Lazy<CSharpCompilation> BuiltSelfTest = new Lazy<CSharpCompilation>(BuildSelfTest);

        /// <summary>ScrollToArms itself, src/.</summary>
        public static CSharpCompilation Compilation => Built.Value;

        /// <summary>The self-test plugin, selftest/, which reaches ScrollToArms's insides as its project grants it.</summary>
        public static CSharpCompilation SelfTest => BuiltSelfTest.Value;

        /// <summary>What a release ships: ScrollToArms alone.</summary>
        public static IEnumerable<CSharpCompilation> Released
        {
            get
            {
                yield return Compilation;
            }
        }

        /// <summary>Both, ScrollToArms first.</summary>
        public static IEnumerable<CSharpCompilation> Compilations
        {
            get
            {
                yield return Compilation;
                yield return SelfTest;
            }
        }

        /// <summary>The repository's root, the folder holding ScrollToArms.csproj.</summary>
        public static string Root()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "ScrollToArms.csproj"))) return dir.FullName;
            }
            throw new InvalidOperationException("ScrollToArms.csproj not found above the test run.");
        }

        /// <summary>ScrollToArms's source and the self-test's as compiled, and the tests' own, read as written: every file whose comments a standard reads.</summary>
        public static IEnumerable<SyntaxTree> WrittenTrees()
        {
            foreach (var compilation in Compilations)
            {
                foreach (var tree in compilation.SyntaxTrees)
                {
                    if (!string.IsNullOrEmpty(tree.FilePath)) yield return tree;
                }
            }
            foreach (var file in Files("tests")) yield return CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file);
        }

        /// <summary>A file's path from the repository's root, with forward slashes: "src/Picker.cs".</summary>
        public static string Relative(SyntaxTree tree) => Relative(tree.FilePath);

        /// <summary>A path from the repository's root, with forward slashes.</summary>
        public static string Relative(string path) => Path.GetRelativePath(Root(), path).Replace('\\', '/');

        /// <summary>Where a node is, as "src/Picker.cs:12", for a failure to name.</summary>
        public static string Where(SyntaxNode node) => $"{Relative(node.SyntaxTree)}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";

        /// <summary>Every node of a kind in ScrollToArms's source and the self-test's, with the semantic model of its file.</summary>
        public static IEnumerable<(T Node, SemanticModel Model)> All<T>() where T : SyntaxNode => In<T>(Compilations);

        /// <summary>Every node of a kind in the given compilations' source, with the semantic model of its file.</summary>
        public static IEnumerable<(T Node, SemanticModel Model)> In<T>(IEnumerable<CSharpCompilation> compilations) where T : SyntaxNode
        {
            foreach (var compilation in compilations)
            {
                foreach (var tree in compilation.SyntaxTrees)
                {
                    if (string.IsNullOrEmpty(tree.FilePath)) continue;
                    var model = compilation.GetSemanticModel(tree);
                    foreach (var node in tree.GetRoot().DescendantNodes().OfType<T>()) yield return (node, model);
                }
            }
        }

        /// <summary>The type a node stands in: its outermost type declaration's name.</summary>
        public static string TopType(SyntaxNode node) =>
            node.AncestorsAndSelf().OfType<BaseTypeDeclarationSyntax>().LastOrDefault()?.Identifier.Text;

        /// <summary>The method, property or other member a node stands in, by name.</summary>
        public static string Member(SyntaxNode node)
        {
            foreach (var up in node.AncestorsAndSelf())
            {
                switch (up)
                {
                    case MethodDeclarationSyntax m: return m.Identifier.Text;
                    case PropertyDeclarationSyntax p: return p.Identifier.Text;
                    case ConstructorDeclarationSyntax c: return c.Identifier.Text;
                    case LocalFunctionStatementSyntax _: continue;
                    case FieldDeclarationSyntax f: return f.Declaration.Variables.First().Identifier.Text;
                    case IndexerDeclarationSyntax _: return "this[]";
                    case OperatorDeclarationSyntax o: return "operator " + o.OperatorToken.Text;
                }
            }
            return null;
        }

        /// <summary>The repository's own files under a folder, leaving out what a build made.</summary>
        public static IEnumerable<string> Files(string folder, string pattern = "*.cs")
        {
            var path = Path.Combine(Root(), folder);
            if (!Directory.Exists(path)) return Enumerable.Empty<string>();
            var separator = Path.DirectorySeparatorChar;
            return Directory.EnumerateFiles(path, pattern, SearchOption.AllDirectories)
                .Where(file => !file.Contains(separator + "bin" + separator) && !file.Contains(separator + "obj" + separator))
                .OrderBy(file => file, StringComparer.Ordinal);
        }

        private static readonly CSharpParseOptions Parse = new CSharpParseOptions(LanguageVersion.Latest);

        private static List<SyntaxTree> Trees(string folder) =>
            Files(folder).Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), Parse, path)).ToList();

        private static CSharpCompilation Build()
        {
            var trees = Trees("src");
            // What ScrollToArms.csproj grants the self-test, which the build writes as an attribute of its own.
            trees.Add(CSharpSyntaxTree.ParseText("[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"ScrollToArms.SelfTest\")]", Parse));
            return CSharpCompilation.Create("ScrollToArms", trees, References(), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }

        private static CSharpCompilation BuildSelfTest() =>
            CSharpCompilation.Create("ScrollToArms.SelfTest", Trees("selftest"), References().Append(Compilation.ToMetadataReference()),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        private static IEnumerable<MetadataReference> References()
        {
            var framework = typeof(ScrollToArmsSource).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .First(a => a.Key == "Net472References").Value;
            return Directory.GetFiles(Path.Combine(Root(), "lib"), "*.dll")
                .Concat(Directory.GetFiles(framework, "*.dll"))
                .Concat(Directory.GetFiles(Path.Combine(framework, "Facades"), "*.dll"))
                .Where(Managed)
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
                .ToList();
        }

        /// <summary>Whether a library is a .NET assembly; the framework folder holds two that are not, which the build never references.</summary>
        private static bool Managed(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var pe = new System.Reflection.PortableExecutable.PEReader(stream))
            {
                return pe.HasMetadata && System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe).IsAssembly;
            }
        }
    }
}
