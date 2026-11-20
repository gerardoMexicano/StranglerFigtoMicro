import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink],
  template: `
    <main class="pagina">
      <a class="boton boton-texto" routerLink="/system-principal">← Volver a System Principal</a>
      <h1>Dashboard</h1>
      <p>Vista general y estadísticas del sistema (hoja en Angular lista para su implementación).</p>
    </main>
  `,
})
export class Dashboard {}
