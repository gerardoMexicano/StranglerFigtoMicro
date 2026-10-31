import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter, RouterOutlet } from '@angular/router';

import { APP_CONFIG, AppConfig } from '../core/config/app-config';
import { MarcoLegacy } from './marco-legacy';

const config: AppConfig = {
  oidc: { authority: 'http://kc/auth/realms/lab', clientId: 'frontend-spa', scope: 'openid' },
  apiBaseUrl: '/api',
  apps: { taxesUrl: '/taxes/Home/Menu', legacyUrl: 'https://192.168.50.10/legacy/Menu' },
};

describe('MarcoLegacy', () => {
  async function pintar() {
    TestBed.configureTestingModule({
      imports: [MarcoLegacy],
      providers: [provideRouter([]), { provide: APP_CONFIG, useValue: config }],
    });
    const fixture = TestBed.createComponent(MarcoLegacy);
    await fixture.whenStable();
    return fixture;
  }

  it('como el Site.Master: marca, Inicio y Menú van a WebForms con página completa', async () => {
    const vista = (await pintar()).nativeElement as HTMLElement;

    const enlaces = [...vista.querySelectorAll<HTMLAnchorElement>('nav a')].map((a) => [
      a.textContent?.trim(),
      a.getAttribute('href'),
    ]);
    expect(enlaces).toEqual([
      ['Sistema Laboratorio', 'https://192.168.50.10/legacy/'],
      ['Inicio', 'https://192.168.50.10/legacy/'],
      ['Menú', 'https://192.168.50.10/legacy/Menu'],
    ]);
  });

  it('pinta la ruta dentro y el pie del Site.Master con el año en curso', async () => {
    const fixture = await pintar();

    expect(fixture.debugElement.query(By.directive(RouterOutlet))).not.toBeNull();
    expect(fixture.nativeElement.querySelector('footer')?.textContent?.trim()).toBe(
      `© ${new Date().getFullYear()} - Sistema Laboratorio`,
    );
  });
});
