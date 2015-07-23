using NUnit.Framework;

namespace Refactoring_Tests
{
    [TestFixture]
    class NullCallbackTest : APMToAsyncAwaitRefactoringTestBase
    {
        [Test]
        public void TestThatNullCallbackArgumentThrowsPreconditionException()
        {
            AssertThatRefactoringOriginalCodeThrowsPreconditionException(
                Code,
                FirstBeginInvocationFinder("request.BeginGetResponse")
            );
        }

        private const string Code = @"using System.Net;

namespace TextInput
{
    class NullCallbackArgument
    {
        void Action(WebRequest request)
        {
            request.BeginGetResponse(null, null);
        }
    }
}";
    }
}
