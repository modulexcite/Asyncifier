using System;
using System.Net;

namespace Challenges
{
    // In this example, the response variable in the Callback method should be made a parameter,
    // and the EndGetResponse call should be moved to the EntryPoint method. However, EndGetResponse
    // might throw a WebException, so the catch block should be copied to the EntryPoint method.
    // But unfortunately, the catch block references identifiers from the Callback method scope that
    // encloses the try-catch statement, so it cannot be easily moved, without taking measures to
    // make those references available in EntryPoint as well.

    // Original source: Topaz Fuel Card (https://github.com/dlhartveld/topaz-fuel-card-windows-phone)

    internal class CatchBlockPropagationChallenge
    {
        public static void EntryPoint()
        {
            // Create a WebRequest (HTTP GET) for a specific URL
            var request = WebRequest.CreateHttp("url://...");

            // Start the asynchronous operation in the background.
            // The request object is passed as 'state'.
            // After completion, the Callback method is executed.
            request.BeginGetResponse(Callback, request);
        }

        private static void Callback(IAsyncResult result)
        {
            // Retrieve the request object from the APM state object.
            var request = (HttpWebRequest)result.AsyncState;

            var x = 0;
            // Code: Do something with x.
            x += 3;
             
            try
            {
                // Retrieve the actual result of the asynchronous operation. The
                // EndGetResponse(...) method can throw a WebException. The
                // response variable must be refactored into a parameter, with
                // request.EndGetResponse(...) as actual argument.
                WebResponse response = request.EndGetResponse(result);

                // Code: Do something with 'response', which might also cause a
                // WebException to be thrown. This means that the catch block
                // cannot be removed when refactoring.
            }
            catch (WebException)
            {
                // Code: Do something with x.
                // Because x is scoped in the method Callback, moving this catch
                // block is impossible without also doing something with x.
            }
        }
    }
}