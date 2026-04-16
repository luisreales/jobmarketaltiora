namespace backend.Infrastructure.Services;

public sealed class MarketIntelligenceEnrichmentOptions
{
    public bool UseSemanticKernel { get; set; }
    public bool ShadowMode { get; set; } = true;
    public int SamplingRatePercent { get; set; } = 20;
    public int MinDescriptionLength { get; set; } = 120;
    public int MaxCallsPerDay { get; set; } = 200;
    public int CacheHours { get; set; } = 168;
    public string PromptVersion { get; set; } = "v1";
    public string PromptTemplate { get; set; } = "Act as a CTO and B2B sales strategist for AltioraTech. Analyze the technical hiring context and infer the underlying technical pain and monetizable service opportunity. Return ONLY valid JSON with keys: pain_point, business_opportunity, altioratech_offer, confidence. Context: {{TechnicalContext}}";
    public bool SkipConsultingCompaniesForLlm { get; set; } = true;
    public string[] ConsultingCompanyBlacklist { get; set; } =
    {
        "Capitole",
        "Globant",
        "NTT Data",
        "TCS",
        "Accenture",
        "Capgemini",
        "Cognizant",
        "EPAM",
        "Stefanini",
        "Softtek"
    };
    public string[] FluffCutMarkers { get; set; } =
    {
        "que ofrecemos",
        "qué ofrecemos",
        "beneficios",
        "acerca de nosotros",
        "about us",
        "somos",
        "ubicacion",
        "ubicación",
        "horario",
        "relocalizacion",
        "relocalización"
    };
}
