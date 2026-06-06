import {
  ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy, OnInit, inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { TechnologyService } from '../../services/technology.service';
import { TechnologyDto, categoryColor, lifecycleColor } from '../../models/technology.models';

@Component({
  selector: 'app-technologies',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule],
  templateUrl: './technologies.html',
})
export class TechnologiesPage implements OnInit, OnDestroy {
  private readonly svc = inject(TechnologyService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroy$ = new Subject<void>();
  private readonly search$ = new Subject<string>();

  techs: TechnologyDto[] = [];
  loading = true;
  rebuilding = false;
  rebuildResult: string | null = null;
  totalCount = 0;
  aiCount = 0;
  emergingCount = 0;

  searchTerm = '';
  selectedCategory = '';
  selectedLifecycle = '';
  sortBy = 'demandScore';

  readonly categories = ['', 'AI', 'Backend', 'Frontend', 'Cloud', 'Database', 'DevOps', 'Architecture', 'Observability', 'Security', 'Messaging'];
  readonly lifecycles = ['', 'Emerging', 'Growing', 'Mature', 'Declining', 'Legacy'];
  readonly sorts = [
    { value: 'demandScore', label: 'Demand' },
    { value: 'momentum', label: 'Momentum' },
    { value: 'mentions', label: 'Mentions' },
    { value: 'opportunity', label: 'Opportunity' },
    { value: 'emerging', label: 'Emerging' },
  ];

  readonly lifecycleColor = lifecycleColor;
  readonly categoryColor = categoryColor;

  ngOnInit(): void {
    this.search$.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      takeUntil(this.destroy$)
    ).subscribe(() => this.load());

    this.load();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onSearch(term: string): void {
    this.searchTerm = term;
    this.search$.next(term);
  }

  onFilterChange(): void {
    this.load();
  }

  private load(): void {
    this.loading = true;
    this.cdr.markForCheck();
    this.svc.getTechnologies({
      search: this.searchTerm || undefined,
      category: this.selectedCategory || undefined,
      lifecycleStage: this.selectedLifecycle || undefined,
      pageSize: 100,
      sortBy: this.sortBy,
    }).subscribe({
      next: (resp) => {
        this.techs = resp.items;
        this.totalCount = resp.totalCount;
        this.aiCount = resp.items.filter(t => t.isAiRelated).length;
        this.emergingCount = resp.items.filter(t => t.lifecycleStage === 'Emerging').length;
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.loading = false;
        this.cdr.markForCheck();
      }
    });
  }

  triggerRebuild(): void {
    this.rebuilding = true;
    this.rebuildResult = null;
    this.cdr.markForCheck();
    this.svc.rebuild().subscribe({
      next: (r) => {
        this.rebuildResult = `Rebuilt: ${r.technologiesUpserted} technologies, ${r.relationshipsUpserted} relationships from ${r.jobsProcessed} jobs.`;
        this.rebuilding = false;
        this.load();
      },
      error: () => {
        this.rebuildResult = 'Rebuild failed. Check backend logs.';
        this.rebuilding = false;
        this.cdr.markForCheck();
      }
    });
  }

  formatGrowth(rate: number): string {
    return rate >= 0 ? `+${rate.toFixed(0)}%` : `${rate.toFixed(0)}%`;
  }

  growthClass(rate: number): string {
    if (rate > 10) return 'text-green-600 font-semibold';
    if (rate < -10) return 'text-red-500';
    return 'text-slate-500';
  }

  trackByTech(_: number, t: TechnologyDto): number { return t.id; }
}
