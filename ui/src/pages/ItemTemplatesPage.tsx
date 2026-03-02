import { useEffect, useMemo, useState } from 'react'
import {
  Button,
  Chip,
  Select,
  SelectItem,
  Spinner,
  Table,
  TableBody,
  TableCell,
  TableColumn,
  TableHeader,
  TableRow,
} from '@heroui/react'
import { api } from '../api/client'

interface ItemTemplateSummary {
  id: string
  name: string
  category: string
  itemId: string
}

interface ItemTemplatePage {
  page: number
  pageSize: number
  totalCount: number
  items: ItemTemplateSummary[]
}

const PAGE_SIZES = [12, 24, 48, 96]

function ItemTemplatePreview({ itemId }: { itemId: string }) {
  const [previewUrl, setPreviewUrl] = useState<string | null>(null)
  const [status, setStatus] = useState<'loading' | 'ok' | 'error'>('loading')

  useEffect(() => {
    let objectUrl: string | null = null
    let disposed = false

    async function loadPreview() {
      setStatus('loading')
      setPreviewUrl(null)

      try {
        const blob = await api.getBlob(`/item-templates/by-item-id/${encodeURIComponent(itemId)}/image`)
        if (disposed) {
          return
        }

        objectUrl = URL.createObjectURL(blob)
        setPreviewUrl(objectUrl)
        setStatus('ok')
      } catch {
        if (!disposed) {
          setStatus('error')
        }
      }
    }

    loadPreview()

    return () => {
      disposed = true
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl)
      }
    }
  }, [itemId])

  if (status === 'loading') {
    return (
      <div className="w-11 h-11 rounded-lg border border-[rgba(106,165,218,0.15)] bg-[rgba(31,28,42,0.75)] flex items-center justify-center">
        <Spinner size="sm" color="primary" />
      </div>
    )
  }

  if (status === 'error' || !previewUrl) {
    return (
      <div className="w-11 h-11 rounded-lg border border-[rgba(106,165,218,0.15)] bg-[rgba(31,28,42,0.75)] flex items-center justify-center">
        <span className="font-mono text-[10px] tracking-wider text-[rgba(185,187,211,0.6)]">N/A</span>
      </div>
    )
  }

  return (
    <div className="w-11 h-11 rounded-lg border border-[rgba(106,165,218,0.18)] bg-[rgba(31,28,42,0.75)] overflow-hidden">
      <img src={previewUrl} alt={itemId} className="w-full h-full object-contain p-1" loading="lazy" />
    </div>
  )
}

