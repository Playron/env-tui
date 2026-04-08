import { useState } from 'react'
import { Search, Edit2, Check, X } from 'lucide-react'
import type { ContactDto } from '../types'
import { updateContact } from '../services/api'
import { AiBadge } from './AiBadge'
import { ConfidenceBar } from './ConfidenceBar'
import toast from 'react-hot-toast'

interface ContactTableProps {
  contacts: ContactDto[]
  sessionId: string
  onContactUpdated: (updated: ContactDto) => void
}

interface EditState {
  contactId: string
  field: keyof ContactDto
  value: string
}

export function ContactTable({ contacts, sessionId, onContactUpdated }: ContactTableProps) {
  const [search, setSearch] = useState('')
  const [editState, setEditState] = useState<EditState | null>(null)
  const [saving, setSaving] = useState(false)

  const filtered = contacts.filter(c => {
    if (!search) return true
    const q = search.toLowerCase()
    return (
      c.fullName?.toLowerCase().includes(q) ||
      c.firstName?.toLowerCase().includes(q) ||
      c.lastName?.toLowerCase().includes(q) ||
      c.email?.toLowerCase().includes(q) ||
      c.phone?.toLowerCase().includes(q) ||
      c.organization?.toLowerCase().includes(q)
    )
  })

  const aiCount = contacts.filter(c => c.extractionSource === 'ai').length

  const startEdit = (contactId: string, field: keyof ContactDto, value: string) => {
    setEditState({ contactId, field, value })
  }

  const saveEdit = async () => {
    if (!editState || saving) return
    setSaving(true)
    try {
      const updated = await updateContact(sessionId, editState.contactId, {
        [editState.field]: editState.value,
      })
      onContactUpdated(updated)
      toast.success('Kontakt oppdatert.')
    } catch {
      toast.error('Kunne ikke lagre endringen.')
    } finally {
      setSaving(false)
      setEditState(null)
    }
  }

  const cancelEdit = () => setEditState(null)

  const renderCell = (contact: ContactDto, field: keyof ContactDto, value?: string) => {
    const isEditing = editState?.contactId === contact.id && editState.field === field
    if (isEditing) {
      return (
        <div className="flex items-center gap-1">
          <input
            autoFocus
            value={editState.value}
            onChange={e => setEditState({ ...editState, value: e.target.value })}
            onKeyDown={e => {
              if (e.key === 'Enter') saveEdit()
              if (e.key === 'Escape') cancelEdit()
            }}
            className="border border-brand-400 rounded px-1 py-0.5 text-sm w-full focus:outline-none focus:ring-1 focus:ring-brand-500"
          />
          <button onClick={saveEdit} disabled={saving} className="text-green-600 hover:text-green-700">
            <Check className="w-4 h-4" />
          </button>
          <button onClick={cancelEdit} className="text-gray-400 hover:text-red-500">
            <X className="w-4 h-4" />
          </button>
        </div>
      )
    }
    return (
      <div
        className="group flex items-center gap-1 cursor-pointer"
        onDoubleClick={() => startEdit(contact.id, field, value ?? '')}
        title="Dobbeltklikk for å redigere"
      >
        <span className="truncate max-w-[140px]">
          {value ?? <span className="text-gray-300">—</span>}
        </span>
        <Edit2 className="w-3 h-3 text-gray-300 group-hover:text-gray-500 opacity-0 group-hover:opacity-100 flex-shrink-0" />
      </div>
    )
  }

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-3">
        <div className="relative flex-1 min-w-48">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
          <input
            type="text"
            placeholder="Søk i kontakter..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="w-full pl-9 pr-4 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"
          />
        </div>
        <div className="flex items-center gap-2 text-sm text-gray-500">
          <span>{filtered.length} / {contacts.length} kontakter</span>
          {aiCount > 0 && (
            <span className="flex items-center gap-1">
              · <AiBadge small /> {aiCount} fra AI
            </span>
          )}
        </div>
      </div>

      <div className="overflow-x-auto rounded-lg border border-gray-200">
        <table className="min-w-full divide-y divide-gray-200 text-sm">
          <thead className="bg-gray-50">
            <tr>
              {['Navn', 'E-post', 'Telefon', 'Organisasjon', 'Stilling', 'Adresse', 'Kilde', 'Konfidensverdi'].map(h => (
                <th key={h} className="px-3 py-2 text-left font-semibold text-gray-600 whitespace-nowrap">
                  {h}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100 bg-white">
            {filtered.length === 0 ? (
              <tr>
                <td colSpan={8} className="py-10 text-center text-gray-400">
                  Ingen kontakter funnet
                </td>
              </tr>
            ) : (
              filtered.map(c => (
                <tr key={c.id} className="hover:bg-gray-50">
                  <td className="px-3 py-2">
                    {renderCell(c, 'fullName', c.fullName ?? (`${c.firstName ?? ''} ${c.lastName ?? ''}`.trim() || undefined))}
                  </td>
                  <td className="px-3 py-2">{renderCell(c, 'email', c.email)}</td>
                  <td className="px-3 py-2">{renderCell(c, 'phone', c.phone)}</td>
                  <td className="px-3 py-2">{renderCell(c, 'organization', c.organization)}</td>
                  <td className="px-3 py-2">{renderCell(c, 'title', c.title)}</td>
                  <td className="px-3 py-2">{renderCell(c, 'address', c.address)}</td>
                  <td className="px-3 py-2">
                    {c.extractionSource === 'ai'
                      ? <AiBadge small />
                      : <span className="text-xs text-gray-400 font-mono">{c.extractionSource}</span>
                    }
                  </td>
                  <td className="px-3 py-2">
                    <ConfidenceBar value={c.confidence} />
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
}
