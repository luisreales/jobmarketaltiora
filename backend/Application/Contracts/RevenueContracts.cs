namespace backend.Application.Contracts;

public record RevenueSummaryDto(
    decimal TotalPipelineValueUsd,
    decimal WeightedExpectedRevenueUsd,
    int TotalActionableClusters,
    int TotalProducts,
    int OpenProducts,
    double AvgCloseProbability,
    double AvgBlueOceanScore,
    FunnelStatsDto ConversionFunnel,
    IList<ServiceModelRevenueDto> ByServiceModel,
    IList<IndustryRevenueDto> ByIndustry,
    IList<TopOpportunityDto> TopOpportunities
);

public record FunnelStatsDto(
    int TotalJobs,
    int AnalyzedJobs,
    int ClusteredInsights,
    int ActionableClusters,
    int SynthesizedClusters,
    int ProductsGenerated,
    int ProductsOpen
);

public record ServiceModelRevenueDto(
    string ServiceModel,
    int ClusterCount,
    decimal WeightedValueUsd,
    double AvgCloseProbability
);

public record IndustryRevenueDto(
    string Industry,
    double TamMillionsUsd,
    int ClusterCount,
    double AvgCloseProbability,
    decimal EstimatedValueUsd
);

public record TopOpportunityDto(
    int ClusterId,
    string Label,
    string PainCategory,
    string Industry,
    string ServiceModel,
    decimal EstimatedDealSizeUsd,
    double CloseProbability,
    decimal ExpectedValueUsd,
    double BlueOceanScore,
    double BuyingIntentScore,
    int JobCount,
    bool HasProduct
);

public record SalesStatusUpdateDto(
    string SalesStatus,
    decimal? WonDealSizeUsd,
    string? SalesNotes
);
