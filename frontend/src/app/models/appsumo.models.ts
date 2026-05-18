export interface AppSumoStats {
  categories: number;
  products: number;
  scraped: number;
  reviews: number;
  byRating: { rating: number; count: number }[];
}

export interface AppSumoCategoryDto {
  id: number;
  name: string;
  slug: string;
  url: string;
  parentSlug: string | null;
  productCount: number;
  scrapedAt: string | null;
}

export interface AppSumoProductDto {
  id: number;
  categoryId: number;
  categoryName: string;
  name: string;
  slug: string;
  url: string;
  description: string | null;
  overallRating: number | null;
  totalReviewCount: number | null;
  pricingModel: string | null;
  scrapeStatus: string;
  lowRatingReviewCount: number;
  scrapedAt: string | null;
}

export interface AppSumoReviewDto {
  id: number;
  productId: number;
  productName: string;
  categoryName: string;
  appSumoReviewId: string | null;
  tacoRating: number;
  reviewerName: string | null;
  reviewDate: string | null;
  reviewText: string;
  foundHelpful: number | null;
  isVerified: boolean;
  createdAt: string;
}

export interface AppSumoScrapeRunDto {
  id: number;
  startedAt: string;
  finishedAt: string | null;
  status: string;
  productsScraped: number;
  reviewsSaved: number;
  errorCount: number;
  notes: string | null;
}

export interface AppSumoProductPagedResult {
  items: AppSumoProductDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface AppSumoReviewPagedResult {
  items: AppSumoReviewDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface AppSumoProductQuery {
  categoryId?: number;
  search?: string;
  scrapeStatus?: string;
  page?: number;
  pageSize?: number;
}

export interface AppSumoReviewQuery {
  productId?: number;
  categoryId?: number;
  tacoRating?: number;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface StartScrapeRequest {
  startCategorySlug?: string | null;
  dryRun?: boolean;
  maxProducts?: number;
}
