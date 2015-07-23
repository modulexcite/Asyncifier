using NUnit.Framework;

namespace Refactoring_Tests
{
    [TestFixture]
    public class AsyncStatePassingTest : APMToAsyncAwaitRefactoringTestBase
    {
        [Test]
        public void TestThatPassedAsyncStateIsIntroducedAsParameterForCallbacks()
        {
            AssertThatOriginalCodeIsRefactoredCorrectly(
                OriginalCodeWithUsedAsyncState,
                RefactoredCodeWithUsedAsyncState,
                FirstBeginInvocationFinder("request.BeginGetResponse")
            );
        }

        // [Test] TODO: Enable this test if AsyncState is removed when it is unused.
        public void TestThatPassedAsyncStateIsRemovedWhenUnused()
        {
            AssertThatOriginalCodeIsRefactoredCorrectly(
                OriginalCodeWithUnusedAsyncState,
                RefactoredCodeWithIgnoredAsyncState,
                FirstBeginInvocationFinder("request.BeginGetResponse")
            );
        }

        private const string OriginalCodeWithUsedAsyncState = @"using System;
using System.Net;

namespace TextInput
{
    class SimpleAPMCase
    {
        public void FireAndForget()
        {
            var request = WebRequest.Create(""http://www.microsoft.com/"");
            request.BeginGetResponse(Callback, request);
        }

        private void Callback(IAsyncResult result)
        {
            var request = (WebRequest)result.AsyncState;
            var response = request.EndGetResponse(result);

            DoSomethingWithRequestAndResponse(request, response);
        }

        private static void DoSomethingWithRequestAndResponse(WebRequest request, WebResponse response) { }
    }
}";

        private const string RefactoredCodeWithUsedAsyncState = @"using System;
using System.Net;
using System.Threading.Tasks;

namespace TextInput
{
    class SimpleAPMCase
    {
        public async void FireAndForget()
        {
            var request = WebRequest.Create(""http://www.microsoft.com/"");
            var task = request.GetResponseAsync();
            await Callback(task, request).ConfigureAwait(false);
        }

        private async Task Callback(Task<WebResponse> task, WebRequest request)
        {
            var response = await task.ConfigureAwait(false);

            DoSomethingWithRequestAndResponse(request, response);
        }

        private static void DoSomethingWithRequestAndResponse(WebRequest request, WebResponse response) { }
    }
}";

        private const string OriginalCodeWithUnusedAsyncState = @"using System;
using System.Net;

namespace TextInput
{
    class SimpleAPMCase
    {
        public void FireAndForget()
        {
            var request = WebRequest.Create(""http://www.microsoft.com/"");
            request.BeginGetResponse(Callback, request);
        }

        private void Callback(IAsyncResult result)
        {
            var request = (WebRequest)result.AsyncState;
            var response = request.EndGetResponse(result);
        }
    }
}";

        private const string RefactoredCodeWithIgnoredAsyncState = @"using System;
using System.Net;
using System.Threading.Tasks;

namespace TextInput
{
    class SimpleAPMCase
    {
        public async void FireAndForget()
        {
            var request = WebRequest.Create(""http://www.microsoft.com/"");
            var task = request.GetResponseAsync();
            await Callback(task).ConfigureAwait(false);
        }

        private async Task Callback(Task<WebResponse> task)
        {
            var response = await task.ConfigureAwait(false);
        }
    }
}";
    }
}
