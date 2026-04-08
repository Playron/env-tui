import { CheckCircle, Circle, Loader2, XCircle } from 'lucide-react'

type Phase =
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

interface Step {
  phase: Phase[]
  label: string
  doneWhen: Phase[]
}

const STEPS: Step[] = [
  { phase: ['uploading'],                   label: 'Laster opp fil',          doneWhen: ['queued', 'extracting', 'regex_done', 'ai_started', 'ai_complete', 'done'] },
  { phase: ['queued', 'extracting'],        label: 'Leser og parser fil',     doneWhen: ['regex_done', 'ai_started', 'ai_complete', 'done'] },
  { phase: ['regex_done'],                  label: 'Regex-ekstraksjon',       doneWhen: ['ai_started', 'ai_complete', 'done'] },
  { phase: ['ai_started', 'ai_complete'],   label: 'AI-ekstraksjon',          doneWhen: ['done'] },
  { phase: ['done'],                        label: 'Lagrer kontakter',        doneWhen: ['done'] },
]

interface Props {
  phase: Phase
  message: string
  progress: number
}

export function ExtractionProgress({ phase, message, progress }: Props) {
  const isError = phase === 'error'

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-5 space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="font-semibold text-gray-800">Ekstraksjon pågår...</h3>
        <span className="text-sm text-gray-500">{Math.round(progress * 100)}%</span>
      </div>

      {/* Progress bar */}
      <div className="w-full h-2 bg-gray-100 rounded-full overflow-hidden">
        <div
          className={`h-full rounded-full transition-all duration-500 ${isError ? 'bg-red-500' : 'bg-brand-600'}`}
          style={{ width: `${Math.round(progress * 100)}%` }}
        />
      </div>

      {/* Steps */}
      <div className="space-y-2">
        {STEPS.map((step, i) => {
          const isActive = step.phase.includes(phase)
          const isDone   = step.doneWhen.includes(phase)
          const isPending = !isActive && !isDone && !isError

          return (
            <div key={i} className="flex items-center gap-3">
              {isDone ? (
                <CheckCircle className="w-4 h-4 text-green-500 shrink-0" />
              ) : isActive ? (
                <Loader2 className="w-4 h-4 text-brand-600 animate-spin shrink-0" />
              ) : isError ? (
                <XCircle className="w-4 h-4 text-red-500 shrink-0" />
              ) : (
                <Circle className="w-4 h-4 text-gray-300 shrink-0" />
              )}
              <span className={`text-sm ${isDone ? 'text-gray-500' : isActive ? 'text-gray-900 font-medium' : 'text-gray-400'}`}>
                {step.label}
              </span>
            </div>
          )
        })}
      </div>

      {/* Current message */}
      {message && (
        <p className={`text-sm ${isError ? 'text-red-600' : 'text-gray-600'}`}>
          {message}
        </p>
      )}
    </div>
  )
}
