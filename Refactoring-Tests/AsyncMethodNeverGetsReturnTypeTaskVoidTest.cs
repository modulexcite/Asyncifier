using NUnit.Framework;

namespace Refactoring_Tests
{
    [TestFixture]
    public class AsyncMethodNeverGetsReturnTypeTaskVoidTest : APMToAsyncAwaitRefactoringTestBase
    {
        [Test]
        public void TestThatVoidReturningMethodsBecomeTaskReturningMethods()
        {
            AssertThatOriginalCodeIsRefactoredCorrectly(
                OriginalCode,
                RefactoredCode,
                FirstBeginInvocationFinder("request.BeginGetResponse")
            );
        }

        private const string OriginalCode = @"using System;
using System.Net;
using System.Threading.Tasks;

namespace TestApp
{
    class AsyncVoid
    {
        public void FireAndForget()
        {
            var request = WebRequest.Create(""http://www.microsoft.com/"");
            request.BeginGetResponse(Inner, request);
        }

        private void Inner(IAsyncResult result)
        {
            var request = (WebRequest)result.AsyncState;
            var response = Final(result, request);
            DoSomethingWithResponse(response);
        }

        private WebResponse Final(IAsyncResult result, WebRequest request)
        {
            var response = request.EndGetResponse(result);

            return response;
        }

        private static void DoSomethingWithRequest(WebRequest request) { }
        private static void DoSomethingWithResponse(WebResponse response) { }
    }
}";

        private const string RefactoredCode = @"using System;
using System.Net;
using System.Threading.Tasks;

namespace TestApp
{
    class AsyncVoid
    {
        public async void FireAndForget()
        {
            var request = WebRequest.Create(""http://www.microsoft.com/"");
            var task = request.GetResponseAsync();
            await Inner(task, request).ConfigureAwait(false);
        }

        private async Task Inner(Task<WebResponse> task, WebRequest request)
        {
            var response = await Final(task, request).ConfigureAwait(false);
            DoSomethingWithResponse(response);
        }

        private WebResponse Final(Task<WebRequest> task, WebRequest request)
        {
            var response = await task.ConfigureAwait(false);

            return response;
        }

        private static void DoSomethingWithRequest(WebRequest request) { }
        private static void DoSomethingWithResponse(WebResponse response) { }
    }
}";
    }
}
