using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ShareCluster.Network
{
    public class NetworkChangeNotifier : IDisposable
    {
        private Timer _checkTimer;
        private bool _isDisposed;
        private readonly NetworkSettings _networkSettings;
        private readonly ILogger<NetworkChangeNotifier> _logger;
        private HashSet<IPAddress> _addresses;
        private bool _addressesKnown;
        private readonly object _syncLock = new object();

        public NetworkChangeNotifier(NetworkSettings networkSettings, ILogger<NetworkChangeNotifier> logger)
        {
            _networkSettings = networkSettings ?? throw new ArgumentNullException(nameof(networkSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _addresses = new HashSet<IPAddress>();
            _addressesKnown = false;
            Start();
        }

        private void Start()
        {
            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
            TimeSpan interval = _networkSettings.NetworkChangeDetectionInterval;
             _checkTimer = new Timer(CheckTimer_Callback, null, TimeSpan.Zero, interval);
        }

        private void CheckTimer_Callback(object state)
        {
            CheckForChanges();
        }

        private void NetworkChange_NetworkAddressChanged(object sender, EventArgs e)
        {
            CheckForChanges();
        }

        public event EventHandler Change;

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            // dispose
            _checkTimer.Dispose();
            NetworkChange.NetworkAddressChanged -= NetworkChange_NetworkAddressChanged;
        }

        private void CheckForChanges()
        {
            bool notifyChange = false;

            lock (_syncLock)
            {
                // detect new set
                var addressesNew = new HashSet<IPAddress>(_addresses.Count);
                try
                {
                    foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (!ip.IsDnsEligible && ip.Address.AddressFamily == AddressFamily.InterNetwork)
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

                if (!_addressesKnown)
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
            if(notifyChange)
            {
                _logger.LogTrace("Found new network configuration.");
                Change?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
