import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { HotelChart } from '../../components/hotel-chart/hotel-chart';
import { HotelAnalysis, HotelPricePoint } from '../../models/hotel.models';
import { HotelService } from '../../services/hotel.service';

@Component({
  selector: 'app-hotel-detail',
  imports: [CommonModule, HotelChart, RouterLink, MatButtonModule],
  templateUrl: './hotel-detail.html',
  styleUrl: './hotel-detail.scss'
})
export class HotelDetail implements OnInit {
  hotelId = 0;
  prices: HotelPricePoint[] = [];
  analysis: HotelAnalysis | null = null;
  displayedColumns = ['captured', 'price', 'currency', 'source', 'seed', 'location', 'stay'];

  constructor(
    private readonly route: ActivatedRoute,
    private readonly hotelService: HotelService
  ) {}

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!id) {
      return;
    }

    this.hotelId = id;
    this.hotelService.getHotelPrices(id).subscribe((prices) => (this.prices = prices));
    this.hotelService.getHotelAnalysis(id).subscribe({
      next: (analysis) => (this.analysis = analysis),
      error: () => (this.analysis = null)
    });
  }

  asYesNo(value: boolean): string {
    return value ? 'Yes' : 'No';
  }

  stayRange(point: HotelPricePoint): string {
    if (!point.checkIn || !point.checkOut) {
      return 'N/A';
    }

    return `${point.checkIn} -> ${point.checkOut}`;
  }

}
