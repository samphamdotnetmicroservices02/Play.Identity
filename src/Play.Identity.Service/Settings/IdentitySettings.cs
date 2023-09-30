namespace Play.Identity.Service.Settings;

public class IdentitySettings
{
    public string AdminUserEmail { get; init; }
    public string AdminUserPassword { get; init; }
    public decimal StartingGil { get; init; }

    /*
    * This comes from Play.Infra/emissary-ingress/mapping.yaml (prefix /identity-svc), we also configure this here to help service can run behind
    * the Api gateway. The service needs to know about path base from Play.Infra/emissary-ingress/mapping.yaml (prefix /identity-svc) because 
    * otherwise all the URLs that it will define for all OAuth and OpenId Connect purposes are not going to be correct. They will missing this 
    * path and that's going to cause a lot of trouble.
    */
    public string PathBase { get; init; }

    //After configuring TLS certificate
    public string CertificateCerFilePath { get; init; }
    public string CertificateKeyFilePath { get; init; }

    public string IsKubernetesLocal { get; init; } // in local kubernetes, we don't use HTTPS
}