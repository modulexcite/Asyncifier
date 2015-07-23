using NUnit.Framework;

namespace Refactoring_Tests
{
    [TestFixture]
    public class AsyncStateUnaliasingTest : APMToAsyncAwaitRefactoringTestBase
    {
        [Test]
        public void TestThatAsyncStateIsProperlyUnaliased()
        {
            AssertThatOriginalCodeIsRefactoredCorrectly(
                OriginalCode,
                RefactoredCode,
                FirstBeginInvocationFinder("request.BeginGetRequestStream")
            );
        }

        private const string OriginalCode = @"using System.Net;

namespace TextInput
{
    public class AliasedAsyncState
    {
        private readonly byte[] _buffer = new byte[110];

        public void Action()
        {
            var request = WebRequest.Create(""http://www.microsoft.com/"");

            request.BeginGetRequestStream(result =>
            {
                try
                {
                    var myRequest = (WebRequest)result.AsyncState;
                    var stream = myRequest.EndGetRequestStream(result);

                    stream.Write(_buffer, 0, _buffer.Length);

                    myRequest.BeginGetResponse(inner =>
                    {
                        var response = myRequest.EndGetResponse(inner);
                    }, null);
                }
                catch (WebException e)
                {
                    // Dummy
                }
            }, request);
        }
    }
}";

        private const string RefactoredCode = @"using System.Net;
using System.Threading.Tasks;

namespace TextInput
{
    public class AliasedAsyncState
    {
        private readonly byte[] _buffer = new byte[110];

        public async void Action()
        {
            var request = WebRequest.Create(""http://www.microsoft.com/"");
            var task = request.GetRequestStreamAsync();
            try
            {
                var stream = await task.ConfigureAwait(false);

                stream.Write(_buffer, 0, _buffer.Length);
                request.BeginGetResponse(inner =>
                {
                    var response = request.EndGetResponse(inner);
                }, null);
            }
            catch (WebException e)
            {
                // Dummy
            }
        }
    }
}";
    }
}
