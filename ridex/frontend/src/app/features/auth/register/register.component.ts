import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  template: `
    <div class="auth-wrapper">
      <div class="auth-card">
        <div class="brand">
          <span class="logo">🚗</span>
          <h1>Create account</h1>
          <p>Join RideX today</p>
        </div>

        <form [formGroup]="form" (ngSubmit)="submit()">
          <div class="row">
            <div class="field">
              <label>First name</label>
              <input formControlName="firstName" placeholder="Jane">
            </div>
            <div class="field">
              <label>Last name</label>
              <input formControlName="lastName" placeholder="Doe">
            </div>
          </div>
          <div class="field">
            <label>Email</label>
            <input type="email" formControlName="email" placeholder="jane@example.com">
            @if (form.get('email')?.invalid && form.get('email')?.touched) {
              <span class="error">Valid email required</span>
            }
          </div>
          <div class="field">
            <label>Phone number</label>
            <input formControlName="phoneNumber" placeholder="+27821234567">
          </div>
          <div class="field">
            <label>Password</label>
            <input type="password" formControlName="password" placeholder="Min 8 chars, 1 uppercase, 1 digit">
            @if (form.get('password')?.invalid && form.get('password')?.touched) {
              <span class="error">Min 8 characters with uppercase and digit</span>
            }
          </div>

          @if (error()) { <div class="alert-error">{{ error() }}</div> }
          @if (success()) { <div class="alert-success">Account created! <a routerLink="/auth/login">Sign in</a></div> }

          <button type="submit" [disabled]="loading()" class="btn-primary">
            {{ loading() ? 'Creating…' : 'Create account' }}
          </button>
        </form>

        <div class="links"><a routerLink="/auth/login">Already have an account? Sign in</a></div>
      </div>
    </div>
  `,
  styles: [`
    .auth-wrapper { min-height: 100vh; display: flex; align-items: center; justify-content: center; background: #f5f5f5; }
    .auth-card { background: #fff; border-radius: 12px; padding: 2rem; width: 100%; max-width: 420px; box-shadow: 0 1px 4px rgba(0,0,0,.08); }
    .brand { text-align: center; margin-bottom: 1.5rem; }
    .logo { font-size: 2.5rem; }
    h1 { font-size: 1.5rem; font-weight: 600; margin: .5rem 0 .25rem; }
    .brand p { color: #666; font-size: .9rem; }
    .row { display: grid; grid-template-columns: 1fr 1fr; gap: .75rem; }
    .field { margin-bottom: 1rem; }
    label { display: block; font-size: .85rem; font-weight: 500; margin-bottom: .35rem; color: #333; }
    input { width: 100%; padding: .6rem .75rem; border: 1px solid #ddd; border-radius: 6px; font-size: .95rem; box-sizing: border-box; }
    input:focus { outline: none; border-color: #4f46e5; }
    .error { color: #e53e3e; font-size: .78rem; display: block; margin-top: .2rem; }
    .alert-error { background: #fff5f5; border: 1px solid #feb2b2; color: #c53030; padding: .65rem; border-radius: 6px; font-size: .85rem; margin-bottom: 1rem; }
    .alert-success { background: #f0fff4; border: 1px solid #9ae6b4; color: #276749; padding: .65rem; border-radius: 6px; font-size: .85rem; margin-bottom: 1rem; }
    .alert-success a { color: #276749; font-weight: 500; }
    .btn-primary { width: 100%; padding: .7rem; background: #4f46e5; color: #fff; border: none; border-radius: 6px; font-size: .95rem; font-weight: 500; cursor: pointer; }
    .btn-primary:disabled { opacity: .6; cursor: not-allowed; }
    .links { text-align: center; margin-top: 1rem; font-size: .85rem; }
    .links a { color: #4f46e5; text-decoration: none; }
  `]
})
export class RegisterComponent {
  private auth = inject(AuthService);
  private fb   = inject(FormBuilder);

  form = this.fb.group({
    firstName:   ['', Validators.required],
    lastName:    ['', Validators.required],
    email:       ['', [Validators.required, Validators.email]],
    phoneNumber: ['', Validators.required],
    password:    ['', [Validators.required, Validators.minLength(8),
                       Validators.pattern(/^(?=.*[A-Z])(?=.*\d).+$/)]]
  });
  loading = signal(false);
  error   = signal('');
  success = signal(false);

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.loading.set(true); this.error.set('');
    this.auth.register(this.form.value as any).subscribe({
      next: () => { this.success.set(true); this.loading.set(false); },
      error: err => { this.error.set(err.error?.detail ?? 'Registration failed'); this.loading.set(false); }
    });
  }
}
