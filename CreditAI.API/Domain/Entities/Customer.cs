namespace CreditAI.API.Domain.Entities;

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int FinancialScore { get; set; }
    public string HistoricText { get; set; } = string.Empty;
    public byte[] BehaviorEmbedding { get; set; } = Array.Empty<byte>();
    public DateTime LastAnalysisDate { get; set; } = DateTime.Now;

}
