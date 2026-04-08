import { useEffect, useState } from 'react'
import { BarChart3, Users, Upload, Brain, Copy, CheckCircle } from 'lucide-react'
import { getDashboard } from '../services/api'
import type { DashboardDto } from '../types'

export function Dashboard() {
  const [data, setData]       = useState<DashboardDto | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError]     = useState<string | null>(null)

  useEffect(() => {
    getDashboard()
      .then(setData)
      .catch(e => setError(e.message))
      .finally(() => setLoading(false))
  }, [])

  if (loading) return <div className="bg-white rounded-xl border border-gray-200 p-8 text-center text-gray-400">Laster statistikk...</div>
  if (error)   return <div className="bg-red-50 rounded-xl border border-red-200 p-6 text-red-700">{error}</div>
  if (!data)   return null

  const stats = [
    { label: 'Totalt opplastinger',   value: data.totalSessions,      icon: <Upload className="w-5 h-5 text-brand-600" /> },
    { label: 'Totalt kontakter',      value: data.totalContacts,       icon: <Users className="w-5 h-5 text-green-600" /> },
    { label: 'AI-ekstraksjoner',      value: data.aiExtractions,       icon: <Brain className="w-5 h-5 text-purple-600" /> },
    { label: 'Duplikater funnet',     value: data.duplicatesFound,     icon: <Copy className="w-5 h-5 text-amber-600" /> },
    { label: 'Duplikater løst',       value: data.duplicatesResolved,  icon: <CheckCircle className="w-5 h-5 text-blue-600" /> },
    { label: 'Opplastinger i år',     value: data.sessionsThisMonth,   icon: <BarChart3 className="w-5 h-5 text-indigo-600" /> },
  ]

  return (
    <div className="space-y-6">
      <h2 className="text-lg font-bold text-gray-900">Dashboard</h2>

      {/* KPI cards */}
      <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
        {stats.map(s => (
          <div key={s.label} className="bg-white rounded-xl border border-gray-200 p-4 flex items-center gap-3">
            <div className="w-10 h-10 rounded-lg bg-gray-50 flex items-center justify-center shrink-0">
              {s.icon}
            </div>
            <div>
              <p className="text-2xl font-bold text-gray-900">{s.value.toLocaleString('nb-NO')}</p>
              <p className="text-xs text-gray-500">{s.label}</p>
            </div>
          </div>
        ))}
      </div>

      {/* File types */}
      {data.byFileType.length > 0 && (
        <div className="bg-white rounded-xl border border-gray-200 p-5">
          <h3 className="font-semibold text-gray-800 mb-4">Filtyper</h3>
          <div className="space-y-2">
            {data.byFileType.map(ft => {
              const pct = data.totalSessions > 0 ? (ft.count / data.totalSessions) * 100 : 0
              return (
                <div key={ft.fileType} className="flex items-center gap-3">
                  <span className="text-sm text-gray-600 w-12">{ft.fileType}</span>
                  <div className="flex-1 h-2 bg-gray-100 rounded-full overflow-hidden">
                    <div className="h-full bg-brand-600 rounded-full" style={{ width: `${pct}%` }} />
                  </div>
                  <span className="text-sm text-gray-500 w-6 text-right">{ft.count}</span>
                </div>
              )
            })}
          </div>
        </div>
      )}

      {/* Activity chart (simple bar) */}
      {data.activityLast30Days.length > 0 && (
        <div className="bg-white rounded-xl border border-gray-200 p-5">
          <h3 className="font-semibold text-gray-800 mb-4">Aktivitet siste 30 dager</h3>
          <div className="flex items-end gap-0.5 h-24">
            {data.activityLast30Days.map(day => {
              const max = Math.max(...data.activityLast30Days.map(d => d.uploads), 1)
              const h   = (day.uploads / max) * 100
              return (
                <div
                  key={day.date}
                  className="flex-1 bg-brand-200 hover:bg-brand-400 rounded-t transition-colors"
                  style={{ height: `${Math.max(h, day.uploads > 0 ? 4 : 0)}%` }}
                  title={`${day.date}: ${day.uploads} opplastinger`}
                />
              )
            })}
          </div>
          <div className="flex justify-between text-xs text-gray-400 mt-1">
            <span>{data.activityLast30Days[0]?.date?.slice(5)}</span>
            <span>{data.activityLast30Days[data.activityLast30Days.length - 1]?.date?.slice(5)}</span>
          </div>
        </div>
      )}
    </div>
  )
}
