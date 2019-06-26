using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Network.Protocol.Messages
{
    /// <summary>
    /// Used it identify types that MVC should not validate.
    /// </summary>
    /// <remarks>
    /// As we're not using MVC validation for messages this should be implemented by all root message contracts used in controllers.
    /// </remarks>
    public interface IMessage
    {
    }
}
