import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-pagina-angular',
  imports: [RouterLink],
  template: `
    <main class="pagina">
      <a class="boton boton-texto" routerLink="/second-system-broken"
        >← Volver a Second System Broken</a
      >
      <h1>Página en Angular</h1>
      <p>Nuevo módulo desarrollado en Angular (hoja en Angular lista para su implementación).</p>
    </main>
  `,
})
export class PaginaAngular {}
