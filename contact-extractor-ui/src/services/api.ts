import type {
  ContactDto,
  ExtractionResultDto,
  LlmSettingsInfoDto,
  PreviewResultDto,
  SessionSummaryDto,
  SupportedFormatDto,
  UploadAcceptedDto,
} from '../types'

const BASE_URL = '/api'

async function handleResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const text = await res.text()
    throw new Error(text || `HTTP ${res.status}`)
  }
  return res.json() as Promise<T>
}

// Upload endpoints

/** Synkron upload (legacy – brukes av useFileUpload) */
export async function uploadFile(file: File): Promise<ExtractionResultDto> {
  const form = new FormData()
  form.append('file', file)
  const res = await fetch(`${BASE_URL}/upload`, { method: 'POST', body: form })
  return handleResponse<ExtractionResultDto>(res)
}

/** Asynkron upload – returnerer 202 med sessionId og stream-URL */
export async function uploadFileAsync(
  file: File,
  signal?: AbortSignal
): Promise<UploadAcceptedDto> {
  const form = new FormData()
  form.append('file', file)
  const res = await fetch(`${BASE_URL}/upload`, {
    method: 'POST',
    body: form,
    signal,
  })
  return handleResponse<UploadAcceptedDto>(res)
}

/** Polling-fallback: hent ferdig resultat fra DB */
export async function getExtractionResult(
  sessionId: string,
  signal?: AbortSignal
): Promise<ExtractionResultDto | null> {
  const res = await fetch(`${BASE_URL}/upload/${sessionId}/result`, { signal })
  if (res.status === 202) return null   // Fortsatt i prosess
  return handleResponse<ExtractionResultDto>(res)
}

export async function previewFile(file: File): Promise<PreviewResultDto> {
  const form = new FormData()
  form.append('file', file)
  const res = await fetch(`${BASE_URL}/upload/preview`, { method: 'POST', body: form })
  return handleResponse<PreviewResultDto>(res)
}

export async function getSupportedFormats(): Promise<SupportedFormatDto[]> {
  const res = await fetch(`${BASE_URL}/upload/supported-formats`)
  return handleResponse<SupportedFormatDto[]>(res)
}

// Contact endpoints
export async function getAllSessions(): Promise<SessionSummaryDto[]> {
  const res = await fetch(`${BASE_URL}/contacts`)
  return handleResponse<SessionSummaryDto[]>(res)
}

export async function getSessionContacts(sessionId: string): Promise<ExtractionResultDto> {
  const res = await fetch(`${BASE_URL}/contacts/${sessionId}`)
  return handleResponse<ExtractionResultDto>(res)
}

export async function updateContact(
  sessionId: string,
  contactId: string,
  data: Partial<ContactDto>
): Promise<ContactDto> {
  const res = await fetch(`${BASE_URL}/contacts/${sessionId}/contacts/${contactId}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data),
  })
  return handleResponse<ContactDto>(res)
}

export async function deleteSession(sessionId: string): Promise<void> {
  const res = await fetch(`${BASE_URL}/contacts/${sessionId}`, { method: 'DELETE' })
  if (!res.ok) {
    const text = await res.text()
    throw new Error(text || `HTTP ${res.status}`)
  }
}

// Export endpoints
export async function exportCsv(sessionId: string): Promise<void> {
  const res = await fetch(`${BASE_URL}/export/${sessionId}/csv`, { method: 'POST' })
  if (!res.ok) throw new Error(`HTTP ${res.status}`)
  const blob = await res.blob()
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `kontakter_${sessionId}.csv`
  a.click()
  URL.revokeObjectURL(url)
}

// Settings endpoints
export async function getLlmSettings(): Promise<LlmSettingsInfoDto> {
  const res = await fetch(`${BASE_URL}/settings/llm`)
  return handleResponse<LlmSettingsInfoDto>(res)
}

export async function exportExcel(sessionId: string): Promise<void> {
  const res = await fetch(`${BASE_URL}/export/${sessionId}/excel`, { method: 'POST' })
  if (!res.ok) throw new Error(`HTTP ${res.status}`)
  const blob = await res.blob()
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `kontakter_${sessionId}.xlsx`
  a.click()
  URL.revokeObjectURL(url)
}
