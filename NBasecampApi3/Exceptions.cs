using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NBasecampApi3
{
    public class BasecampException : Exception
    {
        public BasecampException() { }
        public BasecampException(string message) : base(message) { }
        public BasecampException(string message, Exception innerException) : base(message, innerException) { }

        public Uri RequestUri { get; set; }
    }

    public class BasecampResponseException : BasecampException
    {
        public BasecampResponseException() { }
        public BasecampResponseException(string message) : base(message) { }
        public BasecampResponseException(string message, Exception innerException) : base(message, innerException) { }

        public HttpStatusCode? ResponseStatusCode { get; set; }
        public string ResponseContent { get; set; }
    }

    public class BasecampUnauthorizedResponseException : BasecampResponseException
    {
        public BasecampUnauthorizedResponseException()
        {
            ResponseStatusCode = HttpStatusCode.Unauthorized;
        }
        public BasecampUnauthorizedResponseException(string message) : base(message)
        {
            ResponseStatusCode = HttpStatusCode.Unauthorized;
        }
        public BasecampUnauthorizedResponseException(string message, Exception innerException) : base(message, innerException)
        {
            ResponseStatusCode = HttpStatusCode.Unauthorized;
        }
    }

    public class BasecampTooManyRequestsException : BasecampResponseException
    {
        public BasecampTooManyRequestsException()
        {
            ResponseStatusCode = (HttpStatusCode)429;
        }
        public BasecampTooManyRequestsException(string message) : base(message)
        {
            ResponseStatusCode = (HttpStatusCode)429;
        }
        public BasecampTooManyRequestsException(string message, Exception innerException) : base(message, innerException)
        {
            ResponseStatusCode = (HttpStatusCode)429;
        }

        public TimeSpan? RetryAfter { get; set; }
    }
}
