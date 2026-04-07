import type { ReactNode } from 'react'
import { FileSearch, Sparkles } from 'lucide-react'
import type { LlmSettingsInfoDto } from '../types'

interface LayoutProps {
  children: ReactNode
  llmInfo?: LlmSettingsInfoDto | null
}

const PROVIDER_LABELS: Record<string, string> = {
  claude: 'Claude',
  anthropic: 'Claude',
  openai: 'OpenAI',
  ollama: 'Ollama',
  none: 'Ingen AI',
  disabled: 'Ingen AI',
}

export function Layout({ children, llmInfo }: LayoutProps) {
  return (
    <div className="min-h-screen bg-gray-50">
      <header className="bg-white border-b border-gray-200 shadow-sm">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex items-center justify-between h-16">
            <div className="flex items-center gap-3">
              <FileSearch className="text-brand-600 w-7 h-7" />
              <div>
                <h1 className="text-xl font-bold text-gray-900">Kontaktliste-ekstraktor</h1>
                <p className="text-xs text-gray-500">Ekstraher kontakter fra CSV, Excel, PDF, Word og mer</p>
              </div>
            </div>
            {llmInfo && llmInfo.provider !== 'none' && llmInfo.provider !== 'disabled' && (
              <div className="flex items-center gap-1.5 px-3 py-1.5 bg-purple-50 border border-purple-200 rounded-full text-xs font-medium text-purple-700">
                <Sparkles className="w-3.5 h-3.5" />
                AI: {PROVIDER_LABELS[llmInfo.provider.toLowerCase()] ?? llmInfo.provider}
                {llmInfo.model && <span className="text-purple-500">({llmInfo.model})</span>}
                {!llmInfo.hasApiKey && (
                  <span className="ml-1 text-amber-600">(ingen nøkkel)</span>
                )}
              </div>
            )}
          </div>
        </div>
      </header>
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {children}
      </main>
    </div>
  )
}
