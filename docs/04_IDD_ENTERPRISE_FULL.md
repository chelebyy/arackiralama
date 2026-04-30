# Infrastructure & Deployment Document (IDD)

Date: 2026-02-25
Version: 1.0.0

------------------------------------------------------------------------

# 1. Environment Structure

## Production Environment

| Component | Specification |
|-----------|---------------|
| VPS Provider | Hetzner / DigitalOcean / AWS Lightsail |
| RAM | 8GB |
| vCPU | 4 cores |
| Storage | 160GB SSD |
| OS | Ubuntu 22.04 LTS |
| Network | 1 Gbps |

## Staging Environment

| Component | Specification |
|-----------|---------------|
| VPS | Same provider, smaller instance |
| RAM | 4GB |
| vCPU | 2 cores |
| Isolation | Separate containers, separate DB |

------------------------------------------------------------------------

# 2. VPS & Dokploy Setup

## 2.1 Base Configuration

```bash
# System update
sudo apt update && sudo apt upgrade -y

# Install essential packages
sudo apt install -y fail2ban ufw curl

# Firewall configuration
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow 22/tcp    # SSH (key-only)
sudo ufw allow 80/tcp    # HTTP
sudo ufw allow 443/tcp   # HTTPS
sudo ufw allow 3000/tcp  # Dokploy Panel (optional, can be proxied)
sudo ufw enable

# Fail2ban setup
sudo systemctl enable fail2ban
sudo systemctl start fail2ban
```

## 2.2 Dokploy Installation

```bash
# Install Dokploy (Self-hosted PaaS)
curl -sSL https://dokploy.com/install.sh | sh
```

## 2.3 SSH Security

- Key-only authentication (disable password auth)
- Root login disabled
- Custom SSH port (optional)
- Fail2ban for brute force protection

```bash
# /etc/ssh/sshd_config
PermitRootLogin no
PasswordAuthentication no
PubkeyAuthentication yes
MaxAuthTries 3
```

------------------------------------------------------------------------

# 3. Docker Services Architecture (Dokploy/Traefik)

## 3.1 docker-compose.yml

```yaml
version: '3.8'

services:
  # Backend API
  api:
    build:
      context: ./backend
      dockerfile: Dockerfile
    container_name: api
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:5000
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=rentacar;Username=rentacar;Password=${DB_PASSWORD}
      - Redis__ConnectionString=redis:6379
      - JWT__Secret=${JWT_SECRET}
      - Payment__Provider=${PAYMENT_PROVIDER}
      - Payment__ApiKey=${PAYMENT_API_KEY}
      - Payment__SecretKey=${PAYMENT_SECRET}
    depends_on:
      - postgres
      - redis
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
      interval: 30s
      timeout: 10s
      retries: 3
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.api.rule=Host(`alanyarentacar.com`) && PathPrefix(`/api`)"
      - "traefik.http.routers.api.entrypoints=websecure"
      - "traefik.http.routers.api.tls.certresolver=letsencrypt"
      - "traefik.http.services.api.loadbalancer.server.port=5000"

  # Background Worker
  worker:
    build:
      context: ./backend
      dockerfile: Dockerfile.worker
    container_name: worker
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=rentacar;Username=rentacar;Password=${DB_PASSWORD}
      - Redis__ConnectionString=redis:6379
    depends_on:
      - postgres
      - redis
    restart: unless-stopped

  # Frontend (Next.js - serves both public and admin)
  web:
    build:
      context: ./frontend
      dockerfile: Dockerfile
    container_name: web
    environment:
      - NODE_ENV=production
      - NEXT_PUBLIC_API_URL=/api/v1
      - NEXT_PUBLIC_DEFAULT_LOCALE=tr
      - ADMIN_DOMAIN=admin.alanyarentacar.com
    depends_on:
      - api
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:3000/api/health"]
      interval: 30s
      timeout: 10s
      retries: 3
    labels:
      - "traefik.enable=true"
      # Public Domain
      - "traefik.http.routers.web.rule=Host(`alanyarentacar.com`, `www.alanyarentacar.com`)"
      - "traefik.http.routers.web.entrypoints=websecure"
      - "traefik.http.routers.web.tls.certresolver=letsencrypt"
      # Admin Domain
      - "traefik.http.routers.admin.rule=Host(`admin.alanyarentacar.com`)"
      - "traefik.http.routers.admin.entrypoints=websecure"
      - "traefik.http.routers.admin.tls.certresolver=letsencrypt"
      - "traefik.http.services.web.loadbalancer.server.port=3000"

  # PostgreSQL Database
  postgres:
    image: postgres:18-alpine
    container_name: postgres
    volumes:
      - postgres-data:/var/lib/postgresql/data
      - ./backups:/backups
    environment:
      - POSTGRES_DB=rentacar
      - POSTGRES_USER=rentacar
      - POSTGRES_PASSWORD=${DB_PASSWORD}
    restart: unless-stopped
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U rentacar"]
      interval: 10s
      timeout: 5s
      retries: 5

  # Redis Cache
  redis:
    image: redis:7.4-alpine
    container_name: redis
    volumes:
      - redis-data:/data
    command: redis-server --appendonly yes --maxmemory 256mb --maxmemory-policy allkeys-lru
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  postgres-data:
  redis-data:

networks:
  default:
    driver: bridge
```

