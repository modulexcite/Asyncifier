using NUnit.Framework;

namespace Refactoring_Tests
{
    [TestFixture]
    public class UsingsTest : APMToAsyncAwaitRefactoringTestBase
    {
        [Test]
        public void TestThatMissingSystemThreadingTaskUsingIsAdded()
        {
            AssertThatOriginalCodeIsRefactoredCorrectly(
                OriginalCodeWithoutUsingSystemThreadingTasks,
                RefactoredCode,
                FirstBeginInvocationFinder("request.BeginGetResponse")
            );
        }

        [Test]
        public void TestThatSystemThreadTaskIsNotAddedWhenItsAlreadyThere()
        {
            AssertThatOriginalCodeIsRefactoredCorrectly(
                OriginalCodeWithUsingSystemThreadingTasks,
                RefactoredCode,
                FirstBeginInvocationFinder("request.BeginGetResponse")
            );
        }

        private const string OriginalCodeWithoutUsingSystemThreadingTasks = @"using System;
using System.Net;

namespace TextInput
{
    class Usings
    {
        void FireAndForget()
        {
            var request = WebRequest.Create(""http://www.microsoft.com/"");
            request.BeginGetResponse(result => {
                var response = request.EndGetResponse(result);
            }, null);
        }
    }
}";

        private const string OriginalCodeWithUsingSystemThreadingTasks = @"using System;
using System.Net;
using System.Threading.Tasks;

namespace TextInput
{
    class Usings
    {
        void FireAndForget()
        {
            var request = WebRequest.Create(""http://www.microsoft.com/"");
            request.BeginGetResponse(result => {
                var response = request.EndGetResponse(result);
            }, null);
        }
    }
}";

        private const string RefactoredCode = @"using System;
using System.Net;
using System.Threading.Tasks;

namespace TextInput
{
    class Usings
    {
        async void FireAndForget()
        {
            var request = WebRequest.Create(""http://www.microsoft.com/"");
            var task = request.GetResponseAsync();
            var response = await task.ConfigureAwait(false);
        }
    }
}";
    }
}
