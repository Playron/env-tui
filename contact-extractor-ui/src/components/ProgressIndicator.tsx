import { Loader2 } from 'lucide-react'

interface ProgressIndicatorProps {
  message?: string
}

export function ProgressIndicator({ message = 'Behandler...' }: ProgressIndicatorProps) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 py-12">
      <Loader2 className="w-10 h-10 text-brand-600 animate-spin" />
      <p className="text-gray-600 text-sm font-medium">{message}</p>
    </div>
  )
}
