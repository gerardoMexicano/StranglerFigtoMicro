import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { Configuracion } from './configuracion';
import { Reportes } from './reportes';

describe('Hojas vacías de System Principal', () => {
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
    [Reportes, 'Reportes'],
    [Configuracion, 'Configuración'],
  ])('pinta %s con título "%s" y enlace de regreso a /system-principal', async (comp, titulo) => {
    const vista = await pintar(comp);

    expect(vista.querySelector('h1')?.textContent?.trim()).toBe(titulo);
    expect(vista.querySelector('a')?.getAttribute('href')).toBe('/system-principal');
  });
});
