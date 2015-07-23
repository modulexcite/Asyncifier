using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using NLog;
using System;

namespace Utilities
{
    public static class SemanticModelExtensions
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static int NumMethodSymbolLookups { get; private set; }

        public static void ResetSymbolLookupCounter()
        {
            NumMethodSymbolLookups = 0;
        }

        public static IMethodSymbol LookupMethodSymbol(this SemanticModel model, InvocationExpressionSyntax invocation)
        {
            if (model == null) throw new ArgumentNullException("model");
            if (invocation == null) throw new ArgumentNullException("invocation");

            var expression = invocation.Expression;

            Logger.Trace("Looking up symbol for: {0}", expression);

            var symbol = model.GetSymbolInfo(expression).Symbol;

            var methodSymbol = symbol as IMethodSymbol;
            if (methodSymbol != null)
            {
                NumMethodSymbolLookups++;
                return methodSymbol;
            }

            throw new MethodSymbolMissingException(invocation);
        }

        public static IMethodSymbol LookupMethodSymbol(this SemanticModel model, IdentifierNameSyntax identifier)
        {
            if (model == null) throw new ArgumentNullException("model");
            if (identifier == null) throw new ArgumentNullException("identifier");

            var expression = identifier;

            Logger.Trace("Looking up symbol for: {0}", expression);

            var symbol = model.GetSymbolInfo(expression).Symbol;

            var methodSymbol = symbol as IMethodSymbol;
            if (methodSymbol != null)
            {
                NumMethodSymbolLookups++;
                return methodSymbol;
            }

            throw new MethodSymbolMissingException(expression);
        }

        public static IMethodSymbol LookupMethodSymbol(this SemanticModel model, MemberAccessExpressionSyntax memberAccess)
        {
            if (model == null) throw new ArgumentNullException("model");
            if (memberAccess == null) throw new ArgumentNullException("memberAccess");

            var expression = memberAccess;

            Logger.Trace("Looking up symbol for: {0}", expression);

            var symbol = model.GetSymbolInfo(expression).Symbol;

            var methodSymbol = symbol as IMethodSymbol;
            if (methodSymbol != null)
            {
                NumMethodSymbolLookups++;
                return methodSymbol;
            }

            throw new MethodSymbolMissingException(memberAccess);
        }
    }

    public class SymbolMissingException : Exception
    {
        public SymbolMissingException(String message)
            : base(message)
        {
        }
    }

    public class MethodSymbolMissingException : SymbolMissingException
    {
        public MethodSymbolMissingException(InvocationExpressionSyntax invocation)
            : base("No method symbol found for invocation:\n" + invocation)
        {
        }

        public MethodSymbolMissingException(IdentifierNameSyntax identifier)
            : base("No method symbol found for identifier: " + identifier)
        {
        }

        public MethodSymbolMissingException(MemberAccessExpressionSyntax memberAccess)
            : base("No method symbol found for member access: " + memberAccess)
        {
        }
    }
}
