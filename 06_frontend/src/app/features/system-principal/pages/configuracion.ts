import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-configuracion',
  imports: [RouterLink],
  template: `
    <main class="pagina">
      <a class="boton boton-texto" routerLink="/system-principal">← Volver a System Principal</a>
      <h1>Configuración</h1>
      <p>Ajustes generales de la aplicación (hoja en Angular lista para su implementación).</p>
    </main>
  `,
})
export class Configuracion {}
