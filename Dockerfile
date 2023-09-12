# mcr.microsoft.com/dotnet/aspnet:7.0 AS base: this line is actually tealling us what is going to be our base image to go from
# to execute microservice. That based image, which is in this case project by Microsoft and stored at this mcr.microsoft.com
# location has everything that you need to run any aspnet core application based on aspnet core 7
# By just using this line here, we are making sure that the entire envionment for our microservice is already set correctly
# for an aspnet core 7 application
# when you build image, this is the one line that's going to take more time, because it has to download all the layers corresponding
# to that from mcr.microsoft.com into your box
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 5002

ENV ASPNETCORE_URLS=http://+:5002

# Creates a non-root user with an explicit UID and adds permission to access the /app folder
# For more info, please refer to https://aka.ms/vscode-docker-dotnet-configure-containers
RUN adduser -u 5678 --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

# this stage is for building the image
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
ARG configuration=Release
COPY ["src/Play.Identity.Contracts/Play.Identity.Contracts.csproj", "src/Play.Identity.Contracts/"]
# The instruction of the left side "src/Play.Identity.Service/Play.Identity.Service.csproj" represents what you have in your
# local machine (structure of project). And the argument on the right side "src/Play.Identity.Service/" represents what's 
# going to be available in the docker image, which is a completely different environment.
# So we have to copy the project file because we need to build it on that.
COPY ["src/Play.Identity.Service/Play.Identity.Service.csproj", "src/Play.Identity.Service/"]

RUN dotnet restore "src/Play.Identity.Service/Play.Identity.Service.csproj"

# Copy all the other files. We do this because like I said, each of these lines represent layers. So one layer is going to be
# restore process (RUN dotnet restore "src/Play.Identity.Service/Play.Identity.Service.csproj"), and another layer is going
# to be the copy of all the files (COPY . .)
# And this, separating these two things (RUN dotnet restore ... and COPY . .) helps us in such a way that if there are no new,
# changes into the project file itself, the next time you build this image, it's going to totally just skip this instruction (RUN dotnet restore ...)
# becuase there has nothing, any changes, into the file. It will just move on into the copying phase (COPY . .). If you did 
# not do that and just went ahead and do the build as we have later on (RUN dotnet build "Play.Identity.Service.csproj" -c $configuration -o /app/build)
# it will not be able to distinguish between the restore section, the copying and the build later (RUN dotnet build ...). It 
# will not let you cache thos layers properly. So it's good to have this separated.
# Here we're saying we copy from dot to dot (COPY . .), what does that mean? The first dot represent your local 
# machine (your structure project), the second ot represents the root of your docker image. But we don't want to copy
# from root to root, we take care of inside in src/, we don't care of out side of /src. So we have to make sure that in this
# copy instruction, we are going to say copy from ./src to ./src. So that way, we end up with an src directory inside the docker
# image in this build stage
COPY ./src ./src

WORKDIR "/src/Play.Identity.Service"
# we don't need to build, we can publish directly
# RUN dotnet build "Play.Identity.Service.csproj" -c $configuration -o /app/build

# this "build" word is the same as "FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build" the second stage, and the rename
# this stage as publish
# FROM build AS publish
ARG configuration=Release
RUN dotnet publish "Play.Identity.Service.csproj" -c $configuration  --no-restore -o /app/publish /p:UseAppHost=false

# this comes from base, base is the first stage (FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base). So that means,
# it starts from wherevever we left off over the first stage and move on. We want to start from the first stage 
# because we don't want to include anything related to the dotnet sdk (the second stage). we only want the runtime
# files to execute an exponent core application.
FROM base AS final
WORKDIR /app
# we copy everything from the build stage (FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build) under
# /app/publish from (RUN dotnet publish ... -o /app/publish) into our currencty directory (. next to /app/publish)
# and the current directory (our image) is /app (WORKDIR /app)
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Play.Identity.Service.dll"]
