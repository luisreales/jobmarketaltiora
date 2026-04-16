using Microsoft.Extensions.Configuration;
using Xunit;

namespace backend.Tests;

public class SemanticKernelLocalConfigTests
{
    [Fact]
    public void DevelopmentConfig_ShouldUseAnthropicClaudeSonnetModel()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Development.json", optional: false)
            .Build();

        var modelId = configuration["SemanticKernel:ModelId"];

        Assert.Equal("bedrock/anthropic.claude-4-6-sonnet", modelId);
    }

    [Fact]
    public void EnvironmentVariables_ShouldExposeAnthropicDefaultModels()
    {
        const string anthropicModelKey = "ANTHROPIC_MODEL";
        const string sonnetKey = "ANTHROPIC_DEFAULT_SONNET_MODEL";
        const string haikuKey = "ANTHROPIC_DEFAULT_HAIKU_MODEL";

        var originalAnthropicModel = Environment.GetEnvironmentVariable(anthropicModelKey);
        var originalSonnet = Environment.GetEnvironmentVariable(sonnetKey);
        var originalHaiku = Environment.GetEnvironmentVariable(haikuKey);

        try
        {
            Environment.SetEnvironmentVariable(anthropicModelKey, "bedrock/anthropic.claude-4-6-sonnet");
            Environment.SetEnvironmentVariable(sonnetKey, "bedrock/anthropic.claude-4-6-sonnet");
            Environment.SetEnvironmentVariable(haikuKey, "bedrock/anthropic.claude-4-5-haiku");

            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            Assert.Equal("bedrock/anthropic.claude-4-6-sonnet", configuration[anthropicModelKey]);
            Assert.Equal("bedrock/anthropic.claude-4-6-sonnet", configuration[sonnetKey]);
            Assert.Equal("bedrock/anthropic.claude-4-5-haiku", configuration[haikuKey]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(anthropicModelKey, originalAnthropicModel);
            Environment.SetEnvironmentVariable(sonnetKey, originalSonnet);
            Environment.SetEnvironmentVariable(haikuKey, originalHaiku);
        }
    }
}
