import type {
  AuditLogDto,
  ContactDto,
  DashboardDto,
  DuplicateGroupDto,
  ExtractionResultDto,
  LlmSettingsInfoDto,
  PreviewResultDto,
  SessionSummaryDto,
  SupportedFormatDto,
  TagDto,
  UploadAcceptedDto,
  WebhookConfigDto,
} from '../types'

const BASE_URL = '/api'

async function handleResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const text = await res.text()
    throw new Error(text || `HTTP ${res.status}`)
  }
  return res.json() as Promise<T>
}

// ── Upload endpoints ──────────────────────────────────────────────────────────

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
  if (res.status === 202) return null
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

// ── Contact endpoints ─────────────────────────────────────────────────────────

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
  if (!res.ok) throw new Error(`HTTP ${res.status}`)
}

// ── Export endpoints ──────────────────────────────────────────────────────────

async function downloadFile(url: string, filename: string): Promise<void> {
  const res = await fetch(url, { method: 'POST' })
  if (!res.ok) throw new Error(`HTTP ${res.status}`)
  const blob = await res.blob()
  const link = document.createElement('a')
  link.href = URL.createObjectURL(blob)
  link.download = filename
  link.click()
  URL.revokeObjectURL(link.href)
}

export const exportCsv     = (id: string) => downloadFile(`${BASE_URL}/export/${id}/csv`,     `kontakter_${id}.csv`)
export const exportExcel   = (id: string) => downloadFile(`${BASE_URL}/export/${id}/excel`,   `kontakter_${id}.xlsx`)
export const exportVCard   = (id: string) => downloadFile(`${BASE_URL}/export/${id}/vcard`,   `kontakter_${id}.vcf`)
export const exportGoogle  = (id: string) => downloadFile(`${BASE_URL}/export/${id}/google`,  `google_${id}.csv`)
export const exportOutlook = (id: string) => downloadFile(`${BASE_URL}/export/${id}/outlook`, `outlook_${id}.csv`)

// ── Settings endpoints ────────────────────────────────────────────────────────

export async function getLlmSettings(): Promise<LlmSettingsInfoDto> {
  const res = await fetch(`${BASE_URL}/settings/llm`)
  return handleResponse<LlmSettingsInfoDto>(res)
}

// ── Tag endpoints ─────────────────────────────────────────────────────────────

export async function getTags(): Promise<TagDto[]> {
  const res = await fetch(`${BASE_URL}/tags`)
  return handleResponse<TagDto[]>(res)
}

export async function createTag(name: string, color?: string): Promise<TagDto> {
  const res = await fetch(`${BASE_URL}/tags`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ name, color }),
  })
  return handleResponse<TagDto>(res)
}

export async function deleteTag(tagId: string): Promise<void> {
  const res = await fetch(`${BASE_URL}/tags/${tagId}`, { method: 'DELETE' })
  if (!res.ok) throw new Error(`HTTP ${res.status}`)
}

export async function addTagToContacts(contactIds: string[], tagId: string): Promise<void> {
  const res = await fetch(`${BASE_URL}/tags/contacts/add`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ contactIds, tagId }),
  })
  if (!res.ok) throw new Error(`HTTP ${res.status}`)
}

// ── Duplicate endpoints ───────────────────────────────────────────────────────

export async function getDuplicateGroups(): Promise<DuplicateGroupDto[]> {
  const res = await fetch(`${BASE_URL}/duplicates`)
  return handleResponse<DuplicateGroupDto[]>(res)
}

export async function mergeDuplicates(
  groupId: string,
  primaryContactId: string
): Promise<ContactDto> {
  const res = await fetch(`${BASE_URL}/duplicates/${groupId}/merge`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ primaryContactId }),
  })
  return handleResponse<ContactDto>(res)
}

export async function dismissDuplicates(groupId: string): Promise<void> {
  const res = await fetch(`${BASE_URL}/duplicates/${groupId}/dismiss`, { method: 'POST' })
  if (!res.ok) throw new Error(`HTTP ${res.status}`)
}

// ── Dashboard endpoints ───────────────────────────────────────────────────────

export async function getDashboard(): Promise<DashboardDto> {
  const res = await fetch(`${BASE_URL}/dashboard`)
  return handleResponse<DashboardDto>(res)
}

export async function getAuditLog(): Promise<AuditLogDto[]> {
  const res = await fetch(`${BASE_URL}/dashboard/audit`)
  return handleResponse<AuditLogDto[]>(res)
}

// ── Webhook endpoints ─────────────────────────────────────────────────────────

export async function getWebhooks(): Promise<WebhookConfigDto[]> {
  const res = await fetch(`${BASE_URL}/webhooks`)
  return handleResponse<WebhookConfigDto[]>(res)
}

export async function createWebhook(
  url: string,
  event: string,
  secret?: string
): Promise<WebhookConfigDto> {
  const res = await fetch(`${BASE_URL}/webhooks`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ url, event, secret }),
  })
  return handleResponse<WebhookConfigDto>(res)
}

export async function deleteWebhook(webhookId: string): Promise<void> {
  const res = await fetch(`${BASE_URL}/webhooks/${webhookId}`, { method: 'DELETE' })
  if (!res.ok) throw new Error(`HTTP ${res.status}`)
}

export async function testWebhook(webhookId: string): Promise<void> {
  const res = await fetch(`${BASE_URL}/webhooks/${webhookId}/test`, { method: 'POST' })
  if (!res.ok) throw new Error(`HTTP ${res.status}`)
}
