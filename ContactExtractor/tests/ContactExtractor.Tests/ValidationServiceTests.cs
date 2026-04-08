using ContactExtractor.Api.Services;

namespace ContactExtractor.Tests;

public class ContactValidationServiceTests
{
    private readonly ContactValidationService _service = new();

    [Theory]
    [InlineData("99887766",    true)]
    [InlineData("+4799887766", true)]
    [InlineData("12345678",    true)]
    [InlineData("1234567",     false)]   // for norsk: for kort
    [InlineData("abc",         false)]
    [InlineData(null,          false)]
    [InlineData("",            false)]
    public void ValidatePhone_ReturnsExpected(string? phone, bool expected)
    {
        _service.ValidatePhone(phone).ShouldBe(expected);
    }

    [Theory]
    [InlineData("test@example.com",           true)]
    [InlineData("user+tag@sub.domain.no",     true)]
    [InlineData("invalid",                    false)]
    [InlineData("@nodomain",                  false)]
    [InlineData(null,                         false)]
    public async Task ValidateEmailAsync_FormatCheck_ReturnsExpected(string? email, bool expectedFormat)
    {
        // We only test the format check here (DNS resolution may vary in CI)
        // For emails with bad format, must return false
        if (!expectedFormat)
        {
            var result = await _service.ValidateEmailAsync(email);
            result.ShouldBeFalse();
        }
        else
        {
            // Valid format – may return true or false depending on DNS
            // Just ensure no exception is thrown
            var act = async () => await _service.ValidateEmailAsync(email);
            await Should.NotThrowAsync(act);
        }
    }
}
