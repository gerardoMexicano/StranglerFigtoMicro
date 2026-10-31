import {
  armarAgenda,
  fechaHoraLocal,
  fechaLocal,
  finSugerido,
  HORAS_DE_FIN,
  HORAS_DE_INICIO,
  horaDe,
  sumarDias,
} from './agenda';
import { Reservacion, Sala } from './salas-api.service';

const juarez: Sala = { id: 1, name: 'Sala Juárez', capacity: 8, location: 'Planta baja' };
const hidalgo: Sala = { id: 2, name: 'Sala Hidalgo', capacity: 12, location: 'Primer piso' };

function reservacion(roomId: number, inicio: string, fin: string, id = 1): Reservacion {
  return {
    id,
    roomId,
    startsAt: `2026-10-31T${inicio}:00`,
    endsAt: `2026-10-31T${fin}:00`,
    subject: 'Junta',
    userName: 'juan',
    isMine: true,
    canCancel: true,
  };
}

describe('horario', () => {
  it('24 bloques de 30 minutos entre 08:00 y 20:00, como BookingRules del backend', () => {
    expect(HORAS_DE_INICIO).toHaveLength(24);
    expect(HORAS_DE_INICIO.at(0)).toBe('08:00');
    expect(HORAS_DE_INICIO.at(-1)).toBe('19:30');
    expect(HORAS_DE_FIN.at(0)).toBe('08:30');
    expect(HORAS_DE_FIN.at(-1)).toBe('20:00');
  });

  it('el fin sugerido es un bloque después del inicio', () => {
    expect(finSugerido('09:00')).toBe('09:30');
    expect(finSugerido('09:30')).toBe('10:00');
    expect(finSugerido('19:30')).toBe('20:00');
  });

  it('horaDe toma HH:mm de la fecha-hora del backend', () => {
    expect(horaDe('2026-10-31T09:30:00')).toBe('09:30');
  });
});

describe('fechas locales', () => {
  it('usan el día del navegador, no el de UTC', () => {
    const nocheDelJueves = new Date(2026, 9, 29, 23, 45);

    expect(fechaLocal(nocheDelJueves)).toBe('2026-10-29');
    expect(fechaHoraLocal(nocheDelJueves)).toBe('2026-10-29T23:45');
  });

  it('sumarDias cruza meses y años', () => {
    expect(sumarDias('2026-10-31', 1)).toBe('2026-11-01');
    expect(sumarDias('2026-01-01', -1)).toBe('2025-12-31');
  });
});

describe('armarAgenda', () => {
  const temprano = '2026-10-30T10:00';

  it('sin reservaciones todo está libre: una fila por bloque, una celda por sala', () => {
    const agenda = armarAgenda('2026-10-31', [juarez, hidalgo], [], temprano);

    expect(agenda).toHaveLength(24);
    expect(agenda[0].hora).toBe('08:00');
    expect(agenda[0].celdas.map((c) => c?.tipo)).toEqual(['libre', 'libre']);
  });

  it('una reservación aparece en su bloque de inicio y cubre los que dura', () => {
    const agenda = armarAgenda(
      '2026-10-31',
      [juarez, hidalgo],
      [reservacion(2, '09:00', '10:30')],
      temprano,
    );
    const fila = (hora: string) => agenda.find((f) => f.hora === hora)!;

    expect(fila('09:00').celdas[1]).toMatchObject({ tipo: 'reservada', filas: 3 });
    expect(fila('09:30').celdas[1]).toBeNull();
    expect(fila('10:00').celdas[1]).toBeNull();
    expect(fila('10:30').celdas[1]).toMatchObject({ tipo: 'libre' });
    // La otra sala no se toca
    expect(fila('09:30').celdas[0]).toMatchObject({ tipo: 'libre', sala: juarez });
  });

  it('una reservación hasta las 20:00 cubre hasta el último bloque', () => {
    const agenda = armarAgenda(
      '2026-10-31',
      [juarez],
      [reservacion(1, '19:00', '20:00')],
      temprano,
    );

    expect(agenda.at(-2)?.celdas[0]).toMatchObject({ tipo: 'reservada', filas: 2 });
    expect(agenda.at(-1)?.celdas[0]).toBeNull();
  });

  it('los bloques de hoy que ya empezaron quedan como pasados', () => {
    const agenda = armarAgenda('2026-10-30', [juarez], [], '2026-10-30T10:15');
    const pasada = (hora: string) => {
      const celda = agenda.find((f) => f.hora === hora)!.celdas[0];
      return celda?.tipo === 'libre' && celda.pasada;
    };

    expect(pasada('10:00')).toBe(true);
    expect(pasada('10:30')).toBe(false);
  });
});
