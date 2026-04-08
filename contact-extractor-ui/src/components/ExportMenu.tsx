import { useState, useRef, useEffect } from 'react'
import { Download, ChevronDown } from 'lucide-react'
import toast from 'react-hot-toast'
import { exportCsv, exportExcel, exportVCard, exportGoogle, exportOutlook } from '../services/api'

interface Props {
  sessionId: string
  contactCount: number
}

const FORMATS = [
  { label: 'CSV (standard)',        fn: exportCsv,     ext: 'csv'  },
  { label: 'Excel (.xlsx)',         fn: exportExcel,   ext: 'xlsx' },
  { label: 'vCard (.vcf)',          fn: exportVCard,   ext: 'vcf'  },
  { label: 'Google Contacts CSV',   fn: exportGoogle,  ext: 'csv'  },
  { label: 'Outlook CSV',           fn: exportOutlook, ext: 'csv'  },
]

export function ExportMenu({ sessionId, contactCount }: Props) {
  const [open, setOpen]         = useState(false)
  const [loading, setLoading]   = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node))
        setOpen(false)
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  const handleExport = async (fn: (id: string) => Promise<void>, label: string) => {
    setOpen(false)
    setLoading(true)
    try {
      await fn(sessionId)
      toast.success(`Eksportert som ${label}`)
    } catch (err) {
      toast.error(`Eksport feilet: ${err instanceof Error ? err.message : 'Ukjent feil'}`)
    } finally {
      setLoading(false)
    }
  }

  if (contactCount === 0) return null

  return (
    <div className="relative" ref={ref}>
      <button
        onClick={() => setOpen(v => !v)}
        disabled={loading}
        className="flex items-center gap-1.5 px-3 py-2 text-sm font-medium bg-brand-600 text-white rounded-lg hover:bg-brand-700 disabled:opacity-50 transition-colors"
      >
        <Download className="w-4 h-4" />
        Eksporter
        <ChevronDown className={`w-3 h-3 transition-transform ${open ? 'rotate-180' : ''}`} />
      </button>

      {open && (
        <div className="absolute right-0 top-full mt-1 w-52 bg-white border border-gray-200 rounded-xl shadow-lg z-50 overflow-hidden">
          {FORMATS.map(fmt => (
            <button
              key={fmt.ext + fmt.label}
              onClick={() => handleExport(fmt.fn, fmt.label)}
              className="w-full text-left px-4 py-2.5 text-sm text-gray-700 hover:bg-gray-50 transition-colors border-b border-gray-100 last:border-0"
            >
              {fmt.label}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
