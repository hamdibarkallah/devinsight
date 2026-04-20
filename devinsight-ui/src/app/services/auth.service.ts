import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, tap } from 'rxjs';

export interface AuthResponse {
  token: string; email: string; displayName: string; organizationId: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private tokenKey = 'di_token';
  private userKey = 'di_user';
  currentUser$ = new BehaviorSubject<AuthResponse | null>(this.storedUser());

  constructor(private http: HttpClient) {}

  login(email: string, password: string) {
    return this.http.post<AuthResponse>('/api/auth/login', { email, password }).pipe(
      tap(res => { localStorage.setItem(this.tokenKey, res.token); localStorage.setItem(this.userKey, JSON.stringify(res)); this.currentUser$.next(res); })
    );
  }

  register(email: string, password: string, displayName: string, organizationName: string) {
    return this.http.post<AuthResponse>('/api/auth/register', { email, password, displayName, organizationName }).pipe(
      tap(res => { localStorage.setItem(this.tokenKey, res.token); localStorage.setItem(this.userKey, JSON.stringify(res)); this.currentUser$.next(res); })
    );
  }

  logout() { localStorage.removeItem(this.tokenKey); localStorage.removeItem(this.userKey); this.currentUser$.next(null); }
  getToken() { return localStorage.getItem(this.tokenKey); }
  isLoggedIn() { return !!this.getToken(); }
  private storedUser(): AuthResponse | null {
    const u = localStorage.getItem(this.userKey); return u ? JSON.parse(u) : null;
  }
}
