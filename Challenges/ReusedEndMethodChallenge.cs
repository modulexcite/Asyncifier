using System;
using System.Net;

namespace Challenges
{
    internal class ReusedEndMethodChallenge
    {
        public static void EntryPoint1()
        {
            WebRequest request = WebRequest.Create("url://...");
            request.BeginGetResponse(Callback1, request);
        }

        public static void Callback1(IAsyncResult result)
        {
            var contents = GetResponseContents(result);

            // Do something with contents.
        }

        public static void EntryPoint2()
        {
            WebRequest request = WebRequest.Create("url://...");
            request.BeginGetResponse(Callback2, request);
        }

        public static void Callback2(IAsyncResult result)
        {
            var contents = GetResponseContents(result);

            // Do something else with contents.
        }

        private static String GetResponseContents(IAsyncResult result)
        {
            WebRequest request = (WebRequest)result.AsyncState;
            WebResponse response = request.EndGetResponse(result);

            string contents = @"Do some work to read the contents of the response";

            return contents;
        }
    }
}