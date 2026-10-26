import { Component, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { SalesResource } from './sales-api.service';
import { RangoFechas, SalesTable } from './sales-table';

/** Mismas tablas y títulos que LEgFrontNEWBACK.aspx. */
const TABLAS: readonly { resource: SalesResource; title: string }[] = [
  { resource: 'customer-transactions', title: 'Customer Transactions' },
  { resource: 'invoice-lines', title: 'Invoice Lines' },
  { resource: 'invoices', title: 'Invoices' },
  { resource: 'order-lines', title: 'Order Lines' },
  { resource: 'orders', title: 'Orders' },
];

/**
 * Versión Angular de LEgFrontNEWBACK.aspx contra 05_backend. No la sustituye todavía:
 * la página WebForms sigue en /legacy y los menús legacy siguen apuntando a ella.
 */
@Component({
  selector: 'app-legfrontnewback',
  imports: [FormsModule, RouterLink, SalesTable],
  templateUrl: './legfrontnewback.html',
  styleUrl: './legfrontnewback.css',
})
export class Legfrontnewback {
  protected readonly tablas = TABLAS;

  // El rango por defecto de la página WebForms
  protected readonly inicio = signal('2013-01-01');
  protected readonly fin = signal('2016-12-31');

  /** Lo que reciben las tablas; null hasta el primer "Cargar registros". */
  protected readonly rango = signal<RangoFechas | null>(null);

  protected readonly rangoInvalido = computed(
    () => !this.inicio() || !this.fin() || this.inicio() > this.fin(),
  );

  protected cargar(): void {
    if (this.rangoInvalido()) {
      return;
    }
    // Objeto nuevo en cada clic: con las mismas fechas también recarga
    this.rango.set({ startDate: this.inicio(), endDate: this.fin() });
  }
}
