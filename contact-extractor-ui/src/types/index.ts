export interface ContactDto {
  id: string
  firstName?: string
  lastName?: string
  fullName?: string
  email?: string
  phone?: string
  organization?: string
  title?: string
  address?: string
  confidence: number
  extractionSource: 'regex' | 'ai' | 'manual'
}

export interface ExtractionResultDto {
  sessionId: string
  originalFileName: string
  fileType: string
  totalRowsProcessed: number
  contactsExtracted: number
  usedAi: boolean
  contacts: ContactDto[]
  warnings: string[]
}

export interface ColumnMappingDto {
  sourceColumn: string
  mappedTo?: string
  sampleValues: string[]
}

export interface PreviewResultDto {
  fileName: string
  fileType: string
  headers: string[]
  sampleRows: Record<string, string>[]
  suggestedMappings: ColumnMappingDto[]
}

export interface SessionSummaryDto {
  id: string
  originalFileName: string
  fileType: string
  totalRowsProcessed: number
  contactCount: number
  usedAi: boolean
  createdAt: string
}

export interface SupportedFormatDto {
  extension: string
  description: string
  icon: string
}

export interface LlmSettingsInfoDto {
  provider: string
  model?: string
  hasApiKey: boolean
  baseUrl?: string
}

export type FieldMapping =
  | 'FirstName'
  | 'LastName'
  | 'FullName'
  | 'Email'
  | 'Phone'
  | 'Organization'
  | 'Title'
  | 'Address'
  | ''

export const FIELD_MAPPING_LABELS: Record<string, string> = {
  FirstName:    'Fornavn',
  LastName:     'Etternavn',
  FullName:     'Fullt navn',
  Email:        'E-post',
  Phone:        'Telefon',
  Organization: 'Organisasjon',
  Title:        'Stilling',
  Address:      'Adresse',
  '':           '(Ikke bruk)',
}
