using backend.Application.Contracts;
using backend.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/market")]
public class MarketIntelligenceController(IMarketIntelligenceService marketIntelligenceService) : ControllerBase
{
    [HttpGet("opportunities")]
    public async Task<ActionResult<PagedResultDto<MarketOpportunityDto>>> GetOpportunities(
        [FromQuery] MarketOpportunityQuery query,
        CancellationToken cancellationToken)
    {
        var result = await marketIntelligenceService.GetOpportunitiesAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("leads")]
    public async Task<ActionResult<PagedResultDto<MarketLeadDto>>> GetLeads(
        [FromQuery] MarketLeadsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await marketIntelligenceService.GetLeadsAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("trends")]
    public async Task<ActionResult<IReadOnlyCollection<MarketTrendDto>>> GetTrends(
        [FromQuery] MarketTrendsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await marketIntelligenceService.GetTrendsAsync(query, cancellationToken);
        return Ok(result);
    }
}
