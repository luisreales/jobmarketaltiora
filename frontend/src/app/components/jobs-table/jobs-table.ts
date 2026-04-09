import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnDestroy, OnInit, Output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { JobsQueryRequest, LinkedInJobSummary } from '../../models/job.models';

type FilterKey = 'title' | 'company' | 'location' | 'source' | 'searchTerm' | 'salaryRange';

@Component({
  selector: 'app-jobs-table',
  imports: [CommonModule, RouterLink],
  templateUrl: './jobs-table.html',
  styleUrl: './jobs-table.scss'
})
export class JobsTableComponent implements OnInit, OnDestroy {
  @Input() jobs: LinkedInJobSummary[] = [];
  @Input() totalCount = 0;
  @Input() loading = false;

  @Output() readonly queryChange = new EventEmitter<JobsQueryRequest>();

  readonly pageSizes = Array.from({ length: 20 }, (_, index) => index + 1);
  pageSize = 20;
  pageIndex = 1;

  sortColumn: keyof LinkedInJobSummary | 'capturedAt' = 'capturedAt';
  sortDirection: 'asc' | 'desc' = 'desc';

  filters: Record<FilterKey, string> = {
    title: '',
    company: '',
    location: '',
    source: '',
    searchTerm: '',
    salaryRange: ''
  };

  readonly filterDefinitions: Array<{ key: FilterKey; label: string; placeholder: string }> = [
    { key: 'title', label: 'Title', placeholder: 'Filter by title' },
    { key: 'company', label: 'Company', placeholder: 'Filter by company' },
    { key: 'location', label: 'Location', placeholder: 'Filter by location' },
    { key: 'source', label: 'Source', placeholder: 'linkedin / upwork' },
    { key: 'searchTerm', label: 'Search Term', placeholder: 'dotnet, angular...' },
    { key: 'salaryRange', label: 'Salary', placeholder: 'USD or range' }
  ];

  private debounceTimer: ReturnType<typeof setTimeout> | null = null;

  ngOnInit(): void {
    this.emitQuery();
  }

  ngOnDestroy(): void {
    if (this.debounceTimer) {
      clearTimeout(this.debounceTimer);
      this.debounceTimer = null;
    }
  }

  updateFilter(key: FilterKey, value: string): void {
    this.filters[key] = value;
    this.pageIndex = 1;
    this.scheduleEmitQuery();
  }

  clearFilters(): void {
    for (const key of Object.keys(this.filters) as FilterKey[]) {
      this.filters[key] = '';
    }

    this.pageIndex = 1;
    this.emitQuery();
  }

  toggleSort(column: keyof LinkedInJobSummary | 'capturedAt'): void {
    if (this.sortColumn === column) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
      this.pageIndex = 1;
      this.emitQuery();
      return;
    }

    this.sortColumn = column;
    this.sortDirection = column === 'capturedAt' ? 'desc' : 'asc';
    this.pageIndex = 1;
    this.emitQuery();
  }

  changePageSize(value: number): void {
    const parsed = Number(value);
    this.pageSize = Number.isFinite(parsed) ? Math.min(Math.max(parsed, 1), 20) : 20;
    this.pageIndex = 1;
    this.emitQuery();
  }

  previousPage(): void {
    if (this.pageIndex > 1) {
      this.pageIndex -= 1;
      this.emitQuery();
    }
  }

  nextPage(): void {
    if (this.pageIndex < this.totalPages) {
      this.pageIndex += 1;
      this.emitQuery();
    }
  }

  goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages) {
      this.pageIndex = page;
      this.emitQuery();
    }
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  get pageRange(): number[] {
    return Array.from({ length: this.totalPages }, (_, index) => index + 1);
  }

  private scheduleEmitQuery(): void {
    if (this.debounceTimer) {
      clearTimeout(this.debounceTimer);
    }

    this.debounceTimer = setTimeout(() => {
      this.emitQuery();
      this.debounceTimer = null;
    }, 300);
  }

  private emitQuery(): void {
    this.queryChange.emit({
      page: this.pageIndex,
      pageSize: this.pageSize,
      sortBy: this.sortColumn,
      sortDirection: this.sortDirection,
      title: this.filters.title.trim() || undefined,
      company: this.filters.company.trim() || undefined,
      location: this.filters.location.trim() || undefined,
      source: this.filters.source.trim() || undefined,
      searchTerm: this.filters.searchTerm.trim() || undefined,
      salaryRange: this.filters.salaryRange.trim() || undefined
    });
  }
}
