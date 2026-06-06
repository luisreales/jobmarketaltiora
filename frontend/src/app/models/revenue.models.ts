export interface FunnelStatsDto {
  totalJobs: number;
  analyzedJobs: number;
  clusteredInsights: number;
  actionableClusters: number;
  synthesizedClusters: number;
  productsGenerated: number;
  productsOpen: number;
}

export interface ServiceModelRevenueDto {
  serviceModel: string;
  clusterCount: number;
  weightedValueUsd: number;
  avgCloseProbability: number;
}

export interface IndustryRevenueDto {
  industry: string;
  tamMillionsUsd: number;
  clusterCount: number;
  avgCloseProbability: number;
  estimatedValueUsd: number;
}

export interface TopOpportunityDto {
  clusterId: number;
  label: string;
  painCategory: string;
  industry: string;
  serviceModel: string;
  estimatedDealSizeUsd: number;
  closeProbability: number;
  expectedValueUsd: number;
  blueOceanScore: number;
  buyingIntentScore: number;
  jobCount: number;
  hasProduct: boolean;
}

export interface RevenueSummaryDto {
  totalPipelineValueUsd: number;
  weightedExpectedRevenueUsd: number;
  totalActionableClusters: number;
  totalProducts: number;
  openProducts: number;
  avgCloseProbability: number;
  avgBlueOceanScore: number;
  conversionFunnel: FunnelStatsDto;
  byServiceModel: ServiceModelRevenueDto[];
  byIndustry: IndustryRevenueDto[];
  topOpportunities: TopOpportunityDto[];
}

export interface SalesStatusUpdateDto {
  salesStatus: string;
  wonDealSizeUsd?: number;
  salesNotes?: string;
}

export function serviceModelColor(model: string): string {
  const map: Record<string, string> = {
    'SaaS MVP': 'bg-indigo-100 text-indigo-700 border-indigo-200',
    'Fixed-Price Sprint': 'bg-green-100 text-green-700 border-green-200',
    'Retainer': 'bg-sky-100 text-sky-700 border-sky-200',
    'Consulting': 'bg-amber-100 text-amber-700 border-amber-200',
  };
  return map[model] ?? 'bg-slate-100 text-slate-600 border-slate-200';
}
