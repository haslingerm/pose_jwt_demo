import { Component, DestroyRef, inject, signal } from '@angular/core';
import { DateTimeFormatter, LocalDateTime } from '@js-joda/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { z } from 'zod';
import { MatCard, MatCardContent } from '@angular/material/card';
import { HUB_BASE_URL } from '../../../core/const';
import { AuthService } from '../../../core/auth/auth-service';

@Component({
  selector: 'app-home-time-display',
  imports: [
    MatCard,
    MatCardContent
  ],
  templateUrl: './home-time-display.html',
  styleUrl: './home-time-display.scss'
})
export class HomeTimeDisplay {
  private static readonly timeFormatter = DateTimeFormatter.ofPattern('HH:mm:ss');
  private readonly authService = inject(AuthService);
  private readonly hubConnection: HubConnection;
  private destroyed = false;

  protected readonly currentTime = signal('not yet received');

  constructor() {
    this.hubConnection = new HubConnectionBuilder()
      .withUrl(`${HUB_BASE_URL}/time`, {
        // Called whenever a connection is established - browsers cannot send an Authorization header with
        // WebSockets, so the token is sent in the query string (see backend: JwtBearerEvents.OnMessageReceived).
        // An empty token = anonymous connection.
        accessTokenFactory: async () => await this.authService.getAccessToken() ?? '',
        // the token is sent explicitly, we don't need cookies (and the backend CORS policy doesn't allow them)
        withCredentials: false
      })
      .withAutomaticReconnect()
      .build();

    // register handlers before starting, so we don't miss any message
    this.hubConnection.on('ReceiveTime', (data: unknown) => {
      const formattedTime = HomeTimeDisplay.formatTimeUpdate(data);
      if (formattedTime) {
        this.currentTime.set(formattedTime);
      }
    });
    this.hubConnection.onreconnecting(() => this.currentTime.set('reconnecting...'));
    this.hubConnection.onreconnected(() => console.log('Reconnected to time hub'));
    this.hubConnection.onclose(error => {
      if (this.destroyed) {
        return;
      }
      // The backend closes the connection when the access token expires (CloseOnAuthenticationExpiration).
      // We simply connect again - which fetches a fresh token (or connects anonymously after a logout).
      console.log('Connection to time hub closed, reconnecting', error);
      setTimeout(() => this.connect(), 1000);
    });

    inject(DestroyRef).onDestroy(() => {
      this.destroyed = true;
      void this.hubConnection.stop();
    });

    void this.connect();
  }

  private async connect(): Promise<void> {
    if (this.destroyed) {
      return;
    }
    try {
      await this.hubConnection.start();
      this.currentTime.set('connected and waiting');
    } catch (error) {
      console.error('Failed to connect to time hub', error);
      this.currentTime.set('not connected');
    }
  }

  private static formatTimeUpdate(data: unknown): string | null {
    const result = TimeUpdateWire.safeParse(data);
    if (!result.success) {
      console.error('Failed to parse time update', result.error);
      return null;
    }

    const { currentTime, quality } = result.data;
    const resolution = quality === TimeQuality.High ? 'high' : 'low';
    return `${currentTime.format(HomeTimeDisplay.timeFormatter)} (${resolution} resolution)`;
  }
}

enum TimeQuality {
  Low,
  High
}

const TimeUpdateWire = z.object({
  currentTime: z.string().transform(s => LocalDateTime.parse(s)),
  quality: z.enum(TimeQuality)
});
