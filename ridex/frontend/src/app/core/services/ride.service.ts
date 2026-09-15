import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface CreateRideDto {
  pickupLat:  number;
  pickupLon:  number;
  destLat:    number;
  destLon:    number;
  preference: string;
}

export interface Ride {
  id:          string;
  userId:      string;
  driverId:    string | null;
  pickupLat:   number;
  pickupLon:   number;
  destLat:     number;
  destLon:     number;
  preference:  string;
  status:      'Requested' | 'Accepted' | 'InProgress' | 'Completed' | 'Cancelled';
  fare:        number | null;
  createdAt:   string;
  acceptedAt:  string | null;
  completedAt: string | null;
}

@Injectable({ providedIn: 'root' })
export class RideService {
  private http = inject(HttpClient);

  createRide(dto: CreateRideDto): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(environment.rideApi, dto);
  }

  getRide(id: string): Observable<Ride> {
    return this.http.get<Ride>(`${environment.rideApi}/${id}`);
  }

  getMyRides(): Observable<Ride[]> {
    return this.http.get<Ride[]>(`${environment.rideApi}/my-rides`);
  }

  startRide(id: string): Observable<void> {
    return this.http.put<void>(`${environment.rideApi}/${id}/start`, {});
  }

  completeRide(id: string): Observable<{ fare: number }> {
    return this.http.put<{ fare: number }>(`${environment.rideApi}/${id}/complete`, {});
  }

  cancelRide(id: string): Observable<void> {
    return this.http.put<void>(`${environment.rideApi}/${id}/cancel`, {});
  }
}
