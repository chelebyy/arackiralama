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

# 2. VPS Setup

## 2.1 Base Configuration

```bash
# System update
sudo apt update && sudo apt upgrade -y

# Install essential packages
sudo apt install -y fail2ban ufw docker.io docker-compose

# Firewall configuration
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow 22/tcp    # SSH (key-only)
sudo ufw allow 80/tcp    # HTTP
sudo ufw allow 443/tcp   # HTTPS
sudo ufw enable

# Fail2ban setup
sudo systemctl enable fail2ban
sudo systemctl start fail2ban
```

## 2.2 SSH Security

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

# 3. Docker Services Architecture

## 3.1 docker-compose.yml

```yaml
version: '3.8'

services:
  # Reverse Proxy
  nginx:
    image: nginx:1.24-alpine
    container_name: nginx
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx/nginx.conf:/etc/nginx/nginx.conf:ro
      - ./nginx/ssl:/etc/nginx/ssl:ro
      - certbot-data:/etc/letsencrypt
      - certbot-www:/var/www/certbot
    depends_on:
      - api
      - web
    restart: unless-stopped

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

  # PostgreSQL Database
  postgres:
    image: postgres:15-alpine
    container_name: postgres
    volumes:
      - postgres-data:/var/lib/postgresql/data
      - ./backups:/backups
      - ./init-scripts:/docker-entrypoint-initdb.d
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
    image: redis:7-alpine
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

  # Certbot for SSL
  certbot:
    image: certbot/certbot
    container_name: certbot
    volumes:
      - certbot-data:/etc/letsencrypt
      - certbot-www:/var/www/certbot
    entrypoint: "/bin/sh -c 'trap exit TERM; while :; do certbot renew; sleep 12h & wait $${!}; done;'"

volumes:
  postgres-data:
  redis-data:
  certbot-data:
  certbot-www:

networks:
  default:
    driver: bridge
```

## 3.2 Service Resource Limits

| Service | CPU Limit | Memory Limit |
|---------|-----------|--------------|
| nginx | 0.5 cores | 256MB |
| api | 2 cores | 2GB |
| worker | 1 core | 1GB |
| web | 1 core | 1GB |
| postgres | 1 core | 2GB |
| redis | 0.5 cores | 256MB |

------------------------------------------------------------------------

# 4. Nginx Configuration

## 4.1 nginx.conf

```nginx
user nginx;
worker_processes auto;
error_log /var/log/nginx/error.log warn;
pid /var/run/nginx.pid;

events {
    worker_connections 1024;
}

http {
    include /etc/nginx/mime.types;
    default_type application/octet-stream;

    log_format main '$remote_addr - $remote_user [$time_local] "$request" '
                    '$status $body_bytes_sent "$http_referer" '
                    '"$http_user_agent" "$http_x_forwarded_for"';

    access_log /var/log/nginx/access.log main;

    # Gzip compression
    gzip on;
    gzip_vary on;
    gzip_proxied any;
    gzip_comp_level 6;
    gzip_types text/plain text/css text/xml application/json application/javascript application/rss+xml application/atom+xml image/svg+xml;

    # Rate limiting zones
    limit_req_zone $binary_remote_addr zone=login:10m rate=5r/m;
    limit_req_zone $binary_remote_addr zone=payment:10m rate=10r/m;
    limit_req_zone $binary_remote_addr zone=api:10m rate=100r/m;

    # SSL configuration
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256:ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384;
    ssl_prefer_server_ciphers off;
    ssl_session_cache shared:SSL:10m;
    ssl_session_timeout 10m;

    # Include server blocks
    include /etc/nginx/conf.d/*.conf;
}
```

## 4.2 Server Block - Public Site

