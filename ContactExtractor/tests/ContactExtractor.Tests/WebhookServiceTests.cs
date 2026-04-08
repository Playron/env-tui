using ContactExtractor.Api.Services;
using FluentAssertions;

namespace ContactExtractor.Tests;

public class WebhookServiceTests
{
    [Fact]
    public void ComputeSignature_ReturnsSha256PrefixedHex()
    {
        var signature = WebhookService.ComputeSignature("secret123", "{\"event\":\"test\"}");
        signature.Should().StartWith("sha256=");
        signature.Length.Should().Be(71); // "sha256=" + 64 hex chars
    }

    [Fact]
    public void ComputeSignature_SameInputProducesSameOutput()
    {
        var sig1 = WebhookService.ComputeSignature("key", "payload");
        var sig2 = WebhookService.ComputeSignature("key", "payload");
        sig1.Should().Be(sig2);
    }

    [Fact]
    public void ComputeSignature_DifferentSecrets_ProduceDifferentSignatures()
    {
        var sig1 = WebhookService.ComputeSignature("secret1", "same payload");
        var sig2 = WebhookService.ComputeSignature("secret2", "same payload");
        sig1.Should().NotBe(sig2);
    }
}
