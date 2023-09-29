using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using GreenPipes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Play.Common.HealthChecks;
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
        private readonly IHostEnvironment _environment;
        public Startup(IConfiguration configuration, IHostEnvironment environment)
        {
            Configuration = configuration;
            _environment = environment;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            BsonSerializer.RegisterSerializer(new GuidSerializer(BsonType.String));
            var serviceSettings = Configuration.GetSection(nameof(ServiceSettings)).Get<ServiceSettings>();
            var mongoDbSettings = Configuration.GetSection(nameof(MongoDbSettings)).Get<MongoDbSettings>();
            

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

            AddIdentityServer(services);

            services.AddLocalApiAuthentication();

            services.AddControllers();

            services.AddHostedService<IdentitySeedHostedService>();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Play.Identity.Service", Version = "v1" });
            });

            services.AddHealthChecks()
                .AddMongoDb();

            // /*
            // * https://learn.dotnetacademy.io/courses/take/net-microservices-cloud/lessons/38615232-forwarding-headers-to-the-identity-microservice
            // * Let's configure identity microservice, so it can handle HTTPS reuqests coming from the API Gateway. So when you run a service like
            // * the identity service behind the API gateway, there are a couple of headers that you need to make sure that are forwarded from the 
            // * Api Gateway all the way down to you service. If that doesn't happen, then the HTTPS communication is not going to work properly.
            // * let's make sure that we do that header forwarding.
            // */
            // services.Configure<ForwardedHeadersOptions>(options =>
            // {
            //     /*
            //     * after configure this and app.UseForwardedHeaders(); below. You can use https for Api gateway.
            //     * these headers here identifies what is going to be the originating IP address of the client. So when the client like postman, FE calls the
            //     * Api gateway, what is the IP address from that client. So that's going to come in this header. And the other header is going to be 
            //     * ForwardedHeaders.XForwardedProto. And what that contains is just the protocol that was used at originally, either it was HTTP or HTTPS.
            //     * But that's going to come in that other header.
            //     */
            //     options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            //     /*
            //     * And the next thing we want to do here is to clean up a couple of things that by default are already populated that you don't want them 
            //     * populated with default values.
            //     */
            //     options.KnownNetworks.Clear();
            //     options.KnownProxies.Clear();
            // });
        }


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // /*
            // * https://learn.dotnetacademy.io/courses/take/net-microservices-cloud/lessons/38615232-forwarding-headers-to-the-identity-microservice
            // * this make sure that the ForwardedHeaders are used at in the request pipeline.
            // */
            // app.UseForwardedHeaders();

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

            // app.UseHttpsRedirection();

            app.Use((context, next) =>
            {
                // for identity service can run behind the api gateway
                var identitySettings = Configuration.GetSection(nameof(IdentitySettings)).Get<IdentitySettings>();
                context.Request.PathBase = new PathString(identitySettings.PathBase);

                return next();
            });

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

                endpoints.MapPlayEconomyHealthCheck();
            });
        }

        private void AddIdentityServer(IServiceCollection services)
        {
            var identityServerSettings = Configuration.GetSection(nameof(IdentityServerSettings)).Get<IdentityServerSettings>();

            var builder = services.AddIdentityServer(options =>
            {
                // three properties will be helpful when you don't know why IdentityServer does not work properly
                // we add three configurations below to see verbose message in the console log and that can help a lot
                options.Events.RaiseSuccessEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseErrorEvents = true;

                //This is required in the Docker environment since the strict Linus permissions you'll set there won't allow IdentityServer to crea keys in
                //the default /keys directory
                // options.KeyManagement.KeyPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            })
            .AddAspNetIdentity<ApplicationUser>()
            .AddInMemoryApiScopes(identityServerSettings.ApiScopes)
            .AddInMemoryApiResources(identityServerSettings.ApiResources)
            .AddInMemoryClients(identityServerSettings.Clients)
            .AddDeveloperSigningCredential()
            .AddInMemoryIdentityResources(identityServerSettings.IdentityResources);

            // //After configuring TLS certificate, this code base uses for singing key authen/author in production
            // if (!_environment.IsDevelopment())
            // {
            //     var identitySettings = Configuration.GetSection(nameof(IdentitySettings)).Get<IdentitySettings>();

            //     /*
            //     * read the values of the certificate files in Kubernetes secret
            //     * CreateFromPemFile(), remember that this file is being mounted into a directory in the microservice file system. It is being mounted by
            //     * Kubernetes. So we want to read it from there.
            //     */
            //     var cer = X509Certificate2.CreateFromPemFile(
            //         identitySettings.CertificateCerFilePath,
            //         identitySettings.CertificateKeyFilePath
            //     );
            //     builder.AddSigningCredential(cer);
            // }
        }
    }
}
