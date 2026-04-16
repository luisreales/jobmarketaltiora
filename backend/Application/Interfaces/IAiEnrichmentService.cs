using backend.Application.Contracts;
using backend.Domain.Entities;

namespace backend.Application.Interfaces;

public interface IAiEnrichmentService
{
    Task<JobInsightAnalysisResult> AnalyzeJobAsync(JobOffer job, CancellationToken cancellationToken = default);
}
