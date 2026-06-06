namespace backend.Application.Contracts;

public record TechnologyDto(
    int Id,
    string Name,
    string DisplayName,
    string Category,
    string LifecycleStage,
    int TotalMentions,
    int WeeklyMentions,
    double GrowthRate,
    double MomentumScore,
    double DemandScore,
    double CompetitionScore,
    double OpportunityScore,
    double EmergingScore,
    int IndustryCoverageCount,
    int ClusterCoverageCount,
    bool IsAiRelated,
    bool IsCloudRelated,
    bool IsLegacy,
    double AvgLeadScore,
    double AvgUrgency,
    DateTime FirstSeenAt,
    DateTime LastSeenAt,
    DateTime UpdatedAt
);

public record TechnologyRelationshipDto(
    int TechnologyId,
    string Name,
    string DisplayName,
    string Category,
    int CoOccurrenceCount,
    double CorrelationScore,
    string IndustryAffinity,
    bool AiAffinity
);

public record TechnologyDetailDto(
    int Id,
    string Name,
    string DisplayName,
    string Category,
    string LifecycleStage,
    int TotalMentions,
    int WeeklyMentions,
    double GrowthRate,
    double MomentumScore,
    double DemandScore,
    double CompetitionScore,
    double OpportunityScore,
    double EmergingScore,
    int IndustryCoverageCount,
    int ClusterCoverageCount,
    bool IsAiRelated,
    bool IsCloudRelated,
    bool IsLegacy,
    double AvgLeadScore,
    double AvgUrgency,
    DateTime FirstSeenAt,
    DateTime LastSeenAt,
    DateTime UpdatedAt,
    List<TechnologyRelationshipDto> Relationships
);

public record TechGraphNode(
    int Id,
    string Name,
    string DisplayName,
    string Category,
    string LifecycleStage,
    int TotalMentions,
    double OpportunityScore,
    bool IsAiRelated
);

public record TechGraphEdge(
    int Source,
    int Target,
    int CoOccurrenceCount,
    double CorrelationScore
);

public record TechnologyGraphDto(
    List<TechGraphNode> Nodes,
    List<TechGraphEdge> Edges
);

public record TechRebuildResultDto(
    int TechnologiesUpserted,
    int RelationshipsUpserted,
    int SnapshotsAdded,
    int JobsProcessed,
    TimeSpan Duration,
    DateTime RanAt
);

public record TechQueryRequest(
    string? Search = null,
    string? Category = null,
    string? LifecycleStage = null,
    bool? IsAiRelated = null,
    int Page = 1,
    int PageSize = 50,
    string SortBy = "demandScore"
);

public record IndustryTechDto(
    string Industry,
    List<TechnologyDto> TopTechnologies
);
