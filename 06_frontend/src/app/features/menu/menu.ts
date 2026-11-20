import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { buildMenuOptions, filterByRole } from './menu-options';

/** Menú de bienvenida en /newapp/menu: da acceso a los dos submenús en Angular (System Principal y Second System Broken). */
@Component({
  selector: 'app-menu',
  imports: [RouterLink],
  templateUrl: './menu.html',
  styleUrl: './menu.css',
})
export class Menu {
  private readonly auth = inject(AuthService);
  private readonly todas = buildMenuOptions();

  protected readonly usuario = toSignal(this.auth.user$, { initialValue: null });

  protected readonly opciones = computed(() =>
    filterByRole(this.todas, this.usuario()?.roles ?? []),
  );

  protected salir(): void {
    this.auth.logout();
  }
}
