using NUnit.Framework;

namespace Refactoring_Tests
{
    [TestFixture]
    public class NonMethodReferenceCallbackTest : APMToAsyncAwaitRefactoringTestBase
    {
        [Test]
        public void TestThatRefactoringFieldRefCallbackThrowsPreconditionException()
        {
            AssertThatRefactoringOriginalCodeThrowsPreconditionException(
                FieldRefCode,
                FirstBeginInvocationFinder("request.BeginGetResponse")
            );
        }

        [Test]
        public void TestThatRefactoringParameterRefCallbackThrowsPreconditionException()
        {
            AssertThatRefactoringOriginalCodeThrowsPreconditionException(
                ParameterRefCode,
                FirstBeginInvocationFinder("request.BeginGetResponse")
            );
        }

        [Test]
        public void TestThatRefactoringLocalRefCallbackThrowsPreconditionException()
        {
            AssertThatRefactoringOriginalCodeThrowsPreconditionException(
                LocalRefCode,
                FirstBeginInvocationFinder("request.BeginGetResponse")
            );
        }

        private const string FieldRefCode = @"using System;
using System.Net;

namespace TextInput
{
    class FieldRefCallback
    {
        protected AsyncCallback Callback;

        public void FireAndForgetDelegate()
        {
            var request = WebRequest.Create(""http://www.microsoft.com/"");
            request.BeginGetResponse(Callback, request);
        }
    }
}";

        private const string ParameterRefCode = @"using System;
using System.Net;

namespace TextInput
{
    class ParameterRefCallback
    {
        public void Action(AsyncCallback callback)
        {
            var request = WebRequest.Create(""http://www.microsoft.com/"");
            request.BeginGetResponse(callback, request);
        }
    }
}";

        private const string LocalRefCode = @"using System;
using System.Net;

namespace TextInput
{
    class ParameterRefCallback
    {
        public void Action()
        {
            var callback = new AsyncCallback(result => {
                var req  = (WebRequest)result.AsyncState;
                var response = req.EndGetResponse(result);
            });

            var request = WebRequest.Create(""http://www.microsoft.com/"");
            request.BeginGetResponse(callback, request);
        }
    }
}";
    }
}
