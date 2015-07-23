using NUnit.Framework;

namespace Refactoring_Tests
{
    [TestFixture]
    class CallGraphPathTest : APMToAsyncAwaitRefactoringTestBase
    {
        [Test]
        public void TestThatCallGraphPathComponentContainingDelegateParameterRefIsIgnored()
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
    class CallGraphRefsDelegateParam
    {
        public void Action(HttpWebRequest request, Action<HttpWebRequest, Func<HttpWebResponse>> report)
        {
            request.BeginGetResponse(
                ar => report(
                    request,
                    () => (HttpWebResponse)request.EndGetResponse(ar)
                ),
                null
            );
        }
    }
}";
    }
}
