import type { PreviewResultDto } from '../types'

interface PreviewPanelProps {
  preview: PreviewResultDto
}

export function PreviewPanel({ preview }: PreviewPanelProps) {
  if (preview.headers.length === 0) {
    return (
      <div className="bg-amber-50 border border-amber-200 rounded-lg p-4 text-amber-800 text-sm">
        Ingen kolonner oppdaget. Filen leses som fritekst.
      </div>
    )
  }

  return (
    <div className="space-y-3">
      <h3 className="font-semibold text-gray-700 text-sm uppercase tracking-wide">
        Forhåndsvisning ({preview.headers.length} kolonner · {preview.sampleRows.length} eksempelrader)
      </h3>
      <div className="overflow-x-auto rounded-lg border border-gray-200">
        <table className="min-w-full divide-y divide-gray-200 text-sm">
          <thead className="bg-gray-50">
            <tr>
              {preview.headers.map(h => (
                <th
                  key={h}
                  className="px-3 py-2 text-left font-semibold text-gray-600 whitespace-nowrap"
                >
                  {h}
                  {preview.suggestedMappings.find(m => m.sourceColumn === h)?.mappedTo && (
                    <span className="ml-1 text-xs font-normal text-brand-600">
                      → {preview.suggestedMappings.find(m => m.sourceColumn === h)?.mappedTo}
                    </span>
                  )}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100 bg-white">
            {preview.sampleRows.map((row, i) => (
              <tr key={i} className="hover:bg-gray-50">
                {preview.headers.map(h => (
                  <td key={h} className="px-3 py-2 text-gray-700 max-w-xs truncate">
                    {row[h] ?? ''}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
