import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import { ROLES_ESCRITURA, ROLES_LECTURA, ROLES_LEGACY } from '../../../core/auth/roles';
import { APP_CONFIG } from '../../../core/config/app-config';

export interface CurrentUserDto {
  userId: string;
  userName: string;
  email: string | null;
  roles: string[];
}

export type TemaVisual = 'claro' | 'oscuro' | 'sistema';
export type DensidadTabla = 'normal' | 'compacta';

export interface PreferenciasLocales {
  tema: TemaVisual;
  densidad: DensidadTabla;
  tamanoPagina: 25 | 50 | 100;
}

export const PREFERENCIAS_STORAGE_KEY = 'lab_preferencias_ui';

export const PREFERENCIAS_POR_DEFECTO: PreferenciasLocales = {
  tema: 'sistema',
  densidad: 'normal',
  tamanoPagina: 50,
};

/**
 * L3 — Configuración: migración y definición funcional de /legacy/Configuracion.aspx.
 * Muestra el perfil de identidad y sesión validado por 05_backend (GET /api/system/me),
 * los permisos por rol de Keycloak, la configuración del entorno actual (config.json)
 * y permite ajustar y persistir preferencias visuales locales en el navegador.
 */
@Component({
  selector: 'app-configuracion',
  imports: [FormsModule, RouterLink],
  template: `
    <main class="pagina">
      <a class="boton boton-texto" routerLink="/system-principal">← Volver a System Principal</a>

      <header class="encabezado">
        <div class="icono" aria-hidden="true">⚙️</div>
        <div>
          <h1>Configuración</h1>
          <p class="subtitulo">
            Panel de perfil OIDC (Keycloak), verificación de sesión contra 05_backend y
            preferencias locales de la aplicación.
          </p>
        </div>
      </header>

      @if (avisoGuardado()) {
        <div class="aviso-exito" role="status">
          {{ avisoGuardado() }}
        </div>
      }

      <div class="grid-paneles">
        <section class="panel" aria-labelledby="titulo-identidad">
          <div class="panel-header">
            <h2 id="titulo-identidad">Identidad y Sesión (Keycloak / 05_backend)</h2>
            <button
              id="btnVerificarBackend"
              type="button"
              class="boton-secundario"
              (click)="recargarPerfil()"
            >
              Verificar con Backend
            </button>
          </div>

          <dl class="lista-datos">
            <div>
              <dt>Usuario (Sesión SPA)</dt>
              <dd>{{ usuarioSpa()?.userName || 'Sin sesión' }}</dd>
            </div>
            <div>
              <dt>Nombre para mostrar</dt>
              <dd>{{ usuarioSpa()?.name || '-' }}</dd>
            </div>
            @if (perfilBackend.hasValue()) {
              @let me = perfilBackend.value();
              <div>
                <dt>Subject ID (<code>sub</code> en 05_backend)</dt>
                <dd><code>{{ me.userId }}</code></dd>
              </div>
              <div>
                <dt>Correo electrónico</dt>
                <dd>{{ me.email || 'No especificado en el token' }}</dd>
              </div>
              <div>
                <dt>Estado del Resource Server (.NET 10)</dt>
                <dd class="badge-ok">Token JWT validado en /api/system/me</dd>
              </div>
            } @else if (perfilBackend.error()) {
              <div>
                <dt>Estado del Resource Server (.NET 10)</dt>
                <dd class="badge-error">No se pudo verificar /api/system/me</dd>
              </div>
            }
          </dl>

          <div class="roles-box">
            <h3>Roles del Realm y Permisos</h3>
            <div class="chips">
              @for (rol of rolesActivos(); track rol) {
                <span class="chip-rol">{{ rol }}</span>
              } @empty {
                <span class="chip-vacio">Sin roles asignados</span>
              }
            </div>
            <ul class="lista-permisos">
              <li>
                <strong>Consulta en SPA y API (<code>Lectura</code>):</strong>
                {{ tieneLectura() ? 'Permitido' : 'Denegado' }}
              </li>
              <li>
                <strong>Registro y Carga Masiva (<code>UsuarioOrAdmin</code>):</strong>
                {{ tieneEscritura() ? 'Permitido' : 'Solo lectura (invitado)' }}
              </li>
              <li>
                <strong>Acceso a Módulos Legacy (<code>/legacy</code>, <code>/taxes</code>):</strong>
                {{ tieneAccesoLegacy() ? 'Permitido' : 'Restringido' }}
              </li>
            </ul>
          </div>
        </section>

        <section class="panel" aria-labelledby="titulo-preferencias">
          <h2 id="titulo-preferencias">Preferencias Visuales del Navegador</h2>
          <p class="ayuda">
            Estos ajustes se guardan localmente en tu navegador para personalizar la visualización
            en Angular.
          </p>

          <form class="form-preferencias" (ngSubmit)="guardarPreferencias()">
            <label class="campo">
              <span>Tema visual</span>
              <select
                id="selectTema"
                name="tema"
                [ngModel]="tema()"
                (ngModelChange)="tema.set($event)"
              >
                <option value="sistema">Automático del sistema</option>
                <option value="claro">Claro</option>
                <option value="oscuro">Oscuro</option>
              </select>
            </label>

            <label class="campo">
              <span>Densidad de tablas</span>
              <select
                id="selectDensidad"
                name="densidad"
                [ngModel]="densidad()"
                (ngModelChange)="densidad.set($event)"
              >
                <option value="normal">Normal (espaciado estándar)</option>
                <option value="compacta">Compacta (más filas en pantalla)</option>
              </select>
            </label>

            <label class="campo">
              <span>Tamaño de página preferido</span>
              <select
                id="selectTamanoPagina"
                name="tamanoPagina"
                [ngModel]="tamanoPagina()"
                (ngModelChange)="tamanoPagina.set($event)"
              >
                <option [ngValue]="25">25 registros</option>
                <option [ngValue]="50">50 registros</option>
                <option [ngValue]="100">100 registros</option>
              </select>
            </label>

            <div class="acciones-form">
              <button id="btnGuardarPreferencias" type="submit" class="boton-primario">
                Guardar preferencias
              </button>
              <button
                id="btnRestaurarPreferencias"
                type="button"
                class="boton-secundario"
                (click)="restaurarPreferencias()"
              >
                Restaurar valores por defecto
              </button>
            </div>
          </form>
        </section>

        <section class="panel" aria-labelledby="titulo-entorno">
          <h2 id="titulo-entorno">Información del Entorno (Strangler Fig)</h2>
          <dl class="lista-datos">
            <div>
              <dt>Issuer OIDC (Keycloak Authority)</dt>
              <dd><code>{{ config.oidc.authority }}</code></dd>
            </div>
            <div>
              <dt>Client ID SPA</dt>
              <dd><code>{{ config.oidc.clientId }}</code></dd>
            </div>
            <div>
              <dt>Prefijo Backend .NET 10 (Gateway YARP)</dt>
              <dd><code>{{ config.apiBaseUrl }}</code></dd>
            </div>
            <div>
              <dt>Ruta Sistema Principal Legacy (WebForms)</dt>
              <dd><code>{{ config.apps.legacyUrl }}</code></dd>
            </div>
            <div>
              <dt>Ruta Second System Legacy (ASP.NET MVC)</dt>
              <dd><code>{{ config.apps.taxesUrl }}</code></dd>
            </div>
          </dl>
        </section>
      </div>
    </main>
  `,
  styles: `
    .pagina {
      max-width: 1040px;
      margin: 0 auto;
      padding: 1.5rem 1rem 3rem;
    }
    .encabezado {
      display: flex;
      align-items: center;
      gap: 1rem;
      margin: 0.75rem 0 1.5rem;
    }
    .icono {
      font-size: 2.75rem;
    }
    h1 {
      margin: 0;
      color: #0f172a;
      font-size: 1.85rem;
    }
    .subtitulo {
      margin: 0.25rem 0 0;
      color: #475569;
    }
    .grid-paneles {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
      gap: 1.25rem;
    }
    .panel {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      padding: 1.25rem;
      box-shadow: 0 1px 3px rgba(15, 23, 42, 0.05);
    }
    .panel-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 0.5rem;
      margin-bottom: 0.75rem;
    }
    .panel h2 {
      margin: 0 0 0.75rem;
      font-size: 1.15rem;
      color: #0f172a;
    }
    .panel-header h2 {
      margin: 0;
    }
    .lista-datos {
      margin: 0;
      display: flex;
      flex-direction: column;
      gap: 0.65rem;
    }
    .lista-datos div {
      display: flex;
      flex-direction: column;
      gap: 0.15rem;
      padding-bottom: 0.5rem;
      border-bottom: 1px solid #f1f5f9;
    }
    .lista-datos dt {
      font-size: 0.78rem;
      text-transform: uppercase;
      color: #64748b;
      font-weight: 600;
    }
    .lista-datos dd {
      margin: 0;
      font-weight: 600;
      color: #1e293b;
      word-break: break-all;
    }
    .badge-ok {
      color: #065f46 !important;
    }
    .badge-error {
      color: #991b1b !important;
    }
    .roles-box {
      margin-top: 1rem;
      padding-top: 0.75rem;
    }
    .roles-box h3 {
      font-size: 0.95rem;
      margin: 0 0 0.5rem;
      color: #334155;
    }
    .chips {
      display: flex;
      flex-wrap: wrap;
      gap: 0.4rem;
      margin-bottom: 0.75rem;
    }
    .chip-rol {
      background: #e0f2fe;
      color: #0369a1;
      padding: 0.25rem 0.65rem;
      border-radius: 999px;
      font-size: 0.82rem;
      font-weight: 700;
    }
    .chip-vacio {
      color: #64748b;
      font-size: 0.85rem;
    }
    .lista-permisos {
      margin: 0;
      padding-left: 1.2rem;
      font-size: 0.88rem;
      color: #334155;
      line-height: 1.6;
    }
    .ayuda {
      color: #64748b;
      font-size: 0.88rem;
      margin: 0 0 1rem;
    }
    .form-preferencias {
      display: flex;
      flex-direction: column;
      gap: 0.9rem;
    }
    .campo {
      display: flex;
      flex-direction: column;
      gap: 0.3rem;
      font-size: 0.88rem;
      font-weight: 600;
      color: #334155;
    }
    .campo select {
      padding: 0.5rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-size: 0.95rem;
      background: #fff;
    }
    .acciones-form {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
      margin-top: 0.35rem;
    }
    .boton-primario {
      padding: 0.55rem 1rem;
      background: #0f3460;
      color: #ffffff;
      border: none;
      border-radius: 6px;
      font-weight: 600;
      cursor: pointer;
    }
    .boton-secundario {
      padding: 0.5rem 0.85rem;
      background: #f1f5f9;
      color: #1e293b;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-weight: 600;
      cursor: pointer;
    }
    .aviso-exito {
      background: #ecfdf5;
      color: #065f46;
      border: 1px solid #a7f3d0;
      padding: 0.75rem 1rem;
      border-radius: 8px;
      margin-bottom: 1rem;
      font-weight: 600;
    }
  `,
})
export class Configuracion {
  private readonly http = inject(HttpClient);
  protected readonly config = inject(APP_CONFIG);
  protected readonly usuarioSpa = toSignal(inject(AuthService).user$, { initialValue: null });

