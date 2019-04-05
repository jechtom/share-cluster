using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster
{
    public class UIException : Exception
    {
        public UIException(string message) : base(message)
        {
        }

        public UIException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
