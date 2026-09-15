import { Component, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { AuthService } from '../../../core/services/auth.service';
import { RideService, Ride } from '../../../core/services/ride.service';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-driver-dashboard',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="shell">
      <nav class="navbar">
        <span class="brand">🚗 RideX Driver</span>
        <div class="status-controls">
          <span class="status-dot" [class.online]="isOnline()"></span>
          <span>{{ isOnline() ? 'Online' : 'Offline' }}</span>
          <button (click)="toggleOnline()" class="btn-toggle" [class.active]="isOnline()">
            {{ isOnline() ? 'Go offline' : 'Go online' }}
          </button>
        </div>
        <button (click)="auth.logout()" class="btn-ghost">Sign out</button>
      </nav>

      <main class="content">
        <!-- Location update card -->
        <section class="card">
          <h2>Update my location</h2>
          <form [formGroup]="locationForm" (ngSubmit)="updateLocation()">
            <div class="grid-2">
              <div class="field">
                <label>Latitude</label>
                <input type="number" formControlName="lat" placeholder="-26.2041" step="0.0001">
              </div>
              <div class="field">
                <label>Longitude</label>
                <input type="number" formControlName="lon" placeholder="28.0473" step="0.0001">
              </div>
            </div>
            @if (locationSaved()) { <div class="alert-success">Location updated ✓</div> }
            <button type="submit" class="btn-primary">Update location</button>
          </form>
        </section>

        <!-- Active ride card -->
        @if (activeRide()) {
          <section class="card card-active">
            <h2>Active ride</h2>
            <div class="ride-info">
              <div class="info-row">
                <span class="label">Status</span>
                <span class="status-badge status-{{ activeRide()!.status.toLowerCase() }}">{{ activeRide()!.status }}</span>
              </div>
              <div class="info-row">
                <span class="label">Pickup</span>
                <span>({{ activeRide()!.pickupLat | number:'1.4-4' }}, {{ activeRide()!.pickupLon | number:'1.4-4' }})</span>
              </div>
              <div class="info-row">
                <span class="label">Destination</span>
                <span>({{ activeRide()!.destLat | number:'1.4-4' }}, {{ activeRide()!.destLon | number:'1.4-4' }})</span>
              </div>
            </div>

            <div class="ride-actions">
              @if (activeRide()!.status === 'Accepted') {
                <button (click)="startRide()" class="btn-primary">Start ride</button>
              }
              @if (activeRide()!.status === 'InProgress') {
                <button (click)="completeRide()" class="btn-success">Complete ride</button>
              }
              @if (activeRide()!.status === 'Completed') {
                <div class="fare-display">
                  Fare earned: <strong>R{{ activeRide()!.fare | number:'1.2-2' }}</strong>
                </div>
              }
            </div>
          </section>
        }

        <!-- Stats card -->
        <section class="card">
          <h2>My stats</h2>
          <div class="stats-grid">
            <div class="stat">
              <span class="stat-value">{{ profile()?.totalRides ?? 0 }}</span>
              <span class="stat-label">Total rides</span>
            </div>
            <div class="stat">
              <span class="stat-value">R{{ (profile()?.totalEarnings ?? 0) | number:'1.2-2' }}</span>
              <span class="stat-label">Total earnings</span>
            </div>
            <div class="stat">
              <span class="stat-value">{{ (profile()?.rating ?? 5.0) | number:'1.1-1' }}</span>
              <span class="stat-label">Rating ⭐</span>
            </div>
          </div>
        </section>
      </main>
    </div>
  `,
  styles: [`
    .shell { min-height: 100vh; background: #f8f9fa; }
    .navbar { display: flex; align-items: center; gap: 1rem; padding: .9rem 1.5rem; background: #1e293b; color: #fff; }
    .brand { font-size: 1.05rem; font-weight: 600; flex: 1; }
    .status-controls { display: flex; align-items: center; gap: .5rem; font-size: .88rem; }
    .status-dot { width: 8px; height: 8px; border-radius: 50%; background: #ef4444; }
    .status-dot.online { background: #22c55e; }
    .btn-toggle { padding: .3rem .7rem; border-radius: 5px; border: 1px solid #475569; background: #334155; color: #fff; font-size: .8rem; cursor: pointer; }
    .btn-toggle.active { background: #166534; border-color: #166534; }
    .btn-ghost { padding: .4rem .8rem; background: none; border: 1px solid #475569; border-radius: 6px; color: #cbd5e1; font-size: .84rem; cursor: pointer; }
    .content { max-width: 700px; margin: 2rem auto; padding: 0 1rem; display: flex; flex-direction: column; gap: 1.25rem; }
    .card { background: #fff; border-radius: 10px; padding: 1.5rem; box-shadow: 0 1px 3px rgba(0,0,0,.06); }
    .card-active { border: 2px solid #4f46e5; }
    h2 { font-size: 1.05rem; font-weight: 600; margin-bottom: 1rem; }
    .grid-2 { display: grid; grid-template-columns: 1fr 1fr; gap: .75rem; }
    .field { margin-bottom: .85rem; }
    label { display: block; font-size: .82rem; font-weight: 500; margin-bottom: .3rem; color: #444; }
    input { width: 100%; padding: .55rem .7rem; border: 1px solid #ddd; border-radius: 6px; font-size: .9rem; box-sizing: border-box; }
    .btn-primary { padding: .6rem 1.2rem; background: #4f46e5; color: #fff; border: none; border-radius: 6px; font-size: .9rem; font-weight: 500; cursor: pointer; }
    .btn-success { padding: .6rem 1.2rem; background: #166534; color: #fff; border: none; border-radius: 6px; font-size: .9rem; font-weight: 500; cursor: pointer; }
    .alert-success { background: #f0fff4; border: 1px solid #9ae6b4; color: #276749; padding: .55rem; border-radius: 6px; font-size: .83rem; margin-bottom: .75rem; }
    .ride-info { display: flex; flex-direction: column; gap: .5rem; margin-bottom: 1rem; }
    .info-row { display: flex; align-items: center; gap: .75rem; font-size: .88rem; }
    .label { min-width: 90px; color: #666; font-size: .82rem; }
    .status-badge { display: inline-block; padding: .2rem .6rem; border-radius: 99px; font-size: .75rem; font-weight: 500; }
    .status-accepted  { background: #fefce8; color: #854d0e; }
    .status-inprogress{ background: #fff7ed; color: #9a3412; }
    .status-completed { background: #f0fff4; color: #166534; }
    .ride-actions { display: flex; gap: .75rem; align-items: center; }
    .fare-display { font-size: 1rem; color: #166534; font-weight: 500; }
    .stats-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 1rem; }
    .stat { text-align: center; padding: 1rem; background: #f8f9fa; border-radius: 8px; }
    .stat-value { display: block; font-size: 1.4rem; font-weight: 600; color: #1e293b; }
    .stat-label { display: block; font-size: .78rem; color: #888; margin-top: .25rem; }
  `]
})
export class DriverDashboardComponent implements OnInit, OnDestroy {
  auth       = inject(AuthService);
  private rs = inject(RideService);
  private fb = inject(FormBuilder);
  private http = inject(HttpClient);

  isOnline     = signal(false);
  locationSaved= signal(false);
  activeRide   = signal<Ride | null>(null);
  profile      = signal<any>(null);
  private pollInterval: any;

  locationForm = this.fb.group({
    lat: [-26.2041, Validators.required],
    lon: [28.0473,  Validators.required]
  });

  ngOnInit(): void {
    this.loadProfile();
    this.pollInterval = setInterval(() => this.checkActiveRide(), 8000);
  }

  ngOnDestroy(): void { clearInterval(this.pollInterval); }

  loadProfile(): void {
    const id = this.auth.currentUser?.userId;
    if (!id) return;
    this.http.get(`${environment.driverApi}/${id}`).subscribe({
      next: p => this.profile.set(p),
      error: () => {}
    });
  }

  toggleOnline(): void {
    const endpoint = this.isOnline() ? 'go-offline' : 'go-online';
    this.http.put(`${environment.driverApi}/${endpoint}`, {}).subscribe({
      next: () => this.isOnline.update(v => !v),
      error: () => {}
    });
  }

  updateLocation(): void {
    if (this.locationForm.invalid) return;
    const { lat, lon } = this.locationForm.value;
    this.http.put(`${environment.driverApi}/location`, { lat, lon }).subscribe({
      next: () => { this.locationSaved.set(true); setTimeout(() => this.locationSaved.set(false), 2000); },
      error: () => {}
    });
  }

  startRide(): void {
    if (!this.activeRide()) return;
    this.rs.startRide(this.activeRide()!.id).subscribe(() => this.checkActiveRide());
  }

  completeRide(): void {
    if (!this.activeRide()) return;
    this.rs.completeRide(this.activeRide()!.id).subscribe(() => {
      this.checkActiveRide();
      this.loadProfile();
    });
  }

  private checkActiveRide(): void {
    // Poll for rides assigned to this driver
    // In production this would be a SignalR push
    const driverId = this.auth.currentUser?.userId;
    if (!driverId) return;
    this.http.get<Ride[]>(`${environment.rideApi}/my-rides`).subscribe({
      next: rides => {
        const active = rides.find(r =>
          r.status === 'Accepted' || r.status === 'InProgress');
        this.activeRide.set(active ?? null);
      },
      error: () => {}
    });
  }
}
