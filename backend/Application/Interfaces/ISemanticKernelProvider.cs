using Microsoft.SemanticKernel;

namespace backend.Application.Interfaces;

public interface ISemanticKernelProvider
{
    bool IsConfigured { get; }
    Kernel GetKernel();
}
