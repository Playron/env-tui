import { Sparkles } from 'lucide-react'

interface AiBadgeProps {
  small?: boolean
}

export function AiBadge({ small = false }: AiBadgeProps) {
  return (
    <span
      title="Ekstrahert med AI"
      className={`inline-flex items-center gap-0.5 font-semibold rounded-full bg-purple-100 text-purple-700 ${
        small ? 'px-1.5 py-0.5 text-xs' : 'px-2 py-1 text-xs'
      }`}
    >
      <Sparkles className={small ? 'w-3 h-3' : 'w-3.5 h-3.5'} />
      AI
    </span>
  )
}
