import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-pagina-antigua-nuevo-backend',
  imports: [RouterLink],
  template: `
    <main class="pagina">
      <a class="boton boton-texto" routerLink="/second-system-broken"
        >← Volver a Second System Broken</a
      >
      <h1>Página Antigua (Nuevo Backend)</h1>
      <p>
        Consulta conectada al nuevo backend .NET 10 (hoja en Angular lista para su implementación).
      </p>
    </main>
  `,
})
export class PaginaAntiguaNuevoBackend {}
