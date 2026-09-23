#!/bin/bash
# Run all k6 load test scenarios

set -e

BASE_URL="${BASE_URL:-http://localhost:5000}"
ADMIN_EMAIL="${ADMIN_EMAIL:-integration-admin@rentacar.test}"
ADMIN_PASSWORD="${ADMIN_PASSWORD:-IntegrationTestPassword123!}"
CUSTOMER_EMAIL="${CUSTOMER_EMAIL:-}"
CUSTOMER_ACCESS_TOKEN="${CUSTOMER_ACCESS_TOKEN:-}"
SMOKE_MODE="${SMOKE_MODE:-0}"

echo "================================"
echo "RentACar Load Test Suite"
echo "BASE_URL: $BASE_URL"
echo "SMOKE_MODE: $SMOKE_MODE"
echo "================================"
echo ""

mkdir -p results

run_test() {
  local file=$1
  local desc=$2
  echo "Running: $desc ($file)"
  k6 run \
    --env BASE_URL="$BASE_URL" \
    --env ADMIN_EMAIL="$ADMIN_EMAIL" \
    --env ADMIN_PASSWORD="$ADMIN_PASSWORD" \
    --env CUSTOMER_EMAIL="$CUSTOMER_EMAIL" \
    --env CUSTOMER_ACCESS_TOKEN="$CUSTOMER_ACCESS_TOKEN" \
    --env SMOKE_MODE="$SMOKE_MODE" \
    "$file"
  echo ""
}

run_test "availability-query.js" "Availability Query"
run_test "concurrent-search.js" "Concurrent Search"
run_test "concurrent-booking.js" "Concurrent Booking"
run_test "payment-intent.js" "Payment Intent"
if [ "$SMOKE_MODE" = "1" ]; then
  echo "Skipping Admin Dashboard in smoke mode (local admin seed not required)."
else
  run_test "admin-dashboard.js" "Admin Dashboard"
fi
run_test "mixed-traffic.js" "Mixed Traffic"

echo "================================"
echo "All load tests completed"
echo "Results: results/*.json"
echo "================================"
