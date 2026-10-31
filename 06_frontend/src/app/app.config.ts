import { APP_BASE_HREF } from '@angular/common';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { AbstractSecurityStorage, LogLevel, provideAuth } from 'angular-auth-oidc-client';

import { Montaje, MONTAJE_NEWAPP } from './angular-page/montaje';
import { apiTokenInterceptor } from './core/auth/api-token.interceptor';
import { AuthService } from './core/auth/auth.service';
import { MemorySecurityStorage } from './core/auth/memory-security-storage';
import { OidcAuthService, usarCheckSession } from './core/auth/oidc-auth.service';
import { APP_CONFIG, AppConfig } from './core/config/app-config';

/** Providers de la app con el config.json ya leído y el montaje de la URL (main.ts). */
export function crearAppConfig(
  config: AppConfig,
  montaje: Montaje = MONTAJE_NEWAPP,
): ApplicationConfig {
  // Keycloak siempre regresa a la raíz del montaje (/newapp/ o /legacy/AngularPage/);
  // OidcAuthService lleva a la ruta pedida
  const raizDeLaApp = new URL(montaje.baseHref ?? './', document.baseURI).href;

  return {
    providers: [
      provideBrowserGlobalErrorListeners(),
      provideRouter(montaje.routes),
      // En /legacy/AngularPage el router no usa el <base href> (/newapp/), que sigue sirviendo los archivos
      ...(montaje.baseHref ? [{ provide: APP_BASE_HREF, useValue: montaje.baseHref }] : []),
      provideHttpClient(withInterceptors([apiTokenInterceptor])),
      { provide: APP_CONFIG, useValue: config },
      provideAuth({
        config: {
          authority: config.oidc.authority,
          clientId: config.oidc.clientId,
          scope: config.oidc.scope,
          responseType: 'code',
          redirectUrl: raizDeLaApp,
          postLogoutRedirectUri: raizDeLaApp,
          // Renovación con refresh token, sin iframe; el token nunca sale de la memoria
          useRefreshToken: true,
          silentRenew: true,
          renewTimeBeforeTokenExpiresInSeconds: 30,
          // Logout global: se entera cuando la sesión de Keycloak se cierra desde otra app
          startCheckSession: usarCheckSession(config.oidc.authority, location.origin),
          ignoreNonceAfterRefresh: true,
          // Los claims salen del id_token; no hace falta llamar a /userinfo
          autoUserInfo: false,
          // El token lo pone apiTokenInterceptor, no el interceptor de la librería
          secureRoutes: [],
          logLevel: LogLevel.Warn,
        },
      }),
      // Después de provideAuth: reemplaza su DefaultSessionStorageService
      {
        provide: AbstractSecurityStorage,
        useFactory: () => new MemorySecurityStorage(sessionStorage),
      },
      OidcAuthService,
      { provide: AuthService, useExisting: OidcAuthService },
      provideAppInitializer(() => inject(OidcAuthService).iniciar()),
    ],
  };
}
