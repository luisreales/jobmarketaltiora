using backend.Application.Contracts;
using backend.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;

namespace backend.Controllers;

[ApiController]
[Route("api/auth")]
[Route("api/linkedin/auth")]
public class AuthController(
    IJobOrchestrator orchestrator,
    ILinkedInAuthService linkedInAuthService,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("linkedin/login")]
    public async Task<ActionResult<LinkedInLoginResponse>> LinkedInLogin(
        [FromBody] LinkedInLoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("username and password are required");
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await linkedInAuthService.LoginAndPersistSessionAsync(request.Username, request.Password);
            var isSessionValid = await linkedInAuthService.IsSessionValidAsync();

            return Ok(new LinkedInLoginResponse(
                "LinkedIn login completed and session persisted.",
                isSessionValid));
        }
        catch (TimeoutException ex)
        {
            return Problem(
                title: "LinkedIn verification timeout",
                detail: ex.Message,
                statusCode: StatusCodes.Status408RequestTimeout);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                title: "LinkedIn authentication requires manual action",
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (PlaywrightException ex)
        {
            logger.LogError(ex, "Playwright exception in LinkedIn login endpoint");
            return Problem(
                title: "Playwright login error",
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<ProviderAuthStatusResponse>> Login(
        [FromBody] ProviderLoginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await orchestrator.LoginAsync(request.Provider, request.Username, request.Password, cancellationToken);
            var status = await orchestrator.GetAuthStatusAsync(request.Provider, cancellationToken);

            return Ok(new ProviderAuthStatusResponse(
                request.Provider,
                status.isAuthenticated,
                status.lastLoginAt,
                status.lastUsedAt,
                status.expiresAt));
        }
        catch (TimeoutException ex)
        {
            return Problem(
                title: "Authentication timeout",
                detail: ex.Message,
                statusCode: StatusCodes.Status408RequestTimeout);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                title: "Authentication flow requires manual action",
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (PlaywrightException ex)
        {
            logger.LogError(ex, "Playwright exception in provider login endpoint");
            return Problem(
                title: "Playwright login error",
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    [HttpGet("status/{provider}")]
    public async Task<ActionResult<ProviderAuthStatusResponse>> Status(string provider, CancellationToken cancellationToken)
    {
        var status = await orchestrator.GetAuthStatusAsync(provider, cancellationToken);

        return Ok(new ProviderAuthStatusResponse(
            provider,
            status.isAuthenticated,
            status.lastLoginAt,
            status.lastUsedAt,
            status.expiresAt));
    }

    [HttpGet("status")]
    public Task<ActionResult<ProviderAuthStatusResponse>> LinkedInStatusCompatibility(CancellationToken cancellationToken)
    {
        return Status("linkedin", cancellationToken);
    }

    [HttpPost("logout/{provider}")]
    public async Task<ActionResult<ProviderAuthStatusResponse>> Logout(string provider, CancellationToken cancellationToken)
    {
        await orchestrator.LogoutAsync(provider, cancellationToken);
        var status = await orchestrator.GetAuthStatusAsync(provider, cancellationToken);

        return Ok(new ProviderAuthStatusResponse(
            provider,
            status.isAuthenticated,
            status.lastLoginAt,
            status.lastUsedAt,
            status.expiresAt));
    }
}
