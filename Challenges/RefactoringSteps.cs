using System;
using System.Threading.Tasks;

namespace Challenges
{
    internal class RefactoringSteps
    {
    }

    internal class Request
    {
        public IAsyncResult BeginOperation(AsyncCallback callback, Request request)
        {
            throw new NotImplementedException();
        }

        public Response EndOperation(IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        public Task<Response> OperationAsync()
        {
            throw new NotImplementedException();
        }
    }

    internal class Response
    {
    }

    internal class OriginalProgram
    {
        public void Action()
        {
            var request = new Request();
            request.BeginOperation(Callback, request);
        }

        private void Callback(IAsyncResult result)
        {
            var request = (Request)result.AsyncState;
            var response = request.EndOperation(result);
        }
    }

    // Introduce parameter for IAsyncResult.AsyncState
    internal class IntroduceParameter
    {
        public void Action()
        {
            var request = new Request();
            request.BeginOperation(result => Callback(result, (Request)result.AsyncState), request);
        }

        private void Callback(IAsyncResult result, Request request)
        {
            var response = request.EndOperation(result);
        }
    }

    // result.AsyncState is an alias of request. Capture request directly
    // as actual argument.
    internal class UnaliasAsyncState
    {
        public void Action()
        {
            var request = new Request();
            request.BeginOperation(
                result =>
                {
                    Callback(result, request);
                },
                request);
        }

        private void Callback(IAsyncResult result, Request request)
        {
            var response = request.EndOperation(result);
        }
    }

    // Replace APM-related constructs with equivalent TAP-related constructs
    // - IAsyncResult -> Task
    // - BeginXXX(Callback) -> XXXAsync().ContinueWith(Callback)
    // - EndXXX -> task.GetAwaiter().GetResult()
    internal class IntroduceTAP
    {
        public void Action()
        {
            var request = new Request();
            var task = request.OperationAsync()
                              .ContinueWith(t => Callback(t, request));
        }

        private void Callback(Task<Response> task, Request request)
        {
            var response = task.GetAwaiter().GetResult();
        }
    }

    // Replace explicit continuation and GetAwaiter().GetResult()
    // with async/await keywords. When the actual result is awaited
    // in a nested method, introduce async/await while moving up the call chain.
    // Use ConfigureAwait(false) to retain the synchronization behavior of the APM pattern
    internal class IntroduceAsyncAwait
    {
        public async Task Action()
        {
            var request = new Request();
            var task = request.OperationAsync();
            await Callback(task, request);
        }

        private async Task Callback(Task<Response> task, Request request)
        {
            var response = await task.ConfigureAwait(false);
        }
    }
}