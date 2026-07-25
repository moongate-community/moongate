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
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+$/
const PRINTABLE_ASCII_PATTERN = /^[ -~]{8,30}$/

function isEmailValid(email: string): boolean {
  return EMAIL_PATTERN.test(email)
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
  return useMutation({
    mutationFn: (body: VerifyRegistration) =>
      apiFetch<void>('/api/v1/register/verify', { method: 'POST', body: JSON.stringify(body) }),
  })
}
