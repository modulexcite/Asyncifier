using NUnit.Framework;

namespace Refactoring_Tests
{
    [TestFixture]
    class NullableAPMEndReturnTypeTest : APMToAsyncAwaitRefactoringTestBase
    {
        [Test]
        public void TestThatNullableOperationReturnTypesAreHandledCorrectly()
        {
            AssertThatOriginalCodeIsRefactoredCorrectly(
                OriginalCode,
                RefactoredCode,
                FirstBeginInvocationFinder("request.BeginOp")
            );
        }

        private const string OriginalCode = @"using System;
using System.Threading.Tasks;

namespace TextInput
{
    class NullableAPMEndReturnType
    {
        public void Action(MyRequest request)
        {
            request.BeginOp(Callback, null);
        }

        private void Callback(IAsyncResult result)
        {
            var request = (MyRequest)result.AsyncState;
            var i = request.EndOp(result);
        }
    }

    class MyRequest
    {
        public IAsyncResult BeginOp(AsyncCallback callback, object state) { return null; }
        public int? EndOp(IAsyncResult result) { return null; }

        public Task<int?> OpAsync() { return null; }
    }
}";

        private const string RefactoredCode = @"using System;
using System.Threading.Tasks;

namespace TextInput
{
    class NullableAPMEndReturnType
    {
        public async void Action(MyRequest request)
        {
            var task = request.OpAsync();
            await Callback(task).ConfigureAwait(false);
        }

        private async Task Callback(Task<Nullable<int>> task, MyRequest request)
        {
            var i = await task.ConfigureAwait(false);
        }
    }

    class MyRequest
    {
        public IAsyncResult BeginOp(AsyncCallback callback, object state) { return null; }
        public int? EndOp(IAsyncResult result) { return null; }

        public Task<int?> OpAsync() { return null; }
    }
}";
    }
}
