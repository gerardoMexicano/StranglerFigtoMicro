import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { SECOND_SYSTEM_BROKEN_OPTIONS } from './second-system-broken-options';

@Component({
  selector: 'app-second-system-broken',
  imports: [RouterLink],
  templateUrl: './second-system-broken.html',
  styleUrl: './second-system-broken.css',
})
export class SecondSystemBroken {
  private readonly auth = inject(AuthService);

  protected readonly usuario = toSignal(this.auth.user$, { initialValue: null });
  protected readonly opciones = SECOND_SYSTEM_BROKEN_OPTIONS;

  protected salir(): void {
    this.auth.logout();
  }
}
