// Copyright © Erickson Lopez. MIT License.
using System;
using System.Text;
using System.Threading;
using Azure;
using Azure.Security.KeyVault.Secrets;
using AwesomeAssertions;
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.AzureKeyVault;
using NSubstitute;
using Xunit;

namespace EricksonLopez.Auditing.Tests;

public sealed class AzureKeyVaultIntegrityProviderTests
{
    [Fact]
    public void Constructor_NullUri_ThrowsArgumentNullException()
    {
        Action act = () => _ = new AzureKeyVaultIntegrityProvider((Uri)null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("keyVaultUri");
    }

    [Fact]
    public void Constructor_NullSecretClient_ThrowsArgumentNullException()
    {
        Action act = () => _ = new AzureKeyVaultIntegrityProvider((SecretClient)null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("secretClient");
    }

    [Fact]
    public void Constructor_ValidUri_InitializesInstance()
    {
        var uri = new Uri("https://vault-test.vault.azure.net/");
        var provider = new AzureKeyVaultIntegrityProvider(uri);
        provider.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_ValidSecretClient_InitializesInstance()
    {
        var client = Substitute.For<SecretClient>();
        var provider = new AzureKeyVaultIntegrityProvider(client);
        provider.Should().NotBeNull();
    }

    [Fact]
    public void GetCurrentKey_FetchesSecretFromKeyVault_AndCachesResultOnSubsequentCalls()
    {
        // Arrange
        var client = Substitute.For<SecretClient>();
        var rawKey = Encoding.UTF8.GetBytes("super-secret-hmac-key-32bytes!!");
        var base64Key = Convert.ToBase64String(rawKey);

        var secret = new KeyVaultSecret("audit-key-tenant-alpha", base64Key);
        var mockResponse = Substitute.For<Response>();
        var response = Response.FromValue(secret, mockResponse);

        client.GetSecret("audit-key-tenant-alpha").Returns(response);

        var provider = new AzureKeyVaultIntegrityProvider(client);
        var tenant = new TenantId("tenant-alpha");

        // Act - First call: fetches from client
        var key1 = provider.GetCurrentKey(tenant);

        // Act - Second call: should hit cache
        var key2 = provider.GetCurrentKey(tenant);

        // Assert
        key1.ToArray().Should().BeEquivalentTo(rawKey);
        key2.ToArray().Should().BeEquivalentTo(rawKey);

        // Verify SecretClient was called exactly once with the expected secret name
        client.Received(1).GetSecret("audit-key-tenant-alpha");
    }

    [Fact]
    public void GetCurrentKey_DifferentTenants_FetchesRespectiveSecrets()
    {
        // Arrange
        var client = Substitute.For<SecretClient>();
        var rawKey1 = Encoding.UTF8.GetBytes("key-tenant-1-must-be-32-bytes!");
        var rawKey2 = Encoding.UTF8.GetBytes("key-tenant-2-must-be-32-bytes!");

        var secret1 = new KeyVaultSecret("audit-key-t1", Convert.ToBase64String(rawKey1));
        var secret2 = new KeyVaultSecret("audit-key-t2", Convert.ToBase64String(rawKey2));
        var mockResponse = Substitute.For<Response>();

        client.GetSecret("audit-key-t1").Returns(Response.FromValue(secret1, mockResponse));
        client.GetSecret("audit-key-t2").Returns(Response.FromValue(secret2, mockResponse));

        var provider = new AzureKeyVaultIntegrityProvider(client);

        // Act
        var result1 = provider.GetCurrentKey(new TenantId("t1"));
        var result2 = provider.GetCurrentKey(new TenantId("t2"));

        // Assert
        result1.ToArray().Should().BeEquivalentTo(rawKey1);
        result2.ToArray().Should().BeEquivalentTo(rawKey2);
        client.Received(1).GetSecret("audit-key-t1");
        client.Received(1).GetSecret("audit-key-t2");
    }
}
