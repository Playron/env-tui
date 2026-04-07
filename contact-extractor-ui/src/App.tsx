import { useState, useEffect } from 'react'
import { Layout } from './components/Layout'
import { FileUploader } from './components/FileUploader'
import { PreviewPanel } from './components/PreviewPanel'
import { ColumnMapper } from './components/ColumnMapper'
import { ContactTable } from './components/ContactTable'
import { ExportButton } from './components/ExportButton'
import { SessionHistory } from './components/SessionHistory'
import { AiBadge } from './components/AiBadge'
import { useFileUpload } from './hooks/useFileUpload'
import { useContacts } from './hooks/useContacts'
import { getLlmSettings } from './services/api'
import type { ContactDto, ColumnMappingDto, LlmSettingsInfoDto } from './types'
import { Upload, History, Users } from 'lucide-react'

type Tab = 'upload' | 'history'

export default function App() {
  const [activeTab, setActiveTab] = useState<Tab>('upload')
  const [localContacts, setLocalContacts] = useState<ContactDto[]>([])
  const [llmInfo, setLlmInfo] = useState<LlmSettingsInfoDto | null>(null)

  const { isUploading, isPreviewing, preview, result, upload, fetchPreview, reset } =
    useFileUpload()

  const { sessions, activeSession, isLoading, loadSessions, openSession, removeSession } =
    useContacts()

  useEffect(() => {
    getLlmSettings().then(setLlmInfo).catch(() => {/* ignore */})
  }, [])

  // Sync local contacts when a new extraction result arrives
  useEffect(() => {
    if (result) setLocalContacts(result.contacts)
  }, [result])

  const handleFileSelected = (file: File) => {
    reset()
    fetchPreview(file)
  }

  const handleUpload = async (file: File) => {
    await upload(file)
    await loadSessions()
  }

  const displayResult = result ?? activeSession

  const handleContactUpdated = (updated: ContactDto) => {
    setLocalContacts(prev => prev.map(c => (c.id === updated.id ? updated : c)))
  }

  const handleMappingChange = (_: ColumnMappingDto[]) => {
    // Mapping changes are informational; full re-extraction requires re-upload
  }

  return (
    <Layout llmInfo={llmInfo}>
      <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
        {/* Sidebar */}
        <aside className="lg:col-span-1 space-y-4">
          {/* Tabs */}
          <div className="flex rounded-lg overflow-hidden border border-gray-200">
            <button
              onClick={() => setActiveTab('upload')}
              className={`flex-1 flex items-center justify-center gap-2 py-2.5 text-sm font-medium transition-colors
                ${activeTab === 'upload'
                  ? 'bg-brand-600 text-white'
                  : 'bg-white text-gray-600 hover:bg-gray-50'
                }`}
            >
              <Upload className="w-4 h-4" />
              Last opp
            </button>
            <button
              onClick={() => setActiveTab('history')}
              className={`flex-1 flex items-center justify-center gap-2 py-2.5 text-sm font-medium transition-colors
                ${activeTab === 'history'
                  ? 'bg-brand-600 text-white'
                  : 'bg-white text-gray-600 hover:bg-gray-50'
                }`}
            >
              <History className="w-4 h-4" />
              Historikk
              {sessions.length > 0 && (
                <span className={`text-xs px-1.5 py-0.5 rounded-full ${
                  activeTab === 'history' ? 'bg-white/20' : 'bg-gray-200 text-gray-600'
                }`}>
                  {sessions.length}
                </span>
              )}
            </button>
          </div>

          {/* Sidebar content */}
          <div className="bg-white rounded-xl border border-gray-200 p-4">
            {activeTab === 'upload' ? (
              <FileUploader
                onFileSelected={handleFileSelected}
                onUpload={handleUpload}
                isUploading={isUploading}
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
        </aside>

        {/* Main content */}
        <main className="lg:col-span-3 space-y-5">
          {/* Preview */}
          {preview && !result && (
            <section className="bg-white rounded-xl border border-gray-200 p-5 space-y-4">
              <PreviewPanel preview={preview} />
              {preview.suggestedMappings.length > 0 && (
                <ColumnMapper
                  mappings={preview.suggestedMappings}
                  onChange={handleMappingChange}
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
                <ExportButton
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
                contacts={result ? localContacts : displayResult.contacts}
                sessionId={displayResult.sessionId}
                onContactUpdated={handleContactUpdated}
              />
            </section>
          ) : !preview ? (
            <div className="bg-white rounded-xl border border-dashed border-gray-200 p-16 text-center text-gray-400">
              <Users className="w-12 h-12 mx-auto mb-3 opacity-30" />
              <p className="text-lg font-medium">Ingen kontakter å vise</p>
              <p className="text-sm mt-1">Last opp en fil for å komme i gang, eller velg en sesjon fra historikken.</p>
            </div>
          ) : null}
        </main>
      </div>
    </Layout>
  )
}
