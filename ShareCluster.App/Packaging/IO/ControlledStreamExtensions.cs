using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ShareCluster.Packaging.IO
{
    public static class ControlledStreamExtensions
    {
        public static ControlledStream CreateStream(this IStreamController controller, ILoggerFactory loggerFactory, MeasureItem measure = null)
        {
            return new ControlledStream(loggerFactory, controller)
            {
                Measure = measure
            };
        }
    }
}
