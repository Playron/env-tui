import { useEffect, useState } from 'react'
import { Globe, Plus, Trash2, Play } from 'lucide-react'
import toast from 'react-hot-toast'
import { getWebhooks, createWebhook, deleteWebhook, testWebhook } from '../services/api'
import type { WebhookConfigDto } from '../types'

const EVENTS = [
  'extraction.completed',
  'duplicates.found',
  'export.completed',
]

export function WebhookSettings() {
  const [webhooks, setWebhooks] = useState<WebhookConfigDto[]>([])
  const [loading, setLoading]   = useState(true)
  const [showForm, setShowForm] = useState(false)
  const [url, setUrl]           = useState('')
  const [event, setEvent]       = useState(EVENTS[0])
  const [secret, setSecret]     = useState('')
  const [saving, setSaving]     = useState(false)

  const load = () => {
    setLoading(true)
    getWebhooks()
      .then(setWebhooks)
      .catch(e => toast.error(e.message))
      .finally(() => setLoading(false))
  }

  useEffect(() => { load() }, [])

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!url.trim()) return
    setSaving(true)
    try {
      await createWebhook(url.trim(), event, secret || undefined)
      toast.success('Webhook opprettet')
      setUrl(''); setSecret(''); setShowForm(false)
      load()
    } catch (err) {
      toast.error(`Opprettelse feilet: ${err instanceof Error ? err.message : 'Ukjent feil'}`)
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async (id: string) => {
    try {
      await deleteWebhook(id)
      toast.success('Webhook slettet')
      load()
    } catch { toast.error('Sletting feilet') }
  }

  const handleTest = async (id: string) => {
    try {
      await testWebhook(id)
      toast.success('Test-payload sendt')
    } catch { toast.error('Test feilet') }
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Globe className="w-5 h-5 text-brand-600" />
          <h2 className="font-bold text-gray-900">Webhooks</h2>
        </div>
        <button
          onClick={() => setShowForm(v => !v)}
          className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium bg-brand-600 text-white rounded-lg hover:bg-brand-700 transition-colors"
        >
          <Plus className="w-4 h-4" />
          Ny webhook
        </button>
      </div>

      {/* Create form */}
      {showForm && (
        <form onSubmit={handleCreate} className="bg-white rounded-xl border border-gray-200 p-5 space-y-3">
          <h3 className="font-semibold text-gray-800">Registrer ny webhook</h3>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">URL *</label>
            <input
              type="url"
              value={url}
              onChange={e => setUrl(e.target.value)}
              placeholder="https://example.com/webhook"
              required
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Event</label>
            <select
              value={event}
              onChange={e => setEvent(e.target.value)}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"
            >
              {EVENTS.map(ev => <option key={ev} value={ev}>{ev}</option>)}
            </select>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Secret (valgfri, brukes til HMAC-signatur)</label>
            <input
              type="text"
              value={secret}
              onChange={e => setSecret(e.target.value)}
              placeholder="min-hemmelige-nøkkel"
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"
            />
          </div>
          <div className="flex gap-2">
            <button
              type="submit"
              disabled={saving}
              className="px-4 py-2 text-sm font-medium bg-brand-600 text-white rounded-lg hover:bg-brand-700 disabled:opacity-50 transition-colors"
            >
              {saving ? 'Lagrer...' : 'Lagre webhook'}
            </button>
            <button
              type="button"
              onClick={() => setShowForm(false)}
              className="px-4 py-2 text-sm text-gray-600 hover:text-gray-800"
            >
              Avbryt
            </button>
          </div>
        </form>
      )}

      {/* Webhook list */}
      {loading ? (
        <div className="text-center text-gray-400 py-8">Laster webhooks...</div>
      ) : webhooks.length === 0 ? (
        <div className="bg-white rounded-xl border border-dashed border-gray-200 p-12 text-center text-gray-400">
          <Globe className="w-10 h-10 mx-auto mb-3 opacity-30" />
          <p className="font-medium">Ingen webhooks konfigurert</p>
        </div>
      ) : (
        <div className="space-y-2">
          {webhooks.map(wh => (
            <div key={wh.id} className="bg-white rounded-xl border border-gray-200 p-4 flex items-center gap-4">
              <div className="flex-1 min-w-0">
                <p className="text-sm font-medium text-gray-900 truncate">{wh.url}</p>
                <p className="text-xs text-gray-500 mt-0.5">
                  <span className="bg-gray-100 px-1.5 py-0.5 rounded font-mono">{wh.event}</span>
                  {' · '}
                  {new Date(wh.createdAt).toLocaleDateString('nb-NO')}
                  {!wh.isActive && <span className="ml-2 text-red-500">Inaktiv</span>}
                </p>
              </div>
              <div className="flex items-center gap-1 shrink-0">
                <button
                  onClick={() => handleTest(wh.id)}
                  className="p-1.5 text-gray-400 hover:text-brand-600 transition-colors"
                  title="Send test-payload"
                >
                  <Play className="w-4 h-4" />
                </button>
                <button
                  onClick={() => handleDelete(wh.id)}
                  className="p-1.5 text-gray-400 hover:text-red-600 transition-colors"
                  title="Slett webhook"
                >
                  <Trash2 className="w-4 h-4" />
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
