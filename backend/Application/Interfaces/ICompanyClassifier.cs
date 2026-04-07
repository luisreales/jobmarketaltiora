using backend.Application.Contracts;

namespace backend.Application.Interfaces;

public interface ICompanyClassifier
{
    CompanyClassificationResult Classify(string companyName, string description);
}
