import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { Hotel, HotelAnalysis } from '../../models/hotel.models';

interface HotelRow {
  id: number;
  name: string;
  currentPrice: number | null;
  currentPriceSource: string;
  averagePrice: number | null;
  score: number | null;
  trend: string;
}

@Component({
  selector: 'app-hotel-table',
  imports: [CommonModule, MatTableModule, MatButtonModule, RouterLink],
  templateUrl: './hotel-table.html',
  styleUrl: './hotel-table.scss'
})
export class HotelTable {
  @Input() hotels: Hotel[] = [];
  @Input() analysisMap: Record<number, HotelAnalysis | null> = {};

  displayedColumns = ['name', 'currentPrice', 'source', 'averagePrice', 'score', 'trend', 'detail'];

  get rows(): HotelRow[] {
    return this.hotels.map((hotel) => {
      const analysis = this.analysisMap[hotel.id];
      return {
        id: hotel.id,
        name: hotel.name,
        currentPrice: analysis?.currentPrice ?? hotel.currentPrice,
        currentPriceSource: this.formatSource(hotel.currentPriceSource),
        averagePrice: analysis?.averagePrice ?? null,
        score: analysis?.score ?? null,
        trend: analysis ? (analysis.score > 0 ? 'Down' : 'Up') : 'N/A'
      };
    });
  }

  private formatSource(source: string): string {
    const clean = (source || '').trim().toLowerCase();
    if (!clean || clean === 'none') {
      return 'No data';
    }

    return clean
      .split(/[-_\s]+/)
      .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
      .join(' ');
  }

}
