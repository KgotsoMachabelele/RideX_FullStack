import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  template: `
    <div class="auth-wrapper">
      <div class="auth-card">
        <div class="brand">
          <span class="logo">🚗</span>
          <h1>RideX</h1>
          <p>Sign in to your account</p>
        </div>

        <form [formGroup]="form" (ngSubmit)="submit()">
          <div class="field">
            <label for="email">Email</label>
            <input id="email" type="email" formControlName="email"
                   placeholder="you@example.com" autocomplete="email">
            @if (form.get('email')?.invalid && form.get('email')?.touched) {
              <span class="error">Valid email is required</span>
            }
          </div>

          <div class="field">
            <label for="password">Password</label>
            <input id="password" type="password" formControlName="password"
                   placeholder="••••••••" autocomplete="current-password">
            @if (form.get('password')?.invalid && form.get('password')?.touched) {
              <span class="error">Password is required</span>
            }
          </div>

          @if (error()) {
            <div class="alert-error">{{ error() }}</div>
          }

          <button type="submit" [disabled]="loading()" class="btn-primary">
            {{ loading() ? 'Signing in…' : 'Sign in' }}
          </button>
        </form>

        <div class="links">
          <a routerLink="/auth/register">Create account</a>
          <span>·</span>
          <a routerLink="/auth/driver-login">Driver login</a>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .auth-wrapper { min-height: 100vh; display: flex; align-items: center; justify-content: center; background: #f5f5f5; }
    .auth-card { background: #fff; border-radius: 12px; padding: 2rem; width: 100%; max-width: 400px; box-shadow: 0 1px 4px rgba(0,0,0,.08); }
    .brand { text-align: center; margin-bottom: 1.5rem; }
    .logo { font-size: 2.5rem; }
    h1 { font-size: 1.5rem; font-weight: 600; margin: .5rem 0 .25rem; }
    .brand p { color: #666; font-size: .9rem; }
    .field { margin-bottom: 1rem; }
    label { display: block; font-size: .85rem; font-weight: 500; margin-bottom: .35rem; color: #333; }
    input { width: 100%; padding: .6rem .75rem; border: 1px solid #ddd; border-radius: 6px; font-size: .95rem; box-sizing: border-box; transition: border-color .15s; }
    input:focus { outline: none; border-color: #4f46e5; }
    .error { color: #e53e3e; font-size: .78rem; margin-top: .25rem; display: block; }
    .alert-error { background: #fff5f5; border: 1px solid #feb2b2; color: #c53030; padding: .65rem .85rem; border-radius: 6px; font-size: .85rem; margin-bottom: 1rem; }
    .btn-primary { width: 100%; padding: .7rem; background: #4f46e5; color: #fff; border: none; border-radius: 6px; font-size: .95rem; font-weight: 500; cursor: pointer; transition: opacity .15s; }
    .btn-primary:hover:not(:disabled) { opacity: .9; }
    .btn-primary:disabled { opacity: .6; cursor: not-allowed; }
    .links { text-align: center; margin-top: 1.25rem; font-size: .85rem; color: #666; display: flex; gap: .5rem; justify-content: center; }
    .links a { color: #4f46e5; text-decoration: none; }
  `]
})
export class LoginComponent {
  private auth   = inject(AuthService);
  private router = inject(Router);
  private fb     = inject(FormBuilder);

  form    = this.fb.group({ email: ['', [Validators.required, Validators.email]], password: ['', Validators.required] });
  loading = signal(false);
  error   = signal('');

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.loading.set(true);
    this.error.set('');

    const { email, password } = this.form.value;
    this.auth.login(email!, password!).subscribe({
      next: res => this.router.navigate([res.role === 'Driver' ? '/driver/dashboard' : '/user/dashboard']),
      error: err => { this.error.set(err.error?.detail ?? 'Login failed'); this.loading.set(false); }
    });
  }
}
