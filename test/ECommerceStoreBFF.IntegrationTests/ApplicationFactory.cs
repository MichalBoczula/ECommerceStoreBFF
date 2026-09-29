using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Testcontainers.MongoDb;
using Testcontainers.MsSql;

namespace ECommerceStoreBFF.AcceptanceTests;

public class ApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string SqlPassword = "YourStrong@Password123!";
    private const ushort SqlPort = 1433;
    private const ushort ApiPort = 8080;
    private const string MongoConnectionString =
        "mongodb://admin:admin123@mongodb:27017/?authSource=admin&directConnection=true";

    private readonly INetwork _network = new NetworkBuilder().Build();
    private readonly MsSqlContainer _sql;
    private readonly MongoDbContainer _mongo;
    private IContainer? _products;
    private IContainer? _users;
    private IContainer? _invoice;

    public ApplicationFactory()
    {
        _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithNetwork(_network)
            .WithNetworkAliases("product-db")
            .WithPassword(SqlPassword)
            .Build();

        _mongo = new MongoDbBuilder("mongo:8.0")
            .WithNetwork(_network)
            .WithNetworkAliases("mongodb")
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
            var products = _products ?? throw new InvalidOperationException("Products API was not started.");
            var users = _users ?? throw new InvalidOperationException("Users API was not started.");
            var invoice = _invoice ?? throw new InvalidOperationException("Invoice API was not started.");

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GatewaySettings:BaseUrl"] = "http://localhost",
                ["ReverseProxy:Clusters:products-cluster:Destinations:destination1:Address"] = BaseAddress(products).ToString(),
                ["ReverseProxy:Clusters:users-cluster:Destinations:destination1:Address"] = BaseAddress(users).ToString(),
                ["ReverseProxy:Clusters:orders-cluster:Destinations:destination1:Address"] = BaseAddress(invoice).ToString()
            });
        });
    }

    public Uri ProductsBaseAddress => BaseAddress(_products ?? throw new InvalidOperationException("Products API was not started."));
    public Uri UsersBaseAddress => BaseAddress(_users ?? throw new InvalidOperationException("Users API was not started."));
    public Uri InvoiceBaseAddress => BaseAddress(_invoice ?? throw new InvalidOperationException("Invoice API was not started."));

    public async Task InitializeAsync()
    {
        try
        {
            await _network.CreateAsync();
            await Task.WhenAll(_sql.StartAsync(), _mongo.StartAsync());
            await WaitForSqlAsync();

            _products = BuildApiContainer("products", "product-api")
                .WithEnvironment("ConnectionStrings__ProductCatalogDb",
                    $"Server=product-db;Database=ProductsDb;User Id=sa;Password={SqlPassword};TrustServerCertificate=True")
                .WithEnvironment("Database__ApplyMigrations", "true")
                .Build();

            _users = BuildApiContainer("users", "users-api")
                .WithEnvironment("MongoDbSettings__ConnectionString", MongoConnectionString)
                .WithEnvironment("MongoDbSettings__DatabaseName", "bff-users-test")
                .Build();

            await Task.WhenAll(_products.StartAsync(), _users.StartAsync());

            _invoice = BuildApiContainer("invoice", "invoice-api")
                .WithEnvironment("MongoDbSettings__ConnectionString", MongoConnectionString)
                .WithEnvironment("MongoDbSettings__DatabaseName", "bff-invoice-test")
                .WithEnvironment("ExternalServices__ProductCatalog__BaseUrl", "http://product-api:8080")
                .Build();

            await _invoice.StartAsync();
        }
        catch
        {
            await DisposeContainersAsync();
            throw;
        }
    }

    public new async Task DisposeAsync()
    {
        base.Dispose();
        await DisposeContainersAsync();
    }

    private ContainerBuilder BuildApiContainer(string service, string alias) =>
        new ContainerBuilder(GetImage(service))
            .WithNetwork(_network)
            .WithNetworkAliases(alias)
            .WithPortBinding(ApiPort, true)
            .WithEnvironment("ASPNETCORE_URLS", "http://+:8080")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request => request.ForPort(ApiPort).ForPath("/health/ready")));

    private static string GetImage(string service)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "upstream-manifest.json")));
        return manifest.RootElement.GetProperty(service).GetProperty("image").GetString()
            ?? throw new InvalidOperationException($"Missing image for {service} in upstream manifest.");
    }

    private static Uri BaseAddress(IContainer container) =>
        new($"http://{container.Hostname}:{container.GetMappedPublicPort(ApiPort)}/");

    private async Task WaitForSqlAsync()
    {
        var connectionString =
            $"Server={_sql.Hostname},{_sql.GetMappedPublicPort(SqlPort)};" +
            $"Database=master;User Id=sa;Password={SqlPassword};" +
            "TrustServerCertificate=True;Encrypt=False;Connection Timeout=5;";

        for (var attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                await command.ExecuteScalarAsync();
                return;
            }
            catch (SqlException) when (attempt < 29)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
    }

    private async Task DisposeContainersAsync()
    {
        if (_invoice is not null) await _invoice.DisposeAsync();
        if (_users is not null) await _users.DisposeAsync();
        if (_products is not null) await _products.DisposeAsync();
        await _mongo.DisposeAsync();
        await _sql.DisposeAsync();
        await _network.DisposeAsync();
    }
}
