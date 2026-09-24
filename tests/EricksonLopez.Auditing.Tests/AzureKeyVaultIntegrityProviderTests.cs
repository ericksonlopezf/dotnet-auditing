// Copyright © Erickson Lopez. MIT License.
using System;
using AwesomeAssertions;
using EricksonLopez.Auditing.AzureKeyVault;
using Xunit;

namespace EricksonLopez.Auditing.Tests;

public sealed class AzureKeyVaultIntegrityProviderTests
{
    [Fact]
    public void Constructor_NullUri_ThrowsArgumentNullException()
    {
        Action act = () => _ = new AzureKeyVaultIntegrityProvider(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ValidUri_InitializesInstance()
    {
        var uri = new Uri("https://vault-test.vault.azure.net/");
        var provider = new AzureKeyVaultIntegrityProvider(uri);
        provider.Should().NotBeNull();
    }
}
