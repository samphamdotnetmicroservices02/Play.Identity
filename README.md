# Play.Identity.Contracts
Common library used by Play Economy services.

## Clear your link of package on local machine
dotnet nuget list source (check your name of nuget on your local)
dotnet nuget remove source [Name of your link of package you want to remove]

## Create and publish package
```powershell
$version="1.0.4"
$owner="samphamdotnetmicroservices02"
$gh_pat="[PAT HERE]"

dotnet pack src\Play.Identity.Contracts\ --configuration Release -p:PackageVersion=$version -p:RepositoryUrl=https://github.com/$owner/Play.Identity -o ../packages

dotnet nuget push ..\packages\Play.Identity.Contracts.$version.nupkg --api-key $gh_pat --source "github"

--source "github" comes from Play.Infra
```

```mac
version="1.0.4"
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

generic: there are different types of secrets that you can create in coordinators. In our case, we will be creating what we call a generic secret.
identity-secrets: This is the name of secret object 

--from-literal=cosmosdb-connectionstring=$cosmosDbConnString: since we want to provide the secret values from command line, what we will do is say
--from-literal, and then euquals and here is where the actual name of the secret value comes in place. So we will name the secret value "cosmosdb-connectionstring"
and the actual value for the connection string is going to come from the variable "$cosmosDbConnString" that we defined before

-n: What is the namespace that we want to create secrets
```

## Creating the Kubernetes pod
```powershell
kubectl apply -f ./kubernetes/identity.yaml -n $namespace

kubectl get pods -n $namespace
READY 1/1: means that we have one container inside the pod, and that one pod is ready

kubectl get services -n $namespace
TYPE: ClusterIP is the default type, which is ClusterIP meaning that it gets an IP that is local to the cluster
(CLISTER-IP), so only any other ports within the cluster can reachout these microservice right now. And it is
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