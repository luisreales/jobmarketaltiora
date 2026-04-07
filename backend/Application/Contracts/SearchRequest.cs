namespace backend.Application.Contracts;

public sealed record SearchRequest(
    string Query,
    string? Location,
    int Limit,
    int? TotalPaging,
    int? StartPage,
    int? EndPage);