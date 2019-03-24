using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ShareCluster.Packaging.IO
{
    public class StreamsFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly CryptoFacade _cryptoFacade;

        public StreamsFactory(ILoggerFactory loggerFactory, CryptoFacade cryptoFacade)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _cryptoFacade = cryptoFacade ?? throw new ArgumentNullException(nameof(cryptoFacade));
        }

        public ControlledStream CreateControlledStreamFor(IStreamController controller, MeasureItem measure = null)
            => new ControlledStream(_loggerFactory, controller) { Measure = measure };

        public HashStreamController CreateHashStreamController(HashStreamVerifyBehavior behavior, Stream nestedStream)
            => new HashStreamController(_loggerFactory, _cryptoFacade, behavior, nestedStream);

        public HashStreamVerifyBehavior CreateHashStreamBehavior(PackageContentDefinition definition, int[] parts)
            => new HashStreamVerifyBehavior(_loggerFactory, definition, parts);

        public IStreamController CreateFilterPartsStreamController(PackageContentDefinition definition, int[] parts, bool[] partsToKeep, Stream nestedStream)
            => new FilterPartsStreamController(_loggerFactory, parts, partsToKeep, nestedStream);
    }
}
