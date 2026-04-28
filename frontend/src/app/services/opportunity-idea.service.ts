import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { OpportunityIdea, UpdateOpportunityIdeaRequest } from '../models/market.models';

@Injectable({ providedIn: 'root' })
export class OpportunityIdeaService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/opportunity-ideas`;

  getAll(): Observable<OpportunityIdea[]> {
    return this.http.get<OpportunityIdea[]>(this.base);
  }

  update(id: string, body: UpdateOpportunityIdeaRequest): Observable<OpportunityIdea> {
    return this.http.put<OpportunityIdea>(`${this.base}/${id}`, body);
  }
}
