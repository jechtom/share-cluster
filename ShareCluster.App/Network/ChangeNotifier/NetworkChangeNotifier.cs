using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ShareCluster.Network.ChangeNotifier
{
    public class NetworkChangeNotifier : IDisposable, INetworkChangeNotifier
    {
        private Timer _checkTimer;
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);
        private bool _isDisposed;
        private readonly ILogger<NetworkChangeNotifier> _logger;
        private HashSet<IPAddress> _addresses;
        private readonly object _syncLock = new object();
        private bool _isStarted;

        public NetworkChangeNotifier(ILogger<NetworkChangeNotifier> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _addresses = null;
        }

        public void Start()
        {
            if (_isStarted) throw new InvalidOperationException("Already started");
            _isStarted = true;

            NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
            _checkTimer = new Timer(CheckTimer_Callback, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            CheckForChangesAndScheduleNext();
        }

        private void CheckTimer_Callback(object state)
        {
            CheckForChangesAndScheduleNext();
        }

        private void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            CheckForChangesAndScheduleNext();
        }

        private void NetworkChange_NetworkAddressChanged(object sender, EventArgs e)
        {
            CheckForChangesAndScheduleNext();
        }

        public event EventHandler Changed;

        public void Dispose()
        {
            if (!_isStarted) return;
            if (_isDisposed) return;
            _isDisposed = true;

            // dispose
            _checkTimer.Dispose();
            NetworkChange.NetworkAvailabilityChanged -= NetworkChange_NetworkAvailabilityChanged;
            NetworkChange.NetworkAddressChanged -= NetworkChange_NetworkAddressChanged;
        }


        private void CheckForChangesAndScheduleNext()
        {
            try
            {
                var notifyChange = false;
                lock (_syncLock)
                {
                    // detect new set
                    var addressesNew = new HashSet<IPAddress>(_addresses?.Count ?? 10);
                    try
                    {
                        foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                        {
                            if (ni.OperationalStatus != OperationalStatus.Up) continue;

                            foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                            {
                                if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                                {
                                    addressesNew.Add(ip.Address);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogWarning(e, "Can't read network properties");
                    }

                    if (_addresses == null)
                    {
                        // first fetch
                        _addresses = addressesNew;
                    }
                    else
                    {
                        // compare sets
                        if (!addressesNew.SetEquals(_addresses))
                        {
                            _addresses = addressesNew;
                            notifyChange = true;
                        }
                    }
                }

                // notify?
                if (notifyChange)
                {
                    _logger.LogInformation("Found new network configuration.");
                    Changed?.Invoke(this, EventArgs.Empty);
                }
            }
            finally
            {
                // schedule next check
                _checkTimer.Change(_checkInterval, Timeout.InfiniteTimeSpan);
            }
        }
    }
}
