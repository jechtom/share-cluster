using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;

namespace ShareCluster.Network
{
    public class NetworkInterfacesDiscovery 
    {
        public void Start()
        {
            Console.WriteLine("ST");
            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
            Report();
        }

        private void Report()
        {
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface n in adapters)
            {
                Console.WriteLine("   {0} is {1}", n.Name, n.OperationalStatus);
            }
        }

        private void NetworkChange_NetworkAddressChanged(object sender, EventArgs e)
        {
            Console.WriteLine(sender);
            Report();
        }

    }
}
