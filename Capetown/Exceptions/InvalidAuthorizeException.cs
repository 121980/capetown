using System;

namespace Capetown.Exceptions
{
    /// <summary>
    /// Ошибка неавторизованного доступа
    /// </summary>
    public class InvalidAuthorizeException : Exception
    {
        public InvalidAuthorizeException() : base() { }
        public InvalidAuthorizeException( string message ) : base(message) { }
        public InvalidAuthorizeException( string message, System.Exception inner ) : base(message, inner) { }
        
    }
}
