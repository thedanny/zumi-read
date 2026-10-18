FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG SDK_VERSION=10.0.112
ARG ASPNET_VERSION=10.0.12
RUN curl -fsSL "https://builds.dotnet.microsoft.com/dotnet/Sdk/${SDK_VERSION}/dotnet-sdk-${SDK_VERSION}-linux-x64.tar.gz" -o /tmp/dotnet-sdk.tgz \
    && tar -xzf /tmp/dotnet-sdk.tgz -C /usr/share/dotnet \
    && rm /tmp/dotnet-sdk.tgz \
    && curl -fsSL "https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/${ASPNET_VERSION}/aspnetcore-runtime-${ASPNET_VERSION}-linux-x64.tar.gz" -o /tmp/dotnet-runtime.tgz
WORKDIR /src
COPY StorySprout.Api.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish -c Release -o /app --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:8.0
COPY --from=build /tmp/dotnet-runtime.tgz /tmp/dotnet-runtime.tgz
RUN tar -xzf /tmp/dotnet-runtime.tgz -C /usr/share/dotnet \
    && rm /tmp/dotnet-runtime.tgz
WORKDIR /app
COPY --from=build /app ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "StorySprout.Api.dll"]
