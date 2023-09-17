using System;
using System.IO;
using System.Reflection;
using GreenPipes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Play.Common.MassTransit;
using Play.Common.Settings;
using Play.Identity.Service.Entities;
using Play.Identity.Service.Exceptions;
using Play.Identity.Service.HealthChecks;
using Play.Identity.Service.HostedServices;
using Play.Identity.Service.Settings;

namespace Play.Identity.Service
{
    public class Startup
    {
        private const string AllowedOriginSetting = "AllowedOrigin";
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            BsonSerializer.RegisterSerializer(new GuidSerializer(BsonType.String));
            var serviceSettings = Configuration.GetSection(nameof(ServiceSettings)).Get<ServiceSettings>();
            var mongoDbSettings = Configuration.GetSection(nameof(MongoDbSettings)).Get<MongoDbSettings>();
            var identityServerSettings = Configuration.GetSection(nameof(IdentityServerSettings)).Get<IdentityServerSettings>();

            services.Configure<IdentitySettings>(Configuration.GetSection(nameof(IdentitySettings)))
                .AddDefaultIdentity<ApplicationUser>()
                .AddRoles<ApplicationRole>()
                .AddMongoDbStores<ApplicationUser, ApplicationRole, Guid>
                (
                    mongoDbSettings.ConnectionString,
                    serviceSettings.ServiceName
                );

            services.AddMassTransitWithMessageBroker(Configuration, retryConfigurator =>
            {
                retryConfigurator.Interval(3, TimeSpan.FromSeconds(5));
                retryConfigurator.Ignore(typeof(InsufficientFundsException));
                retryConfigurator.Ignore(typeof(UnknownUserException));
            });

            services.AddIdentityServer(options =>
                {
                    // three properties will be helpful when you don't know why IdentityServer does not work properly
                    // we add three configurations below to see verbose message in the console log and that can help a lot
                    options.Events.RaiseSuccessEvents = true;
                    options.Events.RaiseFailureEvents = true;
                    options.Events.RaiseErrorEvents = true;

                    //This is required in the Docker environment since the strict Linus permissions you'll set there won't allow IdentityServer to crea keys in
                    //the default /keys directory
                    options.KeyManagement.KeyPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                })
                .AddAspNetIdentity<ApplicationUser>()
                .AddInMemoryApiScopes(identityServerSettings.ApiScopes)
                .AddInMemoryApiResources(identityServerSettings.ApiResources)
                .AddInMemoryClients(identityServerSettings.Clients)
                .AddInMemoryIdentityResources(identityServerSettings.IdentityResources);

            services.AddLocalApiAuthentication();

            services.AddControllers();

            services.AddHostedService<IdentitySeedHostedService>();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Play.Identity.Service", Version = "v1" });
            });

            services.AddHealthChecks()
                .Add(new HealthCheckRegistration(
                    //Add anyname you want
                    "mongodb",

                    // the second parameter that we're going to provide here is going to be what we call the factory that is going to
                    // create our model to be health check instance.
                    serviceProvider =>
                    {
                        var mongoClient = new MongoClient(mongoDbSettings.ConnectionString);
                        return new MongoDbHealthCheck(mongoClient);
                    },

                    //The next parameter is going to be the failure status. So this is going to be a status that should be reported if the
                    // healthcheck fails.
                    HealthStatus.Unhealthy,

                    //The next parameter we're going to identify here is what we call the tax. So this is a way for you to group a different
                    //set of health check registrations or health checks into specific groups that you want to report in different ways. So
                    //in our case, we're going to create a group that we're going to be calling it, the ready group. Because we're going to
                    //have both live test and readiness checks. And we'll talk about those in a moment. But for now, let's just assign the
                    // ready tag to this.
                    new[] { "ready" },
                    // we can specify time out. So this is how much time this health check is going to wait before giving up. It's try to
                    // do its thing. And if it can't get a healthy status, it's going to just a timeout and produce unhealthy status.
                    TimeSpan.FromSeconds(3)
                ));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Play.Identity.Service v1"));
                app.UseCors(builder =>
                {
                    builder.WithOrigins(Configuration[AllowedOriginSetting])
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles();

            app.UseRouting();

            // if your organization makes less than 1M USD annual gross revenue, you can qualify a free Community
            // Edition license that you can use to go to a Production environment. 
            // More info: https://duendesoftware.com/products/CommunityEdition
            app.UseIdentityServer();

            app.UseAuthorization();

            app.UseCookiePolicy(new CookiePolicyOptions
            {
                MinimumSameSitePolicy = Microsoft.AspNetCore.Http.SameSiteMode.Lax
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapRazorPages();

                //endpoints.MapHealthChecks("/health");
                
                //we also have to create appropriate routes so that our clients can access both our ready and are liveness health checks.
                //we defined a very simple health route. So now we're going to define two routes. Like I said, one route for the readiness check
                //and one route for the liveness check. The readiness check is going to be the one that's going to go ahead and make sure that
                //this microservice is actively ready to start serving requests. So even if they make a service is up and running, it doesn't mean 
                //that it is ready. It needs to make sure that, in this case. a database is ready to go and maybe some other services. While 
                //liveness test is just going to be for making sure that these microservice is alive
                endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions()
                {
                    // this "ready" value comes from "ready" above you defined in .AddHealthChecks().Add(new HealthCheckRegistration...
                    Predicate = (check) => check.Tags.Contains("ready") 
                });

                //this is a liveness check, the liveness check is just verify if the service is alive or not
                 endpoints.MapHealthChecks("/health/live", new HealthCheckOptions()
                {
                    // false means at this point, do not filter out every single health check registration that you have, and just let me know
                    //if the service can respond to this. So that's the way to provide that predicate.
                    Predicate = (check) => false
                });
            });
        }
    }
}
