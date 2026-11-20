export interface SecondSystemBrokenOption {
  id:
    | 'pagina-antigua'
    | 'pagina-antigua-nuevo-backend'
    | 'pagina-angular'
    | 'pagina-principal';
  title: string;
  description: string;
  buttonLabel: string;
  url: string;
}

/** Opciones del menú Second System Broken: reemplazo en Angular de los 4 botones del segundo menú anterior. */
export const SECOND_SYSTEM_BROKEN_OPTIONS: readonly SecondSystemBrokenOption[] = [
  {
    id: 'pagina-antigua',
    title: 'Página Antigua',
    description: 'Módulo base del segundo sistema en Angular.',
    buttonLabel: 'Ir a Página Antigua',
    url: '/second-system-broken/pagina-antigua',
  },
  {
    id: 'pagina-antigua-nuevo-backend',
    title: 'Página Antigua (Nuevo Backend)',
    description: 'Consulta conectada al nuevo backend .NET 10.',
    buttonLabel: 'Ir a Backend Nuevo',
    url: '/second-system-broken/pagina-antigua-nuevo-backend',
  },
  {
    id: 'pagina-angular',
    title: 'Página en Angular',
    description: 'Nuevo módulo desarrollado en Angular.',
    buttonLabel: 'Ir a Angular',
    url: '/second-system-broken/pagina-angular',
  },
  {
    id: 'pagina-principal',
    title: 'Página Principal',
    description: 'Redirige al menú de System Principal.',
    buttonLabel: 'Ir a System Principal',
    url: '/system-principal',
  },
];