  private readonly meUrl = `${this.config.apiBaseUrl.replace(/\/+$/, '')}/system/me`;

  protected readonly perfilBackend = rxResource({
    stream: () => this.http.get<CurrentUserDto>(this.meUrl),
  });

  protected readonly rolesActivos = computed(() => {
    if (this.perfilBackend.hasValue()) {
      return this.perfilBackend.value().roles;
    }
    return this.usuarioSpa()?.roles ?? [];
  });

  protected readonly tieneLectura = computed(() =>
    this.rolesActivos().some((r) => ROLES_LECTURA.includes(r)),
  );

  protected readonly tieneEscritura = computed(() =>
    this.rolesActivos().some((r) => ROLES_ESCRITURA.includes(r)),
  );

  protected readonly tieneAccesoLegacy = computed(() =>
    this.rolesActivos().some((r) => ROLES_LEGACY.includes(r)),
  );

  private readonly iniciales = leerPreferenciasGuardadas();

  protected readonly tema = signal<TemaVisual>(this.iniciales.tema);
  protected readonly densidad = signal<DensidadTabla>(this.iniciales.densidad);
  protected readonly tamanoPagina = signal<25 | 50 | 100>(this.iniciales.tamanoPagina);
  protected readonly avisoGuardado = signal<string | null>(null);

