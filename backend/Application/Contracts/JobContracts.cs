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
    int? EndPage = null);

public record JobSearchResponse(int SavedCount, int TotalFound, DateTime ExecutedAtUtc);

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
    string Url);

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
