import { useEffect, useRef, useState } from 'react'
import toast from 'react-hot-toast'
import { uploadFileAsync, getExtractionResult } from '../services/api'
import type { ExtractionResultDto, SseProgressEvent } from '../types'

type UploadPhase =
  | 'idle'
  | 'uploading'
  | 'queued'
  | 'extracting'
  | 'regex_done'
  | 'ai_started'
  | 'ai_complete'
  | 'saving'
  | 'done'
  | 'error'

interface AsyncUploadState {
  phase: UploadPhase
  message: string
  progress: number
  contactsFoundSoFar: number | null
  result: ExtractionResultDto | null
}

interface UseAsyncUploadReturn {
  state: AsyncUploadState
  upload: (file: File) => void
  reset: () => void
}

const IDLE_STATE: AsyncUploadState = {
  phase: 'idle',
  message: '',
  progress: 0,
  contactsFoundSoFar: null,
  result: null,
}

export function useAsyncUpload(): UseAsyncUploadReturn {
  const [state, setState] = useState<AsyncUploadState>(IDLE_STATE)
  const eventSourceRef = useRef<EventSource | null>(null)
  const abortControllerRef = useRef<AbortController | null>(null)

  // Rydd opp ved unmount
  useEffect(() => {
    return () => {
      eventSourceRef.current?.close()
      abortControllerRef.current?.abort()
    }
  }, [])

  const reset = () => {
    eventSourceRef.current?.close()
    eventSourceRef.current = null
    abortControllerRef.current?.abort()
    abortControllerRef.current = null
    setState(IDLE_STATE)
  }

  const upload = async (file: File) => {
    reset()

    const abortController = new AbortController()
    abortControllerRef.current = abortController

    setState({ ...IDLE_STATE, phase: 'uploading', message: 'Laster opp fil...' })

    try {
      // Steg 1: POST fil → 202 Accepted med sessionId og streamUrl
      const accepted = await uploadFileAsync(file, abortController.signal)

      setState(prev => ({ ...prev, phase: 'queued', message: 'Fil mottatt – starter ekstraksjon...' }))

      // Steg 2: Koble til SSE-stream
      const es = new EventSource(accepted.streamUrl)
      eventSourceRef.current = es

      // Lukk SSE ved abort
      abortController.signal.addEventListener('abort', () => es.close())

      es.onmessage = (e) => {
        const evt: SseProgressEvent = JSON.parse(e.data)
        handleSseEvent(evt, accepted.sessionId, es, abortController.signal)
      }

      es.onerror = () => {
        es.close()
        eventSourceRef.current = null
        // Fallback: start polling
        startPolling(accepted.sessionId, abortController.signal)
      }
    } catch (err) {
      if ((err as DOMException)?.name === 'AbortError') return
      const msg = err instanceof Error ? err.message : 'Ukjent feil'
      setState({ ...IDLE_STATE, phase: 'error', message: msg })
      toast.error(`Opplasting feilet: ${msg}`)
    }
  }

  const handleSseEvent = (
    evt: SseProgressEvent,
    sessionId: string,
    es: EventSource,
    signal: AbortSignal
  ) => {
    setState(prev => ({
      ...prev,
      phase: (evt.stage as UploadPhase) ?? prev.phase,
      message: evt.message,
      progress: evt.progress ?? prev.progress,
      contactsFoundSoFar: evt.contactsFoundSoFar ?? prev.contactsFoundSoFar,
    }))

    if (evt.stage === 'done') {
      es.close()
      eventSourceRef.current = null
      // Hent fullt resultat fra DB
      fetchFinalResult(sessionId, signal)
    }

    if (evt.stage === 'failed') {
      es.close()
      eventSourceRef.current = null
      toast.error(`Ekstraksjon feilet: ${evt.message}`)
    }
  }

  const fetchFinalResult = async (sessionId: string, signal: AbortSignal) => {
    try {
      const result = await getExtractionResult(sessionId, signal)
      if (result) {
        setState(prev => ({ ...prev, phase: 'done', result }))
        if (result.warnings.length > 0)
          result.warnings.forEach(w => toast(w, { icon: '⚠️' }))
        toast.success(`${result.contactsExtracted} kontakter ekstrahert!`)
      }
    } catch (err) {
      if ((err as DOMException)?.name === 'AbortError') return
      toast.error('Kunne ikke hente resultat')
    }
  }

  const startPolling = async (sessionId: string, signal: AbortSignal) => {
    const MAX_ATTEMPTS = 60 // 60 × 2s = 2 min
    for (let i = 0; i < MAX_ATTEMPTS; i++) {
      if (signal.aborted) return

      await new Promise(r => setTimeout(r, 2000))
      if (signal.aborted) return

      try {
        const result = await getExtractionResult(sessionId, signal)
        if (result) {
          setState(prev => ({ ...prev, phase: 'done', result }))
          if (result.warnings.length > 0)
            result.warnings.forEach(w => toast(w, { icon: '⚠️' }))
          toast.success(`${result.contactsExtracted} kontakter ekstrahert!`)
          return
        }
        // 202 – fortsatt i prosess, prøv igjen
      } catch (err) {
        if ((err as DOMException)?.name === 'AbortError') return
        // Nettverksfeil – prøv igjen
      }
    }

    setState(prev => ({ ...prev, phase: 'error', message: 'Ekstraksjon tok for lang tid.' }))
    toast.error('Ekstraksjon tok for lang tid – prøv igjen.')
  }

  return { state, upload, reset }
}
