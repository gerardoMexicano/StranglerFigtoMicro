import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';

import { AuthService } from '../../core/auth/auth.service';

/** Con sesión pero sin ninguno de los roles de /newapp (roleGuard manda aquí). */
@Component({
  selector: 'app-sin-acceso',
  templateUrl: './sin-acceso.html',
  styleUrl: './sin-acceso.css',
})
export class SinAcceso {
  private readonly auth = inject(AuthService);

  protected readonly usuario = toSignal(this.auth.user$, { initialValue: null });

  protected salir(): void {
    this.auth.logout();
  }
}
