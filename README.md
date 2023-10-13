# Play.Identity.Contracts
Common library used by Play Economy services.

## Clear your link of package on local machine
dotnet nuget list source (check your name of nuget on your local)
dotnet nuget remove source [Name of your link of package you want to remove]

## Create and publish package
```powershell
$version="1.0.9"
$owner="samphamdotnetmicroservices02"
$gh_pat="[PAT HERE]"

dotnet pack src\Play.Identity.Contracts\ --configuration Release -p:PackageVersion=$version -p:RepositoryUrl=https://github.com/$owner/Play.Identity -o ../packages

dotnet nuget push ..\packages\Play.Identity.Contracts.$version.nupkg --api-key $gh_pat --source "github"

--source "github" comes from Play.Infra
```

```mac
version="1.0.9"
owner="samphamdotnetmicroservices02"
gh_pat="[PAT HERE]"

dotnet pack src/Play.Identity.Contracts/ --configuration Release -p:PackageVersion=$version -p:RepositoryUrl=https://github.com/$owner/Play.Identity -o ../packages

dotnet nuget push ../packages/Play.Identity.Contracts.$version.nupkg --api-key $gh_pat --source "github"

--source "github" comes from Play.Infra
```

## Build the Docker image
```powershell
$env:GH_OWNER="samphamdotnetmicroservices02"
$env:GH_PAT="[PAT HERE]"
$acrName="samphamplayeconomyacr"

docker build --secret id=GH_OWNER --secret id=GH_PAT -t play.identity:$version .

or

docker build --secret id=GH_OWNER --secret id=GH_PAT -t "$acrName.azurecr.io/play.identity:$version" .

-t is tag, tag is really a human friendly way to identify your Docker image, in this case, in your 
box. And it is composed of two parts. The first part is going to be kind of the name of image, and
the second part is the version that you want to assign to it.
the "." next to $version is the cecil file , the context for this docker build command, which in this case
is just going to be ".", this "." represents the current directory
```

```mac
export GH_OWNER="samphamdotnetmicroservices02"
export GH_PAT="[PAT HERE]"
acrName="samphamplayeconomyacr"

docker build --secret id=GH_OWNER --secret id=GH_PAT -t play.identity:$version .

or

docker build --secret id=GH_OWNER --secret id=GH_PAT -t "$acrName.azurecr.io/play.identity:$version" .

check this link for more details about env variable on mac
https://phoenixnap.com/kb/set-environment-variable-mac
```

```
verify your image
docker images
```

## Run the docker image
```powershell
$adminPass="[PASSWORD HERE]"
$cosmosDbConnString="[CONN STRING HERE]"
$serviceBusConnString="[CONN STRING HERE]"

docker run -it --rm -p 5002:5002 --name identity -e MongoDbSettings__Host=mongo -e RabbitMqSettings__Host=rabbitmq --network playinfra_default play.identity:$version

if you do not use MongoDb and RabbitMQ from Play.Infra, you can remove --network playinfra_default

docker run -it --rm -p 5002:5002 --name identity -e MongoDbSettings__ConnectionString=$cosmosDbConnString -e ServiceBusSetting__ConnectionString=$serviceBusConnString -e ServiceSettings__MessageBroker="SERVICEBUS" -e IdentitySettings__AdminUserPassword=$adminPass play.identity:$version

-it: what it does is it creates and kind of an interactive shell, so that you will not able to go back to your
command line until you cancel the execution of these docker run command.

--rm: means that the docker container that is going to be created has to be destroyed as soon as you exit
the execution of these docker run command. That is just to keep things clean in your box.

-p: -p 5002:5002 it means 5002 on the right side is the port of docker container, the left side is port of your
local machine

--name: this is optional

-e: this is environment variable. MongoDbSettings__: MongoDbSettings comes from appsettings.json, the double underscore "__" allows you to specify envinroment variables that will end up in configuration with the same shape that we have, this is the file in the appsettings.json. "MongoDbSettings__Host" Host represents the property in MongoDbSettings in appsettings.json, this will override whatever configuration you specified in appsettings.json
"MongoDbSettings__Host=mongo" mongo is the container_name that we name it in Play.Infra

--network playinfra_default: run docker network ls to check your playinfra network. "playinfra_default" comes from
this network. This is a network that has been created by docker compose for everything that we declared in this
docker compose file. So all the containers running in or declared in docker compose (Play.Infra) are running 
within a single network, docker network. And that is how they can reach out to each other easily. However our
microservice is going to be running externally to this. They are not going to be running within these docker
compose file.
So how can they reach out to containers are running in a different network? So we have to find a network for
the docker run command. So we have to add parameter that specifies that we want to connect to the same network
where all the other containers are running "playinfra_default (RabbitMq and Mongo)"

And lawtly we have to specify the docker image that we want to run (play.identity:$version)
```

