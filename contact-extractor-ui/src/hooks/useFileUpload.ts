import { useState } from 'react'
import toast from 'react-hot-toast'
import { uploadFile, previewFile } from '../services/api'
import type { ExtractionResultDto, PreviewResultDto } from '../types'

interface UseFileUploadReturn {
  isUploading: boolean
  isPreviewing: boolean
  preview: PreviewResultDto | null
  result: ExtractionResultDto | null
  upload: (file: File) => Promise<void>
  fetchPreview: (file: File) => Promise<void>
  reset: () => void
}

export function useFileUpload(): UseFileUploadReturn {
  const [isUploading, setIsUploading] = useState(false)
  const [isPreviewing, setIsPreviewing] = useState(false)
  const [preview, setPreview] = useState<PreviewResultDto | null>(null)
  const [result, setResult] = useState<ExtractionResultDto | null>(null)

  const fetchPreview = async (file: File) => {
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

  const upload = async (file: File) => {
    setIsUploading(true)
    try {
      const data = await uploadFile(file)
      setResult(data)
      if (data.warnings.length > 0) {
        data.warnings.forEach(w => toast(w, { icon: '⚠️' }))
      }
      toast.success(`${data.contactsExtracted} kontakter ekstrahert!`)
    } catch (err) {
      toast.error(`Opplasting feilet: ${err instanceof Error ? err.message : 'Ukjent feil'}`)
    } finally {
      setIsUploading(false)
    }
  }

  const reset = () => {
    setPreview(null)
    setResult(null)
  }

  return { isUploading, isPreviewing, preview, result, upload, fetchPreview, reset }
}
