import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button, Card, CardBody, CardHeader, Input } from '@heroui/react'
import { api } from '../api/client'
import { useAuthStore } from '../store/authStore'
import type { AuthUser } from '../store/authStore'

export function LoginPage() {
  const navigate = useNavigate()
  const login = useAuthStore((s) => s.login)

  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setLoading(true)

    try {
      const user = await api.post<AuthUser>('/auth/login', { username, password })
      login(user)
      navigate('/dashboard', { replace: true })
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Login failed')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="flex items-center justify-center min-h-screen">
      <Card className="w-full max-w-sm">
        <CardHeader className="flex flex-col gap-1 px-6 pt-6">
          <h1 className="text-2xl font-bold">Moongate</h1>
          <p className="text-default-500 text-sm">Admin Panel</p>
        </CardHeader>
        <CardBody className="px-6 pb-6">
          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            <Input
              label="Username"
              value={username}
              onValueChange={setUsername}
              autoComplete="username"
              isRequired
            />
            <Input
              label="Password"
              type="password"
              value={password}
              onValueChange={setPassword}
              autoComplete="current-password"
              isRequired
            />
            {error && (
              <p className="text-danger text-sm">{error}</p>
            )}
            <Button type="submit" color="primary" isLoading={loading} fullWidth>
              Login
            </Button>
          </form>
        </CardBody>
      </Card>
    </div>
  )
}
