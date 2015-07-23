using System;
using System.Net;
using System.Threading.Tasks;

namespace Challenges
{
    public class OriginalProgram_TryCatchBlock
    {
        public void Action()
        {
            var request = new Request();
            request.BeginOperation(Callback, request);
        }

        private void Callback(IAsyncResult result)
        {
            try
            {
                var request = (Request)result.AsyncState;
                var response = request.EndOperation(result);
            }
            catch (WebException)
            {
            }
        }
    }

    public class RefactoredProgram_TryCatchBlock
    {
        public async void Action()
        {
            var request = WebRequest.Create("http://www.microsoft.com/");
            var task = request.GetResponseAsync();
            await Callback(task, request);
        }

        private static async Task Callback(Task<WebResponse> task, WebRequest request)
        {
            try
            {
                var response = await task.ConfigureAwait(false);
            }
            catch (WebException)
            {
            }
        }
    }
}