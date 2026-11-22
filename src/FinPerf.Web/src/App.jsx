import { useState } from 'react'
import { useApi } from './useApi'
import { SummaryCards, MonthlyTable, RequestState } from './Performance'

// Mantiene el catálogo mientras la consulta activa cambia de portafolio o de fecha.
export function App() {
  const catalog = useApi('/api/portfolios')
  const [selectedId, setSelectedId] = useState(null)
  const [cutoff, setCutoff] = useState('2023-08-15')
  const portfolio = catalog.data?.find(item => item.id === selectedId) ?? catalog.data?.[0]
  const minDate = portfolio?.inceptionDate
  const validDate = Boolean(cutoff && cutoff >= minDate)
  const root = portfolio && validDate ? `/api/portfolios/${portfolio.id}/performance` : null
  const summary = useApi(root && `${root}/summary?asOf=${cutoff}`)
  const monthly = useApi(root && `${root}/monthly?from=${minDate}&to=${cutoff}`)

  // Conserva el corte al cambiar de portafolio o lo adelanta hasta su fecha de inicio.
  function selectPortfolio(event) {
    const next = catalog.data.find(item => item.id === Number(event.target.value))
    setSelectedId(next.id)
    if (cutoff && cutoff < next.inceptionDate) setCutoff(next.inceptionDate)
  }

  return <>
    <header className="topbar"><div className="wrap brand">
      <span className="brand-mark" aria-hidden="true"><i/><i/><i/></span>FinPerf
      <span className="brand-area">Portafolios de inversión</span>
    </div></header>
    <main className="wrap">
      <div className="page-heading"><h1>Rendimiento del portafolio</h1></div>
      <RequestState resource={catalog} label="portafolios" />
      {catalog.data?.length === 0 && <p className="notice" role="status">No hay portafolios disponibles.</p>}
      {portfolio && <>
        <section className="filters" aria-label="Filtros del informe">
          <label>Portafolio
            <select value={portfolio.id} onChange={selectPortfolio}>
              {catalog.data.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}
            </select>
          </label>
          <label>Fecha de corte
            <input type="date" required min={minDate} value={cutoff} onChange={event => setCutoff(event.target.value)} />
          </label>
        </section>
        {!validDate && <p className="notice" role="status">Elige un corte desde el inicio del portafolio ({minDate}).</p>}
        {validDate && <>
          <section aria-labelledby="summary-heading">
            <div className="section-heading"><h2 id="summary-heading">Resumen al corte</h2></div>
            <RequestState resource={summary} label="resumen" />
            {summary.data && <SummaryCards summary={summary.data} />}
          </section>
          <section aria-labelledby="monthly-heading">
            <div className="section-heading"><h2 id="monthly-heading">Retornos mensuales</h2></div>
            <RequestState resource={monthly} label="retornos mensuales" />
            {monthly.data && <MonthlyTable months={monthly.data.months} />}
          </section>
        </>}
      </>}
    </main>
  </>
}
