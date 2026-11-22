const percent = new Intl.NumberFormat('es-CL', {
  style: 'percent', minimumFractionDigits: 2, maximumFractionDigits: 2, signDisplay: 'exceptZero',
})
const pesos = new Intl.NumberFormat('es-CL', { style: 'currency', currency: 'CLP', maximumFractionDigits: 0 })
const periods = [['mtd', 'Mes'], ['qtd', 'Trimestre'], ['ytd', 'Año'], ['itd', 'Desde el inicio']]
const monthLabels = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic']

// Los signos acompañan al color para que se distinga una ganancia de una pérdida.
function tone(value) {
  if (value > 0) return 'positive'
  if (value < 0) return 'negative'
  return 'neutral'
}

// Cada sección puede recuperarse de un error sin volver a pedir el catálogo.
export function RequestState({ resource, label }) {
  if (resource.loading) return <p className="notice" role="status">Cargando {label}…</p>
  if (resource.error) return <div className="notice error" role="alert">
    <span>{resource.error}</span><button onClick={resource.retry}>Reintentar</button>
  </div>
  return null
}

// Presenta las cuatro ventanas calculadas por la API; un retorno indefinido conserva su P&L.
export function SummaryCards({ summary }) {
  return <div className="cards">{periods.map(([key, name]) => {
    const period = summary[key]
    return <article key={key} className="card">
      <h3>{name}<abbr title={name}>{key.toUpperCase()}</abbr></h3>
      <div className={'return ' + tone(period.cumulativeReturn)}>
        {period.cumulativeReturn == null ? '—' : percent.format(period.cumulativeReturn)}
      </div>
      <div className="pnl"><span>P&amp;L</span><strong>{period.pnl == null ? '—' : pesos.format(period.pnl)}</strong></div>
      {period.tradingDays === 0 && <small>Sin datos en el período</small>}
      {period.tradingDays > 0 && period.cumulativeReturn == null && <small>Retorno indefinido</small>}
    </article>
  })}</div>
}

// Usa el año y mes de la fecha ISO sin convertirla a la zona horaria del navegador.
export function MonthlyTable({ months }) {
  if (!months.length) return <p className="notice" role="status">Sin datos mensuales para este período.</p>
  const years = new Map()
  for (const row of months) {
    const year = row.monthStart.slice(0, 4)
    if (!years.has(year)) years.set(year, new Map())
    years.get(year).set(Number(row.monthStart.slice(5, 7)), row)
  }
  return <div className="table-panel">
    <div className="table-scroll" role="region" aria-label="Retornos mensuales por año" tabIndex={0}>
      <table><thead><tr><th scope="col">Año</th>{monthLabels.map(label => <th key={label} scope="col">{label}</th>)}</tr></thead>
      <tbody>{[...years.keys()].sort().reverse().map(year =>
        <tr key={year}><th scope="row">{year}</th>{monthLabels.map((label, index) => {
          const row = years.get(year).get(index + 1)
          const value = row?.cumulativeReturn
          return <td key={label} className={tone(value)} title={row ? `${row.tradingDays} días con posiciones` : 'Sin posiciones'}>
            {value == null ? '—' : percent.format(value)}
          </td>
        })}</tr>
      )}</tbody></table>
    </div>
    <p className="table-note">El mes del corte incluye las posiciones hasta la fecha seleccionada. El guion indica que no hay retorno disponible.</p>
  </div>
}
