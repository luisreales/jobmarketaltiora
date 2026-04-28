import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { AiPromptTemplate, UpdateAiPromptTemplateRequest } from '../models/ai-audit.models';

export interface CreatePromptTemplateRequest {
  key: string;
  template: string;
  version?: string;
  isActive: boolean;
  updatedBy?: string;
}

@Injectable({
  providedIn: 'root'
})
export class PromptAiService {
  private readonly baseUrl = `${environment.apiUrl}/api/ai/prompts`;

  constructor(private readonly http: HttpClient) {}

  getAll(): Observable<AiPromptTemplate[]> {
    return this.http.get<AiPromptTemplate[]>(this.baseUrl);
  }

  getByKey(key: string): Observable<AiPromptTemplate> {
    return this.http.get<AiPromptTemplate>(`${this.baseUrl}/${encodeURIComponent(key)}`);
  }

  create(request: CreatePromptTemplateRequest): Observable<AiPromptTemplate> {
    return this.http.post<AiPromptTemplate>(this.baseUrl, request);
  }

  update(key: string, request: UpdateAiPromptTemplateRequest): Observable<AiPromptTemplate> {
    return this.http.put<AiPromptTemplate>(`${this.baseUrl}/${encodeURIComponent(key)}`, request);
  }

  delete(key: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${encodeURIComponent(key)}`);
  }
}
