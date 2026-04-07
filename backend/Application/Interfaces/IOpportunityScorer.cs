using backend.Domain.Entities;

namespace backend.Application.Interfaces;

public interface IOpportunityScorer
{
    int Score(JobOffer job);
}