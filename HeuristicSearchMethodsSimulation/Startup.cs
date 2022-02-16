using HeuristicSearchMethodsSimulation.Areas.Identity;
using HeuristicSearchMethodsSimulation.Interfaces;
using HeuristicSearchMethodsSimulation.Interfaces.TravelingSalesMan;
using HeuristicSearchMethodsSimulation.Models;
using HeuristicSearchMethodsSimulation.Models.TravelingSalesMan;
using HeuristicSearchMethodsSimulation.Services;
using HeuristicSearchMethodsSimulation.Services.TravelingSalesMan;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using System;
using System.Reflection;
using IdentityRole = HeuristicSearchMethodsSimulation.Models.IdentityRole;
using IdentityUser = HeuristicSearchMethodsSimulation.Models.IdentityUser;

namespace HeuristicSearchMethodsSimulation
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            var mongoConnectionUri = Configuration.GetConnectionString(Consts.MongoConnectionKey);
            var mongoOptionsSection = Configuration.GetSection(nameof(MongoOptions));

            #region Health
            services.AddHealthChecks();
            #endregion

            #region AutoMapper
            services.AddAutoMapper(Assembly.GetExecutingAssembly());
            #endregion

            #region Options
            services.Configure<AuthMessageSenderOptions>(Configuration.GetSection(nameof(AuthMessageSenderOptions)));
            services.Configure<AppOptions>(Configuration.GetSection(nameof(AppOptions)));
            services.Configure<MongoOptions>(mongoOptionsSection);
            services.Configure<TravelingSalesManOptions>(Configuration.GetSection(nameof(TravelingSalesManOptions)));
            #endregion

            #region Identity
            services
                .AddIdentity<IdentityUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddDefaultUI()
                .AddMongoDbStores<IdentityUser, IdentityRole, Guid>(mongoConnectionUri, mongoOptionsSection.Get<MongoOptions>().Databases.Identity)
                .AddDefaultTokenProviders();
            services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<IdentityUser>>();
            services.AddScoped<IEmailSender, EmailSenderService>();
            #endregion

            #region Database
            services.AddSingleton<Func<IMongoClient>>(() => new MongoClient(mongoConnectionUri));
            #endregion

            #region Blazor
            services.AddRazorPages();
            services.AddServerSideBlazor(options => options.MaxBufferedUnacknowledgedRenderBatches = 20);
            #endregion

            #region Services
            services.AddScoped<IAccessControlService, AccessControlService>();
            services
                .AddScoped<ITravelingSalesManFoundationService, TravelingSalesManFoundationService>()
                .AddScoped<ITravelingSalesManHistoryService, TravelingSalesManHistoryService>()
                .AddScoped<ITravelingSalesManNoneService, TravelingSalesManService>()
                .AddScoped<ITravelingSalesManPreselectedService, TravelingSalesManService>()
                .AddScoped<ITravelingSalesManExhaustiveService, TravelingSalesManService>()
                .AddScoped<ITravelingSalesManPartialRandomService, TravelingSalesManService>()
                .AddScoped<ITravelingSalesManPartialImprovingService, TravelingSalesManService>()
                .AddScoped<ITravelingSalesManGuidedDirectService, TravelingSalesManService>()
                .AddScoped<ITravelingSalesManEvolutionaryService, TravelingSalesManService>();
            #endregion
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks(Consts.HealthEndPoint);
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}