```mac
adminPass="[PASSWORD HERE]"
cosmosDbConnString="[CONN STRING HERE]"
serviceBusConnString="[CONN STRING HERE]"

docker run -it --rm -p 5002:5002 --name identity -e MongoDbSettings__Host=mongo -e RabbitMqSettings__Host=rabbitmq --network playinfra_default play.identity:$version

if you do not use MongoDb and RabbitMQ from Play.Infra, you can remove --network playinfra_default

docker run -it --rm -p 5002:5002 --name identity -e MongoDbSettings__ConnectionString=$cosmosDbConnString -e ServiceBusSetting__ConnectionString=$serviceBusConnString -e ServiceSettings__MessageBroker="SERVICEBUS" -e IdentitySettings__AdminUserPassword=$adminPass play.identity:$version
```

## Publish the image
```powershell
$acrName="samphamplayeconomyacr"

az acr login --name $acrName

docker tag play.identity:$version "$acrName.azurecr.io/play.identity:$version"

docker images (check your images for ACR)

docker push "$acrName.azurecr.io/play.identity:$version" (go to your ACR -> Repositories to check your images)


az acr login: in order to be able to publish anything into ACR, you will have to first log in into it. Because remember that an ACR is a private repository.
So people cannot just connect to it from anywhere without providing credentials. It is not a repository like it will be the case in Docker Hub.
This is private, so you need credentials to be able to access it. So to do that, what you can do is use the AZ ACR login command from Azure CLI

docker tag play.identity:$version: The next thing we want to do is a retagging of your image, so that it is ready to be published to ACR. In order to be
able to publish these to ACR, you have to have the name of the repository of your image, has to match a combination of the login server of your ACR
and the accurate repository name (samphamplayeconomyacr.azurecr.io/play.identity:$version, samphamplayeconomyacr.azurecr.io comes from your login server of ACR)

docker push: publishing image
```

```mac
acrName="samphamplayeconomyacr"

az acr login --name $acrName

docker tag play.identity:$version "$acrName.azurecr.io/play.identity:$version"

docker images (check your images for ACR)

docker push "$acrName.azurecr.io/play.identity:$version" (go to your ACR -> Repositories to check your images)
```

## Creating the Kubernetes namespace
```powershell
$namespace="identity"
kubectl create namespace $namespace

namespace: the namespace is nothing more than a way to separate the resources that belong to different applications in your Kubernetes cluster. So usallly 
you will have one namespace paired microservice in this case, we will put all the resources that belong to that specific microservice.
```

```mac
namespace="identity"
kubectl create namespace $namespace
```

## Creating the Kubernetes secrets
```powershell
kubectl create secret generic identity-secrets --from-literal=cosmosdb-connectionstring=$cosmosDbConnString --from-literal=servicebus-connectionstring=$serviceBusConnString --from-literal=admin-password=$adminPass -n $namespace

kubectl get secrets -n $namespace

kubectl delete secret identity-secrets -n $namespace (If you use Azure Key Vault, you do not need Kubernetes secrets, so delete it)

generic: there are different types of secrets that you can create in coordinators. In our case, we will be creating what we call a generic secret.
identity-secrets: This is the name of secret object. You can name it any name you want.

--from-literal=cosmosdb-connectionstring=$cosmosDbConnString: since we want to provide the secret values from command line, what we will do is say
--from-literal, and then euquals and here is where the actual name of the secret value comes in place. So we will name the secret value "cosmosdb-connectionstring"
and the actual value for the connection string is going to come from the variable "$cosmosDbConnString" that we defined before

-n: What is the namespace that we want to create secrets
```

## Creating the Kubernetes pod
```powershell
kubectl apply -f ./kubernetes/identity.yaml -n $namespace

kubectl get deploy (get your deployment name)
kubectl delete deploy identity-deployment -n $namespace (delete deployment if sth went wrong.)

kubectl get pods -n $namespace
kubectl get pods -n $namespace -w
READY 1/1: means that we have one container inside the pod, and that one pod is ready
AGE: is the time your pod run from the past to the current time
-w: listen to it until a new version deployment is alive.

kubectl get services -n $namespace
TYPE: ClusterIP is the default type, which is ClusterIP meaning that it gets an IP that is local to the cluster
(CLUSTER-IP), so only any other ports within the cluster can reachout these microservice right now. And it is
listening in port 80. External-IP needs to define "spec.type: LoadBalancer" in yaml file


kubectl logs identity-deployment-5767558688-p9zh2 -n $namespace
identity-deployment-5767558688-p9zh2: is the name when you run "kubectl get pods -n $namespace"

kubectl logs ...: You want to know what is going on with that pod, what is happening inside that pod

kubectl describe pod identity-deployment-5767558688-p9zh2 -n $namespace

describe pod: this will give you even more insights into definition of the pod.
```

## Measuring specs like CPU, RAM
```
kubectl top pods

It will tell you how much CPU and RAM your containers are using in Kubernetes.

kubectl events -n $namespace
// check events, when deployment is successful, it check a new version service and then kill old pod
```