```nginx
# /etc/nginx/conf.d/public.conf
server {
    listen 80;
    server_name alanyarentacar.com www.alanyarentacar.com;
    
    # Redirect HTTP to HTTPS
    location / {
        return 301 https://$server_name$request_uri;
    }
    
    # Certbot challenge
    location /.well-known/acme-challenge/ {
        root /var/www/certbot;
    }
}

server {
    listen 443 ssl http2;
    server_name alanyarentacar.com www.alanyarentacar.com;

    ssl_certificate /etc/letsencrypt/live/alanyarentacar.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/alanyarentacar.com/privkey.pem;

    # Security headers
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-XSS-Protection "1; mode=block" always;
    add_header Referrer-Policy "strict-origin-when-cross-origin" always;

    # API requests
    location /api/ {
        limit_req zone=api burst=20 nodelay;
        proxy_pass http://api:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }

    # Static files
    location /_next/static/ {
        proxy_pass http://web:3000;
        proxy_cache_valid 200 365d;
        add_header Cache-Control "public, immutable";
    }

    # All other requests to Next.js
    location / {
        proxy_pass http://web:3000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }
}
```

## 4.3 Server Block - Admin Panel

```nginx
# /etc/nginx/conf.d/admin.conf
server {
    listen 80;
    server_name admin.alanyarentacar.com;
    
    location / {
        return 301 https://$server_name$request_uri;
    }
    
    location /.well-known/acme-challenge/ {
        root /var/www/certbot;
    }
}

server {
    listen 443 ssl http2;
    server_name admin.alanyarentacar.com;

    ssl_certificate /etc/letsencrypt/live/alanyarentacar.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/alanyarentacar.com/privkey.pem;

    add_header X-Frame-Options "DENY" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-Robots-Tag "noindex, nofollow" always;

    # Admin API
    location /api/ {
        limit_req zone=api burst=20 nodelay;
        proxy_pass http://api:5000;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # Admin frontend
    location / {
        proxy_pass http://web:3000;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

------------------------------------------------------------------------

# 5. SSL/TLS Configuration

## 5.1 Initial Certificate Setup

```bash
# First time certificate generation
docker-compose run --rm certbot certonly \
  --webroot \
  --webroot-path=/var/www/certbot \
  --email admin@alanyarentacar.com \
  --agree-tos \
  --no-eff-email \
  -d alanyarentacar.com \
  -d www.alanyarentacar.com \
  -d admin.alanyarentacar.com
```

## 5.2 Auto-Renewal

Certbot container handles automatic renewal every 12 hours.
Manual test:
```bash
docker-compose exec certbot certbot renew --dry-run
```

------------------------------------------------------------------------

# 6. Database Configuration

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

# 7. CI/CD Pipeline

## 7.1 GitHub Actions Workflow

```yaml
# .github/workflows/deploy.yml
name: Build and Deploy

on:
  push:
    branches: [main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      # Build Backend
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0'
      
      - name: Build Backend
        run: |
          cd backend
          dotnet restore
          dotnet build --configuration Release
          dotnet test

      # Build Frontend
      - name: Setup Node
        uses: actions/setup-node@v4
        with:
          node-version: '20'
      
      - name: Build Frontend
        run: |
          cd frontend
          npm ci
          npm run build

  deploy:
    needs: build
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    
    steps:
      - name: Deploy to VPS
        uses: appleboy/ssh-action@v1.0.0
        with:
          host: ${{ secrets.VPS_HOST }}
          username: ${{ secrets.VPS_USER }}
          key: ${{ secrets.SSH_PRIVATE_KEY }}
          script: |
            cd /opt/rentacar
            git pull origin main
            docker-compose build
            docker-compose up -d
            docker-compose exec api dotnet ef database update
            docker system prune -f
```

## 7.2 Deployment Script

```bash
#!/bin/bash
# /opt/rentacar/scripts/deploy.sh

set -e

echo "Starting deployment..."

# Pull latest code
git pull origin main

# Build images
docker-compose build

# Run migrations
docker-compose run --rm api dotnet ef database update

# Rolling update
docker-compose up -d

# Health check
sleep 10
if curl -f http://localhost/api/v1/health; then
    echo "Deployment successful!"
else
    echo "Health check failed!"
    exit 1
fi

# Cleanup
docker system prune -f
```

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
