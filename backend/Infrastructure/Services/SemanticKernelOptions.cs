namespace backend.Infrastructure.Services;

public sealed class SemanticKernelOptions
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "OpenAI";
    public string ModelId { get; set; } = "gpt-4o-mini";
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiKeyEnvVar { get; set; } = string.Empty;
    public SemanticKernelApiKeysOptions ApiKeys { get; set; } = new();

    /// <summary>
    /// HTTP timeout in seconds for LLM API calls. Default 300 s (5 min).
    /// Increase if using large context windows or slow models.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;
}

public sealed class SemanticKernelApiKeysOptions
{
    public string OpenAI { get; set; } = string.Empty;
    public string Gemini { get; set; } = string.Empty;
    public string Copilot { get; set; } = string.Empty;
    public string Perplexity { get; set; } = string.Empty;
}
