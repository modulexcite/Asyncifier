using NUnit.Framework;

namespace Refactoring_Tests
{
    [TestFixture]
    public class TwoAPMInstancesTest : APMToAsyncAwaitRefactoringTestBase
    {
        [Test]
        public void TestThatTwoAPMInstancesInSingleSourceFileAreRefactoredCorrectly()
        {
            AssertThatOriginalCodeIsRefactoredCorrectly(
                OriginalCode,
                RefactoredCode,
                FirstBeginInvocationFinder("request.BeginGetResponse"),
                FirstBeginInvocationFinder("stream.BeginWrite")
            );
        }

        private const string OriginalCode = @"using System;
using System.IO;
using System.Net;

namespace TextInput
{
    class TwoSeparateAPMInstances
    {
        public void FireAndForget()
        {
            var request = WebRequest.CreateHttp(""http://www.microsoft.com/"");
            request.BeginGetResponse(result =>
            {
                var response = request.EndGetResponse(result);
                DoSomethingWithResponse(response);
            }, null);

            DoSomethingWhileOperationIsRunning();
        }

        public void Operation(Stream stream, byte[] data)
        {
            stream.BeginWrite(data, 0, data.Length, result =>
            {
                stream.EndWrite(result);
                DoSomethingAfterWrite();
            }, null);

            DoSomethingWhileOperationIsRunning();
        }

        private static void DoSomethingWhileOperationIsRunning() { }
        private static void DoSomethingWithResponse(WebResponse response) { }
        private static void DoSomethingAfterWrite() { }
    }
}";

        private const string RefactoredCode = @"using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace TextInput
{
    class TwoSeparateAPMInstances
    {
        public async void FireAndForget()
        {
            var request = WebRequest.CreateHttp(""http://www.microsoft.com/"");
            var task = request.GetResponseAsync();
            DoSomethingWhileOperationIsRunning();
            var response = await task.ConfigureAwait(false);
            DoSomethingWithResponse(response);
        }

        public async void Operation(Stream stream, byte[] data)
        {
            var task = stream.WriteAsync(data, 0, data.Length);
            DoSomethingWhileOperationIsRunning();
            await task.ConfigureAwait(false);
            DoSomethingAfterWrite();
        }

        private static void DoSomethingWhileOperationIsRunning() { }
        private static void DoSomethingWithResponse(WebResponse response) { }
        private static void DoSomethingAfterWrite() { }
    }
}";
    }
}
