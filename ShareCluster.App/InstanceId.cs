using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster
{
    /// <summary>
    /// Represents random Id of local instance of running app that changes with every start and is forgetten with shutdown.
    /// </summary>
    public class InstanceId
    {
        public InstanceId(CryptoFacade crypto)
        {
            if (crypto == null)
            {
                throw new ArgumentNullException(nameof(crypto));
            }

            Value = crypto.CreateRandom();
        }

        public Id Value { get; }
    }
}
