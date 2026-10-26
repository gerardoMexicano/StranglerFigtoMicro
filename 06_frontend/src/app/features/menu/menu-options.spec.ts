import { AppConfig } from '../../core/config/app-config';
import { buildMenuOptions, filterByRole, MenuOption } from './menu-options';

// URLs distintas a las de producción: prueban que salen de config.json y no del código
const config: AppConfig = {
  oidc: { authority: 'http://kc/auth/realms/lab', clientId: 'frontend-spa', scope: 'openid' },
  apiBaseUrl: '/api',
  apps: {
    taxesUrl: 'http://otro-host/taxes/Home/Menu',
    legacyUrl: 'http://otro-host/legacy/Menu',
  },
};

describe('buildMenuOptions', () => {
  it('arma las tres opciones en orden: taxes, legacy y legfrontnewback', () => {
    expect(buildMenuOptions(config).map((o) => o.id)).toEqual([
      'taxes',
      'legacy',
      'legfrontnewback',
    ]);
  });

  it('taxes y legacy salen de Angular hacia las URLs de config.json', () => {
    const [taxes, legacy] = buildMenuOptions(config);

    expect(taxes).toMatchObject({ url: 'http://otro-host/taxes/Home/Menu', external: true });
    expect(legacy).toMatchObject({ url: 'http://otro-host/legacy/Menu', external: true });
  });

  it('legfrontnewback es una ruta interna de /newapp', () => {
    const legfrontnewback = buildMenuOptions(config)[2];

    expect(legfrontnewback).toMatchObject({ url: '/legfrontnewback', external: false });
  });

  it('cada opción tiene título y descripción para la tarjeta', () => {
    for (const opcion of buildMenuOptions(config)) {
      expect(opcion.title.trim()).not.toBe('');
      expect(opcion.description.trim()).not.toBe('');
    }
  });

  it('en esta fase ninguna opción está restringida por rol', () => {
    expect(buildMenuOptions(config).every((o) => o.roles.length === 0)).toBe(true);
  });

  it('un usuario sin roles ve las tres opciones', () => {
    expect(filterByRole(buildMenuOptions(config), [])).toHaveLength(3);
  });
});

describe('filterByRole (preparado para la división por roles)', () => {
  const opcion = (id: MenuOption['id'], roles: string[]): MenuOption => ({
    id,
    title: id,
    description: id,
    url: `/${id}`,
    external: false,
    roles,
  });

  const opciones = [
    opcion('taxes', []),
    opcion('legacy', ['admin']),
    opcion('legfrontnewback', ['admin', 'usuario']),
  ];

  it('una opción sin roles la ve cualquiera', () => {
    expect(filterByRole(opciones, ['invitado']).map((o) => o.id)).toEqual(['taxes']);
  });

  it('una opción con roles solo la ve quien tenga alguno de ellos', () => {
    expect(filterByRole(opciones, ['usuario']).map((o) => o.id)).toEqual([
      'taxes',
      'legfrontnewback',
    ]);
    expect(filterByRole(opciones, ['admin']).map((o) => o.id)).toEqual([
      'taxes',
      'legacy',
      'legfrontnewback',
    ]);
  });

  it('no altera la lista original', () => {
    filterByRole(opciones, []);
    expect(opciones).toHaveLength(3);
  });
});
