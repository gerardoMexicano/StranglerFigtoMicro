import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-reportes',
  imports: [RouterLink],
  template: `
    <main class="pagina">
      <a class="boton boton-texto" routerLink="/system-principal">← Volver a System Principal</a>
      <h1>Reportes</h1>
      <p>Consulta y genera reportes del sistema (hoja en Angular lista para su implementación).</p>
    </main>
  `,
})
export class Reportes {}
