using System;
using System.Threading.Tasks;

namespace Challenges
{
    internal class SharedEndXxxRefactoring
    {
    }

    internal class OriginalProgram_SharedEndXxx
    {
        public void Action1()
        {
            var request = new Request();
            request.BeginOperation(Callback1, request);
        }

        private void Callback1(IAsyncResult result)
        {
            var response = SharedCode(result);
        }

        public void Action2()
        {
            var request = new Request();
            request.BeginOperation(Callback2, request);
        }

        private void Callback2(IAsyncResult result)
        {
            var response = SharedCode(result);
        }

        private static Response SharedCode(IAsyncResult result)
        {
            var request = (Request)result.AsyncState;
            return request.EndOperation(result);
        }
    }

    internal class RefactoredProgram_SharedEndXxx
    {
        public void Action1()
        {
            var request = new Request();
            var task = request.OperationAsync();
            Callback1(task, request);
        }

        private async void Callback1(Task<Response> task, Request request)
        {
            var response = await SharedCode(task).ConfigureAwait(false);
        }

        public void Action2()
        {
            var request = new Request();
            var task = request.OperationAsync();
            Callback2(task, request);
        }

        private async void Callback2(Task<Response> task, Request request)
        {
            var response = await SharedCode(task).ConfigureAwait(false);
        }

        private async static Task<Response> SharedCode(Task<Response> task)
        {
            return await task.ConfigureAwait(false);
        }
    }
}