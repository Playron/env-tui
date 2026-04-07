interface ConfidenceBarProps {
  value: number   // 0.0 – 1.0
  showLabel?: boolean
}

export function ConfidenceBar({ value, showLabel = true }: ConfidenceBarProps) {
  const pct = Math.round(value * 100)
  const color =
    pct >= 85 ? 'bg-green-500'
    : pct >= 60 ? 'bg-yellow-400'
    : 'bg-red-400'

  return (
    <div className="flex items-center gap-1.5">
      <div className="w-16 h-1.5 bg-gray-200 rounded-full overflow-hidden">
        <div
          className={`h-full rounded-full ${color} transition-all`}
          style={{ width: `${pct}%` }}
        />
      </div>
      {showLabel && (
        <span className={`text-xs font-medium ${
          pct >= 85 ? 'text-green-600' : pct >= 60 ? 'text-yellow-600' : 'text-red-500'
        }`}>
          {pct}%
        </span>
      )}
    </div>
  )
}
