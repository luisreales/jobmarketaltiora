import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Hotel, HotelAnalysis, HotelPricePoint } from '../models/hotel.models';

@Injectable({
  providedIn: 'root'
})
export class HotelService {
  private readonly baseUrl = `${environment.apiUrl}/api/hotels`;

  constructor(private readonly http: HttpClient) {}

  getHotels(): Observable<Hotel[]> {
    return this.http.get<Hotel[]>(this.baseUrl);
  }

  getHotelPrices(id: number): Observable<HotelPricePoint[]> {
    return this.http.get<HotelPricePoint[]>(`${this.baseUrl}/${id}/prices`);
  }

  getHotelAnalysis(id: number): Observable<HotelAnalysis> {
    return this.http.get<HotelAnalysis>(`${this.baseUrl}/${id}/analysis`);
  }

  createHotel(name: string, city: string): Observable<Hotel> {
    return this.http.post<Hotel>(this.baseUrl, { name, city });
  }
}
