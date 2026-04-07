import { History, Trash2, ChevronRight, Loader2 } from 'lucide-react'
import type { SessionSummaryDto } from '../types'
import { AiBadge } from './AiBadge'

const FILE_ICONS: Record<string, string> = {
  '.csv':  '📄',
  '.xlsx': '📊',
  '.pdf':  '📕',
  '.docx': '📝',
  '.txt':  '📃',
  '.vcf':  '👤',
}

interface SessionHistoryProps {
  sessions: SessionSummaryDto[]
  isLoading: boolean
  activeSessionId?: string
  onOpen: (sessionId: string) => void
  onDelete: (sessionId: string) => void
}

export function SessionHistory({
  sessions,
  isLoading,
  activeSessionId,
  onOpen,
  onDelete,
}: SessionHistoryProps) {
  if (isLoading) {
    return (
      <div className="flex items-center justify-center gap-2 py-6 text-gray-500">
        <Loader2 className="w-5 h-5 animate-spin" />
        <span className="text-sm">Laster historikk...</span>
      </div>
    )
  }

  if (sessions.length === 0) {
    return (
      <div className="text-center py-8 text-gray-400">
        <History className="w-8 h-8 mx-auto mb-2 opacity-40" />
        <p className="text-sm">Ingen tidligere opplastinger</p>
      </div>
    )
  }

  return (
    <div className="space-y-2">
      {sessions.map(s => {
        const isActive = s.id === activeSessionId
        const icon = FILE_ICONS[s.fileType] ?? '📄'
        const date = new Date(s.createdAt).toLocaleString('nb-NO', {
          day: '2-digit', month: '2-digit', year: 'numeric',
          hour: '2-digit', minute: '2-digit',
        })

        return (
          <div
            key={s.id}
            className={`
              flex items-center gap-3 p-3 rounded-lg border cursor-pointer transition-colors
              ${isActive
                ? 'bg-brand-50 border-brand-300'
                : 'bg-white border-gray-200 hover:bg-gray-50'
              }
            `}
            onClick={() => onOpen(s.id)}
          >
            <span className="text-2xl flex-shrink-0">{icon}</span>
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-1.5 flex-wrap">
                <p className="font-medium text-gray-800 truncate text-sm">{s.originalFileName}</p>
                {s.usedAi && <AiBadge small />}
              </div>
              <p className="text-xs text-gray-500">
                {s.contactCount} kontakter · {date}
              </p>
            </div>
            <div className="flex items-center gap-1">
              <button
                onClick={e => { e.stopPropagation(); onDelete(s.id) }}
                className="p-1.5 text-gray-400 hover:text-red-500 rounded-md hover:bg-red-50 transition-colors"
                title="Slett sesjon"
              >
                <Trash2 className="w-4 h-4" />
              </button>
              <ChevronRight className={`w-4 h-4 ${isActive ? 'text-brand-600' : 'text-gray-300'}`} />
            </div>
          </div>
        )
      })}
    </div>
  )
}
