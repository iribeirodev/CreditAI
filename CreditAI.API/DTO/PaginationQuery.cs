namespace CreditAI.API.DTO;

public record PaginationQuery
{
    /// <summary>
    /// Número da página a ser retornada. Valor padrão é 1.
    /// </summary>
    public int Page { get; init; } = 1;
    /// <summary>
    /// Quantidade de itens por página. Valor padrão é 20.
    /// </summary>
    public int PageSize { get; init; } = 20;
}
