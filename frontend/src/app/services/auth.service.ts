import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface ProviderAuthStatus {
  provider: string;
  isAuthenticated: boolean;
  lastLoginAt: string | null;
  lastUsedAt: string | null;
  expiresAt: string | null;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/auth`;

  getStatus(provider: string): Observable<ProviderAuthStatus> {
    return this.http.get<ProviderAuthStatus>(`${this.base}/status/${provider}`);
  }

  login(provider: string): Observable<ProviderAuthStatus> {
    return this.http.post<ProviderAuthStatus>(`${this.base}/login`, { provider, username: '', password: '' });
  }

  logout(provider: string): Observable<ProviderAuthStatus> {
    return this.http.post<ProviderAuthStatus>(`${this.base}/logout/${provider}`, {});
  }
}
