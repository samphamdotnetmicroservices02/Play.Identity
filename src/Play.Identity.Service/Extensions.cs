using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using MassTransit;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Play.Common.HealthChecks;
using Play.Common.Logging;
using Play.Common.MassTransit;
using Play.Common.OpenTelemetry;
using Play.Common.Settings;
using Play.Identity.Service.Entities;
using Play.Identity.Service.Exceptions;
using Play.Identity.Service.HostedServices;
using Play.Identity.Service.Settings;

namespace Play.Identity.Service;

public static class Extensions
{
    private const string AllowedOriginSetting = "AllowedOrigin";

    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        BsonSerializer.RegisterSerializer(new GuidSerializer(BsonType.String));
        var serviceSettings = configuration.GetSection(nameof(ServiceSettings)).Get<ServiceSettings>();
        var mongoDbSettings = configuration.GetSection(nameof(MongoDbSettings)).Get<MongoDbSettings>();


        services.Configure<IdentitySettings>(configuration.GetSection(nameof(IdentitySettings)))
            .AddDefaultIdentity<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddMongoDbStores<ApplicationUser, ApplicationRole, Guid>
            (
                mongoDbSettings!.ConnectionString,
                serviceSettings!.ServiceName
            );

        services.AddMassTransitWithMessageBroker(configuration, retryConfigurator =>
        {
            retryConfigurator.Interval(3, TimeSpan.FromSeconds(5));
            retryConfigurator.Ignore(typeof(InsufficientFundsException));
            retryConfigurator.Ignore(typeof(UnknownUserException));
        });

        AddIdentityServer(services, configuration, environment);

        services.AddLocalApiAuthentication();

        services.AddControllers();

