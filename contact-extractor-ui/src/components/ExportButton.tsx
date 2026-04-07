import { useState } from 'react'
import { Download, FileSpreadsheet, FileText, Loader2 } from 'lucide-react'
import { exportCsv, exportExcel } from '../services/api'
import toast from 'react-hot-toast'

interface ExportButtonProps {
  sessionId: string
  contactCount: number
}

export function ExportButton({ sessionId, contactCount }: ExportButtonProps) {
  const [exporting, setExporting] = useState<'csv' | 'excel' | null>(null)

  const handleExport = async (format: 'csv' | 'excel') => {
    setExporting(format)
    try {
      if (format === 'csv') await exportCsv(sessionId)
      else await exportExcel(sessionId)
      toast.success(`Eksportert ${contactCount} kontakter som ${format.toUpperCase()}`)
    } catch {
      toast.error(`Eksport feilet.`)
    } finally {
      setExporting(null)
    }
  }

  return (
    <div className="flex items-center gap-2">
      <span className="text-sm text-gray-500 flex items-center gap-1">
        <Download className="w-4 h-4" />
        Eksporter:
      </span>
      <button
        onClick={() => handleExport('csv')}
        disabled={exporting !== null}
        className="flex items-center gap-1.5 px-3 py-1.5 bg-green-600 hover:bg-green-700 disabled:opacity-50 text-white text-sm font-medium rounded-lg transition-colors"
      >
        {exporting === 'csv'
          ? <Loader2 className="w-4 h-4 animate-spin" />
          : <FileText className="w-4 h-4" />
        }
        CSV
      </button>
      <button
        onClick={() => handleExport('excel')}
        disabled={exporting !== null}
        className="flex items-center gap-1.5 px-3 py-1.5 bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 text-white text-sm font-medium rounded-lg transition-colors"
      >
        {exporting === 'excel'
          ? <Loader2 className="w-4 h-4 animate-spin" />
          : <FileSpreadsheet className="w-4 h-4" />
        }
        Excel
      </button>
    </div>
  )
}
