FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Core/CommerceCore.Domain/CommerceCore.Domain.csproj src/Core/CommerceCore.Domain/
COPY src/Core/CommerceCore.Application/CommerceCore.Application.csproj src/Core/CommerceCore.Application/
COPY src/Infrastructure/CommerceCore.Infrastructure/CommerceCore.Infrastructure.csproj src/Infrastructure/CommerceCore.Infrastructure/
COPY src/Infrastructure/CommerceCore.Persistence/CommerceCore.Persistence.csproj src/Infrastructure/CommerceCore.Persistence/
COPY src/Presentation/CommerceCore.Api/CommerceCore.Api.csproj src/Presentation/CommerceCore.Api/

RUN dotnet restore src/Presentation/CommerceCore.Api/CommerceCore.Api.csproj

COPY . .
RUN dotnet publish src/Presentation/CommerceCore.Api/CommerceCore.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER app
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet CommerceCore.Api.dll --urls http://0.0.0.0:${PORT:-8080}"]
