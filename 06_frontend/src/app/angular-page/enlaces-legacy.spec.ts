import { enlacesLegacy } from './enlaces-legacy';

describe('enlacesLegacy', () => {
  it('sale de apps.legacyUrl: Inicio es /legacy/, Menú la URL tal cual y AngularPage junto a ella', () => {
    expect(enlacesLegacy('https://192.168.50.10/legacy/Menu')).toEqual({
      inicio: 'https://192.168.50.10/legacy/',
      menu: 'https://192.168.50.10/legacy/Menu',
      angularPage: 'https://192.168.50.10/legacy/AngularPage',
    });
  });

  it('una URL relativa (config.json de desarrollo) se completa con el origen de la página', () => {
    expect(enlacesLegacy('/legacy/Menu', 'http://localhost:4200')).toEqual({
      inicio: 'http://localhost:4200/legacy/',
      menu: 'http://localhost:4200/legacy/Menu',
      angularPage: 'http://localhost:4200/legacy/AngularPage',
    });
  });
});
