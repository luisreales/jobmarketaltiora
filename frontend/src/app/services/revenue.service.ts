import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { RevenueSummaryDto, SalesStatusUpdateDto } from '../models/revenue.models';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class RevenueService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/revenue`;

  getSummary(): Observable<RevenueSummaryDto> {
    return this.http.get<RevenueSummaryDto>(`${this.base}/summary`);
  }

  updateSalesStatus(productId: number, dto: SalesStatusUpdateDto): Observable<void> {
    return this.http.patch<void>(`${this.base}/products/${productId}/sales-status`, dto);
  }
}
