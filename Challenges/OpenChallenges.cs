using System;
using System.Net;

namespace Challenges
{
    internal class OpenChallenges
    {
        // EndGetResponse is probably used in the report delegate, but it is impossible
        // to be certain about this without considering surrounding code.
        //
        // From 7Pass@07d6a071247ff885ac0cf74348b5409ed54eae03
        // Keepass/Sources/Web/WebUtils.cs: 29
        public static void Download(string url, ICredentials credentials,
            Action<HttpWebRequest, Func<HttpWebResponse>> report)
        {
            var request = WebRequest.CreateHttp(url);
            request.UserAgent = "7Pass";

            if (credentials != null)
                request.Credentials = credentials;

            request.BeginGetResponse(ar => report(request, () => (HttpWebResponse)request.EndGetResponse(ar)), null);
        }
    }
}