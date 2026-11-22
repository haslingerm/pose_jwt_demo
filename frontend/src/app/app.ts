import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatToolbar } from '@angular/material/toolbar';
import { MatIconButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatListItem, MatNavList } from '@angular/material/list';
import { MatSidenav, MatSidenavContainer, MatSidenavContent } from '@angular/material/sidenav';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { map } from 'rxjs/operators';
import { toSignal } from '@angular/core/rxjs-interop';
import { UserService } from '../core/user/user-service';

@Component({
  selector: 'app-root',
  imports: [
    RouterOutlet, RouterLink, RouterLinkActive, MatToolbar, MatIcon, MatIconButton, MatListItem, MatNavList,
    MatSidenav, MatSidenavContainer, MatSidenavContent
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  protected readonly title = 'JWT Demo';
  protected readonly user = inject(UserService).user;
  protected readonly isHandset = toSignal(inject(BreakpointObserver)
    .observe(Breakpoints.Handset)
    .pipe(map(result => result.matches)), { initialValue: false });
  protected readonly showTitleInSidenav = computed(() => !this.isHandset());
}
