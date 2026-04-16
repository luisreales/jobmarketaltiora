namespace backend.Domain.Entities;

public class AiPromptTemplate
{
    public long Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public string Version { get; set; } = "v1";
    public bool IsActive { get; set; } = true;
    public string? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}