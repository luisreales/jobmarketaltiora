import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { JobsQueryRequest, LinkedInJobSummary } from '../../models/job.models';
import { JobsService } from '../../services/jobs.service';

@Component({
  selector: 'app-dashboard',
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class Dashboard implements OnInit {
  readonly salaryCap = 200000;
  readonly minSalaryGap = 1000;
  readonly pageSize = 20;
  private readonly stateStorageKey = 'jobs.dashboard.state.v1';

  loading = false;
  jobs: LinkedInJobSummary[] = [];

  searchText = '';
  searchLocation = '';
  selectedProvider: 'all' | 'linkedin' | 'upwork' = 'all';
  selectedSort: 'Latest Post' | 'Oldest Post' = 'Latest Post';

  minSalary = 0;
  maxSalary = this.salaryCap;

  page = 1;
  totalPages = 1;
  totalCount = 0;
  private hasCompletedInitialLoad = false;

  private readonly jobsService = inject(JobsService);

  ngOnInit(): void {
    const restoredFromQuery = this.restoreStateFromQueryParams();
    if (!restoredFromQuery) {
      this.restoreStateFromSession();
    }

    this.fetchJobs();
  }

  get minSalaryPercent(): number {
    return (this.minSalary / this.salaryCap) * 100;
  }

  get maxSalaryPercent(): number {
    return (this.maxSalary / this.salaryCap) * 100;
  }

  get activeSalaryRangePercent(): number {
    return Math.max(0, this.maxSalaryPercent - this.minSalaryPercent);
  }

  get searchResultTitle(): string {
    const base = this.searchText.trim() || 'all jobs';
    return `${this.totalCount} search result of '${base}'`;
  }

  onSearch(): void {
    this.page = 1;
    this.persistStateToSession();
    this.syncStateToQueryParams();
    this.fetchJobs();
  }

  onProviderChange(provider: 'all' | 'linkedin' | 'upwork'): void {
    this.selectedProvider = provider;
    this.page = 1;
    this.persistStateToSession();
    this.syncStateToQueryParams();
    this.fetchJobs();
  }

  onSortChange(sort: 'Latest Post' | 'Oldest Post'): void {
    this.selectedSort = sort;
    this.page = 1;
    this.persistStateToSession();
    this.syncStateToQueryParams();
    this.fetchJobs();
  }

  onMinSalaryChange(value: number): void {
    this.minSalary = Math.min(value, this.maxSalary - this.minSalaryGap);

    if (!this.hasCompletedInitialLoad) {
      return;
    }

    this.page = 1;
    this.persistStateToSession();
    this.syncStateToQueryParams();
    this.fetchJobs();
  }

  onMaxSalaryChange(value: number): void {
    this.maxSalary = Math.max(value, this.minSalary + this.minSalaryGap);

    if (!this.hasCompletedInitialLoad) {
      return;
    }

    this.page = 1;
    this.persistStateToSession();
    this.syncStateToQueryParams();
    this.fetchJobs();
  }

  previousPage(): void {
    if (this.page > 1) {
      this.page -= 1;
      this.persistStateToSession();
      this.syncStateToQueryParams();
      this.fetchJobs();
    }
  }

  nextPage(): void {
    if (this.page < this.totalPages) {
      this.page += 1;
      this.persistStateToSession();
      this.syncStateToQueryParams();
      this.fetchJobs();
    }
  }

  goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages) {
      this.page = page;
      this.persistStateToSession();
      this.syncStateToQueryParams();
      this.fetchJobs();
    }
  }

  get pageRange(): number[] {
    return Array.from({ length: this.totalPages }, (_, index) => index + 1);
  }

  formatCurrency(value: number): string {
    return `$ ${new Intl.NumberFormat('de-DE').format(value)}`;
  }

  trackByJobId(_: number, job: LinkedInJobSummary): number {
    return job.id;
  }

  private restoreStateFromQueryParams(): boolean {
    if (typeof window === 'undefined') {
      return false;
    }

    const params = new URLSearchParams(window.location.search);
    let restored = false;

    const pageRaw = params.get('page');
    if (pageRaw !== null) {
      const pageParam = Number(pageRaw);
      if (Number.isFinite(pageParam) && pageParam > 0) {
        this.page = pageParam;
        restored = true;
      }
    }

    const searchParam = params.get('search');
    if (searchParam) {
      this.searchText = searchParam;
      restored = true;
    }

    const locationParam = params.get('location');
    if (locationParam) {
      this.searchLocation = locationParam;
      restored = true;
    }

    const sourceParam = params.get('source');
    if (sourceParam === 'linkedin' || sourceParam === 'upwork' || sourceParam === 'all') {
      this.selectedProvider = sourceParam;
      restored = true;
    }

    const sortParam = params.get('sort');
    if (sortParam === 'oldest') {
      this.selectedSort = 'Oldest Post';
      restored = true;
    } else if (sortParam === 'latest') {
      this.selectedSort = 'Latest Post';
      restored = true;
    }

    const minSalaryRaw = params.get('minSalary');
    if (minSalaryRaw !== null) {
      const minSalaryParam = Number(minSalaryRaw);
      if (Number.isFinite(minSalaryParam) && minSalaryParam >= 0 && minSalaryParam <= this.salaryCap) {
        this.minSalary = minSalaryParam;
        restored = true;
      }
    }

    const maxSalaryRaw = params.get('maxSalary');
    if (maxSalaryRaw !== null) {
      const maxSalaryParam = Number(maxSalaryRaw);
      if (Number.isFinite(maxSalaryParam) && maxSalaryParam >= 0 && maxSalaryParam <= this.salaryCap) {
        this.maxSalary = maxSalaryParam;
        restored = true;
      }
    }

    if (this.maxSalary - this.minSalary < this.minSalaryGap) {
      this.maxSalary = Math.min(this.salaryCap, this.minSalary + this.minSalaryGap);
      this.minSalary = Math.max(0, this.maxSalary - this.minSalaryGap);
    }

    return restored;
  }

  private restoreStateFromSession(): void {
    if (typeof window === 'undefined') {
      return;
    }

    const raw = window.sessionStorage.getItem(this.stateStorageKey);
    if (!raw) {
      return;
    }

    try {
      const state = JSON.parse(raw) as {
        page?: number;
        searchText?: string;
        searchLocation?: string;
        selectedProvider?: 'all' | 'linkedin' | 'upwork';
        selectedSort?: 'Latest Post' | 'Oldest Post';
        minSalary?: number;
        maxSalary?: number;
      };

      if (typeof state.page === 'number' && state.page > 0) {
        this.page = state.page;
      }

      if (typeof state.searchText === 'string') {
        this.searchText = state.searchText;
      }

      if (typeof state.searchLocation === 'string') {
        this.searchLocation = state.searchLocation;
      }

      if (state.selectedProvider === 'all' || state.selectedProvider === 'linkedin' || state.selectedProvider === 'upwork') {
        this.selectedProvider = state.selectedProvider;
      }

      if (state.selectedSort === 'Latest Post' || state.selectedSort === 'Oldest Post') {
        this.selectedSort = state.selectedSort;
      }

      if (typeof state.minSalary === 'number' && state.minSalary >= 0 && state.minSalary <= this.salaryCap) {
        this.minSalary = state.minSalary;
      }

      if (typeof state.maxSalary === 'number' && state.maxSalary >= 0 && state.maxSalary <= this.salaryCap) {
        this.maxSalary = state.maxSalary;
      }

      if (this.maxSalary - this.minSalary < this.minSalaryGap) {
        this.maxSalary = Math.min(this.salaryCap, this.minSalary + this.minSalaryGap);
        this.minSalary = Math.max(0, this.maxSalary - this.minSalaryGap);
      }
    } catch {
      window.sessionStorage.removeItem(this.stateStorageKey);
    }
  }

  private persistStateToSession(): void {
    if (typeof window === 'undefined') {
      return;
    }

    const state = {
      page: this.page,
      searchText: this.searchText,
      searchLocation: this.searchLocation,
      selectedProvider: this.selectedProvider,
      selectedSort: this.selectedSort,
      minSalary: this.minSalary,
      maxSalary: this.maxSalary
    };

    window.sessionStorage.setItem(this.stateStorageKey, JSON.stringify(state));
  }

  private syncStateToQueryParams(): void {
    if (typeof window === 'undefined') {
      return;
    }

    const params = new URLSearchParams(window.location.search);

    if (this.page > 1) {
      params.set('page', String(this.page));
    } else {
      params.delete('page');
    }

    const search = this.searchText.trim();
    if (search) {
      params.set('search', search);
    } else {
      params.delete('search');
    }

    const location = this.searchLocation.trim();
    if (location) {
      params.set('location', location);
    } else {
      params.delete('location');
    }

    if (this.selectedProvider !== 'all') {
      params.set('source', this.selectedProvider);
    } else {
      params.delete('source');
    }

    if (this.selectedSort === 'Oldest Post') {
      params.set('sort', 'oldest');
    } else {
      params.delete('sort');
    }

    if (this.minSalary > 0) {
      params.set('minSalary', String(this.minSalary));
    } else {
      params.delete('minSalary');
    }

    if (this.maxSalary < this.salaryCap) {
      params.set('maxSalary', String(this.maxSalary));
    } else {
      params.delete('maxSalary');
    }

    const query = params.toString();
    const nextUrl = `${window.location.pathname}${query ? `?${query}` : ''}`;
    window.history.replaceState(window.history.state, '', nextUrl);
  }

  private fetchJobs(): void {
    const request: JobsQueryRequest = {
      page: this.page,
      pageSize: this.pageSize,
      sortBy: 'capturedAt',
      sortDirection: this.selectedSort === 'Latest Post' ? 'desc' : 'asc',
      search: this.searchText.trim() || undefined,
      location: this.searchLocation.trim() || undefined,
      source: this.selectedProvider === 'all' ? undefined : this.selectedProvider,
      minSalary: this.minSalary > 0 ? this.minSalary : undefined,
      maxSalary: this.maxSalary < this.salaryCap ? this.maxSalary : undefined
    };

    this.loading = true;

    this.jobsService.queryJobs(request).subscribe({
      next: (response) => {
        this.jobs = response.items;
        this.totalCount = response.totalCount;
        this.totalPages = Math.max(1, response.totalPages);
        this.page = response.page;
      },
      error: () => {
        this.jobs = [];
        this.totalCount = 0;
        this.totalPages = 1;
      },
      complete: () => {
        this.loading = false;
        this.hasCompletedInitialLoad = true;
      }
    });
  }
}
