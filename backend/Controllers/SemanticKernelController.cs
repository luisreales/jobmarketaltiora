using backend.Application.Interfaces;
using backend.Domain.Entities;
using backend.Infrastructure.Data;
using backend.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;

namespace backend.Controllers;

[ApiController]
[Route("api/semantic")]
public class SemanticKernelController(
    ISemanticKernelProvider semanticKernelProvider,
    ApplicationDbContext dbContext,
    IOptions<SemanticKernelOptions> semanticKernelOptions,
    ILogger<SemanticKernelController> logger) : ControllerBase
{
    [HttpGet("hello")]
    public async Task<ActionResult<object>> Hello([FromQuery] string prompt = "Hello", CancellationToken cancellationToken = default)
    {
        var timer = Stopwatch.StartNew();
        var safePrompt = prompt ?? string.Empty;
        var promptHash = ComputeSha256(safePrompt);

        if (!semanticKernelProvider.IsConfigured)
        {
            await SavePromptLogAsync(
                promptHash,
                safePrompt,
                responseText: string.Empty,
                isSuccess: false,
                status: "not_configured",
                errorMessage: "SemanticKernel provider is not configured",
                modelIdOverride: null,
                latencyMs: (int)timer.ElapsedMilliseconds,
                cancellationToken: cancellationToken);

            return BadRequest(new
            {
                message = "Semantic Kernel is not fully configured. Check Provider, ModelId and API key."
            });
        }

        try
        {
            var kernel = semanticKernelProvider.GetKernel();
            var chat = kernel.GetRequiredService<IChatCompletionService>();
            var modelUsed = semanticKernelOptions.Value.ModelId;

            var history = new ChatHistory();
            history.AddSystemMessage("Answer with a short plain-text response in one line.");
            history.AddUserMessage(safePrompt);
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                // Keep test calls short to avoid hitting default HTTP timeout with reasoning models.
                MaxTokens = 128
            };

            var messages = await chat.GetChatMessageContentsAsync(history, executionSettings, kernel, cancellationToken);
            var content = messages.LastOrDefault()?.Content ?? string.Empty;

            // Some reasoning-heavy models can return empty visible text on short prompts.
            // Retry once with a fast chat model so we always capture a human-readable response in audit logs.
            if (string.IsNullOrWhiteSpace(content))
            {
                var fallbackHistory = new ChatHistory();
                fallbackHistory.AddSystemMessage("Respond with one short greeting sentence in plain text.");
                fallbackHistory.AddUserMessage(safePrompt);

                var fallbackSettings = new OpenAIPromptExecutionSettings
                {
                    MaxTokens = 64,
                    ModelId = "gpt-4o-mini"
                };

                var fallbackMessages = await chat.GetChatMessageContentsAsync(fallbackHistory, fallbackSettings, kernel, cancellationToken);
                var fallbackContent = fallbackMessages.LastOrDefault()?.Content ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(fallbackContent))
                {
                    content = fallbackContent;
                    modelUsed = "gpt-4o-mini";
                }
            }

            var hasResponse = !string.IsNullOrWhiteSpace(content);

            await SavePromptLogAsync(
                promptHash,
                safePrompt,
                responseText: content,
                isSuccess: hasResponse,
                status: hasResponse ? "success" : "empty_response",
                errorMessage: null,
                modelIdOverride: modelUsed,
                latencyMs: (int)timer.ElapsedMilliseconds,
                cancellationToken: cancellationToken);

            return Ok(new
            {
                prompt,
                response = content,
                hasResponse,
                modelUsed
            });
        }
        catch (Exception ex)
        {
            await SavePromptLogAsync(
                promptHash,
                safePrompt,
                responseText: string.Empty,
                isSuccess: false,
                status: "failed",
                errorMessage: ex.ToString(),
                modelIdOverride: null,
                latencyMs: (int)timer.ElapsedMilliseconds,
                cancellationToken: cancellationToken);

            logger.LogError(ex, "Semantic hello test failed");
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = "Semantic Kernel provider call failed.",
                detail = ex.Message
            });
        }
    }

    private async Task SavePromptLogAsync(
        string promptHash,
        string promptText,
        string responseText,
        bool isSuccess,
        string status,
        string? errorMessage,
        string? modelIdOverride,
        int latencyMs,
        CancellationToken cancellationToken)
    {
        var provider = semanticKernelOptions.Value.Provider;
        var modelId = string.IsNullOrWhiteSpace(modelIdOverride)
            ? semanticKernelOptions.Value.ModelId
            : modelIdOverride;

        var promptTokens = ApproximateTokens(promptText);
        var completionTokens = ApproximateTokens(responseText);

        dbContext.AiPromptLogs.Add(new AiPromptLog
        {
            Provider = provider,
            ModelId = modelId,
            PromptVersion = "manual-test",
            PromptHash = promptHash,
            PromptText = promptText,
            ResponseText = responseText,
            CacheHit = false,
            IsSuccess = isSuccess,
            Status = status,
            ErrorMessage = errorMessage,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = promptTokens + completionTokens,
            LatencyMs = Math.Max(0, latencyMs),
            CreatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string ComputeSha256(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static int ApproximateTokens(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return Math.Max(1, text.Length / 4);
    }
}
