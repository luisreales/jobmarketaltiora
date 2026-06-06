namespace backend.Application.Contracts;

public record CompanyProfileDto(
    int Id,
    string CompanyName,
    string CompanyType,
    string PrimaryIndustry,
    IList<string> TechStack,
    string TopPainCategory,
    int TotalJobCount,
    double AvgUrgencyScore,
    double AvgOpportunityScore,
    double AvgLeadScore,
    double HiringVelocity,
    bool IsDirectClient,
    bool HasAiInitiative,
    bool HasCloudMigration,
    double ProspectScore,
    DateTime FirstSeenAt,
    DateTime LastSeenAt
);

public record CompanyRebuildResultDto(
    int CompaniesUpserted,
    int JobsProcessed,
    TimeSpan Duration,
    DateTime RanAt
);
