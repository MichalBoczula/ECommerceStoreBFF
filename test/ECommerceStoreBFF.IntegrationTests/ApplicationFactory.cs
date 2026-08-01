using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using Testcontainers.MongoDb;
using Testcontainers.MsSql;
using ECommerceStoreBFF.Infrastructure;
using Microsoft.Data.SqlClient;

namespace ECommerceStoreBFF.AcceptanceTests;

public class ApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer;
    private readonly MongoDbContainer _mongoContainer;
    private IContainer? _productCatalogApiContainer;
    private IContainer? _usersApiContainer;
    private IContainer? _invoiceApiContainer;
    private readonly INetwork _network;

    public ApplicationFactory()
    {
        _network = new NetworkBuilder().Build();

        _msSqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithNetwork(_network)
            .WithNetworkAliases("product-db")
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithEnvironment("MSSQL_SA_PASSWORD", "YourStrong@Password123!")
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
            var productApiUrl = $"http://{_productCatalogApiContainer!.Hostname}:{_productCatalogApiContainer.GetMappedPublicPort(8080)}";
            var usersApiUrl = $"http://{_usersApiContainer!.Hostname}:{_usersApiContainer.GetMappedPublicPort(8080)}";
            var ordersApiUrl = $"http://{_invoiceApiContainer!.Hostname}:{_invoiceApiContainer.GetMappedPublicPort(8080)}";

            var overrides = new Dictionary<string, string?>
            {
                ["ReverseProxy:Clusters:products-cluster:Destinations:destination1:Address"] = productApiUrl,
                ["ReverseProxy:Clusters:users-cluster:Destinations:destination1:Address"] = usersApiUrl,
                ["ReverseProxy:Clusters:orders-cluster:Destinations:destination1:Address"] = ordersApiUrl,

                ["GatewaySettings:BaseUrl"] = "http://localhost:3000"
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
        await WaitUntilSqlIsReady(_msSqlContainer.GetConnectionString());

        _productCatalogApiContainer = new ContainerBuilder("productcatalogapi:latest")
            .WithNetwork(_network)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
            .WithEnvironment("ASPNETCORE_URLS", "http://+:8080")
            .WithEnvironment("ConnectionStrings__ProductCatalogDb",
                "Server=product-db,1433;Database=ProductsDb;User Id=sa;Password=YourStrong@Password123!;TrustServerCertificate=True;Encrypt=False;")
            .WithPortBinding(8080, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8080)))
            .Build();

        _usersApiContainer = new ContainerBuilder("ecommercestoreusersapi:latest")
            .WithNetwork(_network)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
            .WithEnvironment("ASPNETCORE_URLS", "http://+:8080")
            .WithEnvironment("MongoDbSettings__ConnectionString", "mongodb://admin:admin123@compose-mongodb:27017/?authSource=admin&replicaSet=rs0")
            .WithEnvironment("MongoDbSettings__DatabaseName", "ecommerce-store-users-db-test")
            .WithEnvironment("MongoDbSettings__CustomerCollectionName", "customers")
            .WithEnvironment("MongoDbSettings__CustomersHistoryCollectionName", "customers-history")
            .WithEnvironment("MongoDbSettings__AdminCollectionName", "admins")
            .WithEnvironment("MongoDbSettings__AdminsHistoryCollectionName", "admins-history")
            .WithPortBinding(8080, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8080)))
            .Build();

        _invoiceApiContainer = new ContainerBuilder("ecommercestoreinvoiceapi:latest")
            .WithImage("ecommercestoreinvoiceapi:latest")
            .WithNetwork(_network)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
            .WithEnvironment("ASPNETCORE_URLS", "http://+:8080")
            .WithEnvironment("MongoDbSettings__ConnectionString", "mongodb://admin:admin123@compose-mongodb:27017/?authSource=admin&replicaSet=rs0")
            .WithEnvironment("MongoDbSettings__DatabaseName", "ecommerce-store-invoice-db-test")
            .WithEnvironment("MongoDbSettings__ShoppingCartsCollectionName", "shopping-carts")
            .WithEnvironment("MongoDbSettings__OrdersCollectionName", "orders")
            .WithEnvironment("MongoDbSettings__ProductVersionsCollectionName", "product-versions")
            .WithEnvironment("MongoDbSettings__InvoicesCollectionName", "invoices")
            .WithPortBinding(8080, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8080)))
            .Build();

        await Task.WhenAll(
            _productCatalogApiContainer.StartAsync(),
            _usersApiContainer.StartAsync(),
            _invoiceApiContainer.StartAsync()
        );
    }

    public new async Task DisposeAsync()
    {
        if (_productCatalogApiContainer is not null) await _productCatalogApiContainer.DisposeAsync();
        if (_usersApiContainer is not null) await _usersApiContainer.DisposeAsync();
        if (_invoiceApiContainer is not null) await _invoiceApiContainer.DisposeAsync();

        await _msSqlContainer.DisposeAsync();
        await _mongoContainer.DisposeAsync();
        await _network.DeleteAsync();
    }

    private static async Task WaitUntilSqlIsReady(string cs)
    {
        for (var i = 0; i < 30; i++)
        {
            try
            {
                using var conn = new SqlConnection(cs);
                await conn.OpenAsync();
                using var cmd = new SqlCommand("SELECT 1", conn);
                await cmd.ExecuteScalarAsync();
                return;
            }
            catch (SqlException)
            {
                await Task.Delay(1000);
            }
        }
        throw new InvalidOperationException("SQL Server did not become ready in time.");
    }
}