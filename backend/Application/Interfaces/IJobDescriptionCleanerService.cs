namespace backend.Application.Interfaces;

public interface IJobDescriptionCleanerService
{
    CleanedJobDescription Clean(string rawDescription, string companyName = "");
}

public sealed record CleanedJobDescription(
    string CleanText,
    bool IsConsulting,
    int OriginalLength,
    int CleanedLength);
