using NUnit.Framework;

namespace Refactoring_Tests
{
    [TestFixture]
    public class SharedEndXxxTest : APMToAsyncAwaitRefactoringTestBase
    {
        [Test]
        public void TestThatTwoInstancesSharingEndXxxThrowsPreconditionException()
        {
            AssertThatRefactoringOriginalCodeThrowsPreconditionException(
                OriginalCode,
                FirstBeginInvocationFinder("request.BeginGetResponse")
            );
        }

        private const string OriginalCode = @"using System;
using System.Net;

namespace TextInput
{
    class ReusedEndXxx
    {
        public void Action1(WebRequest request)
        {
            request.BeginGetResponse(Callback, request);
        }

        public void Action2(WebRequest request)
        {
            request.BeginGetResponse(Callback, request);
        }

        private void Callback(IAsyncResult result)
        {
            var request = (WebRequest)result.AsyncState;
            var response = request.EndGetResponse(result);

            DoSomethingWithResponse(response);
        }

        private void DoSomethingWithResponse(WebResponse response) { }
    }
}";
    }
}
