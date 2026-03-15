# Pitfalls Research - Araç Kiralama (Phases 6-10)

**Research Date:** 2026-03-14
**Context:** Brownfield - Avoiding common mistakes

## Phase 6: Auth Pitfalls

### JWT Security
| Pitfall | Warning Signs | Prevention |
|---------|---------------|------------|
| Short secrets | JWT secret < 32 chars | Use 256-bit random secret |
| No expiration | Tokens valid forever | 15min access, 7d refresh |
| Algorithm confusion | Accepting 'none' alg | Explicitly validate HS256 |
| Token in localStorage | XSS vulnerability | httpOnly cookies |

### RBAC Mistakes
| Pitfall | Warning Signs | Prevention |
|---------|---------------|------------|
| Client-side only auth | Frontend hides elements | Backend enforces all policies |
| Role escalation | User modifies token | Validate role from DB on sensitive ops |
| Missing audit | No log of access attempts | Log all auth failures |

**Phase to address:** Phase 6

## Phase 7: Notification Pitfalls

### SMS Provider Issues
| Pitfall | Warning Signs | Prevention |
|---------|---------------|------------|
| Single provider | Outage blocks all SMS | Fallback provider (Twilio) |
| No rate limiting | SMS spam | Queue with rate limits |
| Encoding issues | Turkish chars broken | Use UTF-8 encoding |
| Cost overrun | Unexpected bills | Daily limits, monitoring |

### Email Deliverability
| Pitfall | Warning Signs | Prevention |
|---------|---------------|------------|
| Spam folder | Low open rates | SPF, DKIM, DMARC records |
| No tracking | Unknown delivery | Webhook for delivery status |
| Template breaks | Missing variables | Validate all placeholders |

**Phase to address:** Phase 7

## Phase 8: Frontend Pitfalls

### i18n Mistakes
| Pitfall | Warning Signs | Prevention |
|---------|---------------|------------|
| Hardcoded strings | Text not translating | Extract all strings |
| RTL layout breaks | Arabic looks wrong | Test with RTL early |
| Date/number format | Inconsistent display | Use Intl API |
| Missing translations | Keys showing | Fallback language |

### SEO Issues
| Pitfall | Warning Signs | Prevention |
|---------|---------------|------------|
| No meta tags | Poor search ranking | Dynamic meta per page |
| Slow initial load | High bounce rate | Server components |
| Missing sitemap | Pages not indexed | Generate sitemap.xml |

**Phase to address:** Phase 8

## Phase 9: Deployment Pitfalls

### Security Mistakes
| Pitfall | Warning Signs | Prevention |
|---------|---------------|------------|
| Default ports | 5432 exposed | Internal network only |
| No firewall | All ports open | UFW: only 80, 443, 22 |
| Missing backups | Data loss risk | Daily automated backups |
| No SSL | HTTP access | Force HTTPS redirect |

### Performance Issues
| Pitfall | Warning Signs | Prevention |
|---------|---------------|------------|
| No monitoring | Silent failures | Health checks + alerts |
| Log rotation | Disk full | Configure log rotation |
| Memory leaks | OOM crashes | Container limits |

**Phase to address:** Phase 9

## Phase 10: Testing Pitfalls

### Test Coverage Mistakes
| Pitfall | Warning Signs | Prevention |
|---------|---------------|------------|
| Testing only happy path | Edge cases fail | Test error scenarios |
| Mock everything | Integration bugs | Integration tests |
| No E2E | User flow breaks | Playwright booking test |
| Flaky tests | Random failures | Fix or quarantine |

**Phase to address:** Phase 10

## Cross-Phase Pitfalls

### Database
| Pitfall | Warning Signs | Prevention |
|---------|---------------|------------|
| No migrations in prod | Schema mismatch | Automated migration on deploy |
| Missing indexes | Slow queries | Add indexes for new tables |
| Connection exhaustion | Timeouts | Pool sizing, connection string |

### API
| Pitfall | Warning Signs | Prevention |
|---------|---------------|------------|
| Breaking changes | Client errors | Version API (v1, v2) |
| No rate limiting | DDoS vulnerable | Already implemented ✓ |
| Missing validation | Invalid data | FluentValidation on all inputs |

---
*Pitfalls research: 2026-03-14*
