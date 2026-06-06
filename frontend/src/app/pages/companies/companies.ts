import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  OnInit,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CompanyService } from '../../services/company.service';
import { CompanyProfileDto, CompanyRebuildResultDto } from '../../models/company.models';

@Component({
  selector: 'app-companies',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule],
  templateUrl: './companies.html',
})
export class CompaniesPage implements OnInit {
  private readonly svc = inject(CompanyService);
  private readonly cdr = inject(ChangeDetectorRef);

  loading = true;
  rebuilding = false;
  companies: CompanyProfileDto[] = [];
  rebuildResult: CompanyRebuildResultDto | null = null;

  search = '';
  filterIndustry = '';
  filterDirectClient: boolean | undefined = undefined;
  filterAi: boolean | undefined = undefined;
  filterCloudMigration: boolean | undefined = undefined;
  sortBy = 'prospectScore';

  readonly sortOptions = [
    { value: 'prospectScore', label: 'Prospect Score' },
    { value: 'jobCount', label: 'Job Count' },
    { value: 'urgency', label: 'Urgency' },
    { value: 'hiringVelocity', label: 'Hiring Velocity' },
    { value: 'lastSeen', label: 'Last Seen' },
  ];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.cdr.markForCheck();
    this.svc
      .getAll({
        search: this.search || undefined,
        industry: this.filterIndustry || undefined,
        directClient: this.filterDirectClient,
        hasAi: this.filterAi,
        hasCloudMigration: this.filterCloudMigration,
        sortBy: this.sortBy,
        pageSize: 100,
      })
      .subscribe({
        next: (data) => {
          this.companies = data;
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.loading = false;
          this.cdr.markForCheck();
        },
      });
  }

  rebuild(): void {
    this.rebuilding = true;
    this.rebuildResult = null;
    this.cdr.markForCheck();
    this.svc.rebuild().subscribe({
      next: (result) => {
        this.rebuildResult = result;
        this.rebuilding = false;
        this.load();
      },
      error: () => {
        this.rebuilding = false;
        this.cdr.markForCheck();
      },
    });
  }

  toggleFilter(field: 'filterDirectClient' | 'filterAi' | 'filterCloudMigration', value: boolean): void {
    if (this[field] === value) {
      (this[field] as boolean | undefined) = undefined;
    } else {
      (this[field] as boolean | undefined) = value;
    }
    this.load();
  }

  onSearchChange(): void {
    this.load();
  }

  onSortChange(): void {
    this.load();
  }

  prospectScoreColor(score: number): string {
    if (score >= 70) return 'text-green-600 font-bold';
    if (score >= 50) return 'text-amber-600 font-semibold';
    return 'text-slate-500';
  }

  get directCount(): number { return this.companies.filter(c => c.isDirectClient).length; }
  get aiCount(): number { return this.companies.filter(c => c.hasAiInitiative).length; }
  get migrationCount(): number { return this.companies.filter(c => c.hasCloudMigration).length; }

  trackById(_: number, c: CompanyProfileDto): number { return c.id; }
}
