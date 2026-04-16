namespace backend.Application.Interfaces;

public interface IOpportunityScorerService
{
    int CalculateOpportunityScore(string normalizedText, int urgencyScore, bool isConsultingCompany);
}
