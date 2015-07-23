using System;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Challenges
{
    internal class SimpleCase
    {
        public void FireAndForget()
        {
            var request = WebRequest.Create("http://www.google.com/");
            request.BeginGetResponse(CallBack, request);

            // Do something while GET request is in progress.
        }

        private void CallBack(IAsyncResult result)
        {
            var request = (WebRequest)result.AsyncState;
            var response = request.EndGetResponse(result);

            // Do something with the response.
        }

        public async void FireAndForgetRefactored()
        {
            var request = WebRequest.Create("http://www.google.com/");
            var task = request.GetResponseAsync().ConfigureAwait(false);

            // Do something while GET request is in progress.

            await CallbackRefactored(task);
        }

        private static async Task CallbackRefactored(ConfiguredTaskAwaitable<WebResponse> task)
        {
            var response = await task;

            // Do something with the response.
        }
    }
}