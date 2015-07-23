using NUnit.Framework;

namespace Refactoring_Tests
{
    [TestFixture]
    class BeginInTryCatchTest : APMToAsyncAwaitRefactoringTestBase
    {
        [Test]
        public void TestThatBeginInTryCatchIsRefactoredCorrectly()
        {
            AssertThatOriginalCodeIsRefactoredCorrectly(
                OriginalCode,
                RefactoredCode,
                FirstBeginInvocationFinder("request.BeginGetRequestStream")
            );
        }

        private const string OriginalCode = @"using System;
using System.Net;

namespace TextInput
{
    class BeginInTryCatch
    {
        public void Action(WebRequest request)
        {
            try
            {
                request.BeginGetRequestStream(Callback, request);
            }
            catch (Exception e)
            {
            }
        }

        private void Callback(IAsyncResult result)
        {
            var request = (WebRequest)result.AsyncState;
            var stream = request.EndGetRequestStream(result);
        }
    }
}";

        private const string RefactoredCode = @"using System;
using System.Net;
using System.Threading.Tasks;

namespace TextInput
{
    class BeginInTryCatch
    {
        public async void Action(WebRequest request)
        {
            try
            {
                var task = request.GetRequestStreamAsync();
                await Callback(task, request).ConfigureAwait(false);
            }
            catch (Exception e)
            {
            }
        }

        private async Task Callback(Task<Stream> task, WebRequest request)
        {
            var stream = await task.ConfigureAwait(false);
        }
    }
}";
    }
}
