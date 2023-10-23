namespace Play.Identity.Service.Settings;

public class IdentitySettings
{
    public required string AdminUserEmail { get; init; }
    public required string AdminUserPassword { get; init; }
    public decimal StartingGil { get; init; }

    /*
    * This comes from Play.Infra/emissary-ingress/mapping.yaml (prefix /identity-svc), we also configure this here to help service can run behind
    * the Api gateway. The service needs to know about path base from Play.Infra/emissary-ingress/mapping.yaml (prefix /identity-svc) because 
    * otherwise all the URLs that it will define for all OAuth and OpenId Connect purposes are not going to be correct. They will missing this 
    * path and that's going to cause a lot of trouble.
    */
    public required string PathBase { get; init; }

    //After configuring TLS certificate
    public required string CertificateCerFilePath { get; init; }
    public required string CertificateKeyFilePath { get; init; }

    public string IdentityIssuerUri { get; init; } = string.Empty;
}