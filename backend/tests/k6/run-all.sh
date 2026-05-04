#!/bin/bash
# Run all k6 load test scenarios

set -e

BASE_URL="${BASE_URL:-http://localhost:5000}"
ADMIN_EMAIL="${ADMIN_EMAIL:-admin@rentacar.test}"
ADMIN_PASSWORD="${ADMIN_PASSWORD:-password}"

echo "================================"
echo "RentACar Load Test Suite"
echo "BASE_URL: $BASE_URL"
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
    "$file"
  echo ""
}

run_test "availability-query.js" "Availability Query"
run_test "concurrent-search.js" "Concurrent Search"
run_test "concurrent-booking.js" "Concurrent Booking"
run_test "payment-intent.js" "Payment Intent"
run_test "admin-dashboard.js" "Admin Dashboard"
run_test "mixed-traffic.js" "Mixed Traffic"

echo "================================"
echo "All load tests completed"
echo "Results: results/*.json"
echo "================================"
