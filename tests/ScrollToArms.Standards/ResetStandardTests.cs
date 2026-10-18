using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace ScrollToArms.Standards
{
    /// <summary>
    /// A class with a Reset() for the tests forgets all it holds there: every static field that
    /// can change, and every static property with a setter, is assigned or cleared in Reset() or a
    /// method of the class it calls. The tests run many games in one process, so a field Reset()
    /// misses carries one test's state into the next, and tests then pass or fail by the order
    /// they run in. A readonly collection counts only where some code changes what it holds; one
    /// filled where it is declared and only read is a table, not state. Events are left out:
    /// their handlers are other classes', signed up once.
    /// </summary>
    public class ResetStandardTests
    {
        /// <summary>The methods that empty a collection in place.</summary>
        private static readonly HashSet<string> Emptying = new HashSet<string> { "Clear", "TryDequeue", "TryTake", "RemoveAll" };

        /// <summary>The methods that change what a collection holds.</summary>
        private static readonly HashSet<string> Changing = new HashSet<string>
        {
            "Add", "AddRange", "Insert", "Remove", "RemoveAt", "RemoveAll", "Clear", "Enqueue", "Dequeue", "TryDequeue",
            "TryAdd", "TryRemove", "TryTake", "Push", "Pop", "UnionWith", "ExceptWith", "IntersectWith", "Sort", "Reverse",
        };

        [Fact]
        public void ResetForgetsEverythingItsClassHolds()
        {
            var found = new List<string>();
            foreach (var compilation in ScrollToArmsSource.Released)
            {
                var changed = Changed(compilation);
                foreach (var type in Types(compilation.GlobalNamespace))
                {
                    var reset = type.GetMembers("Reset").OfType<IMethodSymbol>().FirstOrDefault(m => m.IsStatic && m.Parameters.Length == 0);
                    if (reset == null) continue;

                    var forgotten = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
                    Walk(compilation, reset, type, new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default), forgotten);

                    foreach (var held in Held(type, changed).Where(h => !forgotten.Contains(h)))
                        found.Add(type.Name + "." + held.Name + " (Reset() at " + Where(reset) + ")");
                }
            }
            Violations.None("hold state their class's Reset() does not forget", found);
        }

        /// <summary>What a class holds that can change: static fields not constant, readonly ones only when they hold a collection some code changes, and static properties with a setter.</summary>
        private static IEnumerable<ISymbol> Held(INamedTypeSymbol type, HashSet<ISymbol> changed)
        {
            foreach (var member in type.GetMembers())
            {
                if (member is IFieldSymbol field && field.IsStatic && !field.IsConst && !field.IsImplicitlyDeclared &&
                    (!field.IsReadOnly || (IsCollection(field.Type) && changed.Contains(field))))
                    yield return field;
                else if (member is IPropertySymbol property && property.IsStatic && property.SetMethod != null)
                    yield return property;
            }
        }

        private static bool IsCollection(ITypeSymbol type) =>
            type.AllInterfaces.Any(i => i.ToDisplayString() == "System.Collections.ICollection" || i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.ICollection<T>");

        /// <summary>Every field or property whose collection some code changes: a changing method called on it, or one of its items assigned.</summary>
        private static HashSet<ISymbol> Changed(Compilation compilation)
        {
            var changed = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            foreach (var tree in compilation.SyntaxTrees)
            {
                var model = compilation.GetSemanticModel(tree);
                foreach (var node in tree.GetRoot().DescendantNodes())
                {
                    ExpressionSyntax receiver = null;
                    if (node is InvocationExpressionSyntax call && call.Expression is MemberAccessExpressionSyntax access && Changing.Contains(access.Name.Identifier.Text))
                        receiver = access.Expression;
                    else if (node is AssignmentExpressionSyntax assignment && assignment.Left is ElementAccessExpressionSyntax element)
                        receiver = element.Expression;

                    var symbol = receiver == null ? null : model.GetSymbolInfo(receiver).Symbol;
                    if (symbol != null) changed.Add(symbol.OriginalDefinition);
                }
            }
            return changed;
        }

        /// <summary>Marks what a method assigns or empties, and walks on into the methods of the same class it calls.</summary>
        private static void Walk(Compilation compilation, IMethodSymbol method, INamedTypeSymbol type, HashSet<IMethodSymbol> seen, HashSet<ISymbol> forgotten)
        {
            if (!seen.Add(method)) return;

            foreach (var reference in method.DeclaringSyntaxReferences)
            {
                var node = reference.GetSyntax();
                var model = compilation.GetSemanticModel(node.SyntaxTree);
                foreach (var assignment in node.DescendantNodes().OfType<AssignmentExpressionSyntax>())
                {
                    var target = model.GetSymbolInfo(assignment.Left).Symbol;
                    if (target != null) forgotten.Add(target.OriginalDefinition);
                }
                foreach (var call in node.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (call.Expression is MemberAccessExpressionSyntax access && Emptying.Contains(access.Name.Identifier.Text))
                    {
                        var receiver = model.GetSymbolInfo(access.Expression).Symbol;
                        if (receiver != null) forgotten.Add(receiver.OriginalDefinition);
                    }
                    if (model.GetSymbolInfo(call).Symbol is IMethodSymbol called && SymbolEqualityComparer.Default.Equals(called.ContainingType, type))
                        Walk(compilation, called.OriginalDefinition, type, seen, forgotten);
                }
            }
        }

        private static IEnumerable<INamedTypeSymbol> Types(INamespaceSymbol space)
        {
            foreach (var type in space.GetTypeMembers().Where(t => t.Locations.Any(l => l.IsInSource))) yield return type;
            foreach (var inner in space.GetNamespaceMembers())
            {
                foreach (var type in Types(inner)) yield return type;
            }
        }

        private static string Where(IMethodSymbol method)
        {
            var node = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
            return node == null ? "?" : ScrollToArmsSource.Where(node);
        }
    }
}
