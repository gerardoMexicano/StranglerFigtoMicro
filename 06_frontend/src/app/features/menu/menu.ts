import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { APP_CONFIG } from '../../core/config/app-config';
import { buildMenuOptions, filterByRole } from './menu-options';

/** Menú de /newapp: solo redirige; las apps legacy siguen desplegadas como siempre. */
@Component({
  selector: 'app-menu',
  imports: [RouterLink],
  templateUrl: './menu.html',
  styleUrl: './menu.css',
})
export class Menu {
  private readonly auth = inject(AuthService);
  private readonly todas = buildMenuOptions(inject(APP_CONFIG));

  protected readonly usuario = toSignal(this.auth.user$, { initialValue: null });

  protected readonly opciones = computed(() =>
    filterByRole(this.todas, this.usuario()?.roles ?? []),
  );

  protected salir(): void {
    this.auth.logout();
  }
}
