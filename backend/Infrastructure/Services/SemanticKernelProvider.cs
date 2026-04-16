using backend.Application.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace backend.Infrastructure.Services;

public sealed class SemanticKernelProvider(
    IOptions<SemanticKernelOptions> options,
    ILogger<SemanticKernelProvider> logger) : ISemanticKernelProvider
{
    private Kernel? kernel;

    public bool IsConfigured
    {
        get
        {
            var cfg = options.Value;
            return cfg.Enabled
                   && !string.IsNullOrWhiteSpace(cfg.Provider)
                   && !string.IsNullOrWhiteSpace(cfg.ModelId)
                   && !string.IsNullOrWhiteSpace(ResolveApiKey(cfg));
        }
    }

    public Kernel GetKernel()
    {
        if (kernel is not null)
        {
            return kernel;
        }

        var builder = Kernel.CreateBuilder();

        var cfg = options.Value;
        var provider = NormalizeProvider(cfg.Provider);
        var endpoint = ResolveEndpoint(provider, cfg.Endpoint);
        var apiKey = ResolveApiKey(cfg);

        if (!cfg.Enabled)
        {
            logger.LogWarning("Semantic Kernel is disabled. Set SemanticKernel:Enabled=true to activate it.");
        }
        else if (string.IsNullOrWhiteSpace(cfg.ModelId) || string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning(
                "Semantic Kernel missing configuration. Provider={Provider}, ModelIdConfigured={ModelIdConfigured}, ApiKeyConfigured={ApiKeyConfigured}",
                provider,
                !string.IsNullOrWhiteSpace(cfg.ModelId),
                !string.IsNullOrWhiteSpace(apiKey));
        }
        else
        {
            // Use a dedicated HttpClient with an extended timeout.
            // Default 100 s is too short for large LLM responses (Claude, GPT-4, etc.).
            var timeoutSeconds = cfg.TimeoutSeconds > 0 ? cfg.TimeoutSeconds : 300;
            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };

            builder.AddOpenAIChatCompletion(
                modelId: cfg.ModelId,
                apiKey: apiKey,
                endpoint: new Uri(endpoint),
                httpClient: httpClient);

            logger.LogInformation(
                "Semantic Kernel configured. Provider={Provider}, ModelId={ModelId}, Endpoint={Endpoint}, TimeoutSeconds={Timeout}",
                provider,
                cfg.ModelId,
                endpoint,
                timeoutSeconds);
        }

        kernel = builder.Build();
        return kernel;
    }

    private static string NormalizeProvider(string? provider)
    {
        var value = provider?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return "OpenAI";
        }

        return value.ToLowerInvariant() switch
        {
            "chatgpt" => "OpenAI",
            "openai" => "OpenAI",
            "gemini" => "Gemini",
            "perplexity" => "Perplexity",
            "copilot" => "Copilot",
            _ => value
        };
    }

    private static string ResolveEndpoint(string provider, string? configuredEndpoint)
    {
        if (!string.IsNullOrWhiteSpace(configuredEndpoint))
        {
            return configuredEndpoint.Trim();
        }

        return provider switch
        {
            "Gemini" => "https://generativelanguage.googleapis.com/v1beta/openai/",
            "Perplexity" => "https://api.perplexity.ai",
            "Copilot" => "https://api.githubcopilot.com",
            _ => "https://api.openai.com/v1"
        };
    }

    private static string ResolveApiKey(SemanticKernelOptions cfg)
    {
        var provider = NormalizeProvider(cfg.Provider);

        var providerSpecificKey = provider switch
        {
            "Gemini" => cfg.ApiKeys.Gemini,
            "Perplexity" => cfg.ApiKeys.Perplexity,
            "Copilot" => cfg.ApiKeys.Copilot,
            _ => cfg.ApiKeys.OpenAI
        };

        if (!string.IsNullOrWhiteSpace(providerSpecificKey))
        {
            return providerSpecificKey.Trim();
        }

        if (!string.IsNullOrWhiteSpace(cfg.ApiKey))
        {
            return cfg.ApiKey.Trim();
        }

        if (!string.IsNullOrWhiteSpace(cfg.ApiKeyEnvVar))
        {
            return Environment.GetEnvironmentVariable(cfg.ApiKeyEnvVar.Trim()) ?? string.Empty;
        }

        var defaultEnvVar = provider switch
        {
            "Gemini" => "GEMINI_API_KEY",
            "Perplexity" => "PERPLEXITY_API_KEY",
            "Copilot" => "COPILOT_API_KEY",
            _ => "OPENAI_API_KEY"
        };

        return Environment.GetEnvironmentVariable(defaultEnvVar) ?? string.Empty;
    }
}
