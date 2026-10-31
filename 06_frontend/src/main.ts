import { bootstrapApplication } from '@angular/platform-browser';
import { crearAppConfig } from './app/app.config';
import { App } from './app/app';
import { montajePara } from './app/angular-page/montaje';
import { loadAppConfig } from './app/core/config/app-config';

// config.json vive junto a index.html (/newapp/config.json); el contenedor lo genera al arrancar.
// También desde /legacy/AngularPage: el <base href> sigue siendo /newapp/.
loadAppConfig(new URL('config.json', document.baseURI).href)
  .then((config) =>
    bootstrapApplication(App, crearAppConfig(config, montajePara(location.pathname))),
  )
  .catch((err) => {
    console.error(err);
    mostrarError(err instanceof Error ? err.message : String(err));
  });

/** Sin configuración no hay app: se avisa en la página en lugar de dejarla en blanco. */
function mostrarError(mensaje: string): void {
  const titulo = document.createElement('h1');
  titulo.textContent = 'No se pudo iniciar la aplicación';
  const detalle = document.createElement('p');
  detalle.textContent = mensaje;
  const contenedor = document.createElement('main');
  contenedor.setAttribute('role', 'alert');
  contenedor.append(titulo, detalle);
  document.body.replaceChildren(contenedor);
}
