using Duende.IdentityServer.Models;

namespace Play.Identity.Service.Settings;

public class IdentityServerSettings
{
    /*
    * ApiScopes that' gonna represent the different levels of access that we are going to be able to grant to 
    *  any of clients that want to make use of our microservice.
    */
    public required IReadOnlyCollection<ApiScope> ApiScopes { get; init; }

    /*
    * this Client is going to be authorized to access microservices
    */
    public required IReadOnlyCollection<Client> Clients { get; init; }

    //define scopes for each audience
    public required IReadOnlyCollection<ApiResource> ApiResources { get; init; }

    //define which scope the Identity server can provide
    public IReadOnlyCollection<IdentityResource> IdentityResources =>
        new IdentityResource[]
        {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
            new IdentityResource("roles", new[] {"role"}) // include roles for scope
        };
}