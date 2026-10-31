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
  it('arma las cinco opciones en orden: taxes, legacy, legfrontnewback, usuarios y salas', () => {
    expect(buildMenuOptions(config).map((o) => o.id)).toEqual([
      'taxes',
      'legacy',
      'legfrontnewback',
      'usuarios',
      'salas',
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

  it('usuarios es una ruta interna de /newapp: ya no abre Usuarios.aspx', () => {
    const usuarios = buildMenuOptions(config)[3];

    expect(usuarios).toMatchObject({ url: '/usuarios', external: false });
  });

  it('salas abre /legacy/AngularPage junto al menú legacy de config.json: es Angular dentro de WebForms', () => {
    const salas = buildMenuOptions(config)[4];

    expect(salas).toMatchObject({ url: 'http://otro-host/legacy/AngularPage', external: true });
  });

  it('cada opción tiene título y descripción para la tarjeta', () => {
    for (const opcion of buildMenuOptions(config)) {
      expect(opcion.title.trim()).not.toBe('');
      expect(opcion.description.trim()).not.toBe('');
    }
  });

  it('las apps legacy son para usuario y admin; las vistas de /newapp también para invitado', () => {
    expect(Object.fromEntries(buildMenuOptions(config).map((o) => [o.id, o.roles]))).toEqual({
      taxes: ['usuario', 'admin'],
      legacy: ['usuario', 'admin'],
      legfrontnewback: ['usuario', 'admin', 'invitado'],
      usuarios: ['usuario', 'admin', 'invitado'],
      salas: ['usuario', 'admin', 'invitado'],
    });
  });

  it.each([
    [['usuario'], ['taxes', 'legacy', 'legfrontnewback', 'usuarios', 'salas']],
    [['admin'], ['taxes', 'legacy', 'legfrontnewback', 'usuarios', 'salas']],
    [['invitado'], ['legfrontnewback', 'usuarios', 'salas']],
    [[], []],
  ])('con roles %j ve %j', (roles, ids) => {
    expect(filterByRole(buildMenuOptions(config), roles).map((o) => o.id)).toEqual(ids);
  });
});

describe('filterByRole', () => {
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