  protected recargarPerfil(): void {
    this.perfilBackend.reload();
  }

  protected guardarPreferencias(): void {
    const prefs: PreferenciasLocales = {
      tema: this.tema(),
      densidad: this.densidad(),
      tamanoPagina: this.tamanoPagina(),
    };
    try {
      localStorage.setItem(PREFERENCIAS_STORAGE_KEY, JSON.stringify(prefs));
    } catch {
      // Ignorar si localStorage no está disponible
    }
    this.avisoGuardado.set('Preferencias guardadas correctamente en este navegador.');
  }

  protected restaurarPreferencias(): void {
    this.tema.set(PREFERENCIAS_POR_DEFECTO.tema);
    this.densidad.set(PREFERENCIAS_POR_DEFECTO.densidad);
    this.tamanoPagina.set(PREFERENCIAS_POR_DEFECTO.tamanoPagina);
    try {
      localStorage.removeItem(PREFERENCIAS_STORAGE_KEY);
    } catch {
      // Ignorar
    }
    this.avisoGuardado.set('Se restauraron las preferencias por defecto.');
  }
}

function leerPreferenciasGuardadas(): PreferenciasLocales {
  try {
    const raw = localStorage.getItem(PREFERENCIAS_STORAGE_KEY);
    if (!raw) {
      return PREFERENCIAS_POR_DEFECTO;
    }
    const parsed = JSON.parse(raw) as Partial<PreferenciasLocales>;
    return {
      tema:
        parsed.tema === 'claro' || parsed.tema === 'oscuro' || parsed.tema === 'sistema'
          ? parsed.tema
          : PREFERENCIAS_POR_DEFECTO.tema,
      densidad:
        parsed.densidad === 'compacta' || parsed.densidad === 'normal'
          ? parsed.densidad
          : PREFERENCIAS_POR_DEFECTO.densidad,
      tamanoPagina:
        parsed.tamanoPagina === 25 || parsed.tamanoPagina === 50 || parsed.tamanoPagina === 100
          ? parsed.tamanoPagina
          : PREFERENCIAS_POR_DEFECTO.tamanoPagina,
    };
  } catch {
    return PREFERENCIAS_POR_DEFECTO;
  }
}
