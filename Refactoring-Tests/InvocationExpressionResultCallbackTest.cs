using NUnit.Framework;

namespace Refactoring_Tests
{
    [TestFixture]
    class InvocationExpressionResultCallbackTest : APMToAsyncAwaitRefactoringTestBase
    {
        [Test]
        public void TestThatRefactoringInvocationExpressionCallbackThrowsPreconditionException()
        {
            AssertThatRefactoringOriginalCodeThrowsPreconditionException(
                Code,
                FirstBeginInvocationFinder("request.BeginGetResponse")
            );
        }

        private const string Code = @"using System;
using System.Net;

namespace TestApp
{
    class InvocationExpressionResultCallback
    {
        public void Action(WebRequest request)
        {
            request.BeginGetResponse(GetCallback(), request);
        }

        private AsyncCallback GetCallback() { return null; }
    }
}";
    }
}
