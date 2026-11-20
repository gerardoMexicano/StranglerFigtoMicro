import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-pagina-antigua',
  imports: [RouterLink],
  template: `
    <main class="pagina">
      <a class="boton boton-texto" routerLink="/second-system-broken"
        >← Volver a Second System Broken</a
      >
      <h1>Página Antigua</h1>
      <p>Módulo base del segundo sistema (hoja en Angular lista para su implementación).</p>
    </main>
  `,
})
export class PaginaAntigua {}
