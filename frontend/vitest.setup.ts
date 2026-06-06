import '@testing-library/jest-dom/vitest'
import { vi } from 'vitest'
import * as enMessages from './i18n/messages/en.json'

function getNestedValue(obj: any, path: string): string {
  const parts = path.split('.')
  let current = obj
  for (const part of parts) {
    if (current && typeof current === 'object' && part in current) {
      current = current[part]
    } else {
      return path
    }
  }
  return typeof current === 'string' ? current : path
}

vi.mock('next-intl', () => ({
  useTranslations: (namespace: string) => (key: string) => {
    const fullPath = namespace ? `${namespace}.${key}` : key
    return getNestedValue(enMessages, fullPath)
  },
  NextIntlClientProvider: ({ children }: { children: React.ReactNode }) => children,
}))
