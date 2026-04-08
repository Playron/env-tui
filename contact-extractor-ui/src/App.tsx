import { useState, useEffect, type ReactNode } from 'react'
import { Layout } from './components/Layout'
import { FileUploader } from './components/FileUploader'
import { PreviewPanel } from './components/PreviewPanel'
import { ColumnMapper } from './components/ColumnMapper'
import { ContactTable } from './components/ContactTable'
import { ExportMenu } from './components/ExportMenu'
import { SessionHistory } from './components/SessionHistory'
import { AiBadge } from './components/AiBadge'
import { ExtractionProgress } from './components/ExtractionProgress'
import { Dashboard } from './components/Dashboard'
import { DuplicateResolver } from './components/DuplicateResolver'
import { WebhookSettings } from './components/WebhookSettings'
import { useAsyncUpload } from './hooks/useAsyncUpload'
import { useContacts } from './hooks/useContacts'
import { getLlmSettings, previewFile } from './services/api'
import type { ContactDto, ColumnMappingDto, LlmSettingsInfoDto, PreviewResultDto } from './types'
import { Upload, History, Users, LayoutDashboard, Copy, Globe } from 'lucide-react'

type Tab = 'upload' | 'history' | 'dashboard' | 'duplicates' | 'webhooks'

export default function App() {
  const [activeTab, setActiveTab]       = useState<Tab>('upload')
  const [localContacts, setLocalContacts] = useState<ContactDto[]>([])
  const [llmInfo, setLlmInfo]           = useState<LlmSettingsInfoDto | null>(null)
  const [preview, setPreview]           = useState<PreviewResultDto | null>(null)
  const [isPreviewing, setIsPreviewing] = useState(false)

  const { state: uploadState, upload, reset: resetUpload } = useAsyncUpload()
  const { sessions, activeSession, isLoading, loadSessions, openSession, removeSession } =
    useContacts()

  useEffect(() => {
    getLlmSettings().then(setLlmInfo).catch(() => {/* ignore */})
  }, [])

  // Sync local contacts when extraction finishes
  useEffect(() => {
    if (uploadState.result) setLocalContacts(uploadState.result.contacts)
  }, [uploadState.result])

  const handleFileSelected = async (file: File) => {
    setPreview(null)
    resetUpload()
    setIsPreviewing(true)
    try {
      const data = await previewFile(file)
      setPreview(data)
    } catch (err) {
      toast.error(`Forhåndsvisning feilet: ${err instanceof Error ? err.message : 'Ukjent feil'}`)
    } finally {
      setIsPreviewing(false)
    }
  }

  const handleUpload = (file: File) => {
    upload(file)
    // Refresh sessions when done (via effect on uploadState.result)
  }

  useEffect(() => {
    if (uploadState.phase === 'done') {
      loadSessions()
    }
  }, [uploadState.phase])

  const displayResult = uploadState.result ?? activeSession

  const handleContactUpdated = (updated: ContactDto) => {
    setLocalContacts(prev => prev.map(c => (c.id === updated.id ? updated : c)))
  }

  const isExtracting = ['uploading', 'queued', 'extracting', 'regex_done', 'ai_started', 'ai_complete'].includes(uploadState.phase)

  const tabs: { id: Tab; label: string; icon: ReactNode }[] = [
    { id: 'upload',     label: 'Last opp',  icon: <Upload className="w-4 h-4" /> },
    { id: 'history',    label: 'Historikk', icon: <History className="w-4 h-4" /> },
    { id: 'duplicates', label: 'Duplikater', icon: <Copy className="w-4 h-4" /> },
    { id: 'dashboard',  label: 'Dashboard', icon: <LayoutDashboard className="w-4 h-4" /> },
    { id: 'webhooks',   label: 'Webhooks',  icon: <Globe className="w-4 h-4" /> },
  ]

  return (
    <Layout llmInfo={llmInfo}>
      <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
        {/* Sidebar */}
        <aside className="lg:col-span-1 space-y-4">
          {/* Tab navigation */}
          <nav className="bg-white rounded-xl border border-gray-200 overflow-hidden">
            {tabs.map(tab => (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`w-full flex items-center gap-2 px-3 py-2.5 text-sm font-medium transition-colors border-b border-gray-100 last:border-0
                  ${activeTab === tab.id
                    ? 'bg-brand-600 text-white'
                    : 'text-gray-600 hover:bg-gray-50'
                  }`}
              >
                {tab.icon}
                {tab.label}
                {tab.id === 'history' && sessions.length > 0 && (
                  <span className={`ml-auto text-xs px-1.5 py-0.5 rounded-full ${
                    activeTab === 'history' ? 'bg-white/20' : 'bg-gray-200 text-gray-600'
                  }`}>
                    {sessions.length}
                  </span>
                )}
              </button>
            ))}
          </nav>

          {/* Sidebar content */}
          {(activeTab === 'upload' || activeTab === 'history') && (
            <div className="bg-white rounded-xl border border-gray-200 p-4">
              {activeTab === 'upload' ? (
                <FileUploader
                  onFileSelected={handleFileSelected}
                  onUpload={handleUpload}
                  isUploading={isExtracting}
                  isPreviewing={isPreviewing}
                />
              ) : (
                <SessionHistory
                  sessions={sessions}
                  isLoading={isLoading}
                  activeSessionId={activeSession?.sessionId}
                  onOpen={id => { openSession(id); setLocalContacts([]) }}
                  onDelete={removeSession}
                />
              )}
            </div>
          )}
        </aside>

        {/* Main content */}
        <main className="lg:col-span-3 space-y-5">
          {/* Dashboard tab */}
          {activeTab === 'dashboard' && <Dashboard />}

          {/* Duplicates tab */}
          {activeTab === 'duplicates' && <DuplicateResolver />}

          {/* Webhooks tab */}
          {activeTab === 'webhooks' && <WebhookSettings />}

          {/* Upload/History content */}
          {(activeTab === 'upload' || activeTab === 'history') && (
            <>
              {/* Extraction progress (SSE stepper) */}
              {isExtracting && (
                <ExtractionProgress phase={uploadState.phase} message={uploadState.message} progress={uploadState.progress} />
              )}

              {/* File preview */}
              {preview && !uploadState.result && !isExtracting && (
                <section className="bg-white rounded-xl border border-gray-200 p-5 space-y-4">
                  <PreviewPanel preview={preview} />
                  {preview.suggestedMappings.length > 0 && (
                    <ColumnMapper
                      mappings={preview.suggestedMappings}
                      onChange={(_: ColumnMappingDto[]) => {}}
                    />
                  )}
                </section>
              )}

              {/* Results */}
              {displayResult ? (
                <section className="bg-white rounded-xl border border-gray-200 p-5 space-y-4">
                  {/* Header */}
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div className="flex items-center gap-2">
                      <Users className="w-5 h-5 text-brand-600" />
                      <div>
                        <div className="flex items-center gap-2">
                          <h2 className="font-bold text-gray-900">{displayResult.originalFileName}</h2>
                          {displayResult.usedAi && <AiBadge />}
                        </div>
                        <p className="text-xs text-gray-500">
                          {displayResult.contactsExtracted} kontakter ekstrahert
                          {displayResult.totalRowsProcessed > 0 &&
                            ` fra ${displayResult.totalRowsProcessed} rader`}
                        </p>
                      </div>
                    </div>
                    <ExportMenu
                      sessionId={displayResult.sessionId}
                      contactCount={displayResult.contactsExtracted}
                    />
                  </div>

                  {/* Warnings */}
                  {displayResult.warnings.length > 0 && (
                    <div className="bg-amber-50 border border-amber-200 rounded-lg p-3 text-sm text-amber-800 space-y-1">
                      {displayResult.warnings.map((w, i) => <p key={i}>⚠️ {w}</p>)}
                    </div>
                  )}

                  {/* Contact table */}
                  <ContactTable
                    contacts={uploadState.result ? localContacts : displayResult.contacts}
                    sessionId={displayResult.sessionId}
                    onContactUpdated={handleContactUpdated}
                  />
                </section>
              ) : !preview && !isExtracting ? (
                <div className="bg-white rounded-xl border border-dashed border-gray-200 p-16 text-center text-gray-400">
                  <Users className="w-12 h-12 mx-auto mb-3 opacity-30" />
                  <p className="text-lg font-medium">Ingen kontakter å vise</p>
                  <p className="text-sm mt-1">Last opp en fil for å komme i gang, eller velg en sesjon fra historikken.</p>
                </div>
              ) : null}
            </>
          )}
        </main>
      </div>
    </Layout>
  )
}
