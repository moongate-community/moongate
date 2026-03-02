import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button, Input, Select, SelectItem } from '@heroui/react'
import { api } from '../api/client'

const ROLES = ['Regular', 'GameMaster', 'Administrator']

export function AddUserPage() {
  const navigate = useNavigate()
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [formData, setFormData] = useState({
    username: '',
    email: '',
    password: '',
    role: 'Regular',
  })

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)

    if (!formData.username.trim() || !formData.password.trim()) {
      setError('Username and Password are required.')
      return
    }

    setIsSubmitting(true)
    try {
      await api.post('/users', {
        username: formData.username.trim(),
        password: formData.password,
        email: formData.email.trim(),
        role: formData.role,
      })

      navigate('/users', { replace: true })
    } catch (submitError) {
      setError(submitError instanceof Error ? submitError.message : 'Failed to create user.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="flex flex-col gap-6 animate-fade-in w-full max-w-[720px] mx-auto">
      <div className="flex items-center justify-between">
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
              Add User
            </h1>
          </div>
          <p className="font-mono text-xs pl-5" style={{ color: 'rgba(185,187,211,0.35)', letterSpacing: '0.1em' }}>
            CREATE NEW ACCOUNT
          </p>
        </div>

        <Button
          variant="light"
          color="primary"
          className="font-mono text-xs uppercase tracking-wider"
          onPress={() => navigate('/users')}
        >
          Back
        </Button>
      </div>

      <form
        onSubmit={handleSubmit}
        className="rounded-xl border border-[rgba(106,165,218,0.15)] bg-[rgba(36,33,48,0.7)] backdrop-blur-md p-5 flex flex-col gap-4"
      >
        <Input
          label="Username"
          value={formData.username}
          onValueChange={(value) => setFormData({ ...formData, username: value })}
          isRequired
          classNames={{
            inputWrapper: 'bg-[#1f1c2a] border-[rgba(106,165,218,0.2)] data-[hover=true]:border-[#6aa5da]',
            label: 'text-[rgba(185,187,211,0.6)] font-mono text-xs tracking-wider',
            input: 'font-mono text-sm text-[#f9f4ed]',
          }}
        />

        <Input
          label="Email"
          type="email"
          value={formData.email}
          onValueChange={(value) => setFormData({ ...formData, email: value })}
          classNames={{
            inputWrapper: 'bg-[#1f1c2a] border-[rgba(106,165,218,0.2)] data-[hover=true]:border-[#6aa5da]',
            label: 'text-[rgba(185,187,211,0.6)] font-mono text-xs tracking-wider',
            input: 'font-mono text-sm text-[#f9f4ed]',
          }}
        />

        <Select
          label="Role"
          selectedKeys={[formData.role]}
          onChange={(event) => setFormData({ ...formData, role: event.target.value })}
          classNames={{
            trigger: 'bg-[#1f1c2a] border border-[rgba(106,165,218,0.2)] data-[hover=true]:border-[#6aa5da]',
            label: 'text-[rgba(185,187,211,0.6)] font-mono text-xs tracking-wider',
            value: 'font-mono text-sm text-[#f9f4ed]',
            popoverContent: 'bg-[#242130] border border-[rgba(106,165,218,0.2)]',
          }}
        >
          {ROLES.map((role) => (
            <SelectItem key={role} className="font-mono text-sm text-[#f9f4ed] hover:bg-[rgba(106,165,218,0.1)]">
              {role}
            </SelectItem>
          ))}
        </Select>

        <Input
          label="Password"
          type="password"
          value={formData.password}
          onValueChange={(value) => setFormData({ ...formData, password: value })}
          isRequired
          classNames={{
            inputWrapper: 'bg-[#1f1c2a] border-[rgba(106,165,218,0.2)] data-[hover=true]:border-[#6aa5da]',
            label: 'text-[rgba(185,187,211,0.6)] font-mono text-xs tracking-wider',
            input: 'font-mono text-sm text-[#f9f4ed]',
          }}
        />

        {error && (
          <div className="p-3 rounded bg-[rgba(239,68,68,0.1)] border border-[rgba(239,68,68,0.2)]">
            <p className="font-mono text-xs text-[#ef4444] tracking-wider">{error}</p>
          </div>
        )}

        <div className="flex items-center justify-end gap-2 pt-1">
          <Button
            type="button"
            color="default"
            variant="light"
            className="font-mono text-xs uppercase tracking-wider"
            onPress={() => navigate('/users')}
          >
            Cancel
          </Button>
          <Button type="submit" color="primary" isLoading={isSubmitting} className="font-mono text-xs uppercase tracking-wider">
            Create User
          </Button>
        </div>
      </form>
    </div>
  )
}
