import { useEffect, useState } from 'react'
import { Copy, Check, X } from 'lucide-react'
import toast from 'react-hot-toast'
import { getDuplicateGroups, mergeDuplicates, dismissDuplicates } from '../services/api'
import type { DuplicateGroupDto, ContactDto } from '../types'
import { ConfidenceBar } from './ConfidenceBar'

export function DuplicateResolver() {
  const [groups, setGroups]   = useState<DuplicateGroupDto[]>([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy]       = useState<string | null>(null)

  const load = () => {
    setLoading(true)
    getDuplicateGroups()
      .then(setGroups)
      .catch(e => toast.error(e.message))
      .finally(() => setLoading(false))
  }

  useEffect(() => { load() }, [])

  const handleMerge = async (groupId: string, primaryId: string) => {
    setBusy(groupId)
    try {
      await mergeDuplicates(groupId, primaryId)
      toast.success('Kontakter slått sammen')
      load()
    } catch (e) {
      toast.error('Sammenslåing feilet')
    } finally {
      setBusy(null)
    }
  }

  const handleDismiss = async (groupId: string) => {
    setBusy(groupId)
    try {
      await dismissDuplicates(groupId)
      toast.success('Duplikatgruppe avvist')
      load()
    } catch (e) {
      toast.error('Avvisning feilet')
    } finally {
      setBusy(null)
    }
  }

  if (loading) return (
    <div className="bg-white rounded-xl border border-gray-200 p-8 text-center text-gray-400">
      Laster duplikater...
    </div>
  )

  if (groups.length === 0) return (
    <div className="bg-white rounded-xl border border-gray-200 p-12 text-center text-gray-400">
      <Copy className="w-12 h-12 mx-auto mb-3 opacity-30" />
      <p className="font-medium">Ingen duplikater funnet</p>
      <p className="text-sm mt-1">Last opp flere filer for å finne mulige duplikater.</p>
    </div>
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-2">
        <Copy className="w-5 h-5 text-amber-500" />
        <h2 className="font-bold text-gray-900">Duplikatgrupper ({groups.length} uløste)</h2>
      </div>

      {groups.map(group => (
        <div key={group.id} className="bg-white rounded-xl border border-gray-200 p-5 space-y-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <span className="text-sm font-medium text-amber-700 bg-amber-50 px-2 py-0.5 rounded">
                {Math.round(group.similarity * 100)}% likhet
              </span>
              <span className="text-sm text-gray-500">{group.contacts.length} kontakter</span>
            </div>
            <button
              onClick={() => handleDismiss(group.id)}
              disabled={busy === group.id}
              className="flex items-center gap-1 text-xs text-gray-500 hover:text-gray-700 disabled:opacity-50"
            >
              <X className="w-3.5 h-3.5" />
              Avvis
            </button>
          </div>

          {/* Side-by-side comparison */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            {group.contacts.map((contact: ContactDto) => (
              <div key={contact.id} className="border border-gray-200 rounded-lg p-3 space-y-1.5">
                <div className="flex items-center justify-between">
                  <p className="font-medium text-sm text-gray-900">
                    {contact.fullName ?? (`${contact.firstName ?? ''} ${contact.lastName ?? ''}`.trim() || '(Ukjent)')}
                  </p>
                  <ConfidenceBar value={contact.confidence} showLabel={false} />
                </div>
                {contact.email    && <p className="text-xs text-gray-600">📧 {contact.email}</p>}
                {contact.phone    && <p className="text-xs text-gray-600">📞 {contact.phone}</p>}
                {contact.organization && <p className="text-xs text-gray-600">🏢 {contact.organization}</p>}

                <button
                  onClick={() => handleMerge(group.id, contact.id)}
                  disabled={busy === group.id}
                  className="mt-2 w-full flex items-center justify-center gap-1.5 py-1.5 text-xs font-medium bg-brand-600 text-white rounded hover:bg-brand-700 disabled:opacity-50 transition-colors"
                >
                  <Check className="w-3.5 h-3.5" />
                  Velg som primær
                </button>
              </div>
            ))}
          </div>
        </div>
      ))}
    </div>
  )
}
