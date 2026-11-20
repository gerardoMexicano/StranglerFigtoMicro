import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { PaginaAngular } from './pagina-angular';
import { PaginaAntigua } from './pagina-antigua';

describe('Hojas vacías de Second System Broken', () => {
  async function pintar(componente: Type<unknown>): Promise<HTMLElement> {
    TestBed.configureTestingModule({
      imports: [componente],
      providers: [provideRouter([])],
    });
    const fixture = TestBed.createComponent(componente);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it.each([
    [PaginaAntigua, 'Página Antigua'],
    [PaginaAngular, 'Página en Angular'],
  ])(
    'pinta %s con título "%s" y enlace de regreso a /second-system-broken',
    async (comp, titulo) => {
      const vista = await pintar(comp);

      expect(vista.querySelector('h1')?.textContent?.trim()).toBe(titulo);
      expect(vista.querySelector('a')?.getAttribute('href')).toBe('/second-system-broken');
    },
  );
});
