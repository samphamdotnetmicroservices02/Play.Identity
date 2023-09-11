using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Play.Identity.Service.Entities;
using Play.Identity.Service.Settings;

namespace Play.Identity.Service.HostedServices;

/*
* The next thing that we have to do is to define the logic to create both these two roles, and our admin user in our identity
* microservice database, on boot, right? On the first time that the server boots, we want to create all these things so that
* they are available from there from there on. And so how can we do that? Probably the best way we can do this in initialization 
* is via a hosted service. A hosted service is just a class that you can register on startup and that can execute asynchronous 
* background task logic when the service host is starting.
*/
public class IdentitySeedHostedService : IHostedService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IdentitySettings _settings;

    public IdentitySeedHostedService(IServiceScopeFactory serviceScopeFactory, IOptions<IdentitySettings> settings)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _settings = settings.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await CreateRoleIfNotExistsAsync(Roles.Admin, roleManager);
        await CreateRoleIfNotExistsAsync(Roles.Player, roleManager);

        var adminUser = await userManager.FindByEmailAsync(_settings.AdminUserEmail);

        if (adminUser is null)
        {
            adminUser = new ApplicationUser
            {
                UserName = _settings.AdminUserEmail,
                Email = _settings.AdminUserEmail,
            };

            await userManager.CreateAsync(adminUser, _settings.AdminUserPassword);
            await userManager.AddToRoleAsync(adminUser, Roles.Admin);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task CreateRoleIfNotExistsAsync(string role, RoleManager<ApplicationRole> roleManager)
    {
        var roleExists = await roleManager.RoleExistsAsync(role);

        if (!roleExists)
        {
            await roleManager.CreateAsync(new ApplicationRole { Name = role });
        }
    }
}