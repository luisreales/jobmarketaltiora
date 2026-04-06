import { Component, OnInit } from '@angular/core';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { HotelTable } from '../../components/hotel-table/hotel-table';
import { Hotel, HotelAnalysis } from '../../models/hotel.models';
import { HotelService } from '../../services/hotel.service';

@Component({
  selector: 'app-dashboard',
  imports: [HotelTable],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class Dashboard implements OnInit {
  hotels: Hotel[] = [];
  analysisMap: Record<number, HotelAnalysis | null> = {};

  constructor(private readonly hotelService: HotelService) {}

  ngOnInit(): void {
    this.hotelService.getHotels().subscribe((hotels) => {
      this.hotels = hotels;

      if (!hotels.length) {
        return;
      }

      const requests = hotels.map((hotel) =>
        this.hotelService.getHotelAnalysis(hotel.id).pipe(catchError(() => of(null)))
      );

      forkJoin(requests).subscribe((analysisList) => {
        this.analysisMap = analysisList.reduce<Record<number, HotelAnalysis | null>>((acc, item, index) => {
          acc[hotels[index].id] = item;
          return acc;
        }, {});
      });
    });
  }

}