        services.AddHostedService<IdentitySeedHostedService>();

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Play.Identity.Service", Version = "v1" });
        });

        services.AddHealthChecks()
            .AddMongoDb();

        services.AddSeqLogging(configuration)
            .AddTracing(configuration)
            .AddMetrics(configuration);

        // in local kubernetes, we don't use HTTPS
        if (!serviceSettings.IsKubernetesLocal.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            /*
            * this is just use for HTTPS
            * https://learn.dotnetacademy.io/courses/take/net-microservices-cloud/lessons/38615232-forwarding-headers-to-the-identity-microservice
            * Let's configure identity microservice, so it can handle HTTPS reuqests coming from the API Gateway. So when you run a service like
            * the identity service behind the API gateway, there are a couple of headers that you need to make sure that are forwarded from the 
            * Api Gateway all the way down to you service. If that doesn't happen, then the HTTPS communication is not going to work properly.
            * let's make sure that we do that header forwarding.
            */
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                /*
                * after configure this and app.UseForwardedHeaders(); below. You can use https for Api gateway.
                * these headers here identifies what is going to be the originating IP address of the client. So when the client like postman, FE calls the
                * Api gateway, what is the IP address from that client. So that's going to come in this header. And the other header is going to be 
                * ForwardedHeaders.XForwardedProto. And what that contains is just the protocol that was used at originally, either it was HTTP or HTTPS.
                * But that's going to come in that other header.
                */
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

                /*
                * And the next thing we want to do here is to clean up a couple of things that by default are already populated that you don't want them 
                * populated with default values.
                */
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });
        }

        return services;
    }

    public static WebApplication ConfigurePipeline(this WebApplication app, IConfiguration configuration, IHostEnvironment environment)
    {
        var serviceSettings = configuration.GetSection(nameof(ServiceSettings)).Get<ServiceSettings>();
        if (!serviceSettings!.IsKubernetesLocal.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            /*
            * this is just use for HTTPS
            * https://learn.dotnetacademy.io/courses/take/net-microservices-cloud/lessons/38615232-forwarding-headers-to-the-identity-microservice
            * this make sure that the ForwardedHeaders are used at in the request pipeline.
            */
            app.UseForwardedHeaders();
        }

        if (environment.IsDevelopment())
        {
            // "UseDeveloperExceptionPage()" before dotnet 6, you have to configure this manually. But now with the minimum hosting model
            // this is no longer required. The runtime will take care of it. It will present if it is needed.
            //app.UseDeveloperExceptionPage();

            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Play.Identity.Service v1"));
            app.UseCors(builder =>
            {
                builder.WithOrigins(configuration[AllowedOriginSetting]!)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        }

        /*
        * Prometheus:
        * With the metrics side, we also need to do one more thing and that is to enable or create or expose what's going to
        * be called the scraping endpoint. So this is the endpoint that tools like Prometheus can use in a giving interval,
        * start pulling down and pulling into Prometheus, the metrics that we've been collecting across the lifetime of the
        * application. This "UseOpenTelemetryPrometheusScrapingEndpoint" is going to stand up that endpoint that it actually
        * ends with /metrics. You can configure it if you want to, for us, that's going to be good enough
        */
        app.UseOpenTelemetryPrometheusScrapingEndpoint();

        app.UseHttpsRedirection();

        app.Use((context, next) =>
        {
            // for identity service can run behind the api gateway
            var identitySettings = configuration.GetSection(nameof(IdentitySettings)).Get<IdentitySettings>();

            if (serviceSettings.IsKubernetesLocal.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                if (context.Request.Host.Value.Equals(serviceSettings.InternalHostAuthority, StringComparison.OrdinalIgnoreCase)
                && context.Request.Path.HasValue
                && context.Request.Path.Value.Equals("/identity-svc/.well-known/openid-configuration/jwks", StringComparison.OrdinalIgnoreCase))
                {
                    var newPath = context.Request.Path.Value.Replace("/identity-svc", string.Empty);
                    context.Request.Path = new PathString(newPath);
                }
            }

            if (!context.Request.Host.Value.Equals(serviceSettings.InternalHostAuthority, StringComparison.OrdinalIgnoreCase))
                context.Request.PathBase = new PathString(identitySettings!.PathBase);

            return next();
        });

        app.UseStaticFiles();

        /*
        * "UseRouting()", it turns to be that there have been some, also some improvements with the minimum hosting model that make
        * better use of how this method was called. And it turns to be that we don't need to call this method anymore. The routing
        * method is no longer needed
        */
        //app.UseRouting();

        // if your organization makes less than 1M USD annual gross revenue, you can qualify a free Community
        // Edition license that you can use to go to a Production environment. 
        // More info: https://duendesoftware.com/products/CommunityEdition
        app.UseIdentityServer();

        app.UseAuthorization();

        app.UseCookiePolicy(new CookiePolicyOptions
        {
            MinimumSameSitePolicy = Microsoft.AspNetCore.Http.SameSiteMode.Lax
        });

        // app.UseEndpoints(endpoints =>
        // {
        //     endpoints.MapControllers();
        //     endpoints.MapRazorPages();

        //     //endpoints.MapHealthChecks("/health");

        //     endpoints.MapPlayEconomyHealthCheck();
        // });
        app.MapControllers();
        app.MapRazorPages();
        //app.MapHealthChecks("/health");
        app.MapPlayEconomyHealthCheck();

        return app;
    }

    public static UserDto? AsDto(this ApplicationUser? user)
    {
        if (user is null) return null;

        return new UserDto(user.Id, user.UserName!, user.Email!, user.Gil, user.CreatedOn);
    }

    private static void AddIdentityServer(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var identityServerSettings = configuration.GetSection(nameof(IdentityServerSettings)).Get<IdentityServerSettings>();
        var identitySettings = configuration.GetSection(nameof(IdentitySettings)).Get<IdentitySettings>();
        var serviceSettings = configuration.GetSection(nameof(ServiceSettings)).Get<ServiceSettings>();

        var builder = services.AddIdentityServer(options =>
        {
            // three properties will be helpful when you don't know why IdentityServer does not work properly
            // we add three configurations below to see verbose message in the console log and that can help a lot
            options.Events.RaiseSuccessEvents = true;
            options.Events.RaiseFailureEvents = true;
            options.Events.RaiseErrorEvents = true;

            //This is required in the Docker environment since the strict Linus permissions you'll set there won't allow IdentityServer to crea keys in
            //the default /keys directory
            options.KeyManagement.KeyPath = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location);

            if (serviceSettings!.IsKubernetesLocal.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                options.IssuerUri = identitySettings!.IdentityIssuerUri;
            }

        })
        .AddAspNetIdentity<ApplicationUser>()
        .AddInMemoryApiScopes(identityServerSettings!.ApiScopes)
        .AddInMemoryApiResources(identityServerSettings.ApiResources)
        .AddInMemoryClients(identityServerSettings.Clients)
        .AddInMemoryIdentityResources(identityServerSettings.IdentityResources);


        if (!serviceSettings!.IsKubernetesLocal.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            /*
            * this is just use for HTTPS
            * After configuring TLS certificate, this code base uses for singing key authen/author in production
            */
            if (!environment.IsDevelopment())
            {
                /*
                * read the values of the certificate files in Kubernetes secret
                * CreateFromPemFile(), remember that this file is being mounted into a directory in the microservice file system. It is being mounted by
                * Kubernetes. So we want to read it from there.
                */
                var cer = X509Certificate2.CreateFromPemFile(
                    identitySettings!.CertificateCerFilePath,
                    identitySettings.CertificateKeyFilePath
                );
                builder.AddSigningCredential(cer);
            }
        }
    }
}