## 3.2 Service Resource Limits

| Service | CPU Limit | Memory Limit | Notes |
|---------|-----------|--------------|-------|
| api | 2 cores | 2GB | ASP.NET Core |
| worker | 1 core | 1GB | Background jobs |
| web | 1 core | 1GB | Next.js SSR |
| postgres | 1.5 cores | 2GB | PostgreSQL 18 |
| redis | 0.5 cores | 512MB | Cache + sessions |

> **Not:** Traefik reverse proxy Dokploy tarafından yönetilir, ayrı bir container olarak görünmez.

------------------------------------------------------------------------

# 4. SSL/TLS & Routing (Traefik)

## 4.1 Automatic SSL

Dokploy uses Traefik as its core reverse proxy. SSL certificates are automatically managed via Let's Encrypt.

- **Resolver:** `letsencrypt` (configured in Dokploy settings)
- **Challenge:** HTTP-01 or DNS-01
- **Auto-renewal:** Handled automatically by Traefik

## 4.2 Security Headers

Security headers are configured via Traefik Middlewares in the Dokploy UI:

- `X-Frame-Options: SAMEORIGIN`
- `X-Content-Type-Options: nosniff`
- `X-XSS-Protection: 1; mode=block`
- `Referrer-Policy: strict-origin-when-cross-origin`

------------------------------------------------------------------------

# 5. Database Configuration

## 6.1 PostgreSQL Settings

```conf
# Custom PostgreSQL config
max_connections = 200
shared_buffers = 512MB
effective_cache_size = 1536MB
maintenance_work_mem = 128MB
work_mem = 2621kB
checkpoint_completion_target = 0.9
wal_buffers = 16MB
default_statistics_target = 100
random_page_cost = 1.1
effective_io_concurrency = 200
```

## 6.2 Backup Strategy

### Automated Daily Backup

```bash
#!/bin/bash
# /opt/rentacar/scripts/backup.sh

BACKUP_DIR="/backups"
DB_NAME="rentacar"
DB_USER="rentacar"
DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/rentacar_${DATE}.sql"

# Create backup
docker-compose exec -T postgres pg_dump -U ${DB_USER} ${DB_NAME} > ${BACKUP_FILE}

# Compress
 gzip ${BACKUP_FILE}

# Upload to S3 (optional)
# aws s3 cp ${BACKUP_FILE}.gz s3://rentacar-backups/

# Cleanup old backups (keep 30 days)
find ${BACKUP_DIR} -name "rentacar_*.sql.gz" -mtime +30 -delete

echo "Backup completed: ${BACKUP_FILE}.gz"
```

### Cron Job

```bash
# /etc/cron.d/rentacar-backup
0 2 * * * root /opt/rentacar/scripts/backup.sh >> /var/log/rentacar-backup.log 2>&1
```

## 6.3 Restore Procedure

```bash
#!/bin/bash
# /opt/rentacar/scripts/restore.sh

BACKUP_FILE=$1
DB_NAME="rentacar"
DB_USER="rentacar"

# Stop application
docker-compose stop api worker

# Restore database
docker-compose exec -T postgres psql -U ${DB_USER} -d postgres -c "DROP DATABASE IF EXISTS ${DB_NAME};"
docker-compose exec -T postgres psql -U ${DB_USER} -d postgres -c "CREATE DATABASE ${DB_NAME};"
docker-compose exec -T postgres psql -U ${DB_USER} -d ${DB_NAME} < ${BACKUP_FILE}

# Start application
docker-compose start api worker

echo "Restore completed from: ${BACKUP_FILE}"
```

------------------------------------------------------------------------

# 7. CI/CD Pipeline (Dokploy Git-based)

## 7.1 GitHub Actions Workflow

Dokploy supports direct Git integration. When a push occurs to the `main` branch, Dokploy automatically pulls the changes and redeploys the services.

```yaml
# .github/workflows/ci.yml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  backend-unit:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0'
      - name: Restore & Build
        run: |
          cd backend
          dotnet restore
          dotnet build --no-restore
      - name: Unit Tests
        run: |
          cd backend
          dotnet test tests/RentACar.Tests --no-build

  backend-integration:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:17-alpine
        env:
          POSTGRES_USER: postgres
          POSTGRES_PASSWORD: postgres
          POSTGRES_DB: rentacar
        ports:
          - 5433:5432
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
      redis:
        image: redis:7-alpine
        ports:
          - 6379:6379
        options: >-
          --health-cmd "redis-cli ping"
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0'
      - name: Restore & Build
        run: |
          cd backend
          dotnet restore
          dotnet build --no-restore
      - name: Integration Tests
        run: |
          cd backend
          dotnet test tests/RentACar.ApiIntegrationTests --no-build

  frontend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup Node
        uses: actions/setup-node@v4
        with:
          node-version: '22'
      - name: Install & Test
        run: |
          cd frontend
          npm ci
          npm test
```

