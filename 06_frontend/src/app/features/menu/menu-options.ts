import { ROLES_LECTURA } from '../../core/auth/roles';

export interface MenuOption {
  id: 'system-principal' | 'second-system-broken';
  title: string;
  description: string;
  url: string;
  /** Todas las rutas del nuevo menú viven dentro de Angular (/newapp). */
  external: false;
  /** Roles de realm que pueden verla. Vacío = todos. */
  roles: readonly string[];
}

/** Opciones del menú principal (/newapp/menu): accesos a los dos sistemas en Angular, sin referencias a legacy ni broken. */
export function buildMenuOptions(): MenuOption[] {
  return [
    {
      id: 'system-principal',
      title: 'System Principal',
      description:
        'Sistema principal en Angular: reportes, usuarios, configuración, dashboard, reservación de salas y ventas.',
      url: '/system-principal',
      external: false,
      roles: ROLES_LECTURA,
    },
    {
      id: 'second-system-broken',
      title: 'Second System Broken',
      description:
        'Segundo sistema en Angular: módulos de consulta, reporte consolidado con nuevo backend y comparativa.',
      url: '/second-system-broken',
      external: false,
      roles: ROLES_LECTURA,
    },
  ];
}

/** Deja las opciones que el usuario puede ver según sus roles. */
export function filterByRole(
  options: readonly MenuOption[],
  userRoles: readonly string[],
): MenuOption[] {
  return options.filter(
    (option) => option.roles.length === 0 || option.roles.some((rol) => userRoles.includes(rol)),
  );
}
