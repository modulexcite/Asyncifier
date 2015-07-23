using System;
using System.Net;
using System.Threading.Tasks;

namespace Challenges
{
    internal class EndXxxDeeperInCallGraph
    {
        public void FireAndForget()
        {
            var request = WebRequest.Create("http://www.microsoft.com/");
            request.BeginGetResponse(result =>
            {
                var response = Nested0(result, request);
                DoSomethingWithResult(response);
            }, null);

            DoSomethingWhileWebRequestIsRunning();
        }

        private static WebResponse Nested0(IAsyncResult result, WebRequest request)
        {
            return Nested1(result, request);
        }

        private static WebResponse Nested1(IAsyncResult result, WebRequest request)
        {
            return request.EndGetResponse(result);
        }

        public async void FireAndForgetAsync()
        {
            var request = WebRequest.Create("http://www.microsoft.com/");
            var task = request.GetResponseAsync();

            DoSomethingWhileWebRequestIsRunning();

            var response = await Nested0(task, request);
            DoSomethingWithResult(response);
        }

        public async Task<WebResponse> Nested0(Task<WebResponse> result, WebRequest request)
        {
            return await Nested1(result);
        }

        private static async Task<WebResponse> Nested1(Task<WebResponse> result)
        {
            return await result;
        }

        private static void DoSomethingWhileWebRequestIsRunning()
        {
        }

        private static void DoSomethingWithResult(WebResponse response)
        {
        }
    }
}