## Creating the Azure Managed Identity and grating it access to Key Vault secrets
from https://learn.microsoft.com/en-gb/azure/aks/workload-identity-deploy-cluster

```powershell
$appname="playeconomy"
$keyVaultName="samphamplayeconomykv"

az identity create --resource-group $appname --name $namespace

$IDENTITY_CLIENT_ID=az identity show -g $appname -n $namespace --query clientId -otsv

az keyvault set-policy -n $keyVaultName --secret-permissions get list --spn $IDENTITY_CLIENT_ID

if you receive an error like "AADSTS530003: Your device is required to be managed to access this resource."
after running "az keyvault set-policy ...", try to run it on Cloud

check your Azure Managed Identity by navigate to resource group -> $keyVaultName -> AccessPolicies to see the $namespace have permission Get, List. And you can see
the $namespace in resource group -> $namespace

az identity create: create one of Azure managed identities
--name: the name of the managed identity.

after run "az identity create ...", we have the "clientId" from the response. What we want to do is to retrieve what is known as the identity clientId,
which we are gonna be using to assign permissions into our key vault.

az identity show: is a command to retrieve details about a managed identity that we have already created. 

-n from "az identity show -g $appname -n $namespace" is the name of identity

--query clientId: we want to say that we do not want to just query all the details about this identity, we want to the specific property of that identity. So we
will say query and retrive the clientId

-otsv: we want the "--query clientId" in a format otsv that is easy to parse for other commands.

az keyvault set-policy: after run "$IDENTITY_CLIENT_ID=az identity ...", use the clientId to grant access to our key vault secrets or to our Azure key vault
-n: the name of key vault
--secret-persmissions get list --spn $IDENTITY_CLIENT_ID: "--secret-persmissions" states that we are going to be grarting permissions into our key vault secrets. 
It could be cetificates, it could be keys or it could be secrets. In this case it is going to be just secrets. And the permission we want to grant is "get list".
"--spn $IDENTITY_CLIENT_ID" And then the identity or the service principle that we want to grant these permissions into, is going to be our identity clientId
```

```mac
appname="playeconomy"
keyVaultName="samphamplayeconomykv"

az identity create --resource-group $appname --name $namespace

export IDENTITY_CLIENT_ID="$(az identity show -g $appname -n $namespace --query clientId -otsv)"

az keyvault set-policy -n $keyVaultName --secret-permissions get list --spn $IDENTITY_CLIENT_ID
```

## Establish the federated identity credential
```powershell
$aksName="samphamplayeconomyaks"

$AKS_OIDC_ISSUER=az aks show -n $aksName -g $appname --query "oidcIssuerProfile.issuerUrl" -otsv

az identity federated-credential create --name $namespace --identity-name $namespace --resource-group $appname --issuer $AKS_OIDC_ISSUER --subject "system:serviceaccount:${namespace}:${namespace}-serviceaccount" --audience api://AzureADTokenExchange

check your result: navigate your resource group $namespace, in this case is identity (Managed Identity) -> Federated credentials tab

retrieve this oidcIssuerProfile.issuerUrl: the only reason why we are able to query this is because when we created the cluster, if you remember we asked it
to enable these OIDC issuer at cluster creation time. So you have to do it that way, otherwise it will not work.

az identity federated-credential... --name: the name of our managed identity. So that name in our case is namespace which in my case is "identity". Because it is
the identity microservice. This is the name of the federated credential

az identity federated-credential... --identity-name: the name of managed identity, the identity name. So for that, we are going to be putting again, namespace.
This is the name of the managed identity we already created before

az identity federated-credential... --subject: your service account that you just created. 
"system:serviceaccount:${namespace}:${namespace}-serviceaccount", the first $namespace, general case is just identity, and the second $namespace is the actual name of the service account which lives in kubernetes/identity.yaml and the ${namespace}-serviceaccount lives in $namespace (identity)
```

```mac
aksName="samphamplayeconomyaks"
export AKS_OIDC_ISSUER="$(az aks show -n $aksName -g "${appname}" --query "oidcIssuerProfile.issuerUrl" -otsv)"

az identity federated-credential create --name $namespace --identity-name $namespace --resource-group $appname --issuer $AKS_OIDC_ISSUER --subject "system:serviceaccount:${namespace}:${namespace}-serviceaccount" --audience api://AzureADTokenExchange
```

## Creating the signing certificate
```powershell
kubectl apply -f ./kubernetes/signing-cer.yaml -n $namespace

kubectl get secret signing-cert -n $namespace -o yaml (get secret from command above, "signing-cert" is the name you defined)
```

"kubectrl get secret ...": after run this command, it includes data:tls.crt and data:tls.key. So there is a combination of CRT and key files that you can
use to actually use the certificate for signing purposes.

## Deploy Kubernetes using Helm chart
Because we deploy our service to Kubernetes using kubectl, So the first thing is to delete one by one

```
kubectl delete deployment identity-deployment -n $namespace
```
