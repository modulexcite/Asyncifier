using System;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;


namespace Utilities
{
    /// <summary>
    /// Pair of old and new SyntaxNodes for ReplaceAll.
    /// </summary>
    public sealed class SyntaxReplacementPair
    {
        /// <summary>The node that must be replaced.</summary>
        public readonly SyntaxNode OldNode;

        /// <summary>The node that will replace the old node.</summary>
        public readonly SyntaxNode NewNode;

        public SyntaxReplacementPair(SyntaxNode oldNode, SyntaxNode newNode)
        {
            if (oldNode == null) throw new ArgumentNullException("oldNode");
            if (newNode == null) throw new ArgumentNullException("newNode");

            OldNode = oldNode;
            NewNode = newNode;
        }
    }
}
