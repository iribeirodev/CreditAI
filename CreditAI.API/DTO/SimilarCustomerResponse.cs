namespace CreditAI.API.DTO;

public record SimilarCustomerResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int FinancialScore { get; init; }
    public float Similarity { get; init; }
}
