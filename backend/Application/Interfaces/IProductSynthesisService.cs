using backend.Domain.Entities;

namespace backend.Application.Interfaces;

public interface IProductSynthesisService
{
    /// <summary>
    /// Generates (or returns cached) tactical LLM plan for a product.
    /// Stores result in ProductSuggestion.SynthesisDetailJson.
    /// Returns null if the product is not found.
    /// </summary>
    Task<ProductSuggestion?> SynthesizeProductAsync(int productId, CancellationToken ct = default);
}
