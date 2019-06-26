using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using ShareCluster.Network;

namespace ShareCluster.Core
{
    /// <summary>
    /// Represents app instance. Based on <see cref="AppInstanceSettings"/> it can start and stop application.
    /// </summary>
    public class AppInstance : IDisposable
    {
        private bool _isStarted;
        private IWebHost _webHost;
        private AppInstanceStarter _bootstrapper;
        
        public void Dispose()
        {
            _bootstrapper.Stop();
            if (_webHost != null) _webHost.Dispose();
        }

        public void Start(AppInstanceSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (_isStarted) throw new InvalidOperationException("Already started.");
            _isStarted = true;

            var installer = new AppInstanceServicesInstaller(settings);
            
            // configure services
            string exeFolder = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            _webHost = new WebHostBuilder()
                .UseKestrel(c =>
                {
                    // extend grace period so server don't kick peer waiting for opening file etc.
                    c.Limits.MinResponseDataRate = new MinDataRate(240, TimeSpan.FromSeconds(20));
                    c.ListenAnyIP(settings.NetworkSettings.TcpServicePort);
                })
                .UseEnvironment("Development")
                .UseContentRoot(Path.Combine(exeFolder, "WebInterface"))
                .ConfigureServices(installer.ConfigureServices)
                .UseStartup<HttpStartup>()
                .Build();

            // start
            _webHost.Start();

            // bootstrap
            _bootstrapper = _webHost.Services.GetRequiredService<AppInstanceStarter>();
            _bootstrapper.Start(settings);
        }
    }
}
