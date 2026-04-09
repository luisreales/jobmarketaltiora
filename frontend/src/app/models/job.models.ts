export interface LinkedInAuthStatus {
  provider?: string;
  isAuthenticated: boolean;
  lastLoginAt: string | null;
  lastUsedAt: string | null;
  expiresAt: string | null;
}

export interface LinkedInJobSummary {
  id: number;
  title: string;
  company: string;
  location: string;
  descriptionPreview?: string;
  category?: string;
  opportunityScore?: number;
  isConsultingCompany?: boolean;
  companyType?: string;
  source: string;
  searchTerm: string;
  capturedAt: string;
  publishedAt: string | null;
  salaryRange: string | null;
  seniority: string | null;
  contractType: string | null;
  url: string;
}

export interface LinkedInJobDetail {
  id: number;
  externalId: string;
  title: string;
  company: string;
  location: string;
  description: string;
  url: string;
  contact: string | null;
  salaryRange: string | null;
  publishedAt: string | null;
  seniority: string | null;
  contractType: string | null;
  source: string;
  searchTerm: string;
  capturedAt: string;
  metadataJson: string | null;
}

export interface LinkedInSearchRequest {
  query: string;
  location?: string;
  limit?: number;
  totalPaging?: number;
  startPage?: number;
  endPage?: number;
}

export interface LinkedInSearchResponse {
  savedCount: number;
  totalFound: number;
  executedAtUtc: string;
}

export interface UpworkTouchedJob {
  id: number;
  title: string;
  company: string;
  descriptionPreview?: string;
  url: string;
  searchTerm: string;
  capturedAt: string;
  detailEndpoint?: string;
}

export interface UpworkScrapeResponse {
  provider: string;
  savedCount: number;
  totalFound: number;
  touchedCount?: number;
  touched: UpworkTouchedJob[];
  executedAtUtc: string;
}

export interface JobsQueryRequest {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
  title?: string;
  company?: string;
  location?: string;
  source?: string;
  searchTerm?: string;
  salaryRange?: string;
}

export interface PagedJobSummaryResponse {
  items: LinkedInJobSummary[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  sortBy: string;
  sortDirection: 'asc' | 'desc';
}
