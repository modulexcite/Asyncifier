using NUnit.Framework;

namespace Refactoring_Tests
{
    [TestFixture]
    class InitiatingMethodReturningValueTest : APMToAsyncAwaitRefactoringTestBase
    {
        // TODO: Right now, a PreconditionException is thrown. But it can be handled (pre-refactoring or support?)
        [Test]
        public void TestThatInitiatingMethodReturningValueIsRefactoredCorrectly()
        {
            AssertThatOriginalCodeIsRefactoredCorrectly(
                OriginalCode,
                RefactoredCode,
                FirstBeginInvocationFinder("request.BeginGetResponse")
            );
        }

        private const string OriginalCode = @"using System.Net;

namespace TextInput
{
    class InitiatingMethodReturnType
    {
        public int Action(WebRequest request)
        {
            request.BeginGetResponse(result =>
            {
                request.EndGetResponse(result);
            }, null);

            return 110;
        }
    }
}";

        private const string RefactoredCode = @"using System.Net;
using System.Threading.Tasks;

namespace TextInput
{
    class InitiatingMethodReturnType
    {
        public async Task<int> Action(WebRequest request)
        {
            var task = request.GetResponseAsync();
            await task.ConfigureAwait(false);

            return 110;
        }
    }
}";
    }
}
