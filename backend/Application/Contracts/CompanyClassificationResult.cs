namespace backend.Application.Contracts;

public sealed class CompanyClassificationResult
{
    public bool IsConsultingCompany { get; set; }
    public string CompanyType { get; set; } = "Unknown";
}
