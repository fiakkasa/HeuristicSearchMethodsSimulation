using Microsoft.AspNetCore.Hosting;

[assembly: HostingStartup(typeof(HeuristicSearchMethodsSimulation.Areas.Identity.IdentityHostingStartup))]
namespace HeuristicSearchMethodsSimulation.Areas.Identity
{
    public class IdentityHostingStartup : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices((context, services) =>
            {
            });
        }
    }
}