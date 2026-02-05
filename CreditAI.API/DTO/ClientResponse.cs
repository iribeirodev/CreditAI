namespace CreditAI.API.DTO;

public record ClientResponse
(
    int Id,
    string Name,
    int FinancialScore,
    string HistoricText,
    DateTime? LastAnalysisDate
);