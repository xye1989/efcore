using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.OData.Batch;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNet.OData.Routing;
using Microsoft.AspNet.OData.Routing.Conventions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Edm;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class ODataQueryTestFixtureInitializer
    {
        public static (string BaseAddress, IHttpClientFactory ClientFactory, IHost SelfHostServer) Initialize<TContext>(
            string storeName,
            Type[] controllers,
            IEdmModel edmModel)
            where TContext : DbContext
        {
            var clientFactory = default(IHttpClientFactory);
            var selfHostServer = Host.CreateDefaultBuilder()
                .ConfigureServices(services => services.AddSingleton<IHostLifetime, NoopHostLifetime>())
                .ConfigureWebHostDefaults(webBuilder => webBuilder
                    .UseKestrel(options => options.Listen(IPAddress.Loopback, 0))
                    .ConfigureServices(services =>
                    {
                        services.AddHttpClient();
                        services.AddOData();
                        services.AddRouting();

                        UpdateConfigureServices<TContext>(services, storeName);
                    })
                    .Configure(app =>
                    {
                         clientFactory = app.ApplicationServices.GetRequiredService<IHttpClientFactory>();

                        app.UseODataBatching();
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            var config = new EndpointRouteConfiguration(endpoints);
                            UpdateConfigure(config, controllers, edmModel);
                        });
                    })
                    .ConfigureLogging((hostingContext, logging) =>
                    {
                        logging.AddDebug();
                        logging.SetMinimumLevel(LogLevel.Warning);
                    }
                )).Build();

            selfHostServer.Start();

            var baseAddress = selfHostServer.Services.GetService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.First();

            return (baseAddress, clientFactory, selfHostServer);
        }

        public static void UpdateConfigureServices<TContext>(IServiceCollection services, string storeName)
            where TContext : DbContext
        {
            services.AddDbContext<TContext>(b =>
                b.UseSqlServer(
                    SqlServerTestStore.CreateConnectionString(storeName)));
        }

        protected static void UpdateConfigure(EndpointRouteConfiguration configuration, Type[] controllers, IEdmModel edmModel)
        {
            configuration.AddControllers(controllers);
            configuration.MaxTop(2).Expand().Select().OrderBy().Filter();

            configuration.MapODataRoute("odata", "odata",
                edmModel,
                new DefaultODataPathHandler(),
                ODataRoutingConventions.CreateDefault(),
                new DefaultODataBatchHandler());
        }

        private class NoopHostLifetime : IHostLifetime
        {
            public Task StopAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task WaitForStartAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }
    }
}

