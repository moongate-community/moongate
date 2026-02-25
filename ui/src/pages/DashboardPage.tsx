import { useEffect, useState } from 'react'
import { Card, CardBody, CardHeader, Chip } from '@heroui/react'
import { api } from '../api/client'

export function DashboardPage() {
  const [health, setHealth] = useState<string | null>(null)
  const [healthError, setHealthError] = useState(false)

  useEffect(() => {
    api.get<string>('/health')
      .then((res) => {
        setHealth(res)
        setHealthError(false)
      })
      .catch(() => setHealthError(true))
  }, [])

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-bold">Dashboard</h1>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        <Card>
          <CardHeader className="pb-0">
            <span className="text-default-500 text-sm font-medium">Server Health</span>
          </CardHeader>
          <CardBody className="pt-2">
            {health !== null || healthError ? (
              <Chip
                color={healthError ? 'danger' : 'success'}
                variant="flat"
                size="lg"
              >
                {healthError ? 'Unavailable' : health}
              </Chip>
            ) : (
              <span className="text-default-400 text-sm">Checking...</span>
            )}
          </CardBody>
        </Card>
      </div>
    </div>
  )
}
