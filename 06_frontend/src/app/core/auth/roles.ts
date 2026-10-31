/** Roles de realm de Keycloak: los mismos que el plugin de migración trae del legacy (ROLE_MAP). */
export const ROLES = {
  usuario: 'usuario',
  admin: 'admin',
  invitado: 'invitado',
} as const;

/** Entrar a /newapp y consultar: cualquiera de los tres (invitado = solo consulta). */
export const ROLES_LECTURA: readonly string[] = [ROLES.usuario, ROLES.admin, ROLES.invitado];

/** Registrar datos (política UsuarioOrAdmin de 05_backend): invitado no. */
export const ROLES_ESCRITURA: readonly string[] = [ROLES.usuario, ROLES.admin];

/** Apps legacy (/taxes, /legacy): invitado no entra. */
export const ROLES_LEGACY: readonly string[] = [ROLES.usuario, ROLES.admin];
