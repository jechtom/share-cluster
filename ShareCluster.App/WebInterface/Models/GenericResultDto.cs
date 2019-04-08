using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.WebInterface.Models
{
    public class GenericResultDto
    {
        private GenericResultDto() { }

        public static GenericResultDto Ok => new GenericResultDto()
        {
            Success = true
        };

        public static GenericResultDto Failed(string message) => new GenericResultDto()
        {
            Success = false,
            ErrorMessage = message
        };

        public string ErrorMessage { get; private set; }
        public bool Success { get; private set; }
    }
}
