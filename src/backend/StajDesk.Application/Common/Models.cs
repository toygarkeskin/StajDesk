namespace StajDesk.Application.Common;

/// <summary>Tüm uçların döndüğü standart yanıt zarfı: { success, data, message, errors }.</summary>
public sealed record ApiResponse<T>(bool Success, T? Data, string? Message = null, IDictionary<string, string[]>? Errors = null)
{
    public static ApiResponse<T> Ok(T data, string? message = null) => new(true, data, message);
}

public static class ApiResponse
{
    public static ApiResponse<object> Fail(string message, IDictionary<string, string[]>? errors = null) =>
        new(false, null, message, errors);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public abstract record PagedQuery
{
    private int _page = 1;
    private int _pageSize = 10;

    public int Page { get => _page; init => _page = value < 1 ? 1 : value; }
    public int PageSize { get => _pageSize; init => _pageSize = value is < 1 ? 10 : Math.Min(value, 100); }
    public string? Search { get; init; }
}

public static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, PagedQuery paging, CancellationToken ct)
    {
        var total = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(query, ct);
        var items = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            query.Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize), ct);
        return new PagedResult<T>(items, paging.Page, paging.PageSize, total);
    }
}
