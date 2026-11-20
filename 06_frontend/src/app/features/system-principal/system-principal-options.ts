export interface SystemPrincipalOption {
  id:
    | 'reportes'
    | 'usuarios'
    | 'configuracion'
    | 'dashboard'
    | 'second-system-broken'
    | 'salas'
    | 'legfrontnewback';
  icon: string;
  title: string;
  description: string;
  buttonLabel: string;
  url: string;
}

/** Opciones del menú System Principal: reemplazo en Angular de los 7 botones del menú principal anterior. */
export const SYSTEM_PRINCIPAL_OPTIONS: readonly SystemPrincipalOption[] = [
  {
    id: 'reportes',
    icon: '📋',
    title: 'Reportes',
    description: 'Consulta y genera reportes del sistema.',
    buttonLabel: 'Ir a Reportes',
    url: '/system-principal/reportes',
  },
  {
    id: 'usuarios',
    icon: '👥',
    title: 'Usuarios',
    description: 'Administración de usuarios y checador de asistencia del sistema.',
    buttonLabel: 'Ir a Usuarios',
    url: '/usuarios',
  },
  {
    id: 'configuracion',
    icon: '⚙️',
    title: 'Configuración',
    description: 'Ajustes generales de la aplicación.',
    buttonLabel: 'Ir a Configuración',
    url: '/system-principal/configuracion',
  },
  {
    id: 'dashboard',
    icon: '📊',
    title: 'Dashboard',
    description: 'Vista general y estadísticas del sistema.',
    buttonLabel: 'Ir a Dashboard',
    url: '/system-principal/dashboard',
  },
  {
    id: 'second-system-broken',
    icon: '🚀',
    title: 'Second System Broken',
    description: 'Acceso directo al segundo sistema en Angular.',
    buttonLabel: 'Ir a Second System Broken',
    url: '/second-system-broken',
  },
  {
    id: 'salas',
    icon: '⭐',
    title: 'Reservación de salas',
    description: 'Agenda del día y reservación de salas de juntas en Angular.',
    buttonLabel: 'Ir a Reservación de salas',
    url: '/salas',
  },
  {
    id: 'legfrontnewback',
    icon: '🥀',
    title: 'LEgFrontNEWBACK',
    description: 'Ventas de WideWorldImporters desde el backend .NET 10.',
    buttonLabel: 'Ir a LEgFrontNEWBACK',
    url: '/legfrontnewback',
  },
];
