using System.Security.Cryptography;
using System.Text.Json;
using Shouldly;

namespace ECommerceStoreBFF.IntegrationTests.Common;

internal static class ConsumerPactArtifact
{
    public static async Task VerifyAndWriteAsync(
        string provider, int interactionCount, string outputVariable, Func<string, Task> verifyAsync)
    {
        Environment.SetEnvironmentVariable("PACT_DO_NOT_TRACK", "true");
        var directory = Directory.CreateTempSubdirectory($"bff-{provider}-pact-");
        try
        {
            var file = $"bff-{provider}.json";
            var firstDirectory = directory.CreateSubdirectory("first").FullName;
            await verifyAsync(firstDirectory);
            var first = await File.ReadAllBytesAsync(Path.Combine(firstDirectory, file));
            var secondDirectory = directory.CreateSubdirectory("second").FullName;
            await verifyAsync(secondDirectory);
            var second = await File.ReadAllBytesAsync(Path.Combine(secondDirectory, file));
            first.AsSpan().SequenceEqual(second).ShouldBeTrue("fresh Pact servers must produce the same bytes");

            using var document = JsonDocument.Parse(first);
            var pact = document.RootElement;
            pact.GetProperty("consumer").GetProperty("name").GetString().ShouldBe("bff");
            pact.GetProperty("provider").GetProperty("name").GetString().ShouldBe(provider);
            pact.GetProperty("interactions").GetArrayLength().ShouldBe(interactionCount);
            pact.GetProperty("metadata").GetProperty("pactSpecification").GetProperty("version").GetString().ShouldBe("4.0");

            var output = Environment.GetEnvironmentVariable(outputVariable);
            if (!string.IsNullOrWhiteSpace(output))
            {
                Directory.CreateDirectory(output);
                await File.WriteAllBytesAsync(Path.Combine(output, file), first);
                await File.WriteAllTextAsync(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    consumer = "bff",
                    provider,
                    pactSpecification = "4.0",
                    file,
                    sha256 = Convert.ToHexStringLower(SHA256.HashData(first)),
                    interactionCount
                }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
