FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props ./
COPY src/Core/CommerceCore.Domain/CommerceCore.Domain.csproj src/Core/CommerceCore.Domain/
COPY src/Core/CommerceCore.Application/CommerceCore.Application.csproj src/Core/CommerceCore.Application/
COPY src/Platform/CommerceCore.Platform.Contracts/CommerceCore.Platform.Contracts.csproj src/Platform/CommerceCore.Platform.Contracts/
COPY src/Platform/CommerceCore.Platform.ControlPlane/CommerceCore.Platform.ControlPlane.csproj src/Platform/CommerceCore.Platform.ControlPlane/
COPY src/Modules/Catalog/CommerceCore.Modules.Catalog.Domain/CommerceCore.Modules.Catalog.Domain.csproj src/Modules/Catalog/CommerceCore.Modules.Catalog.Domain/
COPY src/Modules/Catalog/CommerceCore.Modules.Catalog.Application/CommerceCore.Modules.Catalog.Application.csproj src/Modules/Catalog/CommerceCore.Modules.Catalog.Application/
COPY src/Infrastructure/CommerceCore.Persistence/CommerceCore.Persistence.csproj src/Infrastructure/CommerceCore.Persistence/
COPY src/Workers/CommerceCore.Outbox.Worker/CommerceCore.Outbox.Worker.csproj src/Workers/CommerceCore.Outbox.Worker/

RUN dotnet restore src/Workers/CommerceCore.Outbox.Worker/CommerceCore.Outbox.Worker.csproj
COPY .editorconfig ./
COPY src/ src/
RUN dotnet publish src/Workers/CommerceCore.Outbox.Worker/CommerceCore.Outbox.Worker.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "CommerceCore.Outbox.Worker.dll"]
