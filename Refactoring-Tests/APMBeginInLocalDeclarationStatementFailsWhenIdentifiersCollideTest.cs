using NUnit.Framework;

namespace Refactoring_Tests
{
    [TestFixture]
    public class APMBeginInLocalDeclarationStatementTest : APMToAsyncAwaitRefactoringTestBase
    {
        [Test]
        public void TestThatAPMBeginRewritingUsesNonCollidingIdentifierForLambdaParameter()
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
            var result = request.BeginGetResponse(Callback, request);
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
    }
}
