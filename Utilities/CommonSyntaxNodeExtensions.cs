using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;

namespace Utilities
{
    public static class CommonSyntaxNodeExtensions
    {
        public static T Format<T>(this T node, Workspace workspace) where T : CSharpSyntaxNode
        {
            return (T) Formatter.Format(node, workspace);
        }
    }
}