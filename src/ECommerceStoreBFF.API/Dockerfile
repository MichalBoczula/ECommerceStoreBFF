FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
# Apply published Ubuntu OpenSSL security fixes before returning to the non-root runtime user.
RUN apt-get update \
    && apt-get install --only-upgrade --no-install-recommends -y libssl3t64 openssl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID

FROM mcr.microsoft.com/dotnet/sdk:10.0.100 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["global.json", "Directory.Build.props", "./"]
COPY ["src/ECommerceStoreBFF.API/ECommerceStoreBFF.API.csproj", "src/ECommerceStoreBFF.API/"]
COPY ["src/ECommerceStoreBFF.Application/ECommerceStoreBFF.Application.csproj", "src/ECommerceStoreBFF.Application/"]
COPY ["src/ECommerceStoreBFF.Infrastructure/ECommerceStoreBFF.Infrastructure.csproj", "src/ECommerceStoreBFF.Infrastructure/"]

RUN dotnet restore "src/ECommerceStoreBFF.API/ECommerceStoreBFF.API.csproj"

COPY . .
RUN dotnet publish "src/ECommerceStoreBFF.API/ECommerceStoreBFF.API.csproj" \
    -c "$BUILD_CONFIGURATION" -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ECommerceStoreBFF.API.dll"]
