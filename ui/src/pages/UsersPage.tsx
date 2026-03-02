import { useEffect, useState } from 'react'
import {
  Table, TableHeader, TableColumn, TableBody, TableRow, TableCell,
  Chip, Button, Spinner, Dropdown, DropdownTrigger, DropdownMenu, DropdownItem,
  Modal, ModalContent, ModalHeader, ModalBody, ModalFooter, useDisclosure, Input, Select, SelectItem
} from '@heroui/react'
import { api } from '../api/client'

interface User {
  accountId: string
  username: string
  email: string
  role: string
  isLocked: boolean
  createdUtc: string
  lastLoginUtc: string
  characterCount: number
}

const ROLES = ['Admin', 'Moderator', 'Player', 'Guest']

export function UsersPage() {
  const [users, setUsers] = useState<User[]>([])
  const [loading, setLoading] = useState(true)

  // Modal State
  const { isOpen, onOpen, onOpenChange } = useDisclosure()
  const [modalMode, setModalMode] = useState<'create' | 'edit'>('create')
  const [selectedUser, setSelectedUser] = useState<User | null>(null)

  // Form State
  const [formData, setFormData] = useState({
    username: '',
    email: '',
    password: '',
    role: 'Player',
    isLocked: false
  })
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    fetchUsers()
  }, [])

  async function fetchUsers() {
    setLoading(true)
    try {
      const data = await api.get<User[]>('/users')
      setUsers(data)
    } catch (error) {
      console.error('Failed to fetch users', error)
    } finally {
      setLoading(false)
    }
  }

  function openCreateModal() {
    setModalMode('create')
    setFormData({ username: '', email: '', password: '', role: 'Player', isLocked: false })
    setError(null)
    onOpen()
  }

  function openEditModal(user: User) {
    setModalMode('edit')
    setSelectedUser(user)
    setFormData({ 
      username: user.username, 
      email: user.email || '', 
      password: '', // Blank unless changing
      role: user.role, 
      isLocked: user.isLocked 
    })
    setError(null)
    onOpen()
  }

  async function handleSubmit(onClose: () => void) {
    setError(null)
    setIsSubmitting(true)
    try {
      if (modalMode === 'create') {
        if (!formData.username || !formData.password) {
          throw new Error('Username and Password are required.')
        }
        await api.post('/users', {
          username: formData.username,
          password: formData.password,
          email: formData.email || undefined,
          role: formData.role
        })
      } else if (modalMode === 'edit' && selectedUser) {
        const payload: Record<string, unknown> = {}
        if (formData.username !== selectedUser.username) payload.username = formData.username
        if (formData.email !== selectedUser.email) payload.email = formData.email || null
        if (formData.password) payload.password = formData.password
        if (formData.role !== selectedUser.role) payload.role = formData.role
        if (formData.isLocked !== selectedUser.isLocked) payload.isLocked = formData.isLocked
        
        await api.put(`/users/${selectedUser.accountId}`, payload)
      }
      
      await fetchUsers()
      onClose()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'An error occurred during save.')
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleToggleLock(user: User) {
    if (!confirm(`Are you sure you want to ${user.isLocked ? 'unlock' : 'lock'} account ${user.username}?`)) return
    try {
      await api.put(`/users/${user.accountId}`, { isLocked: !user.isLocked })
      fetchUsers()
    } catch {
      alert('Failed to update lock status')
    }
  }

  async function handleDelete(user: User) {
    if (!confirm(`Are you sure you want to permanently delete account ${user.username}? This action cannot be undone.`)) return
    try {
      await api.delete(`/users/${user.accountId}`)
      fetchUsers()
    } catch {
      alert('Failed to delete user')
    }
  }

  const roleColorMap: Record<string, "default" | "primary" | "secondary" | "success" | "warning" | "danger"> = {
    admin: "danger",
    moderator: "warning",
    player: "primary",
    guest: "default"
  }

  return (
    <div className="flex flex-col gap-6 animate-fade-in w-full max-w-[1200px] mx-auto">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <div className="flex items-center gap-3 mb-1">
            <div style={{
              width: '2px', height: '20px',
              background: '#6aa5da',
              borderRadius: '1px',
              boxShadow: '0 0 6px rgba(106,165,218,0.5)',
            }} />
            <h1 className="font-cinzel font-semibold tracking-wider"
              style={{ color: '#f9f4ed', fontSize: '18px', letterSpacing: '0.12em' }}>
              Users Management
            </h1>
          </div>
          <p className="font-mono text-xs pl-5"
            style={{ color: 'rgba(185,187,211,0.35)', letterSpacing: '0.1em' }}>
            ACCOUNTS DIRECTORY
          </p>
        </div>

        <Button color="primary" variant="flat" onPress={openCreateModal} className="font-mono text-xs uppercase tracking-wider">
          + Add User
        </Button>
      </div>

      {/* Table */}
      <div className="rounded-xl border border-[rgba(106,165,218,0.15)] bg-[rgba(36,33,48,0.7)] backdrop-blur-md p-2">
        <Table 
          aria-label="Users table"
          classNames={{
            wrapper: "bg-transparent shadow-none",
            th: "bg-[rgba(106,165,218,0.1)] text-[#6aa5da] font-mono text-xs tracking-widest uppercase border-b border-[rgba(106,165,218,0.15)]",
            td: "border-b border-[rgba(106,165,218,0.05)] py-4 font-mono text-sm",
          }}
        >
          <TableHeader>
            <TableColumn>ACCOUNT</TableColumn>
            <TableColumn>ROLE</TableColumn>
            <TableColumn>STATUS</TableColumn>
            <TableColumn>CHARACTERS</TableColumn>
            <TableColumn>LAST LOGIN</TableColumn>
            <TableColumn align="center">ACTIONS</TableColumn>
          </TableHeader>
          <TableBody 
            items={users} 
            isLoading={loading}
            loadingContent={<Spinner color="primary" />}
            emptyContent={"No users found."}
          >
            {(item) => (
              <TableRow key={item.accountId}>
                <TableCell>
                  <div className="flex flex-col gap-1">
                    <span className="text-[#f9f4ed] font-medium">{item.username}</span>
                    <span className="text-xs text-[rgba(185,187,211,0.6)]">{item.email}</span>
                  </div>
                </TableCell>
                <TableCell>
                  <Chip size="sm" variant="flat" color={roleColorMap[item.role.toLowerCase()] || "default"} className="font-mono text-xs uppercase">
                    {item.role}
                  </Chip>
                </TableCell>
                <TableCell>
                  <Chip size="sm" variant="dot" color={item.isLocked ? "danger" : "success"} className="border-none font-mono text-xs uppercase">
                    {item.isLocked ? "Locked" : "Active"}
                  </Chip>
                </TableCell>
                <TableCell>
                  <span className="text-[rgba(185,187,211,0.8)]">{item.characterCount}</span>
                </TableCell>
                <TableCell>
                  <span className="text-xs text-[rgba(185,187,211,0.6)]">
                    {new Date(item.lastLoginUtc).toLocaleDateString()}
                  </span>
                </TableCell>
                <TableCell>
                  <div className="relative flex justify-end items-center gap-2">
                    <Dropdown className="bg-[#242130] border border-[rgba(106,165,218,0.2)]">
                      <DropdownTrigger>
                        <Button isIconOnly variant="light" size="sm" className="text-[rgba(185,187,211,0.6)] hover:text-[#f9f4ed]">
                          <span className="font-bold tracking-widest rotate-90">...</span>
                        </Button>
                      </DropdownTrigger>
                      <DropdownMenu aria-label="User Actions" className="font-mono text-xs uppercase tracking-wider">
                        <DropdownItem key="edit" className="text-[#6aa5da]" onPress={() => openEditModal(item)}>Edit</DropdownItem>
                        <DropdownItem key="lock" className={item.isLocked ? "text-success" : "text-warning"} onPress={() => handleToggleLock(item)}>
                          {item.isLocked ? "Unlock Account" : "Lock Account"}
                        </DropdownItem>
                        <DropdownItem key="delete" className="text-danger" color="danger" onPress={() => handleDelete(item)}>Delete</DropdownItem>
                      </DropdownMenu>
                    </Dropdown>
                  </div>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>

      {/* User Form Modal */}
      <Modal isOpen={isOpen} onOpenChange={onOpenChange} backdrop="blur" classNames={{
        base: "bg-[#242130] border border-[rgba(106,165,218,0.2)]",
        header: "border-b border-[rgba(106,165,218,0.1)]",
        footer: "border-t border-[rgba(106,165,218,0.1)]",
        closeButton: "hover:bg-[rgba(106,165,218,0.1)] active:bg-[rgba(106,165,218,0.2)]"
      }}>
        <ModalContent>
          {(onClose) => (
            <>
              <ModalHeader className="flex flex-col gap-1">
                <h2 className="font-cinzel tracking-wider text-[#6aa5da]">
                  {modalMode === 'create' ? 'Create New User' : 'Edit User'}
                </h2>
              </ModalHeader>
              <ModalBody className="py-6">
                <div className="flex flex-col gap-4">
                  <Input 
                    label="Username" 
                    value={formData.username}
                    onValueChange={(val) => setFormData({...formData, username: val})}
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
                    onValueChange={(val) => setFormData({...formData, email: val})}
                    classNames={{
                      inputWrapper: 'bg-[#1f1c2a] border-[rgba(106,165,218,0.2)] data-[hover=true]:border-[#6aa5da]',
                      label: 'text-[rgba(185,187,211,0.6)] font-mono text-xs tracking-wider',
                      input: 'font-mono text-sm text-[#f9f4ed]',
                    }}
                  />

                  <Select 
                    label="Role"
                    selectedKeys={[formData.role]}
                    onChange={(e) => setFormData({...formData, role: e.target.value})}
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
                    label={modalMode === 'create' ? "Password" : "New Password (Optional)"} 
                    type="password"
                    value={formData.password}
                    onValueChange={(val) => setFormData({...formData, password: val})}
                    isRequired={modalMode === 'create'}
                    classNames={{
                      inputWrapper: 'bg-[#1f1c2a] border-[rgba(106,165,218,0.2)] data-[hover=true]:border-[#6aa5da]',
                      label: 'text-[rgba(185,187,211,0.6)] font-mono text-xs tracking-wider',
                      input: 'font-mono text-sm text-[#f9f4ed]',
                    }}
                  />

                  {error && (
                    <div className="p-3 rounded bg-[rgba(239,68,68,0.1)] border border-[rgba(239,68,68,0.2)]">
                      <p className="font-mono text-xs text-[#ef4444] tracking-wider">⚠ {error}</p>
                    </div>
                  )}
                </div>
              </ModalBody>
              <ModalFooter>
                <Button color="default" variant="light" onPress={onClose} className="font-mono text-xs tracking-wider">
                  Cancel
                </Button>
                <Button color="primary" isLoading={isSubmitting} onPress={() => handleSubmit(onClose)} className="font-mono text-xs uppercase tracking-wider">
                  {modalMode === 'create' ? 'Create' : 'Save Changes'}
                </Button>
              </ModalFooter>
            </>
          )}
        </ModalContent>
      </Modal>

    </div>
  )
}