## 7.2 Dokploy Deployment Setup

1. **Connect Repository:** Link GitHub repository in Dokploy Panel.
2. **Configure Service:** Create a "Compose" service in Dokploy.
3. **Environment Variables:** Add secrets (DB_PASSWORD, JWT_SECRET, etc.) in Dokploy UI.
4. **Auto-Deploy:** Enable "Automatic Deployment" on push to `main`.
5. **Health Checks:** Ensure `docker-compose.yml` has proper health checks for Traefik routing.

------------------------------------------------------------------------

# 8. Monitoring & Alerting

## 8.1 Basic Monitoring (MVP)

### Uptime Monitoring
- **UptimeRobot** or **Pingdom**
- Check every 5 minutes
- Alert on downtime > 2 minutes
- Monitor: domain.com, admin.domain.com

### Log Aggregation
```bash
# Docker logs
docker-compose logs -f --tail=100 api
docker-compose logs -f --tail=100 worker

# Log rotation
docker-compose exec api sh -c "ls -la /app/logs"
```

### Disk Space Alert
```bash
#!/bin/bash
# /opt/rentacar/scripts/disk-check.sh

USAGE=$(df / | tail -1 | awk '{print $5}' | sed 's/%//')
if [ $USAGE -gt 80 ]; then
    echo "Disk usage is ${USAGE}%" | mail -s "RentACar Disk Alert" admin@alanyarentacar.com
fi
```

## 8.2 Future Enhancements

### Prometheus + Grafana Stack
- API response times
- Error rates by endpoint
- Payment success/failure rates
- Background job queue depth
- Database query performance

### Sentry Integration
- Exception tracking
- Performance monitoring
- Release tracking

------------------------------------------------------------------------

# 9. Security Configuration

## 9.1 Application Security

### Environment Variables

Create `.env` file (never commit to git):

```bash
# Database
DB_PASSWORD=secure_random_password_here

# JWT
JWT_SECRET=very_long_random_secret_key_min_32_chars

# Payment Provider
PAYMENT_PROVIDER=MockProvider
PAYMENT_API_KEY=test_key
PAYMENT_SECRET=test_secret

# SMTP (for notifications)
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_USER=notifications@alanyarentacar.com
SMTP_PASSWORD=app_specific_password
```

### Secrets Management

```bash
# Generate secure secrets
openssl rand -base64 32  # JWT secret
openssl rand -hex 16     # DB password
```

## 9.2 Network Security

### Firewall Rules

```bash
# UFW Status
sudo ufw status verbose

# Should show:
# 22/tcp   ALLOW IN    Anywhere (SSH key-only)
# 80/tcp   ALLOW IN    Anywhere
# 443/tcp  ALLOW IN    Anywhere
```

### Docker Network Isolation

```yaml
# Internal network for DB only
networks:
  frontend:
    driver: bridge
  backend:
    driver: bridge
    internal: true  # No external access
```

------------------------------------------------------------------------

# 10. Disaster Recovery

## 10.1 RPO / RTO

| Metric | Target | Implementation |
|--------|--------|----------------|
| RPO (Recovery Point) | 24 hours | Daily automated backups |
| RTO (Recovery Time) | 4 hours | Documented restore procedure |

## 10.2 Recovery Procedures

### Scenario 1: Database Corruption

1. Stop application: `docker-compose stop api worker`
2. Restore from backup: `./scripts/restore.sh /backups/rentacar_20260301_020000.sql.gz`
3. Verify data integrity
4. Start application: `docker-compose start api worker`

### Scenario 2: VPS Failure

1. Provision new VPS with same specs
2. Install Docker and Docker Compose
3. Clone repository
4. Restore environment variables
5. Restore database from S3/backup storage
6. Update DNS to new IP
7. Verify SSL certificates

### Scenario 3: Complete Data Loss

1. Provision new infrastructure
2. Initialize fresh database
3. Re-configure payment provider
4. Manually recreate vehicle inventory
5. Accept loss of historical reservations

------------------------------------------------------------------------

# 11. Scaling Roadmap

## 11.1 Phase 1: Vertical Scaling (Immediate)

- Upgrade VPS: 8GB → 16GB RAM
- Add CPU cores: 4 → 6 vCPU
- Optimize PostgreSQL settings
- Enable PgBouncer connection pooling

## 11.2 Phase 2: Horizontal Scaling (Future)

- Load balancer (HAProxy / Nginx Plus)
- Multiple API instances
- PostgreSQL read replica
- Redis Cluster
- CDN for static assets (CloudFlare)

## 11.3 Phase 3: Microservices (If Needed)

- Payment service extraction
- Notification service (SMS/Email)
- Reservation service
- Admin service

------------------------------------------------------------------------

END OF DOCUMENT
