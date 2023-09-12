# Play.Identity.Contracts
Common library used by Play Economy services.

## Create and publish package
```powershell
$version="1.0.1"
$owner="samphamdotnetmicroservices02"
$gh_pat="[PAT HERE]"

dotnet pack src\Play.Identity.Contracts\ --configuration Release -p:PackageVersion=$version -p:RepositoryUrl=https://github.com/$owner/Play.Identity -o ../packages

dotnet nuget push ..\packages\Play.Identity.Contracts.$version.nupkg --api-key $gh_pat --source "github"

--source "github" comes from Play.Infra
```

```mac
version="1.0.1"
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
docker build --secret id=GH_OWNER --secret id=GH_PAT -t play.identity:$version .

-t is tack, tack is really a human friendly way to identify your Docker image, in this case, in your 
box. And it is composed of two parts. The first part is going to be kind of the name of image, and
the second part is the version that you want to assign to it.
the "." next to $version is the cecil file , the context for this docker build command, which in this case
is just going to be ".", this "." represents the current directory
```

```mac
export GH_OWNER="samphamdotnetmicroservices02" 
export GH_PAT="[PAT HERE]"
docker build --secret id=GH_OWNER --secret id=GH_PAT -t play.identity:$version .
check this link for more details about env variable on mac
https://phoenixnap.com/kb/set-environment-variable-mac
```

```
verify your image
docker images
```