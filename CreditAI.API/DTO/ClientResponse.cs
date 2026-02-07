namespace CreditAI.API.DTO;

/// <summary>
/// Representa os dados de retorno de um cliente após processamento.
/// </summary>
public record ClientResponse
{
    /// <summary>
    /// Identificador GUID único do cliente, utilizado para referência e rastreamento.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Nome completo
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Pontuação de crédito tradicional
    /// </summary>
    public int FinancialScore { get; init; }

    /// <summary>
    /// Histórico textual utilizado para a geração do embedding
    /// </summary>
    public string HistoricText { get; init; }

    /// <summary>
    /// Data e hora de criação
    /// </summary>
    public DateTime? CreatedAt { get; init; }
}