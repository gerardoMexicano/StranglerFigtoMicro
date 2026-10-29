import { ROLES_LECTURA, ROLES_LEGACY } from '../../core/auth/roles';
import { AppConfig } from '../../core/config/app-config';

export interface MenuOption {
  id: 'taxes' | 'legacy' | 'legfrontnewback' | 'usuarios';
  title: string;
  description: string;
  url: string;
  /** true: sale de Angular (app legacy en IIS). false: ruta de /newapp. */
  external: boolean;
  /** Roles de realm que pueden verla. Vacío = todos. */
  roles: readonly string[];
}

/** Las opciones del menú, con las URLs de las apps legacy tomadas de config.json. */
export function buildMenuOptions(config: AppConfig): MenuOption[] {
  return [
    {
      id: 'taxes',
      title: 'BrokenTaxes',
      description: 'Aplicación MVC (/taxes) en IIS.',
      url: config.apps.taxesUrl,
      external: true,
      roles: ROLES_LEGACY,
    },
    {
      id: 'legacy',
      title: 'Legacy WebForms',
      description: 'Aplicación WebForms (/legacy) en IIS.',
      url: config.apps.legacyUrl,
      external: true,
      roles: ROLES_LEGACY,
    },
    {
      id: 'legfrontnewback',
      title: 'LEgFrontNEWBACK',
      description: 'Ventas de WideWorldImporters desde el backend .NET 10.',
      url: '/legfrontnewback',
      external: false,
      roles: ROLES_LECTURA,
    },
    {
      id: 'usuarios',
      title: 'Usuarios',
      description: 'Checador de asistencia: registra tu ubicación (antes Usuarios.aspx).',
      url: '/usuarios',
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
