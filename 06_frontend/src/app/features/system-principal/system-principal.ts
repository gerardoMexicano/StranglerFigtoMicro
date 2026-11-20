import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { SYSTEM_PRINCIPAL_OPTIONS } from './system-principal-options';

@Component({
  selector: 'app-system-principal',
  imports: [RouterLink],
  templateUrl: './system-principal.html',
  styleUrl: './system-principal.css',
})
export class SystemPrincipal {
  private readonly auth = inject(AuthService);

  protected readonly usuario = toSignal(this.auth.user$, { initialValue: null });
  protected readonly opciones = SYSTEM_PRINCIPAL_OPTIONS;

  protected salir(): void {
    this.auth.logout();
  }
}
