import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-driver-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  template: `
    <div class="auth-wrapper">
      <div class="auth-card">
        <div class="brand">
          <span class="logo">🚗</span>
          <h1>Driver login</h1>
          <p>Sign in to your driver account</p>
        </div>
        <form [formGroup]="form" (ngSubmit)="submit()">
          <div class="field">
            <label>Email</label>
            <input type="email" formControlName="email">
          </div>
          <div class="field">
            <label>Password</label>
            <input type="password" formControlName="password">
          </div>
          @if (error()) { <div class="alert-error">{{ error() }}</div> }
          <button type="submit" [disabled]="loading()" class="btn-primary">
            {{ loading() ? 'Signing in…' : 'Sign in as driver' }}
          </button>
        </form>
        <div class="links"><a routerLink="/auth/login">User login</a></div>
      </div>
    </div>
  `,
  styles: [`
    .auth-wrapper { min-height: 100vh; display: flex; align-items: center; justify-content: center; background: #1e293b; }
    .auth-card { background: #fff; border-radius: 12px; padding: 2rem; width: 100%; max-width: 380px; }
    .brand { text-align: center; margin-bottom: 1.5rem; }
    .logo { font-size: 2.5rem; }
    h1 { font-size: 1.4rem; font-weight: 600; margin: .4rem 0 .2rem; }
    .brand p { color: #666; font-size: .88rem; }
    .field { margin-bottom: 1rem; }
    label { display: block; font-size: .82rem; font-weight: 500; margin-bottom: .3rem; }
    input { width: 100%; padding: .6rem .75rem; border: 1px solid #ddd; border-radius: 6px; font-size: .9rem; box-sizing: border-box; }
    .alert-error { background: #fff5f5; border: 1px solid #feb2b2; color: #c53030; padding: .6rem; border-radius: 6px; font-size: .83rem; margin-bottom: .85rem; }
    .btn-primary { width: 100%; padding: .7rem; background: #1e293b; color: #fff; border: none; border-radius: 6px; font-size: .92rem; font-weight: 500; cursor: pointer; }
    .btn-primary:disabled { opacity: .6; }
    .links { text-align: center; margin-top: 1rem; font-size: .84rem; }
    .links a { color: #4f46e5; text-decoration: none; }
  `]
})
export class DriverLoginComponent {
  private auth   = inject(AuthService);
  private router = inject(Router);
  private fb     = inject(FormBuilder);

  form    = this.fb.group({ email: ['', [Validators.required, Validators.email]], password: ['', Validators.required] });
  loading = signal(false);
  error   = signal('');

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.loading.set(true);
    const { email, password } = this.form.value;
    this.auth.driverLogin(email!, password!).subscribe({
      next: () => this.router.navigate(['/driver/dashboard']),
      error: err => { this.error.set(err.error?.detail ?? 'Login failed'); this.loading.set(false); }
    });
  }
}
