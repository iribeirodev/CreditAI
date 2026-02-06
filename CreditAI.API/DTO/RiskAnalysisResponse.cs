namespace CreditAI.API.DTO;

public record RiskAnalysisResponse
{
    public Guid CustomerId { get; init; }
    public string Analysis { get; init; } = string.Empty;
}
