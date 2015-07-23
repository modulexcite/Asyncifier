using NUnit.Framework;

namespace Refactoring_Tests
{
    [TestFixture]
    public class LambdaCallbackTest : APMToAsyncAwaitRefactoringTestBase
    {
        [Test]
        public void TestThatTheSimpleCaseWithParenthesizedLambdaCallbackIsRefactoredCorrectly()
        {
            AssertThatOriginalCodeIsRefactoredCorrectly(
                OriginalCodeWithParenthesizedLambda,
                RefactoredCode,
                FirstBeginInvocationFinder("request.BeginGetResponse")
            );
        }

        [Test]
        public void TestThatTheSimpleCaseWithSimpleLambdaCallbackIsRefactoredCorrectly()
        {
            AssertThatOriginalCodeIsRefactoredCorrectly(
                OriginalCodeWithSimpleLambda,
                RefactoredCode,
                FirstBeginInvocationFinder("request.BeginGetResponse")
            );
        }

        [Test]
        public void TestThatLambdaCallbackWithSetButUnreferencedAsyncStateWhichIsAlsoCapturedDirectlyIsRefactoredCorrectly()
        {
            AssertThatOriginalCodeIsRefactoredCorrectly(
                OriginalCodeWithSimpleLambdaWithCapturedAsyncState,
                RefactoredCode,
                FirstBeginInvocationFinder("request.BeginGetResponse")
            );
        }

        private const string OriginalCodeWithParenthesizedLambda = @"using System;
using System.Net;

namespace TextInput
{
    class SimpleAPMCase
    {
        public void FireAndForget()
        {
            var request = WebRequest.Create(""http://www.microsoft.com/"");
            request.BeginGetResponse((result) => {
                var response = request.EndGetResponse(result);

                DoSomethingWithResponse(response);
            }, null);

            DoSomethingWhileGetResponseIsRunning();
        }

        private static void DoSomethingWhileGetResponseIsRunning() { }
        private static void DoSomethingWithResponse(WebResponse response) { }
    }
}";

        private const string OriginalCodeWithSimpleLambda = @"using System;
using System.Net;

namespace TextInput
{
    class SimpleAPMCase
    {
        public void FireAndForget()
        {
            var request = WebRequest.Create(""http://www.microsoft.com/"");
            request.BeginGetResponse(result => {
                var response = request.EndGetResponse(result);

                DoSomethingWithResponse(response);
            }, null);

            DoSomethingWhileGetResponseIsRunning();
        }

        private static void DoSomethingWhileGetResponseIsRunning() { }
        private static void DoSomethingWithResponse(WebResponse response) { }
    }
}";

        private const string OriginalCodeWithSimpleLambdaWithCapturedAsyncState = @"using System;
using System.Net;

namespace TextInput
{
    class SimpleAPMCase
    {
        public void FireAndForget()
        {
            var request = WebRequest.Create(""http://www.microsoft.com/"");
            request.BeginGetResponse(result => {
                var response = request.EndGetResponse(result);

                DoSomethingWithResponse(response);
            }, request);

            DoSomethingWhileGetResponseIsRunning();
        }

        private static void DoSomethingWhileGetResponseIsRunning() { }
        private static void DoSomethingWithResponse(WebResponse response) { }
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
            var response = await task.ConfigureAwait(false);

            DoSomethingWithResponse(response);
        }

        private static void DoSomethingWhileGetResponseIsRunning() { }
        private static void DoSomethingWithResponse(WebResponse response) { }
    }
}";
    }
}
