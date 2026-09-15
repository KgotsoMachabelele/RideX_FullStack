import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { AuthService } from '../../../core/services/auth.service';
import { RideService, Ride } from '../../../core/services/ride.service';

@Component({
  selector: 'app-user-dashboard',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="shell">
      <!-- Navbar -->
      <nav class="navbar">
        <span class="brand">🚗 RideX</span>
        <span class="greeting">Hello, {{ user?.firstName }}</span>
        <button (click)="auth.logout()" class="btn-ghost">Sign out</button>
      </nav>

      <main class="content">
        <!-- Request ride card -->
        <section class="card">
          <h2>Request a ride</h2>
          <form [formGroup]="rideForm" (ngSubmit)="requestRide()">
            <div class="grid-2">
              <div class="field">
                <label>Pickup latitude</label>
                <input type="number" formControlName="pickupLat" placeholder="-26.2041">
              </div>
              <div class="field">
                <label>Pickup longitude</label>
                <input type="number" formControlName="pickupLon" placeholder="28.0473">
              </div>
              <div class="field">
                <label>Destination latitude</label>
                <input type="number" formControlName="destLat" placeholder="-26.1076">
              </div>
              <div class="field">
                <label>Destination longitude</label>
                <input type="number" formControlName="destLon" placeholder="28.0567">
              </div>
            </div>
            <div class="field">
              <label>Ride type</label>
              <select formControlName="preference">
                <option>Economy</option>
                <option>Premium</option>
                <option>XL</option>
              </select>
            </div>

            @if (rideError()) { <div class="alert-error">{{ rideError() }}</div> }
            @if (activeRideId()) {
              <div class="alert-success">
                Ride requested! ID: <code>{{ activeRideId() }}</code>
                <span class="status-badge status-{{ activeRide()?.status?.toLowerCase() }}">
                  {{ activeRide()?.status }}
                </span>
              </div>
            }

            <button type="submit" [disabled]="requesting()" class="btn-primary">
              {{ requesting() ? 'Requesting…' : 'Request ride' }}
            </button>
          </form>
        </section>

        <!-- Ride history -->
        <section class="card">
          <div class="card-header">
            <h2>My rides</h2>
            <button (click)="loadRides()" class="btn-ghost btn-sm">Refresh</button>
          </div>

          @if (loadingRides()) { <div class="loading">Loading rides…</div> }

          @if (rides().length === 0 && !loadingRides()) {
            <p class="empty">No rides yet. Request your first ride above.</p>
          }

          <div class="ride-list">
            @for (ride of rides(); track ride.id) {
              <div class="ride-item">
                <div class="ride-meta">
                  <span class="status-badge status-{{ ride.status.toLowerCase() }}">{{ ride.status }}</span>
                  <span class="ride-date">{{ ride.createdAt | date:'medium' }}</span>
                </div>
                <div class="ride-details">
                  <span>📍 ({{ ride.pickupLat | number:'1.4-4' }}, {{ ride.pickupLon | number:'1.4-4' }})</span>
                  <span>🏁 ({{ ride.destLat | number:'1.4-4' }}, {{ ride.destLon | number:'1.4-4' }})</span>
                  <span class="badge-pref">{{ ride.preference }}</span>
                </div>
                @if (ride.fare) {
                  <div class="ride-fare">Fare: <strong>R{{ ride.fare | number:'1.2-2' }}</strong></div>
                }
                @if (ride.status === 'Requested' || ride.status === 'Accepted') {
                  <button (click)="cancelRide(ride.id)" class="btn-danger btn-sm">Cancel</button>
                }
              </div>
            }
          </div>
        </section>
      </main>
    </div>
  `,
  styles: [`
    .shell { min-height: 100vh; background: #f8f9fa; }
    .navbar { display: flex; align-items: center; gap: 1rem; padding: .9rem 1.5rem; background: #fff; border-bottom: 1px solid #eee; }
    .brand { font-size: 1.1rem; font-weight: 600; flex: 1; }
    .greeting { color: #555; font-size: .9rem; }
    .content { max-width: 760px; margin: 2rem auto; padding: 0 1rem; display: flex; flex-direction: column; gap: 1.5rem; }
    .card { background: #fff; border-radius: 10px; padding: 1.5rem; box-shadow: 0 1px 3px rgba(0,0,0,.06); }
    .card-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; }
    h2 { font-size: 1.1rem; font-weight: 600; margin-bottom: 1rem; }
    .card-header h2 { margin: 0; }
    .grid-2 { display: grid; grid-template-columns: 1fr 1fr; gap: .75rem; }
    .field { margin-bottom: .85rem; }
    label { display: block; font-size: .82rem; font-weight: 500; margin-bottom: .3rem; color: #444; }
    input, select { width: 100%; padding: .55rem .7rem; border: 1px solid #ddd; border-radius: 6px; font-size: .9rem; box-sizing: border-box; }
    .btn-primary { padding: .65rem 1.25rem; background: #4f46e5; color: #fff; border: none; border-radius: 6px; font-size: .9rem; font-weight: 500; cursor: pointer; }
    .btn-ghost { padding: .45rem .85rem; background: none; border: 1px solid #ddd; border-radius: 6px; font-size: .85rem; cursor: pointer; }
    .btn-danger { padding: .35rem .75rem; background: #fff; color: #c53030; border: 1px solid #feb2b2; border-radius: 6px; font-size: .82rem; cursor: pointer; }
    .btn-sm { padding: .35rem .75rem; font-size: .82rem; }
    .alert-error { background: #fff5f5; border: 1px solid #feb2b2; color: #c53030; padding: .65rem; border-radius: 6px; font-size: .85rem; margin-bottom: .85rem; }
    .alert-success { background: #f0fff4; border: 1px solid #9ae6b4; color: #276749; padding: .65rem; border-radius: 6px; font-size: .85rem; margin-bottom: .85rem; display: flex; align-items: center; gap: .5rem; flex-wrap: wrap; }
    .status-badge { display: inline-block; padding: .2rem .6rem; border-radius: 99px; font-size: .75rem; font-weight: 500; }
    .status-requested { background: #ebf4ff; color: #1a56db; }
    .status-accepted  { background: #fefce8; color: #854d0e; }
    .status-inprogress{ background: #fff7ed; color: #9a3412; }
    .status-completed { background: #f0fff4; color: #166534; }
    .status-cancelled { background: #f9fafb; color: #6b7280; }
    .ride-list { display: flex; flex-direction: column; gap: .75rem; }
    .ride-item { padding: .85rem; border: 1px solid #f0f0f0; border-radius: 8px; }
    .ride-meta { display: flex; align-items: center; gap: .5rem; margin-bottom: .4rem; }
    .ride-date { color: #888; font-size: .78rem; }
    .ride-details { display: flex; flex-wrap: wrap; gap: .5rem; font-size: .82rem; color: #555; margin-bottom: .4rem; }
    .badge-pref { background: #f3f4f6; padding: .15rem .5rem; border-radius: 4px; font-size: .75rem; }
    .ride-fare { font-size: .85rem; color: #333; }
    .loading, .empty { color: #888; font-size: .9rem; text-align: center; padding: 1.5rem 0; }
    code { background: #f3f4f6; padding: .1rem .35rem; border-radius: 3px; font-size: .8rem; }
  `]
})
export class UserDashboardComponent implements OnInit {
  auth        = inject(AuthService);
  private rs  = inject(RideService);
  private fb  = inject(FormBuilder);

  user         = this.auth.currentUser;
  rides        = signal<Ride[]>([]);
  loadingRides = signal(false);
  requesting   = signal(false);
  rideError    = signal('');
  activeRideId = signal<string | null>(null);
  activeRide   = signal<Ride | null>(null);

  rideForm = this.fb.group({
    pickupLat:  [-26.2041, Validators.required],
    pickupLon:  [28.0473,  Validators.required],
    destLat:    [-26.1076, Validators.required],
    destLon:    [28.0567,  Validators.required],
    preference: ['Economy']
  });

  ngOnInit(): void { this.loadRides(); }

  requestRide(): void {
    if (this.rideForm.invalid) return;
    this.requesting.set(true); this.rideError.set('');
    this.rs.createRide(this.rideForm.value as any).subscribe({
      next: res => {
        this.activeRideId.set(res.id);
        this.requesting.set(false);
        this.pollRide(res.id);
        this.loadRides();
      },
      error: err => { this.rideError.set(err.error?.detail ?? 'Failed to request ride'); this.requesting.set(false); }
    });
  }

  loadRides(): void {
    this.loadingRides.set(true);
    this.rs.getMyRides().subscribe({
      next: r => { this.rides.set(r); this.loadingRides.set(false); },
      error: () => this.loadingRides.set(false)
    });
  }

  cancelRide(id: string): void {
    this.rs.cancelRide(id).subscribe(() => this.loadRides());
  }

  private pollRide(id: string): void {
    const poll = setInterval(() => {
      this.rs.getRide(id).subscribe(ride => {
        this.activeRide.set(ride);
        if (ride.status === 'Completed' || ride.status === 'Cancelled') {
          clearInterval(poll);
          this.loadRides();
        }
      });
    }, 5000);
  }
}
