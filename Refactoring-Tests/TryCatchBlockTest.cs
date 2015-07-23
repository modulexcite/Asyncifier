using NUnit.Framework;

namespace Refactoring_Tests
{
    [TestFixture]
    public class TryCatchBlockTest : APMToAsyncAwaitRefactoringTestBase
    {
        [Test]
        public void TestThatEndXxxInTryBlockIsRefactoredCorrectly()
        {
            AssertThatOriginalCodeIsRefactoredCorrectly(
                OriginalCode,
                RefactoredCode,
                FirstBeginInvocationFinder("request.BeginGetResponse")
            );
        }

        private const string OriginalCode = @"using System;
using System.Net;

namespace TextInput
{
    class SimpleAPMCase
    {
        public void FireAndForget()
        {
            var request = WebRequest.Create(""http://www.microsoft.com/"");
            request.BeginGetResponse(Callback, request);

            DoSomethingWhileGetResponseIsRunning();
        }

        private void Callback(IAsyncResult result)
        {
            var request = (WebRequest)result.AsyncState;

            try
            {
                var response = request.EndGetResponse(result);

                DoSomethingWithResponse(response);
            }
            catch (WebException e)
            {
                HandleException(e);
            }
        }

        private static void DoSomethingWhileGetResponseIsRunning() { }
        private static void DoSomethingWithResponse(WebResponse response) { }
        private static void HandleException(WebException e) { }
    }
}";

        private const string RefactoredCode = @"using System;
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
            DoSomethingWhileGetResponseIsRunning();
            await Callback(task, request).ConfigureAwait(false);
        }

        private async Task Callback(Task<WebResponse> task, WebRequest request)
        {

            try
            {
                var response = await task.ConfigureAwait(false);

                DoSomethingWithResponse(response);
            }
            catch (WebException e)
            {
                HandleException(e);
            }
        }

        private static void DoSomethingWhileGetResponseIsRunning() { }
        private static void DoSomethingWithResponse(WebResponse response) { }
        private static void HandleException(WebException e) { }
    }
}";
    }
}
