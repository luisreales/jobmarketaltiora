export interface MarketOpportunityQuery {
  fromDate?: string;
  toDate?: string;
  source?: string;
  minOpportunityScore?: number;
  minUrgencyScore?: number;
  page?: number;
  pageSize?: number;
}

export interface MarketLeadsQuery {
  painPoint?: string;
  source?: string;
  minScore?: number;
  page?: number;
  pageSize?: number;
}

export interface MarketTrendsQuery {
  windowDays?: number;
  source?: string;
}

export interface MarketOpportunity {
  painPoint: string;
  painCategory: string;
  opportunityCount: number;
  avgOpportunityScore: number;
  avgUrgencyScore: number;
  topTechStack: string;
  suggestedMvp: string;
}

export interface MarketLead {
  jobId: number;
  company: string;
  title: string;
  painPoint: string;
  opportunityScore: number;
  urgencyScore: number;
  suggestedSolution: string;
  leadMessage: string;
  source: string;
  url: string;
  capturedAt: string;
}

export interface MarketTrend {
  painCategory: string;
  currentCount: number;
  previousCount: number;
  trendPercentage: number;
}

export interface PagedMarketResponse<TItem> {
  items: TItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  sortBy: string;
  sortDirection: 'asc' | 'desc';
}

// ── Cluster models (Fase 1–4) ──────────────────────────────────────────────────

export interface MarketClusterQuery {
  minBlueOceanScore?: number;
  painCategory?: string;
  industry?: string;
  opportunityType?: string;
  isActionable?: boolean;
  page?: number;
  pageSize?: number;
}

export interface ClusterLeadsQuery {
  minLeadScore?: number;
  page?: number;
  pageSize?: number;
}

export interface MarketCluster {
  id: number;
  label: string;
  painCategory: string;
  industry: string;
  companyType: string;
  normalizedTechStack: string;

  // Market signals
  jobCount: number;
  directClientCount: number;
  directClientRatio: number;
  avgOpportunityScore: number;
  avgUrgencyScore: number;
  growthRate: number;

  // Scoring
  blueOceanScore: number;
  roiRank: number;

  // Decision Engine
  opportunityType: 'MVPProduct' | 'QuickWin' | 'Consulting' | 'Ignore';
  isActionable: boolean;
  recommendedStrategy: string;
  priorityScore: number;

  // LLM synthesis
  synthesizedPain?: string;
  synthesizedMvp?: string;
  synthesizedLeadMessage?: string;
  mvpType?: string;
  estimatedBuildDays?: number;
  estimatedDealSizeUsd?: number;
  llmStatus: 'pending' | 'completed' | 'failed' | 'done' | 'needs_review' | 'skipped';

  lastUpdatedAt: string;
}

export interface ClusterLead {
  jobId: number;
  company: string;
  title: string;
  painCategory: string;
  opportunityScore: number;
  urgencyScore: number;
  leadScore: number;
  suggestedSolution: string;
  leadMessage: string;
  isDirectClient: boolean;
  source: string;
  url: string;
  capturedAt: string;
}

export interface ClusterRebuildResult {
  clustersUpserted: number;
  clustersEvaluated: number;
  actionableClusters: number;
  ranAt: string;
}

// ── Product Generator models ───────────────────────────────────────────────────

export interface ProductQuery {
  opportunityType?: string;
  industry?: string;
  page?: number;
  pageSize?: number;
}

export interface ProductSuggestion {
  id: number;
  productName: string;
  productDescription: string;
  whyNow: string;
  offer: string;
  actionToday: string;
  techFocus: string;
  estimatedBuildDays: number;
  minDealSizeUsd: number;
  maxDealSizeUsd: number;
  // Aggregated market signals
  totalJobCount: number;
  avgDirectClientRatio: number;
  avgUrgencyScore: number;
  topBlueOceanScore: number;
  clusterCount: number;
  // Decision
  priorityScore: number;
  opportunityType: 'MVPProduct' | 'QuickWin' | 'Consulting';
  industry: string;
  // LLM
  synthesisDetailJson?: string | null;
  llmStatus: 'pending' | 'completed' | 'failed';
  generatedAt: string;
}

export interface ProductGenerateResult {
  productsGenerated: number;
  actionableClusters: number;
  ranAt: string;
}
