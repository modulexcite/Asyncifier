using System;
using System.Net;

namespace Test
{
    internal class Constants
    {
        public const string GoogleUrl = "http://www.google.com/";
    }

    internal class SimpleAPMCase
    {
        public void FireAndForget()
        {
            var request = WebRequest.Create(Constants.GoogleUrl);
            request.BeginGetResponse(CallBack, request);

            // Do something while GET request is in progress.
        }

        private void CallBack(IAsyncResult result)
        {
            var request = (WebRequest)result.AsyncState;
            var response = request.EndGetResponse(result);

            // Do something with the response.
        }
    }

    /// <summary>
    /// Refactored version of SimpleAPMCase. Note the use of ConfigureAwait(false), to make sure that the callee's synchronization context is reused.
    /// </summary>
    internal class SimpleAPMCaseRefactored
    {
        public async void FireAndForget()
        {
            var request = WebRequest.Create(Constants.GoogleUrl);
            var task = request.GetResponseAsync().ConfigureAwait(false);

            // Do something while GET request is in progress.

            var response = await task;

            // Do something with the response.
        }
    }

    internal class SimpleAPMCaseWithoutCallback
    {
        public void FireAndForget()
        {
            var request = WebRequest.Create(Constants.GoogleUrl);
            var result = request.BeginGetResponse(null, request);

            // Do something while GET request is in progress.

            var response = request.EndGetResponse(result);

            // Do something with the response.
        }
    }

    internal class SimpleAPMCaseWithoutCallbackRefactored
    {
        public async void FireAndForget()
        {
            var request = WebRequest.Create(Constants.GoogleUrl);
            var task = request.GetResponseAsync();

            // Do something while GET request is in progress.

            var response = await task;

            // Do something with the response.
        }
    }
}