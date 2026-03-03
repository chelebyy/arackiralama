import { describe, it, expect } from 'vitest'
import { cn, generateAvatarFallback, getInitials } from './utils'

describe('cn', () => {
  it('should merge class names correctly', () => {
    const result = cn('foo', 'bar')
    expect(result).toBe('foo bar')
  })

  it('should handle conditional classes', () => {
    const result = cn('base', true && 'included', false && 'excluded')
    expect(result).toBe('base included')
  })

  it('should merge tailwind classes correctly', () => {
    const result = cn('px-2 py-1', 'px-4')
    expect(result).toBe('py-1 px-4')
  })

  it('should handle undefined and null values', () => {
    const result = cn('base', undefined, null, 'end')
    expect(result).toBe('base end')
  })
})

describe('generateAvatarFallback', () => {
  it('should generate initials from full name', () => {
    expect(generateAvatarFallback('John Doe')).toBe('JD')
  })

  it('should handle single name', () => {
    expect(generateAvatarFallback('John')).toBe('J')
  })

  it('should handle multiple names', () => {
    expect(generateAvatarFallback('John Michael Doe')).toBe('JMD')
  })

  it('should capitalize letters', () => {
    expect(generateAvatarFallback('john doe')).toBe('JD')
  })

  it('should handle extra spaces', () => {
    expect(generateAvatarFallback('  John   Doe  ')).toBe('JD')
  })

  it('should return empty string for empty input', () => {
    expect(generateAvatarFallback('')).toBe('')
  })
})

describe('getInitials', () => {
  it('should get initials from first and last name', () => {
    expect(getInitials('John Doe')).toBe('JD')
  })

  it('should capitalize initials', () => {
    expect(getInitials('john doe')).toBe('JD')
  })

  it('should take first and second name initials', () => {
    // Note: getInitials takes nameParts[0] and nameParts[1], not first and last
    expect(getInitials('John Michael Doe')).toBe('JM')
  })
})
