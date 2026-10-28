import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';

/** Presentación pública de /newapp: única puerta de entrada al login de Keycloak. */
@Component({
  selector: 'app-inicio',
  imports: [RouterLink],
  templateUrl: './inicio.html',
  styleUrl: './inicio.css',
})
export class Inicio {
  private readonly auth = inject(AuthService);

  protected readonly conSesion = toSignal(this.auth.isAuthenticated$, { initialValue: false });

  protected entrar(): void {
    this.auth.login('/menu');
  }
}
