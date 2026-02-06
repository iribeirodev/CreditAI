namespace CreditAI.API.DTO;

/// <summary>
/// Representa os dados de retorno de um cliente após processamento.
/// </summary>
public record ClientResponse
{
    /// <summary>
    /// Identificador único do cliente
    /// </summary>
    public int Id { get; init; }

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
    /// Data e hora da última atualização ou análise do cliente pela IA
    /// </summary>
    public DateTime? LastAnalysisDate { get; init; }
}