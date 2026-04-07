namespace backend.Domain.Entities;

public class ProviderSession
{
    public int Id { get; set; }
    public string Provider { get; set; } = "linkedin";
    public bool IsAuthenticated { get; set; }
    public string Username { get; set; } = string.Empty;
    public DateTime? LastLoginAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
