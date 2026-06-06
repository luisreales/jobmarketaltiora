using Microsoft.SemanticKernel;

namespace backend.Application.Interfaces;

public interface ISemanticKernelProvider
{
    bool IsConfigured { get; }

    /// <summary>True when an embedding model is configured and available.</summary>
    bool IsEmbeddingConfigured { get; }

    Kernel GetKernel();
}
