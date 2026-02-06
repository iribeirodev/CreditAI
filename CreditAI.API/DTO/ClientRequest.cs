using System.ComponentModel.DataAnnotations;

namespace CreditAI.API.DTO;

public record ClientRequest
{
    [Required]
    [StringLength(150, MinimumLength = 3)]
    public string Name { get; init; } = string.Empty;

    [Range(0, 1000)]
    public int FinancialScore { get; init; }

    [Required]
    [StringLength(4000, MinimumLength = 10)]
    public string HistoricText { get; init; } = string.Empty;
}
