using backend.Application.Contracts;

namespace backend.Application.Interfaces;

public interface ITechnologyIntelligenceService
{
    Task<TechRebuildResultDto> RebuildAsync(CancellationToken ct = default);
}
