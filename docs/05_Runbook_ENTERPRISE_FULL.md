# Production Runbook

Date: 2026-02-25
Version: 2.0.0 - Enterprise Edition

------------------------------------------------------------------------

# Table of Contents

1. [API Down](#1-api-down)
2. [Payment Webhook Issue](#2-payment-webhook-issue)
3. [Reservation Conflict Spike](#3-reservation-conflict-spike)
4. [Database Restore](#4-database-restore)
5. [Redis Degraded Mode](#5-redis-degraded-mode)
6. [SMS Provider Down](#6-sms-provider-down)
7. [High CPU/Memory](#7-high-cpumemory)
8. [Security Incident](#8-security-incident)
9. [Quick Reference](#9-quick-reference)

------------------------------------------------------------------------

# 1. API Down

## 1.1 Symptoms

- HTTP 502/503 errors from nginx
- Health check endpoint `/api/v1/health` returning non-200
- Response time > 30 seconds or timeout
- Container status `unhealthy` or `restarting`
- Error in nginx logs: `connect() failed (111: Connection refused)`

## 1.2 Initial Assessment

```bash
# Step 1: Check overall container status
cd /opt/rentacar
docker-compose ps

# Expected output:
# NAME                IMAGE               STATUS
# nginx               nginx:1.24-alpine   Up 5 hours
# api                 rentacar_api        Up (healthy)
# postgres            postgres:15-alpine  Up 5 hours
# redis               redis:7-alpine      Up 5 hours
```

## 1.3 Diagnostic Steps

### Step 1: Check API container health

```bash
# Check if API container is running
docker ps --filter name=api --format "table {{.Names}}\t{{.Status}}\t{{.Health}}"

# Check recent container events
docker events --filter container=api --since 10m

# Inspect container state
docker inspect api --format='{{.State.Status}} - {{.State.Health.Status}}'
```

### Step 2: Examine application logs

```bash
# Get last 100 log lines
docker-compose logs --tail=100 api

# Follow logs in real-time
docker-compose logs -f --tail=50 api

# Check for specific error patterns
docker-compose logs api | grep -E "(ERROR|FATAL|Exception|Stack trace)" | tail -20

# Check worker logs (background jobs may indicate issues)
docker-compose logs --tail=50 worker
```

### Step 3: Check resource utilization

```bash
# Container stats
docker stats api --no-stream

# Check if container is OOM killed
docker inspect api --format='{{.State.OOMKilled}}'

# Check exit code if container stopped
docker inspect api --format='{{.State.ExitCode}}'
```

### Step 4: Verify database connectivity

```bash
# Test database connection from API container
docker-compose exec api curl -f http://localhost:5000/health/db

# Check PostgreSQL directly
docker-compose exec postgres pg_isready -U rentacar

# Check connection count
docker-compose exec postgres psql -U rentacar -c "SELECT count(*) FROM pg_stat_activity;"
```

### Step 5: Verify Redis connectivity

```bash
# Test Redis connection from API container
docker-compose exec api curl -f http://localhost:5000/health/cache

# Check Redis directly
docker-compose exec redis redis-cli ping

# Check Redis memory usage
docker-compose exec redis redis-cli info memory | grep used_memory_human
```

## 1.4 Resolution Procedures

### Procedure A: Container Restart

```bash
# Step 1: Graceful restart
cd /opt/rentacar
docker-compose restart api

# Step 2: Wait for health check
sleep 15
docker-compose ps api

# Step 3: Verify health endpoint
curl -f http://localhost:5000/health

# Step 4: Check application is responding
curl -f http://localhost/api/v1/vehicles/available?pickup_datetime=2026-04-01T10:00:00Z&return_datetime=2026-04-02T10:00:00Z
```

### Procedure B: Full Stack Restart (if container restart fails)

```bash
# Step 1: Stop services gracefully
cd /opt/rentacar
docker-compose stop api worker

# Step 2: Check for stuck containers
docker ps -a | grep api

# Step 3: Remove stuck container if necessary
# WARNING: Only if container is stuck, not for healthy containers
docker-compose rm -f api worker

# Step 4: Rebuild if needed
docker-compose build api worker

# Step 5: Start services
docker-compose up -d api worker

# Step 6: Verify startup
sleep 20
curl -f http://localhost:5000/health
docker-compose ps
```

### Procedure C: Database Connection Pool Exhaustion

```bash
# Step 1: Check active connections
docker-compose exec postgres psql -U rentacar -c "
SELECT state, count(*) 
FROM pg_stat_activity 
WHERE datname = 'rentacar' 
GROUP BY state;"

# Step 2: Identify idle connections
docker-compose exec postgres psql -U rentacar -c "
SELECT pid, usename, application_name, state, query_start 
FROM pg_stat_activity 
WHERE state = 'idle' 
AND query_start < NOW() - INTERVAL '10 minutes';"

# Step 3: Terminate idle connections (use with caution)
docker-compose exec postgres psql -U rentacar -c "
SELECT pg_terminate_backend(pid) 
FROM pg_stat_activity 
WHERE state = 'idle' 
AND query_start < NOW() - INTERVAL '10 minutes';"

# Step 4: Restart API to reset connection pool
docker-compose restart api
```

## 1.5 Verification

```bash
# Checklist for verification:
# 1. Container status healthy
docker-compose ps api | grep -q "healthy" && echo "✓ Container healthy"

# 2. Health endpoint responds
curl -f -s http://localhost:5000/health > /dev/null && echo "✓ Health endpoint OK"

# 3. API responds to requests
curl -f -s http://localhost/api/v1/vehicles/groups > /dev/null && echo "✓ API responding"

# 4. No error spikes in logs
docker-compose logs --tail=50 api | grep -c "ERROR" | awk '{if($1<5) print "✓ Error count acceptable: " $1; else print "✗ Too many errors: " $1}'

# 5. Response time acceptable
time curl -s http://localhost/api/v1/vehicles/groups > /dev/null
```

------------------------------------------------------------------------

# 2. Payment Webhook Issue

## 2.1 Symptoms

- Payments marked as "Pending" but not updating to "Paid"
- Webhook endpoint returning 400/500 errors
- Missing webhook events in application logs
- Customer charged but reservation not confirmed
- Webhook signature verification failures

## 2.2 Initial Assessment

```bash
# Check webhook endpoint availability
curl -f -X POST http://localhost/api/v1/payments/webhook/iyzico -d '{}' -H "Content-Type: application/json"
# Expected: 400 Bad Request (signature check failed) - this means endpoint is reachable

# Check recent webhook logs
docker-compose logs api | grep -i webhook | tail -30
docker-compose logs worker | grep -i payment | tail -30
```

## 2.3 Diagnostic Steps

### Step 1: Check pending payment intents

```bash
# Access database and check pending payments
docker-compose exec postgres psql -U rentacar -c "
SELECT 
    id, 
    reservation_id, 
    status, 
    provider, 
    amount, 
    created_at,
    updated_at
FROM payment_intents 
WHERE status IN ('Pending', 'Pending3DS')
AND created_at > NOW() - INTERVAL '1 hour'
ORDER BY created_at DESC;"
```

### Step 2: Check webhook delivery logs

```bash
# Check nginx access logs for webhook calls
sudo tail -100 /var/log/nginx/access.log | grep webhook

# Check for 4xx/5xx responses to webhook endpoints
sudo tail -1000 /var/log/nginx/access.log | grep webhook | awk '{print $9}' | sort | uniq -c

# Check application logs for webhook processing
docker-compose logs api | grep -E "(webhook|Webhook)" | tail -50
```

### Step 3: Verify webhook signature configuration

```bash
# Check environment variables
docker-compose exec api env | grep -i payment

# Expected:
# Payment__Provider=iyzico
# Payment__ApiKey=...
# Payment__SecretKey=...
```

## 2.4 Manual Payment Verification (Iyzico)

### Step 1: Get payment intent details

```bash
# Query database for payment intent
docker-compose exec postgres psql -U rentacar -c "
SELECT 
    pi.id,
    pi.provider_transaction_id,
    pi.reservation_id,
    r.public_code,
    pi.amount,
    pi.status
FROM payment_intents pi
JOIN reservations r ON pi.reservation_id = r.id
WHERE r.public_code = 'ABC123';"
```

### Step 2: Manual inquiry via Iyzico API

```bash
# Execute inquiry from within API container
docker-compose exec api curl -X POST \
  https://api.iyzico.com/payment/inquiry \
  -H "Content-Type: application/json" \
  -d '{
    "locale": "tr",
    "conversationId": "inquiry_$(date +%s)",
    "paymentId": "PAYMENT_ID_FROM_DB",
    "paymentConversationId": "RESERVATION_PUBLIC_CODE"
  }'
```

### Step 3: Manual retry of payment processing

```bash
# Trigger manual retry via admin endpoint (requires admin token)
ADMIN_TOKEN=$(curl -s -X POST http://localhost/api/admin/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@alanyarentacar.com","password":"ADMIN_PASSWORD"}' | jq -r '.data.token')

# Retry payment processing
curl -X POST http://localhost/api/admin/v1/payments/retry \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"payment_intent_id": "PI_ID_HERE"}'
```

## 2.5 Webhook Retry Procedures

### Procedure A: Retry from Database Queue

```bash
# Check queued webhooks
docker-compose exec postgres psql -U rentacar -c "
SELECT 
    id,
    payment_intent_id,
    event_type,
    retry_count,
    next_retry_at,
    status
FROM webhook_events
WHERE status IN ('Pending', 'Failed')
AND retry_count < 5
ORDER BY next_retry_at;"

# Trigger immediate retry
docker-compose exec postgres psql -U rentacar -c "
UPDATE webhook_events 
SET next_retry_at = NOW(), retry_count = retry_count + 1
WHERE id = 'WEBHOOK_EVENT_ID';"

# Restart worker to process immediately
docker-compose restart worker
```

### Procedure B: Manual Webhook Replay

```bash
# Reconstruct and replay webhook (for missed events)
# Get payment details
docker-compose exec postgres psql -U rentacar -c "
SELECT 
    pi.id,
    pi.provider_transaction_id,
    pi.provider_response
FROM payment_intents pi
WHERE pi.reservation_id = 'RESERVATION_ID';"

# Replay via admin API
ADMIN_TOKEN="YOUR_ADMIN_TOKEN"
curl -X POST http://localhost/api/admin/v1/payments/simulate-webhook \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "payment_intent_id": "PI_ID",
    "event_type": "payment.succeeded",
    "payload": {"transaction_id": "TXN_ID"}
  }'
```

## 2.6 Verification

```bash
# Check payment status updated
docker-compose exec postgres psql -U rentacar -c "
SELECT 
    r.public_code,
    r.status as reservation_status,
    pi.status as payment_status,
    pi.updated_at
FROM reservations r
JOIN payment_intents pi ON r.id = pi.reservation_id
WHERE r.public_code = 'ABC123';"

# Expected: reservation_status = 'Paid', payment_status = 'Succeeded'

# Check webhook processed
docker-compose logs worker --tail=100 | grep -E "(webhook|payment)" | grep -i success
```

------------------------------------------------------------------------

# 3. Reservation Conflict Spike

## 3.1 Symptoms

- Multiple `RESERVATION_CONFLICT` errors (409)
- Customers unable to book available vehicles
- Overlapping reservation counts spike
- Redis hold keys not expiring properly

## 3.2 Diagnostic Steps

### Step 1: Check active holds

```bash
# Check Redis hold keys
docker-compose exec redis redis-cli keys "hold:*" | wc -l

# Check hold key TTLs
docker-compose exec redis redis-cli --scan --pattern "hold:*" | head -10 | while read key; do
  echo "$key TTL: $(docker-compose exec redis redis-cli ttl $key)"
done
```

### Step 2: Check overlapping reservations

```bash
# Query overlapping reservations
docker-compose exec postgres psql -U rentacar -c "
SELECT 
    v.plate,
    v.id as vehicle_id,
    COUNT(r.id) as reservation_count
FROM vehicles v
JOIN reservations r ON v.id = r.assigned_vehicle_id
WHERE r.status IN ('Paid', 'Active')
AND r.pickup_datetime <= '2026-04-05'
AND r.return_datetime >= '2026-04-01'
GROUP BY v.id, v.plate
HAVING COUNT(r.id) > 1;"
```

### Step 3: Clear expired holds

```bash
# Delete expired hold keys manually
docker-compose exec redis redis-cli --scan --pattern "hold:*" | while read key; do
  ttl=$(docker-compose exec redis redis-cli ttl $key)
  if [ "$ttl" -lt 0 ]; then
    echo "Deleting expired key: $key"
    docker-compose exec redis redis-cli del $key
  fi
done
```

## 3.3 Verification

```bash
# Confirm holds cleared
docker-compose exec redis redis-cli keys "hold:*" | wc -l

# Test availability endpoint
curl "http://localhost/api/v1/vehicles/available?pickup_datetime=2026-04-01T10:00:00Z&return_datetime=2026-04-05T10:00:00Z"
```

------------------------------------------------------------------------

# 4. Database Restore

## 4.1 Symptoms

- Data corruption detected
- Accidental deletion of critical data
- Database container crash with data integrity issues
- Need to rollback to previous state

## 4.2 Pre-Restore Checklist

```bash
# Step 1: Identify backup file
ls -la /opt/rentacar/backups/ | tail -10

# Step 2: Verify backup integrity
gunzip -t /opt/rentacar/backups/rentacar_20260301_020000.sql.gz && echo "Backup OK"

# Step 3: Calculate downtime window
# Typical restore time: 5-15 minutes for < 1GB database

# Step 4: Notify stakeholders
echo "Database restore starting at $(date). Estimated downtime: 15 minutes."
```

## 4.3 Standard Restore Procedure

### Step 1: Stop dependent services

```bash
cd /opt/rentacar

# Stop API and worker containers
docker-compose stop api worker

# Verify stopped
docker-compose ps | grep -E "(api|worker)"
```

### Step 2: Backup current state (even if corrupted)

```bash
# Create emergency backup of current state
DATE=$(date +%Y%m%d_%H%M%S)
docker-compose exec -T postgres pg_dump -U rentacar rentacar > /opt/rentacar/backups/emergency_backup_${DATE}.sql
gzip /opt/rentacar/backups/emergency_backup_${DATE}.sql
```

### Step 3: Restore from backup

```bash
# Set backup file path
BACKUP_FILE="/opt/rentacar/backups/rentacar_20260301_020000.sql.gz"

# Method A: Direct restore with psql

# Step 3a: Drop and recreate database
docker-compose exec postgres psql -U rentacar -d postgres -c "
-- Terminate existing connections
SELECT pg_terminate_backend(pid) 
FROM pg_stat_activity 
WHERE datname = 'rentacar';

-- Drop and recreate
DROP DATABASE IF EXISTS rentacar;
CREATE DATABASE rentacar OWNER rentacar;"

# Step 3b: Restore data
gunzip -c $BACKUP_FILE | docker-compose exec -T postgres psql -U rentacar -d rentacar

# Method B: Using pg_restore (for custom format backups)
# gunzip -c $BACKUP_FILE | docker-compose exec -T postgres pg_restore -U rentacar -d rentacar --clean --if-exists
```

### Step 4: Verify restore

```bash
# Check table row counts
docker-compose exec postgres psql -U rentacar -c "
SELECT 
    schemaname,
    relname as table_name,
    n_live_tup as row_count
FROM pg_stat_user_tables
ORDER BY n_live_tup DESC;"

# Check critical data
docker-compose exec postgres psql -U rentacar -c "
SELECT 
    (SELECT COUNT(*) FROM reservations) as reservation_count,
    (SELECT COUNT(*) FROM vehicles) as vehicle_count,
    (SELECT COUNT(*) FROM customers) as customer_count;"
```

### Step 5: Restart services

```bash
# Start services
docker-compose start api worker

# Verify health
sleep 15
curl -f http://localhost:5000/health
docker-compose ps
```

## 4.4 Point-in-Time Recovery (PITR)

If WAL archiving is enabled:

```bash
# Stop PostgreSQL
docker-compose stop postgres

# Prepare recovery.conf (PostgreSQL 15 uses postgresql.conf with restore_command)
cat > /opt/rentacar/postgres-recovery.conf << 'EOF'
restore_command = 'cp /backups/wal/%f %p'
recovery_target_time = '2026-03-01 14:30:00'
recovery_target_action = 'promote'
EOF

# Mount recovery config and start
docker-compose -f docker-compose.yml -f docker-compose.recovery.yml up -d postgres

# Monitor recovery progress
docker-compose logs -f postgres | grep -E "(restore|recovery|redo)"
```

## 4.5 Single Table Restore

For targeted recovery without full restore:

```bash
# Extract single table from backup
gunzip -c /opt/rentacar/backups/rentacar_20260301_020000.sql.gz | \
  grep -A 1000 "CREATE TABLE vehicles" | \
  grep -B 1000 "COPY vehicles" > /tmp/vehicles_table.sql

# Or use pg_restore with table filter
docker-compose exec -T postgres pg_restore \
  -U rentacar \
  -d rentacar \
  --clean --if-exists \
  --table=vehicles \
  < backup_file
```

## 4.6 Verification

```bash
# Data integrity check
docker-compose exec postgres psql -U rentacar -c "
SELECT 
    'Reservations' as table_name,
    COUNT(*) as count,
    MAX(created_at) as latest
FROM reservations
UNION ALL
SELECT 
    'Payments',
    COUNT(*),
    MAX(created_at)
FROM payment_intents
UNION ALL
SELECT 
    'Vehicles',
    COUNT(*),
    MAX(updated_at)
FROM vehicles;"

# Application connectivity test
curl -f http://localhost/api/v1/vehicles/groups
curl -f http://localhost/api/v1/vehicles/available?pickup_datetime=2026-04-01T10:00:00Z&return_datetime=2026-04-02T10:00:00Z
```

------------------------------------------------------------------------

# 5. Redis Degraded Mode

## 5.1 Symptoms

- Redis container restarting frequently
- Memory usage at limit (eviction happening)
- High latency on cache operations
- Cache misses increasing
- Connection errors to Redis

## 5.2 Diagnostic Steps

### Step 1: Check Redis health

```bash
# Basic connectivity
docker-compose exec redis redis-cli ping

# Memory usage
docker-compose exec redis redis-cli info memory | grep -E "(used_memory_human|maxmemory_human|used_memory_peak_human)"

# Connection info
docker-compose exec redis redis-cli info clients | grep -E "(connected_clients|blocked_clients)"

# Stats
docker-compose exec redis redis-cli info stats | grep -E "(keyspace_hits|keyspace_misses|evicted_keys)"
```

### Step 2: Check for memory pressure

```bash
# Memory fragmentation
docker-compose exec redis redis-cli info memory | grep mem_fragmentation_ratio

# If ratio > 1.5, fragmentation is high

# Biggest keys (may indicate memory issues)
docker-compose exec redis redis-cli --bigkeys

# Slow log
docker-compose exec redis redis-cli slowlog get 10
```

## 5.3 Resolution Procedures

### Procedure A: Redis Restart

```bash
# Step 1: Switch to DB-only mode (via API config or env)
# Edit docker-compose.yml temporarily to disable cache
docker-compose stop api worker

# Step 2: Restart Redis
docker-compose restart redis

# Step 3: Verify Redis health
docker-compose exec redis redis-cli ping
docker-compose exec redis redis-cli info memory

# Step 4: Clear potentially corrupted cache
docker-compose exec redis redis-cli FLUSHDB

# Step 5: Restart API with cache re-enabled
docker-compose start api worker
```

### Procedure B: Switch to DB-Only Mode (Emergency)

```bash
# Step 1: Create override file for degraded mode
cat > /opt/rentacar/docker-compose.override.yml << 'EOF'
version: '3.8'
services:
  api:
    environment:
      - Redis__Enabled=false
      - Cache__Provider=InMemory
  worker:
    environment:
      - Redis__Enabled=false
      - Cache__Provider=InMemory
EOF

# Step 2: Restart with override
docker-compose -f docker-compose.yml -f docker-compose.override.yml up -d api worker

# Step 3: Verify application works without Redis
curl -f http://localhost/api/v1/vehicles/groups
```

### Procedure C: Memory Issue Resolution

```bash
# If memory is the issue, identify and delete large keys

# Find largest keys
docker-compose exec redis redis-cli --bigkeys

# Check specific key sizes
docker-compose exec redis redis-cli keys "*" | while read key; do
  size=$(docker-compose exec redis redis-cli memory usage "$key")
  echo "$size $key"
done | sort -rn | head -20

# Delete problematic keys (use with caution)
# docker-compose exec redis redis-cli del "problematic:key"

# Or flush all and restart
docker-compose exec redis redis-cli FLUSHDB
```

## 5.4 Verification

```bash
# Redis responding normally
docker-compose exec redis redis-cli ping

# Memory usage acceptable
docker-compose exec redis redis-cli info memory | grep used_memory_human

# No evictions happening
docker-compose exec redis redis-cli info stats | grep evicted_keys

# Application cache operations working
curl -f http://localhost/api/v1/vehicles/available?pickup_datetime=2026-04-01T10:00:00Z&return_datetime=2026-04-02T10:00:00Z
```

------------------------------------------------------------------------

# 6. SMS Provider Down

## 6.1 Symptoms

- SMS notifications not being sent
- Netgsm API errors in logs
- Customer complaints about missing SMS
- Queue buildup for SMS jobs

## 6.2 Diagnostic Steps

```bash
# Check SMS queue depth
docker-compose exec postgres psql -U rentacar -c "
SELECT 
    status,
    provider,
    COUNT(*) as count
FROM sms_notifications
WHERE created_at > NOW() - INTERVAL '1 hour'
GROUP BY status, provider;"

# Check worker logs for SMS errors
docker-compose logs worker | grep -i sms | tail -30
docker-compose logs worker | grep -i netgsm | tail -30
```

## 6.3 Fallback Provider Switch

### Step 1: Verify Twilio credentials are configured

```bash
# Check environment
docker-compose exec api env | grep -i twilio

# Expected:
# Twilio__AccountSid=...
# Twilio__AuthToken=...
# Twilio__FromNumber=...
```

### Step 2: Switch to Twilio (Fallback)

```bash
# Method A: Temporary environment override
cat > /opt/rentacar/docker-compose.sms-fallback.yml << 'EOF'
version: '3.8'
services:
  api:
    environment:
      - SMS__PrimaryProvider=Twilio
      - SMS__FallbackEnabled=true
  worker:
    environment:
      - SMS__PrimaryProvider=Twilio
      - SMS__FallbackEnabled=true
EOF

docker-compose -f docker-compose.yml -f docker-compose.sms-fallback.yml up -d api worker

# Method B: Update .env and restart
cd /opt/rentacar
sed -i 's/SMS_PROVIDER=Netgsm/SMS_PROVIDER=Twilio/' .env
docker-compose restart api worker
```

### Step 3: Retry failed SMS messages

```bash
# Mark failed messages for retry
docker-compose exec postgres psql -U rentacar -c "
UPDATE sms_notifications 
SET 
    status = 'Pending',
    provider = 'Twilio',
    retry_count = retry_count + 1,
    updated_at = NOW()
WHERE status = 'Failed'
AND created_at > NOW() - INTERVAL '2 hours';"

# Restart worker to process immediately
docker-compose restart worker
```

## 6.4 Verification

```bash
# Check SMS being sent via Twilio
docker-compose logs worker | grep -i twilio | tail -20

# Check queue processing
docker-compose exec postgres psql -U rentacar -c "
SELECT 
    status,
    COUNT(*) as count
FROM sms_notifications
WHERE created_at > NOW() - INTERVAL '30 minutes'
GROUP BY status;"

# Expected: Fewer Pending, more Sent
```

------------------------------------------------------------------------

# 7. High CPU/Memory

## 7.1 Symptoms

- VPS load average > 4 (on 4-core system)
- Memory usage > 80%
- Container OOM kills
- Response times degrading
- Docker stats showing high resource usage

## 7.2 Monitoring Commands

### System Level

```bash
# Overall system load
uptime

# CPU and memory usage
top -bn1 | head -20
htop  # if available

# Memory details
free -h

# Disk I/O
iostat -x 1 5  # if sysstat installed

# Process list by CPU
ps aux --sort=-%cpu | head -10

# Process list by memory
ps aux --sort=-%mem | head -10
```

### Docker Level

```bash
# Container resource usage
docker stats --no-stream

# Container-specific stats
docker stats api --no-stream
docker stats postgres --no-stream

# Docker system info
docker system df
docker system info | grep -E "(Memory|CPU)"
```

### Application Level

```bash
# API container processes
docker-compose exec api ps aux

# PostgreSQL active queries
docker-compose exec postgres psql -U rentacar -c "
SELECT 
    pid,
    usename,
    application_name,
    state,
    query_start,
    EXTRACT(EPOCH FROM (NOW() - query_start)) as duration_seconds,
    LEFT(query, 100) as query_preview
FROM pg_stat_activity
WHERE state != 'idle'
ORDER BY duration_seconds DESC;"

# Slow queries
docker-compose exec postgres psql -U rentacar -c "
SELECT 
    query,
    calls,
    total_exec_time,
    mean_exec_time
FROM pg_stat_statements
ORDER BY mean_exec_time DESC
LIMIT 10;"
```

## 7.3 Scale-Up Procedures

### Procedure A: Vertical Scaling (Increase Limits)

```bash
# Create override for increased resources
cat > /opt/rentacar/docker-compose.scale.yml << 'EOF'
version: '3.8'
services:
  api:
    deploy:
      resources:
        limits:
          cpus: '3.0'
          memory: 3G
        reservations:
          cpus: '1.0'
          memory: 1G
  
  postgres:
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 3G
  
  redis:
    deploy:
      resources:
        limits:
          memory: 512M
EOF

docker-compose -f docker-compose.yml -f docker-compose.scale.yml up -d
```

### Procedure B: Connection Pool Tuning

```bash
# If database connections are the bottleneck
# Update PostgreSQL config
docker-compose exec postgres psql -U rentacar -c "
ALTER SYSTEM SET max_connections = 300;
ALTER SYSTEM SET shared_buffers = '1GB';
SELECT pg_reload_conf();"

# Restart PostgreSQL to apply
docker-compose restart postgres
```

### Procedure C: Application Optimization

```bash
# Enable API response compression (if not already)
# Check nginx config includes gzip
grep gzip /opt/rentacar/nginx/nginx.conf

# Restart nginx
docker-compose restart nginx

# Clear application cache to reduce memory
docker-compose exec redis redis-cli FLUSHDB
```

### Procedure D: Emergency Resource Free

```bash
# Prune Docker system
docker system prune -f
docker volume prune -f

# Clear logs
sudo truncate -s 0 /var/log/nginx/access.log
sudo truncate -s 0 /var/log/nginx/error.log

# Restart heavy containers
docker-compose restart api worker
```

## 7.4 Verification

```bash
# Check load reduced
uptime

# Check memory usage
free -h

# Check container stats
docker stats --no-stream

# Verify application responsive
curl -f -w "@curl-format.txt" -o /dev/null -s http://localhost/api/v1/health
```

------------------------------------------------------------------------

# 8. Security Incident

## 8.1 Symptoms

- Suspicious activity in access logs
- Multiple failed login attempts
- Unauthorized API access
- Data exfiltration attempts
- Malformed requests targeting vulnerabilities

## 8.2 Log Analysis Commands

### Nginx Access Log Analysis

```bash
# Check for suspicious IPs
cat /var/log/nginx/access.log | awk '{print $1}' | sort | uniq -c | sort -rn | head -20

# Check for failed requests (4xx/5xx)
cat /var/log/nginx/access.log | awk '{print $9}' | sort | uniq -c | sort -rn

# Check for scanning behavior
grep -E "(admin|wp-login|phpmyadmin|\.env|config\.xml)" /var/log/nginx/access.log | head -20

# Check for SQL injection attempts
grep -iE "(union.*select|drop.*table|1=1|sleep\(|waitfor)" /var/log/nginx/access.log

# Time-based analysis
awk '{print $4}' /var/log/nginx/access.log | cut -d: -f2 | sort | uniq -c | sort -rn
```

### Application Log Analysis

```bash
# Check for authentication failures
docker-compose logs api | grep -i "authentication failed" | tail -30

# Check for rate limit hits
docker-compose logs api | grep -i "rate limit" | tail -30

# Check for invalid tokens
docker-compose logs api | grep -i "invalid token" | tail -30

# Check for admin access
docker-compose logs api | grep -i "admin" | tail -50
```

### Database Access Analysis

```bash
# Check active connections by IP
docker-compose exec postgres psql -U rentacar -c "
SELECT 
    client_addr,
    count(*) as connection_count,
    usename
FROM pg_stat_activity
WHERE datname = 'rentacar'
GROUP BY client_addr, usename;"
```

## 8.3 IP Ban Procedures

### Method A: Nginx Level Block

```bash
# Add to nginx.conf in server block
cat >> /opt/rentacar/nginx/block.conf << 'EOF'
# Blocked IPs
deny 192.168.1.100;
deny 10.0.0.50;
# Add more as needed
EOF

# Include in main nginx.conf
# Add: include /etc/nginx/block.conf;

# Reload nginx
docker-compose exec nginx nginx -s reload
```

### Method B: UFW Firewall Block

```bash
# Block IP at firewall level
sudo ufw deny from 192.168.1.100

# Check UFW status
sudo ufw status verbose

# List blocked IPs
sudo ufw status numbered
```

### Method C: Fail2ban (if configured)

```bash
# Check fail2ban status
sudo fail2ban-client status

# Check specific jail
sudo fail2ban-client status nginx-auth

# Ban IP manually
sudo fail2ban-client set nginx-auth banip 192.168.1.100

# Unban IP
sudo fail2ban-client set nginx-auth unbanip 192.168.1.100
```

## 8.4 Incident Response Procedures

### Step 1: Immediate Containment

```bash
# Identify attacker IP
ATTACKER_IP="192.168.1.100"

# Block immediately
sudo ufw deny from $ATTACKER_IP

# Check current connections from attacker
sudo netstat -an | grep $ATTACKER_IP

# Kill connections if active
# sudo ss -K dst $ATTACKER_IP
```

### Step 2: Evidence Collection

```bash
# Create incident directory
mkdir -p /opt/rentacar/incidents/$(date +%Y%m%d_%H%M%S)
INCIDENT_DIR="/opt/rentacar/incidents/$(date +%Y%m%d_%H%M%S)"

# Collect logs
grep $ATTACKER_IP /var/log/nginx/access.log > $INCIDENT_DIR/nginx_attacker.log
docker-compose logs api | grep $ATTACKER_IP > $INCIDENT_DIR/api_attacker.log 2>&1

# Collect system state
uptime > $INCIDENT_DIR/system_state.txt
free -h >> $INCIDENT_DIR/system_state.txt
docker ps >> $INCIDENT_DIR/system_state.txt

# Database audit
docker-compose exec postgres psql -U rentacar -c "
SELECT 
    action,
    timestamp,
    details
FROM audit_log
WHERE timestamp > NOW() - INTERVAL '1 hour'
ORDER BY timestamp DESC;" > $INCIDENT_DIR/audit_log.txt

# Compress evidence
tar -czf $INCIDENT_DIR.tar.gz $INCIDENT_DIR
```

### Step 3: Impact Assessment

```bash
# Check for successful unauthorized access
docker-compose logs api | grep -E "(200|201|204)" | grep $ATTACKER_IP

# Check database for modifications
docker-compose exec postgres psql -U rentacar -c "
SELECT 
    tablename,
    schemaname,
    n_tup_ins,
    n_tup_upd,
    n_tup_del
FROM pg_stat_user_tables
WHERE n_tup_upd > 0 OR n_tup_del > 0
ORDER BY n_tup_upd + n_tup_del DESC;"

# Check for new admin users
docker-compose exec postgres psql -U rentacar -c "
SELECT 
    id,
    email,
    role,
    created_at
FROM admin_users
WHERE created_at > NOW() - INTERVAL '1 hour';"
```

### Step 4: Recovery Actions

```bash
# If admin credentials compromised:
# Reset admin password
docker-compose exec postgres psql -U rentacar -c "
UPDATE admin_users 
SET password_hash = 'NEW_HASH_HERE',
    updated_at = NOW()
WHERE email = 'compromised@admin.com';"

# Invalidate all sessions (if session table exists)
docker-compose exec postgres psql -U rentacar -c "
UPDATE user_sessions 
SET revoked_at = NOW() 
WHERE user_id IN (
    SELECT id FROM admin_users WHERE email = 'compromised@admin.com'
);"

# Rotate JWT secret (requires restart)
# Update .env with new secret
docker-compose restart api
```

## 8.5 Post-Incident Verification

```bash
# Verify attacker blocked
sudo ufw status | grep $ATTACKER_IP

# Check no active connections
sudo netstat -an | grep $ATTACKER_IP || echo "No active connections"

# Verify application secure
curl -f http://localhost/api/v1/health

# Check logs for continued attacks
tail -100 /var/log/nginx/access.log | grep $ATTACKER_IP || echo "No further activity from attacker"
```

------------------------------------------------------------------------

# 9. Quick Reference

## 9.1 Emergency Contacts

| Role | Contact | Method |
|------|---------|--------|
| On-Call Engineer | - | Slack #alerts |
| Database Admin | - | Slack #db-team |
| Security Team | - | Slack #security |
| Payment Provider | Iyzico | support@iyzico.com |
| Hosting Provider | Hetzner/DO | Support Portal |

## 9.2 Critical Commands

```bash
# Full stack restart
cd /opt/rentacar && docker-compose restart

# Database backup now
docker-compose exec -T postgres pg_dump -U rentacar rentacar > /opt/rentacar/backups/emergency_$(date +%Y%m%d_%H%M%S).sql

# Check all services
docker-compose ps

# View all logs
docker-compose logs --tail=100

# System resources
free -h && df -h && uptime
```

## 9.3 Service Ports

| Service | Port | Health Endpoint |
|---------|------|-----------------|
| nginx | 80, 443 | - |
| api | 5000 | /health |
| postgres | 5432 | pg_isready |
| redis | 6379 | PING |

## 9.4 Backup Locations

| Type | Location | Retention |
|------|----------|-----------|
| Database | /opt/rentacar/backups/ | 30 days |
| Nginx logs | /var/log/nginx/ | 7 days |
| App logs | Docker volumes | 3 days |

## 9.5 Escalation Matrix

| Severity | Response Time | Action |
|----------|---------------|--------|
| P1 - Site Down | 15 minutes | Page on-call, start war room |
| P2 - Degraded | 1 hour | Notify team, begin diagnostics |
| P3 - Minor | 4 hours | Create ticket, schedule fix |
| P4 - Cosmetic | 24 hours | Backlog item |

------------------------------------------------------------------------

# 10. Document History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2026-02-25 | Initial 25-line runbook |
| 2.0.0 | 2026-02-25 | Enterprise expansion - 300+ lines |

------------------------------------------------------------------------

END OF DOCUMENT
