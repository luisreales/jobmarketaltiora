import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatButtonModule } from '@angular/material/button';
import { SearchForm } from '../../components/search-form/search-form';
import { BookingSearchResult, SearchQuery } from '../../models/hotel.models';
import { BookingService } from '../../services/booking.service';

interface GroupedSearchResult {
  name: string;
  city: string;
  lowestPrice: number;
  currency: string;
  offers: BookingSearchResult[];
}

@Component({
  selector: 'app-search',
  imports: [CommonModule, SearchForm, MatCardModule, MatExpansionModule, MatButtonModule],
  templateUrl: './search.html',
  styleUrl: './search.scss'
})
export class Search {
  results: BookingSearchResult[] = [];
  loading = false;
  currentMode: 'real' | 'database' = 'real';

  constructor(private readonly bookingService: BookingService) {}

  get groupedResults(): GroupedSearchResult[] {
    const grouped = new Map<string, GroupedSearchResult>();

    for (const result of this.results) {
      const key = result.name.trim().toLowerCase();
      const existing = grouped.get(key);

      if (!existing) {
        grouped.set(key, {
          name: result.name,
          city: result.city,
          lowestPrice: result.price,
          currency: result.currency,
          offers: [result]
        });
        continue;
      }

      existing.offers.push(result);
      if (result.price < existing.lowestPrice) {
        existing.lowestPrice = result.price;
        existing.currency = result.currency;
      }
    }

    return Array.from(grouped.values())
      .map((group) => ({
        ...group,
        offers: [...group.offers].sort((a, b) => a.price - b.price)
      }))
      .sort((a, b) => a.lowestPrice - b.lowestPrice);
  }

  search(query: SearchQuery): void {
    this.currentMode = query.mode;
    this.loading = true;
    this.bookingService.search(query).subscribe({
      next: (results) => (this.results = results),
      complete: () => (this.loading = false),
      error: () => {
        this.results = [];
        this.loading = false;
      }
    });
  }

}
