using System;
using System.Linq;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.IO;

namespace ShareCluster.Network
{
    public class ClusterAnnouncer : IDisposable
    {
        private readonly NetworkSettings _settings;
        private List<Messages.ClusterDiscoveryItem> _clusters;
        private UdpClient _client;
        private CancellationTokenSource _cancel;
        private Task _task;

        public ClusterAnnouncer(NetworkSettings settings, IEnumerable<ClusterInfo> clusters)
        {
            this._settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _clusters = (clusters ?? throw new ArgumentNullException(nameof(clusters)))
                .Select(c => new Messages.ClusterDiscoveryItem()
                {
                    Name = c.Name,
                    Hash = c.Hash.Data
                }).ToList();
        }

        public void Dispose()
        {
            _cancel.Cancel();
            _task.Wait();
        }

        public void Start()
        {
            _client = new UdpClient(_settings.UdpAnnouncePort);
            _cancel = new CancellationTokenSource();
            _task = Task.Run(StartInternal, _cancel.Token)
                .ContinueWith(c=>
                {
                    Console.WriteLine("Disposing UDP client.");
                    _client.Dispose();
                });
        }

        private async Task StartInternal()
        {
            while (!_cancel.IsCancellationRequested)
            {
                var rec = await _client.ReceiveAsync().WithCancellation(_cancel.Token);
                try
                {
                    Messages.AnnounceReq messageReq;
                    messageReq = _settings.MessageSerializer.Deserialize<Messages.AnnounceReq>(rec.Buffer);
                    Console.WriteLine($"Received request from: {messageReq.ClientName}");
                }
                catch(TaskCanceledException)
                {
                    return;
                }
                catch
                {
                    Console.WriteLine($"Cannot read message from: {rec.RemoteEndPoint}");
                }

                try
                {
                    byte[] bytes = _settings.MessageSerializer.Serialize(new Messages.AnnounceRes()
                        {
                            IsSuccess = true,
                            FailReason = null,
                            ServerVersion = 1,
                            Clusters = _clusters
                        });
                    await _client.SendAsync(bytes, bytes.Length, rec.RemoteEndPoint).WithCancellation(_cancel.Token);

                }
                catch (TaskCanceledException)
                {
                    return;
                }
                catch (SocketException)
                {
                    Console.WriteLine($"Client closed connection befory reply: {rec.RemoteEndPoint}");
                }
                catch(Exception e)
                {
                    Console.WriteLine($"Error sending response to client: {e}");
                }
            }
        }
    }
}
