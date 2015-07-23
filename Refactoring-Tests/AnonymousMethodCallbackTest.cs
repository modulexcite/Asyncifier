using NUnit.Framework;

namespace Refactoring_Tests
{
    [TestFixture]
    class AnonymousMethodCallbackTest : APMToAsyncAwaitRefactoringTestBase
    {
        [Test]
        public void TestThatAnonymousMethodCallbackIsRefactoredCorrectly()
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
    class AnonymousMethodCallback
    {
        public void Action(WebRequest request)
        {
            request.BeginGetResponse(new AsyncCallback(delegate(IAsyncResult result)
            {
                var inner = (WebRequest)result.AsyncState;
                var response = inner.EndGetResponse(result);
            }), request);
        }
    }
}";

        private const string RefactoredCode = @"using System;
using System.Net;
using System.Threading.Tasks;

namespace TextInput
{
    class AnonymousMethodCallback
    {
        public async void Action(WebRequest request)
        {
            var task = request.GetResponseAsync();
            var response = await task.ConfigureAwait(false);
        }
    }
}";
    }
}
