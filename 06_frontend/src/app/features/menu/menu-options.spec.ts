import { buildMenuOptions, filterByRole, MenuOption } from './menu-options';

describe('buildMenuOptions', () => {
  it('arma los dos accesos a los nuevos menús en Angular: system-principal y second-system-broken', () => {
    expect(buildMenuOptions().map((o) => o.id)).toEqual([
      'system-principal',
      'second-system-broken',
    ]);
  });

  it('ambos accesos son rutas internas de Angular sin referencias a legacy ni taxes', () => {
    const [systemPrincipal, secondSystemBroken] = buildMenuOptions();

    expect(systemPrincipal).toMatchObject({
      title: 'System Principal',
      url: '/system-principal',
      external: false,
    });
    expect(secondSystemBroken).toMatchObject({
      title: 'Second System Broken',
      url: '/second-system-broken',
      external: false,
    });
  });

  it('cada opción tiene título y descripción para la tarjeta', () => {
    for (const opcion of buildMenuOptions()) {
      expect(opcion.title.trim()).not.toBe('');
      expect(opcion.description.trim()).not.toBe('');
    }
  });

  it('ambos menús están disponibles para usuario, admin e invitado', () => {
    expect(Object.fromEntries(buildMenuOptions().map((o) => [o.id, o.roles]))).toEqual({
      'system-principal': ['usuario', 'admin', 'invitado'],
      'second-system-broken': ['usuario', 'admin', 'invitado'],
    });
  });

  it.each([
    [['usuario'], ['system-principal', 'second-system-broken']],
    [['admin'], ['system-principal', 'second-system-broken']],
    [['invitado'], ['system-principal', 'second-system-broken']],
    [[], []],
  ])('con roles %j ve %j', (roles, ids) => {
    expect(filterByRole(buildMenuOptions(), roles).map((o) => o.id)).toEqual(ids);
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
    opcion('system-principal', []),
    opcion('second-system-broken', ['admin', 'usuario']),
  ];

  it('una opción sin roles la ve cualquiera', () => {
    expect(filterByRole(opciones, ['invitado']).map((o) => o.id)).toEqual(['system-principal']);
  });

  it('una opción con roles solo la ve quien tenga alguno de ellos', () => {
    expect(filterByRole(opciones, ['usuario']).map((o) => o.id)).toEqual([
      'system-principal',
      'second-system-broken',
    ]);
    expect(filterByRole(opciones, ['admin']).map((o) => o.id)).toEqual([
      'system-principal',
      'second-system-broken',
    ]);
  });

  it('no altera la lista original', () => {
    filterByRole(opciones, []);
    expect(opciones).toHaveLength(2);
  });
});
