namespace CreditAI.API.DTO;

public class PagedResponse<T>
{
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
}
