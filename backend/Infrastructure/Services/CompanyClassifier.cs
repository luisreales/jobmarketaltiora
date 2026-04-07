using backend.Application.Contracts;
using backend.Application.Interfaces;

namespace backend.Infrastructure.Services;

public sealed class CompanyClassifier : ICompanyClassifier
{
    private static readonly string[] ConsultingCompanies =
    [
        "softtek",
        "capgemini",
        "stefanini",
        "globant",
        "accenture",
        "cognizant",
        "epam",
        "epam systems"
    ];

    private static readonly string[] ConsultingKeywords =
    [
        "clients",
        "outsourcing",
        "staff augmentation",
        "for our client",
        "third-party projects"
    ];

    public CompanyClassificationResult Classify(string companyName, string description)
    {
        var normalizedCompany = Normalize(companyName);
        var normalizedDescription = Normalize(description);

        if (!string.IsNullOrWhiteSpace(normalizedCompany) &&
            ConsultingCompanies.Any(x => normalizedCompany.Contains(x, StringComparison.Ordinal)))
        {
            return new CompanyClassificationResult
            {
                IsConsultingCompany = true,
                CompanyType = "Consulting"
            };
        }

        if (!string.IsNullOrWhiteSpace(normalizedDescription) &&
            ConsultingKeywords.Any(x => normalizedDescription.Contains(x, StringComparison.Ordinal)))
        {
            return new CompanyClassificationResult
            {
                IsConsultingCompany = true,
                CompanyType = "Consulting"
            };
        }

        if (string.IsNullOrWhiteSpace(normalizedCompany) && string.IsNullOrWhiteSpace(normalizedDescription))
        {
            return new CompanyClassificationResult
            {
                IsConsultingCompany = false,
                CompanyType = "Unknown"
            };
        }

        return new CompanyClassificationResult
        {
            IsConsultingCompany = false,
            CompanyType = "DirectClient"
        };
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }
}
