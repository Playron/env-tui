import { useState } from 'react'
import type { ColumnMappingDto } from '../types'
import { FIELD_MAPPING_LABELS } from '../types'

const FIELD_OPTIONS = Object.keys(FIELD_MAPPING_LABELS)

interface ColumnMapperProps {
  mappings: ColumnMappingDto[]
  onChange: (updated: ColumnMappingDto[]) => void
}

export function ColumnMapper({ mappings, onChange }: ColumnMapperProps) {
  const [localMappings, setLocalMappings] = useState(mappings)

  const updateMapping = (index: number, mappedTo: string) => {
    const updated = localMappings.map((m, i) =>
      i === index ? { ...m, mappedTo: mappedTo || undefined } : m
    )
    setLocalMappings(updated)
    onChange(updated)
  }

  return (
    <div className="space-y-3">
      <h3 className="font-semibold text-gray-700 text-sm uppercase tracking-wide">
        Kolonne-mapping
      </h3>
      <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
        {localMappings.map((m, i) => (
          <div
            key={m.sourceColumn}
            className="flex items-center gap-2 bg-white border border-gray-200 rounded-lg px-3 py-2"
          >
            <div className="flex-1 min-w-0">
              <p className="text-xs text-gray-500 truncate">{m.sourceColumn}</p>
              {m.sampleValues[0] && (
                <p className="text-xs text-gray-400 italic truncate">f.eks: {m.sampleValues[0]}</p>
              )}
            </div>
            <select
              value={m.mappedTo ?? ''}
              onChange={e => updateMapping(i, e.target.value)}
              className="text-sm border border-gray-300 rounded-md px-2 py-1 focus:outline-none focus:ring-2 focus:ring-brand-500"
            >
              {FIELD_OPTIONS.map(opt => (
                <option key={opt} value={opt}>
                  {FIELD_MAPPING_LABELS[opt]}
                </option>
              ))}
            </select>
          </div>
        ))}
      </div>
    </div>
  )
}
