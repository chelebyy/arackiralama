import '@testing-library/jest-dom/vitest'
import { vi } from 'vitest'
import * as enMessages from './i18n/messages/en.json'

function getNestedValue(obj: any, path: string): unknown {
  const parts = path.split('.')
  let current = obj
  for (const part of parts) {
    if (current && typeof current === 'object' && part in current) {
      current = current[part]
    } else {
      return path
    }
  }
  return current ?? path
}

function interpolate(message: string, values?: Record<string, unknown>): string {
  if (!values) return message

  return Object.entries(values).reduce(
    (text, [key, value]) => text.replaceAll(`{${key}}`, String(value)),
    message
  )
}

vi.mock('next-intl', () => ({
  useMessages: () => enMessages,
  useTranslations: (namespace?: string) => (key: string, values?: Record<string, unknown>) => {
    const fullPath = namespace ? `${namespace}.${key}` : key
    const message = getNestedValue(enMessages, fullPath)
    if (typeof message !== 'string') return key
    return fullPath === 'legal.sectionLabel' ? interpolate(message, values) : message
  },
  NextIntlClientProvider: ({ children }: { children: React.ReactNode }) => children,
}))
