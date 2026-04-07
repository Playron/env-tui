import { useCallback, useState, useRef } from 'react'
import { Upload, FileText, X } from 'lucide-react'
import { ProgressIndicator } from './ProgressIndicator'

const ACCEPTED_EXTENSIONS = ['.csv', '.xlsx', '.pdf', '.docx', '.txt', '.vcf']
const FILE_ICONS: Record<string, string> = {
  '.csv':  '📄',
  '.xlsx': '📊',
  '.pdf':  '📕',
  '.docx': '📝',
  '.txt':  '📃',
  '.vcf':  '👤',
}

function getExtension(name: string) {
  return name.slice(name.lastIndexOf('.')).toLowerCase()
}

interface FileUploaderProps {
  onFileSelected: (file: File) => void
  onUpload: (file: File) => void
  isUploading: boolean
  isPreviewing: boolean
}

export function FileUploader({
  onFileSelected,
  onUpload,
  isUploading,
  isPreviewing,
}: FileUploaderProps) {
  const [dragging, setDragging] = useState(false)
  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const inputRef = useRef<HTMLInputElement>(null)

  const handleFile = useCallback(
    (file: File) => {
      const ext = getExtension(file.name)
      if (!ACCEPTED_EXTENSIONS.includes(ext)) {
        alert(`Filtypen "${ext}" støttes ikke.\n\nStøttede formater: ${ACCEPTED_EXTENSIONS.join(', ')}`)
        return
      }
      setSelectedFile(file)
      onFileSelected(file)
    },
    [onFileSelected]
  )

  const onDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault()
      setDragging(false)
      const file = e.dataTransfer.files[0]
      if (file) handleFile(file)
    },
    [handleFile]
  )

  const onInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (file) handleFile(file)
  }

  const clearFile = () => {
    setSelectedFile(null)
    if (inputRef.current) inputRef.current.value = ''
  }

  if (isUploading || isPreviewing) {
    return <ProgressIndicator message={isUploading ? 'Ekstraher kontakter...' : 'Laster forhåndsvisning...'} />
  }

  return (
    <div className="space-y-4">
      <div
        onDragOver={e => { e.preventDefault(); setDragging(true) }}
        onDragLeave={() => setDragging(false)}
        onDrop={onDrop}
        onClick={() => !selectedFile && inputRef.current?.click()}
        className={`
          relative border-2 border-dashed rounded-xl p-10 text-center cursor-pointer
          transition-colors duration-150
          ${dragging
            ? 'border-brand-500 bg-brand-50'
            : 'border-gray-300 hover:border-brand-400 hover:bg-gray-50'
          }
        `}
      >
        <input
          ref={inputRef}
          type="file"
          accept={ACCEPTED_EXTENSIONS.join(',')}
          className="hidden"
          onChange={onInputChange}
        />
        {selectedFile ? (
          <div className="flex flex-col items-center gap-2">
            <span className="text-4xl">{FILE_ICONS[getExtension(selectedFile.name)] ?? '📄'}</span>
            <p className="font-semibold text-gray-800">{selectedFile.name}</p>
            <p className="text-sm text-gray-500">{(selectedFile.size / 1024).toFixed(1)} KB</p>
            <button
              onClick={e => { e.stopPropagation(); clearFile() }}
              className="text-gray-400 hover:text-red-500 transition-colors"
            >
              <X className="w-5 h-5" />
            </button>
          </div>
        ) : (
          <div className="flex flex-col items-center gap-3">
            <Upload className="w-10 h-10 text-gray-400" />
            <div>
              <p className="text-gray-700 font-medium">Dra og slipp en fil her</p>
              <p className="text-sm text-gray-500">eller klikk for å velge fil</p>
            </div>
            <div className="flex flex-wrap justify-center gap-2 mt-2">
              {ACCEPTED_EXTENSIONS.map(ext => (
                <span
                  key={ext}
                  className="px-2 py-0.5 bg-gray-100 text-gray-600 text-xs rounded-full font-mono"
                >
                  {FILE_ICONS[ext]} {ext}
                </span>
              ))}
            </div>
          </div>
        )}
      </div>

      {selectedFile && (
        <div className="flex gap-3">
          <button
            onClick={() => onUpload(selectedFile)}
            className="flex-1 flex items-center justify-center gap-2 bg-brand-600 hover:bg-brand-700 text-white font-semibold py-3 px-6 rounded-lg transition-colors"
          >
            <FileText className="w-4 h-4" />
            Ekstraher kontakter
          </button>
        </div>
      )}
    </div>
  )
}
