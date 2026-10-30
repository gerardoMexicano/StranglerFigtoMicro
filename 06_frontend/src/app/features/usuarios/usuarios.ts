import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';
import { ROLES_ESCRITURA } from '../../core/auth/roles';
import { AsistenciaApiService } from './asistencia-api.service';
import { Geolocalizacion, GeolocalizacionError, Ubicacion } from './geolocalizacion';
import { MapaAsistencia } from './mapa-asistencia';
import { NotificacionNativa } from './notificacion-nativa';

export type Estado = 'listo' | 'ubicando' | 'registrando';

export interface Aviso {
  tipo: 'exito' | 'error' | 'info';
  texto: string;
}

/**
 * Checador de Asistencia: reemplaza /legacy/Usuarios.aspx. Igual que la página WebForms ubica al
 * usuario y lo muestra en el mapa; además registra la asistencia en 05_backend y muestra el
 * historial. invitado solo ve su ubicación: el backend no le deja registrar.
 */
@Component({
  selector: 'app-usuarios',
  imports: [DatePipe, DecimalPipe, MapaAsistencia, RouterLink],
  templateUrl: './usuarios.html',
  styleUrl: './usuarios.css',
})
export class Usuarios {
  private readonly api = inject(AsistenciaApiService);
  private readonly geolocalizacion = inject(Geolocalizacion);
  private readonly notificacion = inject(NotificacionNativa);
  private readonly usuario = toSignal(inject(AuthService).user$, { initialValue: null });

  protected readonly puedeRegistrar = computed(
    () => this.usuario()?.roles.some((rol) => ROLES_ESCRITURA.includes(rol)) ?? false,
  );

  protected readonly estado = signal<Estado>('listo');
  protected readonly ubicacion = signal<Ubicacion | null>(null);
  protected readonly aviso = signal<Aviso | null>(null);

  protected readonly textoBoton = computed(() => {
    switch (this.estado()) {
      case 'ubicando':
        return 'Obteniendo ubicación...';
      case 'registrando':
        return 'Registrando asistencia...';
      default:
        if (this.ubicacion() === null) {
          return this.puedeRegistrar() ? 'Registrar mi asistencia' : 'Obtener mi ubicación';
        }
        return this.puedeRegistrar() ? 'Registrar de nuevo' : 'Actualizar mi ubicación';
    }
  });

  protected readonly historial = rxResource({ stream: () => this.api.misRegistros() });

  protected async checar(): Promise<void> {
    if (this.estado() !== 'listo') {
      return;
    }
    this.aviso.set(null);

    this.estado.set('ubicando');
    let ubicacion: Ubicacion;
    try {
      ubicacion = await this.geolocalizacion.obtener();
    } catch (e) {
      this.terminar({ tipo: 'error', texto: mensajeDeGeolocalizacion(e) });
      return;
    }
    this.ubicacion.set(ubicacion);

    if (!this.puedeRegistrar()) {
      this.terminar({
        tipo: 'info',
        texto: `Ubicación actual: ${coordenadas(ubicacion)}. Tu rol es de consulta: no se registra.`,
      });
      return;
    }

    this.estado.set('registrando');
    try {
      await firstValueFrom(this.api.registrar(ubicacion));
    } catch (e) {
      this.terminar({ tipo: 'error', texto: mensajeDeRegistro(e) });
      return;
    }

    this.historial.reload();
    this.terminar({ tipo: 'exito', texto: `Asistencia registrada en ${coordenadas(ubicacion)}` });
    void this.notificacion.avisar('Asistencia registrada');
  }

  private terminar(aviso: Aviso): void {
    this.aviso.set(aviso);
    this.estado.set('listo');
  }
}

function coordenadas(u: Ubicacion): string {
  return `${u.latitud.toFixed(6)}, ${u.longitud.toFixed(6)}`;
}

function mensajeDeGeolocalizacion(error: unknown): string {
  return error instanceof GeolocalizacionError ? error.message : 'Error al obtener la ubicación';
}

function mensajeDeRegistro(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'No se pudo registrar la asistencia.';
  }
  if (error.status === 0) {
    return 'Sin conexión con el backend: la asistencia no se registró.';
  }
  if (error.status === 403) {
    return 'Tu rol no puede registrar asistencia.';
  }
  const detalle = (error.error as { detail?: string } | null)?.detail;
  return detalle ?? `El backend respondió ${error.status}: la asistencia no se registró.`;
}
