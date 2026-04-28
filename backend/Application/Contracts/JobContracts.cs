namespace backend.Application.Contracts;

public record ProviderLoginRequest(string Provider, string Username, string Password);

public record ProviderAuthStatusResponse(string Provider, bool IsAuthenticated, DateTime? LastLoginAt, DateTime? LastUsedAt, DateTime? ExpiresAt);

public record JobSearchRequest(
    string Query,
    string? Location,
    int Limit = 20,
    List<string>? Providers = null,
    int? TotalPaging = null,
    int? StartPage = null,
    int? EndPage = null,
    bool ShowBrowser = false);

public record JobSearchResponse(int SavedCount, int TotalFound, DateTime ExecutedAtUtc);

public class JobsQueryRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "capturedAt";
    public string SortDirection { get; set; } = "desc";
    public string? Search { get; set; }
    public string? Title { get; set; }
    public string? Company { get; set; }
    public string? Location { get; set; }
    public string? Source { get; set; }
    public string? SearchTerm { get; set; }
    public string? SalaryRange { get; set; }
    public int? MinSalary { get; set; }
    public int? MaxSalary { get; set; }
}

public record PagedResultDto<T>(
    IReadOnlyCollection<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    string SortBy,
    string SortDirection);

public class JobFilter
{
    public int? MinScore { get; set; }
    public bool DirectOnly { get; set; } = true;
    public string? Category { get; set; }
    public string? CompanyType { get; set; }
    public string? Search { get; set; }
}

public record JobSummaryDto(
    int Id,
    string Title,
    string Company,
    string Location,
    string DescriptionPreview,
    string Category,
    int OpportunityScore,
    bool IsConsultingCompany,
    string CompanyType,
    string Source,
    string SearchTerm,
    DateTime CapturedAt,
    DateTime? PublishedAt,
    string? SalaryRange,
    string? Seniority,
    string? ContractType,
    string Url,
    // Human-in-the-loop funnel state
    bool HasOpportunity,
    int? OpportunityId);

public record JobFullListDto(
    int Id,
    string ExternalId,
    string Title,
    string Company,
    string Location,
    string Description,
    string Category,
    int OpportunityScore,
    bool IsConsultingCompany,
    string CompanyType,
    string Url,
    string Source,
    string SearchTerm,
    DateTime CapturedAt,
    string? MetadataJson);

public record JobDetailDto(
    int Id,
    string ExternalId,
    string Title,
    string Company,
    string Location,
    string Description,
    string Category,
    int OpportunityScore,
    bool IsConsultingCompany,
    string CompanyType,
    string Url,
    string? Contact,
    string? SalaryRange,
    DateTime? PublishedAt,
    string? Seniority,
    string? ContractType,
    string Source,
    string SearchTerm,
    DateTime CapturedAt,
    string? MetadataJson);

public record JobLeadDto(
    int Id,
    string Title,
    string Company,
    string Location,
    string Category,
    int OpportunityScore,
    string CompanyType,
    string Url,
    DateTime CapturedAt);
