using NUnit.Framework;

namespace Refactoring_Tests
{
    [TestFixture]
    class ReturnStatementPreconditionTest : APMToAsyncAwaitRefactoringTestBase
    {
        [Test]
        public void TestThatRefactoringBeginXxxInReturnStatementThrowsPreconditionException()
        {
            // Fails. Currently implemented in batch tool.
            AssertThatRefactoringOriginalCodeThrowsPreconditionException(
                Code,
                FirstBeginInvocationFinder("request.BeginGetResponse")
            );
        }

        private const string Code = @"using System;
using System.Net;

namespace TestApp
{
    class ReturnIAsyncResult
    {
        public IAsyncResult Action(WebRequest request)
        {
            return request.BeginGetResponse(result =>
            {
                request.EndGetResponse(result);
            }, null);
        }
    }
}
";
    }
}
