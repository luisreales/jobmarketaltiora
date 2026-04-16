namespace backend.Infrastructure.Services;

public enum IndustryType
{
    Unknown = 0,
    Fintech = 1,
    Health = 2,
    Ecommerce = 3,
    Logistics = 4,
    SaaS = 5,
    Media = 6,
    Insurance = 7,
    Education = 8,
    Retail = 9,
    Telecom = 10,
    Government = 11,
}

/// <summary>
/// Infers industry from job title, company name, and description.
/// Used to enrich JobInsight with an Industry field that improves cluster commercial relevance
/// and feeds the BuyingPowerScore in the Opportunity Engine.
/// </summary>
public sealed class IndustryClassifier
{
    // Buying power by industry (0–100). Used by Opportunity Engine (Fase 2).
    private static readonly Dictionary<IndustryType, int> BuyingPower = new()
    {
        { IndustryType.Fintech,     100 },
        { IndustryType.Health,       90 },
        { IndustryType.Ecommerce,    85 },
        { IndustryType.Logistics,    80 },
        { IndustryType.Insurance,    80 },
        { IndustryType.SaaS,         75 },
        { IndustryType.Telecom,      70 },
        { IndustryType.Retail,       65 },
        { IndustryType.Media,        60 },
        { IndustryType.Education,    55 },
        { IndustryType.Government,   45 },
        { IndustryType.Unknown,      50 },
    };

    private static readonly (IndustryType Industry, string[] Signals)[] Rules =
    [
        (IndustryType.Fintech, [
            "fintech", "bank", "banking", "financial", "payment", "payments", "trading",
            "investment", "insurance", "mortgage", "lending", "credit", "wealth", "crypto",
            "blockchain", "defi", "neobank", "wallet", "remittance", "forex"
        ]),
        (IndustryType.Health, [
            "health", "healthcare", "medical", "hospital", "clinic", "pharma", "biotech",
            "telemedicine", "ehr", "emr", "patient", "clinical", "diagnostic", "radiology",
            "dental", "surgery", "nursing", "wellness", "medtech"
        ]),
        (IndustryType.Ecommerce, [
            "ecommerce", "e-commerce", "marketplace", "retail", "shopify", "checkout",
            "cart", "fulfillment", "catalog", "product listing", "merchant", "b2c", "d2c",
            "direct to consumer", "online store", "magento", "woocommerce"
        ]),
        (IndustryType.Logistics, [
            "logistics", "supply chain", "shipping", "freight", "warehouse", "fleet",
            "delivery", "tracking", "last mile", "carrier", "transportation", "dispatch",
            "inventory management", "3pl", "distribution center"
        ]),
        (IndustryType.Insurance, [
            "insurance", "insurer", "underwriting", "claims", "actuarial", "reinsurance",
            "policy management", "broker", "insuretech"
        ]),
        (IndustryType.SaaS, [
            "saas", "software as a service", "platform", "subscription", "multi-tenant",
            "cloud-native", "b2b software", "enterprise software", "api platform", "crm",
            "erp", "hris", "hr platform", "accounting software", "project management tool"
        ]),
        (IndustryType.Media, [
            "media", "streaming", "content", "broadcast", "publishing", "entertainment",
            "gaming", "video", "music", "podcast", "ott", "vod", "news", "social network"
        ]),
        (IndustryType.Education, [
            "education", "edtech", "learning", "lms", "university", "school", "e-learning",
            "curriculum", "student", "academic", "training platform", "bootcamp"
        ]),
        (IndustryType.Telecom, [
            "telecom", "telecommunications", "telco", "carrier", "mobile network",
            "5g", "isp", "internet service", "fiber", "broadband", "mvno"
        ]),
        (IndustryType.Government, [
            "government", "public sector", "ministry", "federal", "municipality",
            "civil service", "public administration", "ngo", "non-profit", "nonprofit"
        ]),
        (IndustryType.Retail, [
            "retail", "supermarket", "grocery", "fashion", "apparel", "luxury", "pos",
            "point of sale", "brick and mortar", "omnichannel"
        ]),
    ];

    /// <summary>
    /// Classifies industry from a combined text (title + company + description snippet).
    /// Returns the first matching industry in priority order, or Unknown.
    /// </summary>
    public IndustryType Classify(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return IndustryType.Unknown;
        }

        var lower = text.ToLowerInvariant();

        foreach (var (industry, signals) in Rules)
        {
            if (signals.Any(signal => lower.Contains(signal, StringComparison.Ordinal)))
            {
                return industry;
            }
        }

        return IndustryType.Unknown;
    }

    /// <summary>Returns the canonical string name used for storage and display.</summary>
    public static string Label(IndustryType industry) => industry.ToString();

    /// <summary>Returns the buying power score (0–100) for use in BlueOceanScore.</summary>
    public static int GetBuyingPower(IndustryType industry) =>
        BuyingPower.TryGetValue(industry, out var score) ? score : 50;

    public static int GetBuyingPower(string industryLabel)
    {
        return Enum.TryParse<IndustryType>(industryLabel, ignoreCase: true, out var industry)
            ? GetBuyingPower(industry)
            : 50;
    }
}
