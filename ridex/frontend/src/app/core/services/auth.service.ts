import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface LoginResponse {
  userId:    string;
  token:     string;
  role:      string;
  firstName: string;
  expiresAt: string;
}

export interface AuthUser {
  userId:    string;
  firstName: string;
  role:      'User' | 'Driver';
  token:     string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http   = inject(HttpClient);
  private router = inject(Router);

  private _user$ = new BehaviorSubject<AuthUser | null>(this.loadFromStorage());
  readonly user$  = this._user$.asObservable();

  get currentUser(): AuthUser | null { return this._user$.value; }
  get isLoggedIn():  boolean          { return !!this._user$.value; }
  get token():       string | null    { return this._user$.value?.token ?? null; }

  login(email: string, password: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${environment.userApi}/login`, { email, password })
      .pipe(tap(res => this.setUser(res)));
  }

  driverLogin(email: string, password: string): Observable<any> {
    return this.http.post<any>(`${environment.driverApi}/login`, { email, password })
      .pipe(tap(res => this.setUser({ ...res, role: 'Driver', firstName: res.firstName ?? 'Driver' })));
  }

  register(payload: { email: string; password: string; firstName: string; lastName: string; phoneNumber: string }) {
    return this.http.post<{ id: string }>(`${environment.userApi}/register`, payload);
  }

  logout(): void {
    localStorage.removeItem('ridex_auth');
    this._user$.next(null);
    this.router.navigate(['/auth/login']);
  }

  private setUser(res: any): void {
    const user: AuthUser = {
      userId:    res.userId ?? res.driverId,
      firstName: res.firstName,
      role:      res.role,
      token:     res.token,
    };
    localStorage.setItem('ridex_auth', JSON.stringify(user));
    this._user$.next(user);
  }

  private loadFromStorage(): AuthUser | null {
    try {
      const raw = localStorage.getItem('ridex_auth');
      return raw ? JSON.parse(raw) : null;
    } catch { return null; }
  }
}
