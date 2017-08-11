using System;
using System.Linq;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;

namespace ShareCluster.Network
{
    public class ClusterDiscovery
    {
        private readonly NetworkSettings _settings;
        private List<Messages.ClusterDiscoveryItem> _clusters;
        private UdpClient client;
        private CancellationTokenSource _cancel;

        public ClusterDiscovery(NetworkSettings settings)
        {
            this._settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }
        
        public async Task<IEnumerable<Messages.AnnounceRes>> Discover()
        {
            var result = new List<Messages.AnnounceRes>();

            using (client = new UdpClient())
            {
                var ip = new IPEndPoint(IPAddress.Broadcast, _settings.UdpAnnouncePort);

                byte[] message = ZeroFormatter.ZeroFormatterSerializer.Serialize(new Messages.AnnounceReq()
                {
                    ClientApp = "ShareCluster.App",
                    ClientVersion = 1,
                    ClientName = "Client Name Placeholder"
                });

                var lengthSent = await client.SendAsync(message, message.Length, ip);
                if(lengthSent != message.Length)
                {
                    throw new InvalidOperationException("Cannot send discovery datagram.");
                }

                var timeout = new CancellationTokenSource(_settings.DiscoveryTimeout);
                while(!timeout.IsCancellationRequested)
                {
                    Messages.AnnounceRes response = null; 
                    try
                    {
                        var messageBytes = await client.ReceiveAsync().WithCancellation(timeout.Token);
                        response = ZeroFormatter.ZeroFormatterSerializer.Deserialize<Messages.AnnounceRes>(messageBytes.Buffer);
                    }
                    catch(TaskCanceledException)
                    {
                        break;
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine($"Cannot deserialize discovery response: {e}");
                    }

                    result.Add(response);
                }
            }
            return result;
        }

        private async Task StartInternal()
        {
            while (true)
            {
                var rec = await client.ReceiveAsync();
                Messages.AnnounceReq messageReq;
                messageReq = ZeroFormatter.ZeroFormatterSerializer.Deserialize<Messages.AnnounceReq>(rec.Buffer);
                Console.WriteLine($"Received request from: {messageReq.ClientName}");

                var bytes = ZeroFormatter.ZeroFormatterSerializer.Serialize(new Messages.AnnounceRes()
                {
                    IsSuccess = true,
                    FailReason = null,
                    ServerVersion = 1,
                    Clusters = _clusters
                });

                await client.SendAsync(bytes, bytes.Length, rec.RemoteEndPoint);
            }
        }
    }
}
