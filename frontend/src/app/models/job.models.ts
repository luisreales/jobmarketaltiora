export interface LinkedInAuthStatus {
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
  location: string;
  limit: number;
}

export interface LinkedInSearchResponse {
  savedCount: number;
  totalFound: number;
  executedAtUtc: string;
}
