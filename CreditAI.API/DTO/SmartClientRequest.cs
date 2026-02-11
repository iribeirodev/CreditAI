using System.ComponentModel.DataAnnotations;

namespace CreditAI.API.DTO;

public record SmartClientRequest
{
    [Required]
    public string Name { get; init; } = string.Empty;

    [Range(0, 1000)]
    public int FinancialScore { get; init; }

    [Required]
    public object RawData { get; init; }
}
