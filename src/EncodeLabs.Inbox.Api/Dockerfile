FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY EncodeLabs.Inbox.sln ./
COPY src/EncodeLabs.Inbox.Domain/EncodeLabs.Inbox.Domain.csproj src/EncodeLabs.Inbox.Domain/
COPY src/EncodeLabs.Inbox.Application/EncodeLabs.Inbox.Application.csproj src/EncodeLabs.Inbox.Application/
COPY src/EncodeLabs.Inbox.Infrastructure/EncodeLabs.Inbox.Infrastructure.csproj src/EncodeLabs.Inbox.Infrastructure/
COPY src/EncodeLabs.Inbox.Api/EncodeLabs.Inbox.Api.csproj src/EncodeLabs.Inbox.Api/
RUN dotnet restore src/EncodeLabs.Inbox.Api/EncodeLabs.Inbox.Api.csproj

COPY src/ src/
RUN dotnet publish src/EncodeLabs.Inbox.Api/EncodeLabs.Inbox.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EncodeLabs.Inbox.Api.dll"]
