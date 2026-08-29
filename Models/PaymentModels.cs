using System.Text.Json.Serialization;

namespace ExpenseTracker.Models;

public sealed class ConnectOnboardingResult
{
    [JsonPropertyName("onboardingUrl")]
    public string OnboardingUrl { get; set; } = string.Empty;

    [JsonPropertyName("chargesEnabled")]
    public bool ChargesEnabled { get; set; }

    [JsonPropertyName("payoutsEnabled")]
    public bool PayoutsEnabled { get; set; }

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = string.Empty;
}

public sealed class PaymentRequestResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("checkoutUrl")]
    public string CheckoutUrl { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTimeOffset? ExpiresAt { get; set; }

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("paidAt")]
    public DateTimeOffset? PaidAt { get; set; }
}
