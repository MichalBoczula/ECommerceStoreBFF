using ECommerceStoreBFF.AcceptanceTests;
using System.Net;

namespace ECommerceStoreBFF.IntegrationTests.Features
{
    public class HealthCheckTests : IClassFixture<ApplicationFactory>
    {
        private readonly ApplicationFactory _factory;

        public HealthCheckTests(ApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task ProductContainer_HealthCheck_ShouldReturnOk()
        {
            // Arrange
            using var client = new HttpClient();

            // Act
            var response = await client.GetAsync("http://localhost:5000/health");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task BffContainer_HealthCheck_ShouldReturnOk()
        {
            // Arrange
            using var client = new HttpClient();

            // Act
            var response = await client.GetAsync("http://localhost:3000/health");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}