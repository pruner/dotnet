using System;

namespace Pruner.Instrumenter
{
    class ValidationException : Exception
    {
        public ValidationException(string message) : base(message)
        {
        }
    }
}