import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { BookingSearchResult, SearchQuery } from '../models/hotel.models';

@Injectable({
  providedIn: 'root'
})
export class BookingService {
  private readonly baseUrl = `${environment.apiUrl}/api/booking`;

  constructor(private readonly http: HttpClient) {}

  search(query: SearchQuery): Observable<BookingSearchResult[]> {
    const params = new HttpParams()
      .set('location', query.location)
      .set('checkIn', query.checkIn)
      .set('checkOut', query.checkOut)
      .set('adults', query.adults)
      .set('kids', query.kids)
      .set('rooms', query.rooms)
      .set('mode', query.mode);

    return this.http.get<BookingSearchResult[]>(`${this.baseUrl}/search`, { params });
  }
}
