using backend.Application.Contracts;

namespace backend.Application.Interfaces;

public interface ICompanyIntelligenceService
{
    Task<CompanyRebuildResultDto> RebuildAsync(CancellationToken ct = default);
}
