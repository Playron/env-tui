import { useState, useEffect, useCallback } from 'react'
import toast from 'react-hot-toast'
import { getAllSessions, getSessionContacts, deleteSession } from '../services/api'
import type { SessionSummaryDto, ExtractionResultDto } from '../types'

interface UseContactsReturn {
  sessions: SessionSummaryDto[]
  activeSession: ExtractionResultDto | null
  isLoading: boolean
  loadSessions: () => Promise<void>
  openSession: (sessionId: string) => Promise<void>
  removeSession: (sessionId: string) => Promise<void>
  clearActiveSession: () => void
}

export function useContacts(): UseContactsReturn {
  const [sessions, setSessions] = useState<SessionSummaryDto[]>([])
  const [activeSession, setActiveSession] = useState<ExtractionResultDto | null>(null)
  const [isLoading, setIsLoading] = useState(false)

  const loadSessions = useCallback(async () => {
    setIsLoading(true)
    try {
      const data = await getAllSessions()
      setSessions(data)
    } catch {
      toast.error('Kunne ikke laste sesjonshistorikk.')
    } finally {
      setIsLoading(false)
    }
  }, [])

  const openSession = useCallback(async (sessionId: string) => {
    setIsLoading(true)
    try {
      const data = await getSessionContacts(sessionId)
      setActiveSession(data)
    } catch {
      toast.error('Kunne ikke hente kontakter for sesjonen.')
    } finally {
      setIsLoading(false)
    }
  }, [])

  const removeSession = useCallback(async (sessionId: string) => {
    try {
      await deleteSession(sessionId)
      setSessions(prev => prev.filter(s => s.id !== sessionId))
      if (activeSession?.sessionId === sessionId) setActiveSession(null)
      toast.success('Sesjon slettet.')
    } catch {
      toast.error('Kunne ikke slette sesjonen.')
    }
  }, [activeSession])

  const clearActiveSession = useCallback(() => setActiveSession(null), [])

  useEffect(() => {
    loadSessions()
  }, [loadSessions])

  return {
    sessions,
    activeSession,
    isLoading,
    loadSessions,
    openSession,
    removeSession,
    clearActiveSession,
  }
}
