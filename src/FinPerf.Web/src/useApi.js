import { useEffect, useState } from 'react'

// Carga una URL y cancela el trabajo anterior cuando cambian los filtros o se desmonta la vista.
export function useApi(url) {
  const [attempt, setAttempt] = useState(0)
  const [response, setResponse] = useState(null)

  useEffect(() => {
    if (!url) return
    const controller = new AbortController()

    async function load() {
      try {
        const res = await fetch(url, { signal: controller.signal, headers: { Accept: 'application/json' } })
        if (!res.ok) {
          const problem = await res.json().catch(() => null)
          throw new Error(problem?.detail || `El servidor respondió con el código ${res.status}.`)
        }
        const data = await res.json()
        if (!controller.signal.aborted) setResponse({ url, attempt, data })
      } catch (error) {
        if (!controller.signal.aborted) setResponse({ url, attempt, error: error.message })
      }
    }

    load()
    return () => controller.abort()
  }, [url, attempt])

  // Solo se muestra una respuesta si corresponde a la selección y al intento actuales.
  const current = response?.url === url && response.attempt === attempt ? response : null
  return { data: current?.data, error: current?.error, loading: Boolean(url && !current),
    retry: () => setAttempt(value => value + 1) }
}
