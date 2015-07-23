using NUnit.Framework;

namespace Refactoring_Tests
{
    [TestFixture]
    public class NestedAPMInstances : APMToAsyncAwaitRefactoringTestBase
    {
        [Test]
        public void TestThatNestedAPMInstancesInSingleSourceFileAreRefactoredCorrectly()
        {
            AssertThatOriginalCodeIsRefactoredCorrectly(
                OriginalCode,
                RefactoredCode,
                FirstBeginInvocationFinder("request.BeginGetRequestStream"),
                FirstBeginInvocationFinder("stream.BeginWrite"),
                FirstBeginInvocationFinder("request.BeginGetResponse")
            );
        }

        private const string OriginalCode = @"using System.IO;
using System.Net;

namespace Scratchpad
{
    class SimpleAPMCase
    {
        private readonly byte[] _buffer = new byte[1024];

        public void FireAndForget()
        {
            var request = WebRequest.Create(""http://www.microsoft.com/"");
            request.BeginGetRequestStream(outer =>
            {
                var stream = request.EndGetRequestStream(outer);

                stream.BeginWrite(_buffer, 0, _buffer.Length, inner =>
                {
                    stream.EndWrite(inner);

                    request.BeginGetResponse(result =>
                    {
                        var response = request.EndGetResponse(result);

                        DoSomethingWithResponse(response);
                    }, null);
                }, null);
            }, null);

            DoSomethingWhileGetResponseIsRunning();
        }

        private static void DoSomethingWhileGetResponseIsRunning() { }
        private static void DoSomethingWithResponse(WebResponse response) { }
    }
}";

        private const string RefactoredCode = @"using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Scratchpad
{
    class SimpleAPMCase
    {
        private readonly byte[] _buffer = new byte[1024];

        public async void FireAndForget()
        {
            var request = WebRequest.Create(""http://www.microsoft.com/"");
            var task = request.GetRequestStreamAsync();
            DoSomethingWhileGetResponseIsRunning();
            var stream = await task.ConfigureAwait(false);
            var task2 = stream.WriteAsync(_buffer, 0, _buffer.Length);
            await task2.ConfigureAwait(false);
            var task3 = request.GetResponseAsync();
            var response = await task3.ConfigureAwait(false);

            DoSomethingWithResponse(response);
        }

        private static void DoSomethingWhileGetResponseIsRunning() { }
        private static void DoSomethingWithResponse(WebResponse response) { }
    }
}";
    }
}
