import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, linkedSignal, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';
import { ROLES_ESCRITURA } from '../../core/auth/roles';
import {
  armarAgenda,
  Celda,
  fechaHoraLocal,
  fechaLocal,
  finSugerido,
  HORAS_DE_FIN,
  HORAS_DE_INICIO,
  horaDe,
  sumarDias,
} from './agenda';
import { Reservacion, SalasApiService } from './salas-api.service';

export interface Aviso {
  tipo: 'exito' | 'error';
  texto: string;
}

type CeldaLibre = Extract<Celda, { tipo: 'libre' }>;

/**
 * Reservación de salas: funcionalidad nueva que se ve en /legacy/AngularPage, dentro del marco de
 * WebForms. Muestra la agenda del día por sala y deja reservar en bloques de 30 minutos.
 * invitado solo consulta: el backend no le deja reservar ni cancelar.
 */
@Component({
  selector: 'app-salas',
  imports: [FormsModule],
  templateUrl: './salas.html',
  styleUrl: './salas.css',
})
export class Salas {
  private readonly api = inject(SalasApiService);
  private readonly usuario = toSignal(inject(AuthService).user$, { initialValue: null });

  protected readonly puedeReservar = computed(
    () => this.usuario()?.roles.some((rol) => ROLES_ESCRITURA.includes(rol)) ?? false,
  );

  protected readonly fecha = signal(fechaLocal(new Date()));
  /** Se toma al cambiar de día o recargar: los bloques que ya empezaron no se ofrecen. */
  private readonly ahora = signal(fechaHoraLocal(new Date()));

  protected readonly salas = rxResource({ stream: () => this.api.salas() });
  protected readonly reservaciones = rxResource({
    params: () => this.fecha(),
    stream: ({ params }) => this.api.reservaciones(params),
  });

  protected readonly agenda = computed(() =>
    armarAgenda(
      this.fecha(),
      this.salas.hasValue() ? this.salas.value() : [],
      this.reservaciones.hasValue() ? this.reservaciones.value() : [],
      this.ahora(),
    ),
  );

  protected readonly iniciosPosibles = computed(() =>
    HORAS_DE_INICIO.filter((hora) => `${this.fecha()}T${hora}` > this.ahora()),
  );

  // Formulario: la primera sala y el primer bloque libre del día, hasta que el usuario elige otro
  protected readonly salaId = linkedSignal<number | null>(() =>
    this.salas.hasValue() ? (this.salas.value()[0]?.id ?? null) : null,
  );
  protected readonly inicio = linkedSignal(() => this.iniciosPosibles()[0] ?? '');
  protected readonly fin = linkedSignal(() => (this.inicio() ? finSugerido(this.inicio()) : ''));
  protected readonly asunto = signal('');

  protected readonly finesPosibles = computed(() =>
    HORAS_DE_FIN.filter((hora) => hora > this.inicio()),
  );

  protected readonly guardando = signal(false);
  protected readonly aviso = signal<Aviso | null>(null);

  protected readonly formularioValido = computed(
    () =>
      this.salaId() !== null &&
      this.inicio() !== '' &&
      this.fin() > this.inicio() &&
      this.asunto().trim() !== '',
  );

  protected readonly horaDe = horaDe;

  protected cambiarFecha(fecha: string): void {
    if (/^\d{4}-\d{2}-\d{2}$/.test(fecha)) {
      this.ahora.set(fechaHoraLocal(new Date()));
      this.fecha.set(fecha);
    }
  }

  protected moverDia(dias: number): void {
    this.cambiarFecha(sumarDias(this.fecha(), dias));
  }

  protected irAHoy(): void {
    this.cambiarFecha(fechaLocal(new Date()));
  }

  /** Clic en un bloque libre de la agenda: lo pone en el formulario. */
  protected elegir(celda: CeldaLibre): void {
    this.salaId.set(celda.sala.id);
    this.inicio.set(celda.hora);
    this.aviso.set(null);
    document.getElementById('asunto')?.focus();
  }

  protected nombreDeSala(id: number | null): string {
    return (
      (this.salas.hasValue() && this.salas.value().find((s) => s.id === id)?.name) || 'la sala'
    );
  }

  protected async reservar(): Promise<void> {
    const salaId = this.salaId();
    if (!this.formularioValido() || this.guardando() || salaId === null) {
      return;
    }
    const fecha = this.fecha();
    const [inicio, fin] = [this.inicio(), this.fin()];

    this.guardando.set(true);
    this.aviso.set(null);
    try {
      await firstValueFrom(
        this.api.reservar({
          roomId: salaId,
          startsAt: `${fecha}T${inicio}:00`,
          endsAt: `${fecha}T${fin}:00`,
          subject: this.asunto().trim(),
        }),
      );
      this.asunto.set('');
      this.aviso.set({
        tipo: 'exito',
        texto: `Reservaste ${this.nombreDeSala(salaId)} de ${inicio} a ${fin}.`,
      });
    } catch (e) {
      this.aviso.set({ tipo: 'error', texto: mensajeDeError(e, 'reservar') });
    } finally {
      this.guardando.set(false);
      this.recargar();
    }
  }

  protected async cancelar(reservacion: Reservacion): Promise<void> {
    const horario = `${horaDe(reservacion.startsAt)} a ${horaDe(reservacion.endsAt)}`;
    if (!confirm(`¿Cancelar "${reservacion.subject}" (${horario})?`)) {
      return;
    }

    this.aviso.set(null);
    try {
      await firstValueFrom(this.api.cancelar(reservacion.id));
      this.aviso.set({ tipo: 'exito', texto: `Cancelaste "${reservacion.subject}" (${horario}).` });
    } catch (e) {
      this.aviso.set({ tipo: 'error', texto: mensajeDeError(e, 'cancelar') });
    } finally {
      this.recargar();
    }
  }

  private recargar(): void {
    this.ahora.set(fechaHoraLocal(new Date()));
    this.reservaciones.reload();
  }
}

function mensajeDeError(error: unknown, accion: 'reservar' | 'cancelar'): string {
  const fallo = accion === 'reservar' ? 'no se guardó la reservación' : 'no se canceló';
  if (!(error instanceof HttpErrorResponse)) {
    return `Ocurrió un error: ${fallo}.`;
  }
  if (error.status === 0) {
    return `Sin conexión con el backend: ${fallo}.`;
  }
  const detalle = (error.error as { detail?: string } | null)?.detail;
  if (error.status === 403 && !detalle) {
    return `Tu rol no puede ${accion} salas.`;
  }
  return detalle ?? `El backend respondió ${error.status}: ${fallo}.`;
}
