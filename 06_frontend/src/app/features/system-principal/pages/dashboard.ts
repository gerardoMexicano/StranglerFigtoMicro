import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import {
  DashboardApiService,
  KpisDashboard,
  RANGO_DASHBOARD_POR_DEFECTO,
  RangoDashboard,
} from '../dashboard-api.service';

const MESES = [
  'Enero',
  'Febrero',
  'Marzo',
  'Abril',
  'Mayo',
  'Junio',
  'Julio',
  'Agosto',
  'Septiembre',
  'Octubre',
  'Noviembre',
  'Diciembre',
];

type Formato = 'moneda' | 'porcentaje' | 'entero';

interface TarjetaKpi {
  id: 'ingresos' | 'margen' | 'ordenes' | 'ticket';
  etiqueta: string;
  campo: keyof KpisDashboard;
  formato: Formato;
}

/** El KPI Strip de Dashboard.aspx, en el mismo orden. */
const TARJETAS: readonly TarjetaKpi[] = [
  { id: 'ingresos', etiqueta: 'Ingresos del período', campo: 'revenue', formato: 'moneda' },
  { id: 'margen', etiqueta: 'Margen bruto %', campo: 'grossMarginPct', formato: 'porcentaje' },
  { id: 'ordenes', etiqueta: 'Total de órdenes', campo: 'orders', formato: 'entero' },
  { id: 'ticket', etiqueta: 'Ticket promedio', campo: 'averageTicket', formato: 'moneda' },
];

/** Formateadores de Dashboard.aspx (fmtMXN, fmtPct, fmtInt). */
export function moneda(valor: number): string {
  return (
    '$' +
    Number(valor).toLocaleString('es-MX', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
  );
}

export function porcentaje(valor: number): string {
  return `${Number(valor).toFixed(2)}%`;
}

export function entero(valor: number): string {
  return Number(valor).toLocaleString('es-MX');
}

function formatear(valor: number, formato: Formato): string {
  switch (formato) {
    case 'moneda':
      return moneda(valor);
    case 'porcentaje':
      return porcentaje(valor);
    default:
      return entero(valor);
  }
}

/** renderDelta de Dashboard.aspx; sin período anterior el porcentaje queda en 0. */
function delta(actual: number, anterior: number, formato: Formato) {
  const diferencia = actual - anterior;
  const pct = anterior !== 0 ? Math.abs((diferencia / Math.abs(anterior)) * 100).toFixed(1) : '0';
  const sube = diferencia >= 0;
  return {
    sube,
    texto: `${sube ? '▲' : '▼'} ${formatear(Math.abs(diferencia), formato)} (${pct}%) vs período ant.`,
  };
}

/**
 * Versión Angular de /legacy/Dashboard.aspx contra 05_backend (/api/dashboard/executive).
 * Las gráficas de Chart.js pasan a tablas y barras con CSS: mismos datos, sin dependencias nuevas.
 */
@Component({
  selector: 'app-dashboard',
  imports: [FormsModule, RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard {
  private readonly api = inject(DashboardApiService);

  protected readonly moneda = moneda;
  protected readonly porcentaje = porcentaje;

  protected readonly inicio = signal(RANGO_DASHBOARD_POR_DEFECTO.startDate);
  protected readonly fin = signal(RANGO_DASHBOARD_POR_DEFECTO.endDate);

  /** Como el Page_Load: carga sola con el rango por defecto. */
  private readonly rango = signal<RangoDashboard>({ ...RANGO_DASHBOARD_POR_DEFECTO });

  protected readonly rangoInvalido = computed(
    () => !this.inicio() || !this.fin() || this.inicio() > this.fin(),
  );

  protected readonly dashboard = rxResource({
    params: () => this.rango(),
    stream: ({ params }) => this.api.consultar(params),
  });

  protected readonly datos = computed(() =>
    this.dashboard.hasValue() ? this.dashboard.value() : undefined,
  );

  protected readonly error = computed(() => {
    const error = this.dashboard.error();
    return error ? mensajeDeError(error) : null;
  });

  protected readonly kpis = computed(() => {
    const kpis = this.datos()?.kpis;
    return TARJETAS.map((t) => ({
      ...t,
      valor: kpis ? formatear(kpis.current[t.campo], t.formato) : '—',
      delta: kpis ? delta(kpis.current[t.campo], kpis.previous[t.campo], t.formato) : null,
    }));
  });

  protected readonly meses = computed(() =>
    (this.datos()?.monthlyRevenue ?? []).map((m) => ({
      ...m,
      etiqueta: `${MESES[m.month - 1]} ${m.year}`,
    })),
  );

  /** Ancho de cada barra relativo al cliente con más margen. */
  protected readonly clientes = computed(() => {
    const clientes = this.datos()?.topCustomers ?? [];
    const maximo = Math.max(0, ...clientes.map((c) => c.netMargin));
    return clientes.map((c) => ({
      ...c,
      ancho: maximo > 0 ? Math.max(0, (c.netMargin / maximo) * 100) : 0,
    }));
  });

  /** Participación de cada categoría en el margen total, como el tooltip de la dona. */
  protected readonly categorias = computed(() => {
    const categorias = this.datos()?.categoryMargins ?? [];
    const total = categorias.reduce((suma, c) => suma + c.grossMargin, 0);
    return categorias.map((c) => {
      const participacion = total !== 0 ? (c.grossMargin / total) * 100 : 0;
      return { ...c, participacion, pct: participacion.toFixed(1) };
    });
  });

  protected readonly vendedores = computed(() => this.datos()?.salespeople ?? []);

  protected aplicar(): void {
    if (this.rangoInvalido()) {
      return;
    }
    this.rango.set({ startDate: this.inicio(), endDate: this.fin() });
  }
}

function mensajeDeError(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'No se pudo cargar el dashboard.';
  }
  if (error.status === 0) {
    return 'Sin conexión con el backend.';
  }
  const problema = error.error as { title?: string; detail?: string } | null;
  const texto = problema?.title ?? problema?.detail;
  return texto ? `${texto} (${error.status}).` : `El backend respondió ${error.status}.`;
}
