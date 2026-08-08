using ECommerceStoreBFF.AcceptanceTests;

namespace ECommerceStoreBFF.IntegrationTests.Common
{
    [CollectionDefinition("Api Test Collection")]
    public class SharedTestCollection : ICollectionFixture<ApplicationFactory>
    {
    }
}
