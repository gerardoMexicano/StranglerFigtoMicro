import { Reservacion, Sala } from './salas-api.service';

/** Mismo horario que BookingRules de 05_backend: 08:00 a 20:00 en bloques de 30 minutos. */
export const APERTURA_MINUTOS = 8 * 60;
export const CIERRE_MINUTOS = 20 * 60;
export const BLOQUE_MINUTOS = 30;

/** Inicio de cada bloque: "08:00", "08:30", ..., "19:30". */
export const HORAS_DE_INICIO: readonly string[] = horasEntre(
  APERTURA_MINUTOS,
  CIERRE_MINUTOS - BLOQUE_MINUTOS,
);

/** Fin posible de una reservación: "08:30", ..., "20:00". */
export const HORAS_DE_FIN: readonly string[] = horasEntre(
  APERTURA_MINUTOS + BLOQUE_MINUTOS,
  CIERRE_MINUTOS,
);

export type Celda =
  | { tipo: 'libre'; sala: Sala; hora: string; pasada: boolean }
  /** filas: cuántos bloques ocupa (rowspan) */
  | { tipo: 'reservada'; reservacion: Reservacion; filas: number };

export interface FilaAgenda {
  hora: string;
  /** Una por sala, en el orden de las salas. null: la cubre una reservación de más arriba. */
  celdas: (Celda | null)[];
}

/**
 * La agenda del día como tabla: una fila por bloque y una columna por sala. Cada reservación
 * aparece una vez, en su bloque de inicio, y ocupa tantas filas como dure.
 * ahora: "2026-10-30T10:15" (hora local); los bloques que ya empezaron no se ofrecen.
 */
export function armarAgenda(
  fecha: string,
  salas: readonly Sala[],
  reservaciones: readonly Reservacion[],
  ahora: string,
): FilaAgenda[] {
  const filas: FilaAgenda[] = HORAS_DE_INICIO.map((hora) => ({
    hora,
    celdas: salas.map((sala) => ({
      tipo: 'libre',
      sala,
      hora,
      pasada: `${fecha}T${hora}` <= ahora,
    })),
  }));

  salas.forEach((sala, columna) => {
    for (const reservacion of reservaciones) {
      if (reservacion.roomId !== sala.id) {
        continue;
      }
      const desde = Math.max(Math.floor(indiceDeBloque(reservacion.startsAt)), 0);
      const hasta = Math.min(Math.ceil(indiceDeBloque(reservacion.endsAt)), filas.length);
      if (hasta <= desde) {
        continue;
      }
      filas[desde].celdas[columna] = { tipo: 'reservada', reservacion, filas: hasta - desde };
      for (let i = desde + 1; i < hasta; i++) {
        filas[i].celdas[columna] = null;
      }
    }
  });

  return filas;
}

/** "2026-10-30T09:30:00" → "09:30" */
export function horaDe(fechaHora: string): string {
  return fechaHora.slice(11, 16);
}

/** La hora de fin que se propone al elegir un inicio: un bloque después. */
export function finSugerido(inicio: string): string {
  return horaDeMinutos(minutosDe(inicio) + BLOQUE_MINUTOS);
}

/** Fecha local "2026-10-30" (input type="date"); toISOString() daría la de UTC. */
export function fechaLocal(fecha: Date): string {
  return `${fecha.getFullYear()}-${dos(fecha.getMonth() + 1)}-${dos(fecha.getDate())}`;
}

/** Fecha y hora local "2026-10-30T10:15", comparable como texto con fecha + "T" + hora. */
export function fechaHoraLocal(fecha: Date): string {
  return `${fechaLocal(fecha)}T${dos(fecha.getHours())}:${dos(fecha.getMinutes())}`;
}

export function sumarDias(fecha: string, dias: number): string {
  const [anio, mes, dia] = fecha.split('-').map(Number);
  return fechaLocal(new Date(anio, mes - 1, dia + dias));
}

function indiceDeBloque(fechaHora: string): number {
  return (minutosDe(horaDe(fechaHora)) - APERTURA_MINUTOS) / BLOQUE_MINUTOS;
}

function minutosDe(hora: string): number {
  const [h, m] = hora.split(':').map(Number);
  return h * 60 + m;
}

function horaDeMinutos(minutos: number): string {
  return `${dos(Math.floor(minutos / 60))}:${dos(minutos % 60)}`;
}

function horasEntre(desde: number, hasta: number): string[] {
  const horas: string[] = [];
  for (let m = desde; m <= hasta; m += BLOQUE_MINUTOS) {
    horas.push(horaDeMinutos(m));
  }
  return horas;
}

function dos(n: number): string {
  return String(n).padStart(2, '0');
}
