using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Utilities
{
    public static class SyntaxNodeExtensions
    {
        public static string ToLog(this SyntaxNode node)
        {
            return System.Environment.NewLine + System.Environment.NewLine + node.ToString() + System.Environment.NewLine + System.Environment.NewLine;
        }

        /// <summary>
        /// Replace all old nodes in the given pairs with their corresponding new nodes.
        /// </summary>
        /// <typeparam name="T">Subtype of SyntaxNode that supports the
        /// replacement of descendent nodes.</typeparam>
        /// <param name="node">The SyntaxNode or subtype to operate on.</param>
        /// <param name="syntaxReplacementPairs">The SyntaxNodeReplacementPair
        /// instances that each contain both the old node that is to be
        /// replaced, and the new node that will replace the old node.</param>
        /// <returns>The SyntaxNode that contains all the replacmeents.</returns>
        public static T ReplaceAll<T>(this T node, params SyntaxReplacementPair[] syntaxReplacementPairs) where T : SyntaxNode
        {
            return node.ReplaceNodes(
                syntaxReplacementPairs.Select(pair => pair.OldNode),
                (oldNode, newNode) => syntaxReplacementPairs.First(pair => pair.OldNode == oldNode).NewNode
            );
        }

        /// <summary>
        /// Replace all old nodes in the given pairs with their corresponding new nodes.
        /// </summary>
        /// <typeparam name="T">Subtype of SyntaxNode that supports the
        /// replacement of descendent nodes.</typeparam>
        /// <param name="node">The SyntaxNode or subtype to operate on.</param>
        /// <param name="replacementPairs">The SyntaxNodeReplacementPair
        /// instances that each contain both the old node that is to be
        /// replaced, and the new node that will replace the old node.</param>
        /// <returns>The SyntaxNode that contains all the replacmeents.</returns>
        public static T ReplaceAll<T>(this T node, IEnumerable<SyntaxReplacementPair> replacementPairs) where T : SyntaxNode
        {
            return node.ReplaceNodes(
                replacementPairs.Select(pair => pair.OldNode),
                (oldNode, newNode) => replacementPairs.First(pair => pair.OldNode == oldNode).NewNode
            );
        }
    }
}
