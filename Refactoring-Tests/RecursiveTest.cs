using NUnit.Framework;

namespace Refactoring_Tests
{
    [TestFixture]
    public class RecursiveTest : APMToAsyncAwaitRefactoringTestBase
    {
        [Test]
        public void TestThatRecursiveCaseThrowsPreconditionException()
        {
            AssertThatRefactoringOriginalCodeThrowsPreconditionException(
                OriginalCode,
                FirstBeginInvocationFinder("stream.BeginRead")
            );
        }

        private const string OriginalCode = @"using System;
using System.IO;

namespace TestApp
{
    class RecursiveAPM
    {
        private const int Count = 1024;

        private readonly byte[] _buffer = new byte[Count];
        private int _read;

        void Action(Stream stream)
        {
            _read = 0;
            stream.BeginRead(_buffer, 0, Count, Callback, stream);
        }

        void Callback(IAsyncResult result)
        {
            var stream = (Stream)result.AsyncState;
            _read += stream.EndRead(result);

            if (_read < Count)
            {
                stream.BeginRead(_buffer, _read, Count - _read, Callback, stream);
            }
        }
    }
}
";
    }
}
