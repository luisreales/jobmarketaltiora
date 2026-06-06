export interface CompanyProfileDto {
  id: number;
  companyName: string;
  companyType: string;
  primaryIndustry: string;
  techStack: string[];
  topPainCategory: string;
  totalJobCount: number;
  avgUrgencyScore: number;
  avgOpportunityScore: number;
  avgLeadScore: number;
  hiringVelocity: number;
  isDirectClient: boolean;
  hasAiInitiative: boolean;
  hasCloudMigration: boolean;
  prospectScore: number;
  firstSeenAt: string;
  lastSeenAt: string;
}

export interface CompanyRebuildResultDto {
  companiesUpserted: number;
  jobsProcessed: number;
  duration: string;
  ranAt: string;
}
