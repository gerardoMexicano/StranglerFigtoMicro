<%@ Page Language="vb" AutoEventWireup="false" CodeBehind="Dashboard.aspx.vb" Inherits="WebApp_WebForms.Dashboard" %>
<!DOCTYPE html>
<html lang="es">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <meta name="description" content="Dashboard ejecutivo de ventas — WideWorldImporters" />
    <title>Dashboard Ejecutivo — WideWorldImporters</title>
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous" />
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700;800&display=swap" rel="stylesheet" />
    <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.2/dist/chart.umd.min.js"></script>
    <style>
        /* ── Reset & tokens ── */
        *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

        :root {
            --bg:        #0d1117;
            --surface:   #161b22;
            --surface2:  #1c2128;
            --border:    #30363d;
            --accent:    #58a6ff;
            --accent2:   #3fb950;
            --accent3:   #f78166;
            --accent4:   #d2a8ff;
            --accent5:   #ffa657;
            --text:      #e6edf3;
            --muted:     #8b949e;
            --green:     #2ea043;
            --red:       #da3633;
            --radius:    12px;
            --shadow:    0 4px 24px rgba(0,0,0,.45);
        }

        body {
            font-family: 'Inter', sans-serif;
            background: var(--bg);
            color: var(--text);
            min-height: 100vh;
        }

        /* ── Top bar ── */
        .topbar {
            display: flex;
            align-items: center;
            justify-content: space-between;
            padding: 16px 32px;
            background: var(--surface);
            border-bottom: 1px solid var(--border);
            position: sticky;
            top: 0;
            z-index: 100;
        }
        .topbar-brand {
            display: flex;
            align-items: center;
            gap: 12px;
            font-weight: 700;
            font-size: 1.1rem;
            color: var(--accent);
            text-decoration: none;
        }
        .topbar-brand span { font-size: 1.5rem; }
        .topbar-right { display: flex; align-items: center; gap: 12px; }
        .topbar-right a {
            color: var(--muted);
            text-decoration: none;
            font-size: .85rem;
            transition: color .2s;
        }
        .topbar-right a:hover { color: var(--text); }

        /* ── Filter bar ── */
        .filter-bar {
            display: flex;
            align-items: center;
            gap: 12px;
            padding: 16px 32px;
            background: var(--surface2);
            border-bottom: 1px solid var(--border);
            flex-wrap: wrap;
        }
        .filter-bar label { font-size: .8rem; color: var(--muted); font-weight: 500; }
        .filter-bar input[type="date"] {
            background: var(--surface);
            border: 1px solid var(--border);
            color: var(--text);
            padding: 7px 12px;
            border-radius: 8px;
            font-family: inherit;
            font-size: .9rem;
            cursor: pointer;
            transition: border-color .2s;
        }
        .filter-bar input[type="date"]:focus { outline: none; border-color: var(--accent); }
        .btn-filtrar {
            background: var(--accent);
            color: #0d1117;
            border: none;
            padding: 8px 20px;
            border-radius: 8px;
            font-family: inherit;
            font-weight: 600;
            font-size: .9rem;
            cursor: pointer;
            transition: opacity .2s, transform .15s;
        }
        .btn-filtrar:hover { opacity: .85; transform: translateY(-1px); }
        .filter-note { font-size: .78rem; color: var(--muted); margin-left: auto; }

        /* ── Main grid ── */
        .dash-main {
            padding: 28px 32px;
            display: flex;
            flex-direction: column;
            gap: 24px;
        }

        /* ── KPI Strip ── */
        .kpi-strip {
            display: grid;
            grid-template-columns: repeat(4, 1fr);
            gap: 16px;
        }
        .kpi-card {
            background: var(--surface);
            border: 1px solid var(--border);
            border-radius: var(--radius);
            padding: 20px 24px;
            display: flex;
            flex-direction: column;
            gap: 6px;
            position: relative;
            overflow: hidden;
            transition: transform .2s, box-shadow .2s;
        }
        .kpi-card::before {
            content: '';
            position: absolute;
            top: 0; left: 0; right: 0;
            height: 3px;
        }
        .kpi-card:nth-child(1)::before { background: var(--accent); }
        .kpi-card:nth-child(2)::before { background: var(--accent2); }
        .kpi-card:nth-child(3)::before { background: var(--accent4); }
        .kpi-card:nth-child(4)::before { background: var(--accent5); }
        .kpi-card:hover { transform: translateY(-3px); box-shadow: var(--shadow); }

        .kpi-label { font-size: .75rem; color: var(--muted); font-weight: 500; letter-spacing: .04em; text-transform: uppercase; }
        .kpi-value { font-size: 1.7rem; font-weight: 800; color: var(--text); line-height: 1; }
        .kpi-delta {
            display: inline-flex;
            align-items: center;
            gap: 4px;
            font-size: .8rem;
            font-weight: 600;
            padding: 2px 8px;
            border-radius: 20px;
            width: fit-content;
        }
        .kpi-delta.up   { background: rgba(46,160,67,.18); color: #3fb950; }
        .kpi-delta.down { background: rgba(218,54,51,.18);  color: #f85149; }

        /* ── Chart rows ── */
        .row-2 {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 20px;
        }
        .row-3 {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 20px;
        }

        .chart-card {
            background: var(--surface);
            border: 1px solid var(--border);
            border-radius: var(--radius);
            padding: 24px;
            display: flex;
            flex-direction: column;
            gap: 16px;
            box-shadow: var(--shadow);
        }
        .chart-card.full { grid-column: 1 / -1; }

        .chart-header { display: flex; align-items: flex-start; justify-content: space-between; }
        .chart-title { font-size: .95rem; font-weight: 700; color: var(--text); }
        .chart-subtitle { font-size: .78rem; color: var(--muted); margin-top: 3px; }
        .chart-badge {
            font-size: .72rem;
            font-weight: 600;
            padding: 3px 10px;
            border-radius: 20px;
            background: var(--surface2);
            color: var(--muted);
            border: 1px solid var(--border);
            white-space: nowrap;
        }

        .chart-wrap {
            position: relative;
            height: 280px;
        }
        .chart-wrap canvas { width: 100% !important; height: 100% !important; }

        /* ── Vendedor table ── */
        .vendedor-table {
            width: 100%;
            border-collapse: collapse;
            font-size: .85rem;
        }
        .vendedor-table thead th {
            padding: 10px 14px;
            text-align: left;
            font-size: .75rem;
            color: var(--muted);
            font-weight: 600;
            text-transform: uppercase;
            letter-spacing: .05em;
            border-bottom: 1px solid var(--border);
        }
        .vendedor-table tbody tr {
            border-bottom: 1px solid var(--border);
            transition: background .15s;
        }
        .vendedor-table tbody tr:hover { background: var(--surface2); }
        .vendedor-table tbody td {
            padding: 11px 14px;
            color: var(--text);
        }
        .badge-ok  { display:inline-block; padding:2px 8px; border-radius:20px; background:rgba(46,160,67,.18); color:#3fb950; font-weight:600; font-size:.78rem; }
        .badge-bad { display:inline-block; padding:2px 8px; border-radius:20px; background:rgba(218,54,51,.18);  color:#f85149; font-weight:600; font-size:.78rem; }

        /* ── Error panel ── */
        .error-panel {
            display: none;
            background: rgba(218,54,51,.1);
            border: 1px solid var(--red);
            border-radius: var(--radius);
            padding: 16px 20px;
            color: #f85149;
            font-size: .9rem;
        }
        .error-panel.visible { display: block; }

        /* ── Loading overlay ── */
        #loadingOverlay {
            position: fixed; inset: 0;
            background: rgba(13,17,23,.75);
            display: flex; align-items: center; justify-content: center;
            z-index: 999;
            backdrop-filter: blur(4px);
        }
        .spinner {
            width: 48px; height: 48px;
            border: 4px solid var(--border);
            border-top-color: var(--accent);
            border-radius: 50%;
            animation: spin .8s linear infinite;
        }
        @keyframes spin { to { transform: rotate(360deg); } }

        /* ── Responsive ── */
        @media (max-width: 1024px) {
            .kpi-strip  { grid-template-columns: repeat(2, 1fr); }
            .row-2, .row-3 { grid-template-columns: 1fr; }
        }
        @media (max-width: 640px) {
            .kpi-strip  { grid-template-columns: 1fr; }
            .dash-main  { padding: 16px; }
            .filter-bar { padding: 12px 16px; }
            .topbar     { padding: 12px 16px; }
        }
    </style>
</head>
<body>
<div id="loadingOverlay"><div class="spinner"></div></div>

<form id="form1" runat="server">

    <!-- ── Top bar ── -->
    <header class="topbar">
        <a href="Menu.aspx" class="topbar-brand">
            <span>📊</span> Dashboard Ejecutivo
        </a>
        <div class="topbar-right">
            <a href="Menu.aspx">⬅ Menú principal</a>
        </div>
    </header>

    <!-- ── Filter bar ── -->
    <div class="filter-bar">
        <label for="txtFechaIni">Desde</label>
        <input type="date" id="txtFechaIni" runat="server" />

        <label for="txtFechaFin">Hasta</label>
        <input type="date" id="txtFechaFin" runat="server" />

        <asp:Button ID="btnFiltrar" runat="server" Text="Aplicar filtro"
                    CssClass="btn-filtrar"
                    OnClick="btnFiltrar_Click" />

        <span class="filter-note">Base: WideWorldImporters</span>
    </div>

    <!-- ── Main content ── -->
    <main class="dash-main">

        <!-- Error panel -->
        <div id="errorPanel" class="error-panel"></div>

        <!-- ── Visual 5: KPI Strip ── -->
        <section class="kpi-strip" aria-label="KPIs ejecutivos">
            <div class="kpi-card">
                <div class="kpi-label">Ingresos del período</div>
                <div class="kpi-value" id="kpiIngresos">—</div>
                <div class="kpi-delta" id="kpiIngresosDelta">…</div>
            </div>
            <div class="kpi-card">
                <div class="kpi-label">Margen bruto %</div>
                <div class="kpi-value" id="kpiMargen">—</div>
                <div class="kpi-delta" id="kpiMargenDelta">…</div>
            </div>
            <div class="kpi-card">
                <div class="kpi-label">Total de órdenes</div>
                <div class="kpi-value" id="kpiOrdenes">—</div>
                <div class="kpi-delta" id="kpiOrdenesDelta">…</div>
            </div>
            <div class="kpi-card">
                <div class="kpi-label">Ticket promedio</div>
                <div class="kpi-value" id="kpiTicket">—</div>
                <div class="kpi-delta" id="kpiTicketDelta">…</div>
            </div>
        </section>

        <!-- ── Visual 1: Revenue vs Costo vs Margen ── -->
        <div class="chart-card full">
            <div class="chart-header">
                <div>
                    <div class="chart-title">Revenue · Costo · Margen mensual</div>
                    <div class="chart-subtitle">Ingresos brutos, costo total y margen neto agrupados por mes</div>
                </div>
                <span class="chart-badge">Visual 1</span>
            </div>
            <div class="chart-wrap">
                <canvas id="chartRevenueCosto" aria-label="Gráfica Revenue vs Costo vs Margen"></canvas>
            </div>
        </div>

        <!-- ── Visual 2 + Visual 3 ── -->
        <div class="row-2">
            <!-- Visual 2: Top 10 clientes -->
            <div class="chart-card">
                <div class="chart-header">
                    <div>
                        <div class="chart-title">Top 10 clientes por rentabilidad neta</div>
                        <div class="chart-subtitle">Margen neto acumulado en el período</div>
                    </div>
                    <span class="chart-badge">Visual 2</span>
                </div>
                <div class="chart-wrap" style="height:320px;">
                    <canvas id="chartTop10" aria-label="Top 10 clientes por margen"></canvas>
                </div>
            </div>

            <!-- Visual 3: Margen por categoría -->
            <div class="chart-card">
                <div class="chart-header">
                    <div>
                        <div class="chart-title">Margen por categoría de producto</div>
                        <div class="chart-subtitle">Distribución del margen bruto por grupo de stock</div>
                    </div>
                    <span class="chart-badge">Visual 3</span>
                </div>
                <div class="chart-wrap" style="height:320px;">
                    <canvas id="chartCategorias" aria-label="Margen por categoría"></canvas>
                </div>
            </div>
        </div>

        <!-- ── Visual 4: Eficiencia vendedor ── -->
        <div class="chart-card full">
            <div class="chart-header">
                <div>
                    <div class="chart-title">Eficiencia del vendedor</div>
                    <div class="chart-subtitle">Revenue total generado y % de descuento otorgado — rojo si descuento &gt; 10 %</div>
                </div>
                <span class="chart-badge">Visual 4</span>
            </div>
            <div style="overflow-x:auto;">
                <table class="vendedor-table" id="tablaVendedores">
                    <thead>
                        <tr>
                            <th>#</th>
                            <th>Vendedor</th>
                            <th>Revenue total</th>
                            <th>% Descuento prom.</th>
                            <th>Estado</th>
                        </tr>
                    </thead>
                    <tbody id="tablaVendedoresBody">
                        <tr><td colspan="5" style="text-align:center;color:var(--muted);padding:24px;">Cargando…</td></tr>
                    </tbody>
                </table>
            </div>
        </div>

    </main>
</form>

<!-- ═══════════════════════════════════════════════
     JAVASCRIPT — charts y KPIs
════════════════════════════════════════════════ -->
<script>
/* ─── Paleta de colores ─── */
const PALETTE = [
    '#58a6ff','#3fb950','#f78166','#d2a8ff','#ffa657',
    '#79c0ff','#56d364','#ffa198','#c7a5ff','#ffcf8a'
];
const GRID_COLOR  = 'rgba(48,54,61,0.8)';
const TEXT_COLOR  = '#8b949e';
const FONT_FAMILY = "'Inter', sans-serif";

Chart.defaults.font.family   = FONT_FAMILY;
Chart.defaults.color          = TEXT_COLOR;
Chart.defaults.plugins.legend.labels.boxWidth = 12;

/* ─── Formateadores ─── */
const fmtMXN   = v => '$' + Number(v).toLocaleString('es-MX', {minimumFractionDigits:2, maximumFractionDigits:2});
const fmtPct   = v => Number(v).toFixed(2) + '%';
const fmtInt   = v => Number(v).toLocaleString('es-MX');

/* ─── KPI delta ─── */
function renderDelta(elId, actual, anterior, isCurrency, isPct) {
    const el = document.getElementById(elId);
    if (!el) return;
    const diff = actual - anterior;
    const pct  = anterior !== 0 ? ((diff / Math.abs(anterior)) * 100).toFixed(1) : 0;
    const isUp = diff >= 0;
    el.className = 'kpi-delta ' + (isUp ? 'up' : 'down');
    const arrow = isUp ? '▲' : '▼';
    const label = isCurrency ? fmtMXN(Math.abs(diff)) : (isPct ? fmtPct(Math.abs(diff)) : fmtInt(Math.abs(diff)));
    el.textContent = arrow + ' ' + label + ' (' + Math.abs(pct) + '%) vs período ant.';
}

/* ─── Inicialización principal ─── */
document.addEventListener('DOMContentLoaded', function () {
    document.getElementById('loadingOverlay').style.display = 'none';

    /* ── Error global ── */
    if (typeof dashError !== 'undefined') {
        const ep = document.getElementById('errorPanel');
        ep.textContent = '⚠️ Error al cargar datos: ' + dashError.error;
        ep.classList.add('visible');
        return;
    }

    /* ─── KPI Strip (Visual 5) ─── */
    if (typeof dashKPIs !== 'undefined') {
        const k = dashKPIs;

        document.getElementById('kpiIngresos').textContent = fmtMXN(k.ingresosActual);
        document.getElementById('kpiMargen').textContent   = fmtPct(k.margenPctActual);
        document.getElementById('kpiOrdenes').textContent  = fmtInt(k.ordenesActual);
        document.getElementById('kpiTicket').textContent   = fmtMXN(k.ticketActual);

        renderDelta('kpiIngresosDelta', k.ingresosActual,   k.ingresosAnt,   true,  false);
        renderDelta('kpiMargenDelta',   k.margenPctActual,  k.margenPctAnt,  false, true);
        renderDelta('kpiOrdenesDelta',  k.ordenesActual,    k.ordenesAnt,    false, false);
        renderDelta('kpiTicketDelta',   k.ticketActual,     k.ticketAnt,     true,  false);
    }

    /* ─── Visual 1: Revenue vs Costo vs Margen ─── */
    if (typeof dashRevenueCosto !== 'undefined' && dashRevenueCosto.length > 0) {
        const labels   = dashRevenueCosto.map(d => d.mes);
        const ingresos = dashRevenueCosto.map(d => d.ingresos);
        const costos   = dashRevenueCosto.map(d => d.costo);
        const margenes = dashRevenueCosto.map(d => d.margen);

        new Chart(document.getElementById('chartRevenueCosto'), {
            type: 'bar',
            data: {
                labels,
                datasets: [
                    {
                        label: 'Ingresos brutos',
                        data: ingresos,
                        backgroundColor: 'rgba(88,166,255,0.7)',
                        borderColor: '#58a6ff',
                        borderWidth: 1,
                        borderRadius: 4
                    },
                    {
                        label: 'Costo total',
                        data: costos,
                        backgroundColor: 'rgba(247,129,102,0.7)',
                        borderColor: '#f78166',
                        borderWidth: 1,
                        borderRadius: 4
                    },
                    {
                        label: 'Margen bruto',
                        data: margenes,
                        type: 'line',
                        borderColor: '#3fb950',
                        backgroundColor: 'rgba(63,185,80,0.12)',
                        pointBackgroundColor: '#3fb950',
                        pointRadius: 5,
                        pointHoverRadius: 7,
                        borderWidth: 2.5,
                        fill: true,
                        tension: 0.35,
                        yAxisID: 'y'
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { position: 'top' },
                    tooltip: {
                        callbacks: {
                            label: ctx => ' ' + ctx.dataset.label + ': ' + fmtMXN(ctx.parsed.y)
                        }
                    }
                },
                scales: {
                    x: { grid: { color: GRID_COLOR }, ticks: { maxRotation: 45 } },
                    y: {
                        grid: { color: GRID_COLOR },
                        ticks: { callback: v => '$' + (v/1000).toFixed(0) + 'k' }
                    }
                }
            }
        });
    } else {
        sinDatos('chartRevenueCosto');
    }

    /* ─── Visual 2: Top 10 clientes ─── */
    if (typeof dashTop10 !== 'undefined' && dashTop10.length > 0) {
        const sorted  = [...dashTop10].sort((a, b) => a.margen - b.margen);
        const labels  = sorted.map(d => d.cliente);
        const valores = sorted.map(d => d.margen);

        new Chart(document.getElementById('chartTop10'), {
            type: 'bar',
            data: {
                labels,
                datasets: [{
                    label: 'Margen neto',
                    data: valores,
                    backgroundColor: PALETTE.map(c => c + 'bb'),
                    borderColor: PALETTE,
                    borderWidth: 1,
                    borderRadius: 4
                }]
            },
            options: {
                indexAxis: 'y',
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label: ctx => ' Margen: ' + fmtMXN(ctx.parsed.x),
                            afterLabel: ctx => ' Cliente: ' + ctx.label
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { color: GRID_COLOR },
                        ticks: { callback: v => '$' + (v/1000).toFixed(0) + 'k' }
                    },
                    y: { grid: { color: 'transparent' }, ticks: { font: { size: 11 } } }
                }
            }
        });
    } else {
        sinDatos('chartTop10');
    }

    /* ─── Visual 3: Margen por categoría (Donut) ─── */
    if (typeof dashCategorias !== 'undefined' && dashCategorias.length > 0) {
        const labels  = dashCategorias.map(d => d.categoria);
        const valores = dashCategorias.map(d => d.margen);

        new Chart(document.getElementById('chartCategorias'), {
            type: 'doughnut',
            data: {
                labels,
                datasets: [{
                    data: valores,
                    backgroundColor: PALETTE.map(c => c + 'cc'),
                    borderColor: PALETTE,
                    borderWidth: 2,
                    hoverOffset: 12
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '58%',
                plugins: {
                    legend: { position: 'right', labels: { font: { size: 11 }, padding: 14 } },
                    tooltip: {
                        callbacks: {
                            label: ctx => {
                                const total = ctx.dataset.data.reduce((a, b) => a + b, 0);
                                const pct   = (ctx.parsed / total * 100).toFixed(1);
                                return ' ' + ctx.label + ': ' + fmtMXN(ctx.parsed) + ' (' + pct + '%)';
                            }
                        }
                    }
                }
            }
        });
    } else {
        sinDatos('chartCategorias');
    }

    /* ─── Visual 4: Tabla eficiencia vendedor ─── */
    if (typeof dashVendedores !== 'undefined' && dashVendedores.length > 0) {
        const tbody = document.getElementById('tablaVendedoresBody');
        tbody.innerHTML = '';
        dashVendedores.forEach((v, i) => {
            const tr = document.createElement('tr');
            const badgeCls = v.alerta ? 'badge-bad' : 'badge-ok';
            const badgeTxt = v.alerta ? '⚠ Alto descuento' : '✓ OK';
            tr.innerHTML = `
                <td style="color:var(--muted);font-size:.8rem;">${i + 1}</td>
                <td style="font-weight:600;">${v.vendedor}</td>
                <td>${fmtMXN(v.revenue)}</td>
                <td>${fmtPct(v.pctDescuento)}</td>
                <td><span class="${badgeCls}">${badgeTxt}</span></td>`;
            tbody.appendChild(tr);
        });
    } else {
        document.getElementById('tablaVendedoresBody').innerHTML =
            '<tr><td colspan="5" style="text-align:center;color:var(--muted);padding:24px;">Sin datos para el período seleccionado</td></tr>';
    }
});

/* ─── Helper: mensaje sin datos ─── */
function sinDatos(canvasId) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    canvas.style.display = 'none';
    const msg = document.createElement('div');
    msg.style.cssText = 'display:flex;align-items:center;justify-content:center;height:100%;color:var(--muted);font-size:.9rem;';
    msg.textContent = 'Sin datos para el período seleccionado';
    canvas.parentNode.appendChild(msg);
}
</script>
</body>
</html>
