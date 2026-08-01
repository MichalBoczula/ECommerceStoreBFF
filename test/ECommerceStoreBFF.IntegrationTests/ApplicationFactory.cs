using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using ECommerceStoreBFF.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.MongoDb;
using Testcontainers.MsSql;

namespace ECommerceStoreBFF.AcceptanceTests;

public class ApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string SqlPassword = "YourStrong@Password123!";

    private readonly MsSqlContainer _msSqlContainer;
    private readonly MongoDbContainer _mongoContainer;
    private readonly INetwork _network;

    private IContainer? _productCatalogApiContainer;
    private IContainer? _usersApiContainer;
    private IContainer? _bffContainer;

    public ApplicationFactory()
    {
        _network = new NetworkBuilder().Build();

        _msSqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithNetwork(_network)
            .WithNetworkAliases("product-db")
            .WithPassword(SqlPassword)
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithEnvironment("SQLCMDUSER", "sa")
            .WithEnvironment("SQLCMDPASSWORD", SqlPassword)
            .WithEnvironment("MSSQL_SA_PASSWORD", SqlPassword)
            .Build();

        _mongoContainer = new MongoDbBuilder("mongo:8.0")
            .WithNetwork(_network)
            .WithNetworkAliases("compose-mongodb")
            .WithUsername("admin")
            .WithPassword("admin123")
            .WithReplicaSet("rs0")
            .Build();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["GatewaySettings:BaseUrl"] = "http://localhost",
                ["ReverseProxy:Clusters:products-cluster:Destinations:destination1:Address"] = "http://localhost:5000",

                ["ReverseProxy:Clusters:users-cluster:Destinations:destination1:Address"] = "http://localhost:6500"
            };

            config.AddInMemoryCollection(overrides);
        });

        builder.ConfigureServices((context, services) =>
        {
            services.AddInfrastructureServices(context.Configuration);
        });
    }

    public async Task InitializeAsync()
    {
        await _network.CreateAsync();
        await Task.WhenAll(_msSqlContainer.StartAsync(), _mongoContainer.StartAsync());

        _productCatalogApiContainer = new ContainerBuilder("productcatalogapi:latest")
            .WithNetwork(_network)
            .WithNetworkAliases("product-api")
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
            .WithEnvironment("ASPNETCORE_URLS", "http://+:8080")
            .WithEnvironment("ConnectionStrings__ProductCatalogDb",
                "Server=product-db;Database=ProductsDb;User Id=sa;Password=YourStrong@Password123!;TrustServerCertificate=True")
            .WithPortBinding(5000, 8080)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8080).ForPath("/health")))
            .Build();

        _usersApiContainer = new ContainerBuilder("ecommercestoreusersapi:latest")
             .WithNetwork(_network)
             .WithNetworkAliases("users-api")
             .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
             .WithEnvironment("ASPNETCORE_URLS", "http://+:8080")
             .WithEnvironment("MongoDbSettings__ConnectionString", "mongodb://admin:admin123@compose-mongodb:27017/?authSource=admin&directConnection=true")
             .WithEnvironment("MongoDbSettings__DatabaseName", "ecommerce-store-users-db-test")
             .WithEnvironment("MongoDbSettings__CustomerCollectionName", "customers")
             .WithEnvironment("MongoDbSettings__CustomersHistoryCollectionName", "customers-history")
             .WithEnvironment("MongoDbSettings__AdminCollectionName", "admins")
             .WithEnvironment("MongoDbSettings__AdminsHistoryCollectionName", "admins-history")
             .WithPortBinding(6500, 8080)
             .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8080).ForPath("/health")))
             .Build();

        await Task.WhenAll(_productCatalogApiContainer.StartAsync(), _usersApiContainer.StartAsync());

        _bffContainer = new ContainerBuilder("ecommercestorebffapi:latest")
            .WithNetwork(_network)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
            .WithEnvironment("ASPNETCORE_URLS", "http://+:8080")
            .WithEnvironment("ReverseProxy__Clusters__products-cluster__Destinations__destination1__Address", "http://product-api:8080")
            .WithEnvironment("ReverseProxy__Clusters__users-cluster__Destinations__destination1__Address", "http://users-api:8080")
            .WithEnvironment("GatewaySettings__BaseUrl", "http://localhost:3000")

            .WithPortBinding(3000, 8080)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8080).ForPath("/health")))
            .Build();

        await _bffContainer.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        if (_bffContainer is not null) await _bffContainer.DisposeAsync();
        if (_usersApiContainer is not null) await _usersApiContainer.DisposeAsync();
        if (_productCatalogApiContainer is not null) await _productCatalogApiContainer.DisposeAsync();

        await _mongoContainer.DisposeAsync();
        await _msSqlContainer.DisposeAsync();
        await _network.DeleteAsync();
    }
}