import { useCallback, useEffect, useRef, useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { apiFetch } from './api'
import type { components } from './api-types'

export type RegisterAccount = components['schemas']['RegisterRequest']
export type ResendVerification = components['schemas']['ResendVerificationRequest']
export type VerifyRegistration = components['schemas']['VerifyEmailRequest']

export type RegistrationField = 'username' | 'email' | 'password' | 'confirmation'
export type RegistrationValidationCode = 'invalid' | 'mismatch'
export type RegistrationValidationErrors = Partial<Record<RegistrationField, RegistrationValidationCode>>

type RegistrationInput = RegisterAccount & { confirmation: string }

const USERNAME_PATTERN = /^[A-Za-z0-9._-]{3,30}$/
const UNQUOTED_EMAIL_LOCAL_PART_PATTERN = /^[^\s@.]+(?:\.[^\s@.]+)*$/
const QUOTED_EMAIL_LOCAL_PART_PATTERN = /^"(?:[^"\\\r\n]|\\.)*"$/
const EMAIL_DOMAIN_PATTERN = /^[^\s@]+$/
const PRINTABLE_ASCII_PATTERN = /^[ -~]{8,30}$/

function isEmailValid(email: string): boolean {
  const separator = email.lastIndexOf('@')
  if (separator <= 0 || separator === email.length - 1) {
    return false
  }

  const localPart = email.slice(0, separator)
  const domain = email.slice(separator + 1)
  return (
    EMAIL_DOMAIN_PATTERN.test(domain) &&
    (UNQUOTED_EMAIL_LOCAL_PART_PATTERN.test(localPart) || QUOTED_EMAIL_LOCAL_PART_PATTERN.test(localPart))
  )
}

export function validateRegistration(input: RegistrationInput): RegistrationValidationErrors {
  const errors: RegistrationValidationErrors = {}

  if (!USERNAME_PATTERN.test(input.username.trim())) {
    errors.username = 'invalid'
  }

  if (!isEmailValid(input.email.trim())) {
    errors.email = 'invalid'
  }

  if (!PRINTABLE_ASCII_PATTERN.test(input.password)) {
    errors.password = 'invalid'
  }

  if (input.confirmation !== input.password) {
    errors.confirmation = 'mismatch'
  }

  return errors
}

export function validateResend(input: ResendVerification): RegistrationValidationErrors {
  const errors: RegistrationValidationErrors = {}

  if (!USERNAME_PATTERN.test(input.username.trim())) {
    errors.username = 'invalid'
  }

  if (!isEmailValid(input.email.trim())) {
    errors.email = 'invalid'
  }

  return errors
}

export function useRegisterAccount() {
  return useMutation({
    mutationFn: (body: RegisterAccount) =>
      apiFetch<void>('/api/v1/register', { method: 'POST', body: JSON.stringify(body) }),
  })
}

export function useResendVerification() {
  return useMutation({
    mutationFn: (body: ResendVerification) =>
      apiFetch<void>('/api/v1/register/resend', { method: 'POST', body: JSON.stringify(body) }),
  })
}

export function useVerifyRegistration() {
  const [isPending, setIsPending] = useState(false)
  const requestId = useRef(0)
  const mounted = useRef(true)

  useEffect(() => {
    mounted.current = true

    return () => {
      mounted.current = false
      requestId.current += 1
    }
  }, [])

  const mutateAsync = useCallback(async (body: VerifyRegistration) => {
    const currentRequest = ++requestId.current
    setIsPending(true)

    try {
      await apiFetch<void>('/api/v1/register/verify', {
        method: 'POST',
        body: JSON.stringify(body),
      })
    } finally {
      if (mounted.current && requestId.current === currentRequest) {
        setIsPending(false)
      }
    }
  }, [])

  const reset = useCallback(() => {
    requestId.current += 1
    setIsPending(false)
  }, [])

  return { mutateAsync, reset, isPending }
}