export function ItemTemplatesPage() {
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(24)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [result, setResult] = useState<ItemTemplatePage>({
    page: 1,
    pageSize: 24,
    totalCount: 0,
    items: [],
  })

  useEffect(() => {
    let mounted = true

    async function load() {
      setLoading(true)
      setError(null)

      try {
        const payload = await api.get<ItemTemplatePage>(`/item-templates?page=${page}&pageSize=${pageSize}`)
        if (mounted) {
          setResult(payload)
        }
      } catch (fetchError) {
        if (mounted) {
          setError(fetchError instanceof Error ? fetchError.message : 'Failed to load item templates.')
        }
      } finally {
        if (mounted) {
          setLoading(false)
        }
      }
    }

    load()
    return () => {
      mounted = false
    }
  }, [page, pageSize])

  const totalPages = useMemo(() => Math.max(1, Math.ceil(result.totalCount / result.pageSize)), [result])

  function goPreviousPage() {
    setPage((currentPage) => Math.max(1, currentPage - 1))
  }

  function goNextPage() {
    setPage((currentPage) => Math.min(totalPages, currentPage + 1))
  }

  return (
    <div className="flex flex-col gap-6 animate-fade-in w-full max-w-[1360px] mx-auto">
      <div className="flex items-end justify-between gap-4 flex-wrap">
        <div>
          <div className="flex items-center gap-3 mb-1">
            <div
              style={{
                width: '2px',
                height: '20px',
                background: '#6aa5da',
                borderRadius: '1px',
                boxShadow: '0 0 6px rgba(106,165,218,0.5)',
              }}
            />
            <h1
              className="font-cinzel font-semibold tracking-wider"
              style={{ color: '#f9f4ed', fontSize: '18px', letterSpacing: '0.12em' }}
            >
              Item Templates
            </h1>
          </div>
          <p className="font-mono text-xs pl-5" style={{ color: 'rgba(185,187,211,0.35)', letterSpacing: '0.1em' }}>
            CATALOG WITH LIVE ART PREVIEWS
          </p>
        </div>

        <div className="flex items-center gap-3">
          <Chip
            variant="flat"
            className="font-mono text-xs tracking-wider uppercase"
            style={{
              background: 'rgba(106,165,218,0.1)',
              border: '1px solid rgba(106,165,218,0.2)',
              color: '#6aa5da',
            }}
          >
            Total: {result.totalCount}
          </Chip>

          <Select
            disallowEmptySelection
            size="sm"
            label="Rows"
            labelPlacement="outside-left"
            selectedKeys={[String(pageSize)]}
            onSelectionChange={(keys) => {
              const firstKey = Array.from(keys)[0]
              const parsedPageSize = Number(firstKey)
              if (!Number.isNaN(parsedPageSize)) {
                setPage(1)
                setPageSize(parsedPageSize)
              }
            }}
            className="w-44"
            classNames={{
              trigger: 'bg-[rgba(36,33,48,0.7)] border border-[rgba(106,165,218,0.2)]',
              value: 'font-mono text-xs text-[#f9f4ed]',
              label: 'font-mono text-xs text-[rgba(185,187,211,0.6)]',
              selectorIcon: 'text-[#6aa5da]',
              popoverContent: 'bg-[#242130] border border-[rgba(106,165,218,0.2)]',
            }}
          >
            {PAGE_SIZES.map((size) => (
              <SelectItem key={String(size)} className="font-mono text-xs">
                {size}
              </SelectItem>
            ))}
          </Select>
        </div>
      </div>

      {error && (
        <div className="rounded-lg border border-[rgba(239,68,68,0.28)] bg-[rgba(239,68,68,0.1)] px-4 py-3">
          <p className="font-mono text-xs uppercase tracking-wider text-[#ef4444]">Error: {error}</p>
        </div>
      )}

      <div className="rounded-xl border border-[rgba(106,165,218,0.15)] bg-[rgba(36,33,48,0.7)] backdrop-blur-md p-2">
        <Table
          aria-label="Item templates table"
          classNames={{
            wrapper: 'bg-transparent shadow-none',
            th: 'bg-[rgba(106,165,218,0.1)] text-[#6aa5da] font-mono text-xs tracking-widest uppercase border-b border-[rgba(106,165,218,0.15)]',
            td: 'border-b border-[rgba(106,165,218,0.05)] py-3 font-mono text-sm',
          }}
        >
          <TableHeader>
            <TableColumn width={80}>ART</TableColumn>
            <TableColumn>ID</TableColumn>
            <TableColumn>NAME</TableColumn>
            <TableColumn>CATEGORY</TableColumn>
            <TableColumn>ITEM ID</TableColumn>
          </TableHeader>
          <TableBody
            items={result.items}
            isLoading={loading}
            loadingContent={<Spinner color="primary" />}
            emptyContent={error ? 'Unable to load templates.' : 'No item templates found.'}
          >
            {(item) => (
              <TableRow key={item.id}>
                <TableCell>
                  <ItemTemplatePreview itemId={item.itemId} />
                </TableCell>
                <TableCell>
                  <span className="text-[#6aa5da] font-semibold">{item.id}</span>
                </TableCell>
                <TableCell>
                  <span className="text-[#f9f4ed]">{item.name || 'Unnamed'}</span>
                </TableCell>
                <TableCell>
                  <span className="text-[rgba(185,187,211,0.8)]">{item.category || 'General'}</span>
                </TableCell>
                <TableCell>
                  <Chip
                    size="sm"
                    variant="flat"
                    className="font-mono text-xs tracking-wider"
                    style={{
                      background: 'rgba(106,165,218,0.1)',
                      border: '1px solid rgba(106,165,218,0.15)',
                      color: '#f9f4ed',
                    }}
                  >
                    {item.itemId}
                  </Chip>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>

      <div className="flex items-center justify-between rounded-lg border border-[rgba(106,165,218,0.15)] bg-[rgba(36,33,48,0.55)] px-4 py-3">
        <div className="font-mono text-xs tracking-wider text-[rgba(185,187,211,0.6)] uppercase">
          Page {result.page} / {totalPages}
        </div>

        <div className="flex items-center gap-2">
          <Button
            size="sm"
            variant="flat"
            color="primary"
            isDisabled={result.page <= 1 || loading}
            onPress={goPreviousPage}
            className="font-mono text-xs uppercase tracking-wider"
          >
            Previous
          </Button>
          <Button
            size="sm"
            variant="flat"
            color="primary"
            isDisabled={result.page >= totalPages || loading}
            onPress={goNextPage}
            className="font-mono text-xs uppercase tracking-wider"
          >
            Next
          </Button>
        </div>
      </div>
    </div>
  )
}
