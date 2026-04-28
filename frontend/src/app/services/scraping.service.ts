import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface ScrapeRequest {
  query: string;
  location: string;
  limit: number;
  providers: string[];
  totalPaging?: number;
  startPage?: number;
  endPage?: number;
  showBrowser?: boolean;
}

export interface ScrapeResult {
  savedCount: number;
  totalFound: number;
  timestamp: string;
}

@Injectable({
  providedIn: 'root'
})
export class ScrapingService {
  private apiUrl = `${environment.apiUrl}/api/jobs`;

  constructor(private http: HttpClient) {}

  scrapeLinkedIn(request: ScrapeRequest): Observable<ScrapeResult> {
    return this.http.post<ScrapeResult>(`${this.apiUrl}/search/scrape`, request);
  }

  scrapeUpwork(request: ScrapeRequest): Observable<ScrapeResult> {
    return this.http.post<ScrapeResult>(`${this.apiUrl}/search/scrape/upwork/login-and-scrape`, request);
  }

  scrapeMultiProvider(request: ScrapeRequest): Observable<ScrapeResult> {
    return this.http.post<ScrapeResult>(`${this.apiUrl}/search/scrape`, request);
  }
}
