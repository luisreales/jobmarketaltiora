import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ScrapingService } from '../../services/scraping.service';

interface ScrapeRequest {
  query: string;
  location: string;
  limit: number;
  providers: string[];
  totalPaging?: number;
  startPage?: number;
  endPage?: number;
  showBrowser?: boolean;
}

interface ScrapeResult {
  savedCount: number;
  totalFound: number;
  timestamp: string;
}

@Component({
  selector: 'app-scraping',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './scraping.html',
  styleUrls: ['./scraping.css']
})
export class ScrapingComponent implements OnInit, OnDestroy {
  // LinkedIn scraping
  linkedinQuery: string = '.NET';
  linkedinLocation: string = 'Remote';
  linkedinLimit: number = 500;
  linkedinStartPage: number = 1;
  linkedinEndPage: number = 20;
  linkedinProviders: string[] = ['linkedin'];
  isLinkedInScraping: boolean = false;
  linkedinResult: ScrapeResult | null = null;
  linkedinError: string | null = null;

  // Upwork scraping
  upworkQuery: string = '.NET';
  upworkLocation: string = 'Remote';
  upworkLimit: number = 20;
  upworkProviders: string[] = ['upwork'];
  isUpworkScraping: boolean = false;
  upworkResult: ScrapeResult | null = null;
  upworkError: string | null = null;

  // Multi-provider
  multiQuery: string = '.NET';
  multiLocation: string = 'Remote';
  multiLimit: number = 20;
  selectedProviders: string[] = [];
  isMultiScraping: boolean = false;
  multiResult: ScrapeResult | null = null;
  multiError: string | null = null;

  availableProviders = ['linkedin', 'indeed', 'upwork'];

  constructor(private scrapingService: ScrapingService) {}

  ngOnInit(): void {
    this.selectedProviders = [...this.linkedinProviders];
  }

  ngOnDestroy(): void {
    // Cleanup if needed
  }

  onProviderToggle(provider: string): void {
    if (this.selectedProviders.includes(provider)) {
      this.selectedProviders = this.selectedProviders.filter(p => p !== provider);
    } else {
      this.selectedProviders.push(provider);
    }
  }

  isProviderSelected(provider: string): boolean {
    return this.selectedProviders.includes(provider);
  }

  async scrapeLinkedIn(): Promise<void> {
    if (!this.linkedinQuery.trim()) {
      this.linkedinError = 'Please enter a search query';
      return;
    }

    this.isLinkedInScraping = true;
    this.linkedinError = null;
    this.linkedinResult = null;

    try {
      const request: ScrapeRequest = {
        query: this.linkedinQuery,
        location: this.linkedinLocation,
        limit: this.linkedinLimit,
        startPage: this.linkedinStartPage,
        endPage: this.linkedinEndPage,
        providers: this.linkedinProviders,
      };

      const result = await this.scrapingService.scrapeLinkedIn(request).toPromise();
      this.linkedinResult = result || null;
    } catch (error: any) {
      this.linkedinError = error?.error?.detail || error?.message || 'Error scraping LinkedIn';
    } finally {
      this.isLinkedInScraping = false;
    }
  }

  async scrapeUpwork(): Promise<void> {
    if (!this.upworkQuery.trim()) {
      this.upworkError = 'Please enter a search query';
      return;
    }

    this.isUpworkScraping = true;
    this.upworkError = null;
    this.upworkResult = null;

    try {
      const request: ScrapeRequest = {
        query: this.upworkQuery,
        location: this.upworkLocation,
        limit: this.upworkLimit,
        providers: this.upworkProviders,
        showBrowser: true,
      };

      const result = await this.scrapingService.scrapeUpwork(request).toPromise();
      this.upworkResult = result || null;
    } catch (error: any) {
      const errorMessage = error?.error?.detail || error?.message || 'Error scraping Upwork';
      if (errorMessage.includes('scraper API is not available') || errorMessage.includes('Timeout connecting')) {
        this.upworkError = 'Upwork scraper service is not running. Please start it with: docker-compose up scraper-api';
      } else {
        this.upworkError = errorMessage;
      }
    } finally {
      this.isUpworkScraping = false;
    }
  }

  async scrapeMultiProvider(): Promise<void> {
    if (!this.multiQuery.trim()) {
      this.multiError = 'Please enter a search query';
      return;
    }

    if (this.selectedProviders.length === 0) {
      this.multiError = 'Please select at least one provider';
      return;
    }

    this.isMultiScraping = true;
    this.multiError = null;
    this.multiResult = null;

    try {
      const request: ScrapeRequest = {
        query: this.multiQuery,
        location: this.multiLocation,
        limit: this.multiLimit,
        providers: this.selectedProviders,
      };

      const result = await this.scrapingService.scrapeMultiProvider(request).toPromise();
      this.multiResult = result || null;
    } catch (error: any) {
      const errorMessage = error?.error?.detail || error?.message || 'Error scraping jobs';
      if (errorMessage.includes('scraper API is not available') || errorMessage.includes('Timeout connecting')) {
        this.multiError = 'Upwork scraper service is not running. Please start it with: docker-compose up scraper-api';
      } else {
        this.multiError = errorMessage;
      }
    } finally {
      this.isMultiScraping = false;
    }
  }

  formatDate(dateString: string): string {
    try {
      const date = new Date(dateString);
      return date.toLocaleString();
    } catch {
      return dateString;
    }
  }
}
