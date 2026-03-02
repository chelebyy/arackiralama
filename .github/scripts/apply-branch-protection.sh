#!/usr/bin/env bash
set -euo pipefail

REPOSITORY="${1:-}"
BRANCH="${2:-main}"

extract_repo_from_remote() {
  local remote_url
  remote_url="$(git config --get remote.origin.url 2>/dev/null || true)"

  if [[ -z "$remote_url" ]]; then
    return 0
  fi

  remote_url="${remote_url%.git}"
  remote_url="${remote_url#git@github.com:}"
  remote_url="${remote_url#https://github.com/}"
  remote_url="${remote_url#http://github.com/}"
  printf '%s' "$remote_url"
}

if ! command -v gh >/dev/null 2>&1; then
  echo "GitHub CLI (gh) is required." >&2
  exit 1
fi

if ! gh auth status --hostname github.com >/dev/null 2>&1; then
  echo "GitHub CLI is not authenticated or token is invalid. Run: gh auth login" >&2
  exit 1
fi

if [[ -z "$REPOSITORY" ]]; then
  REPOSITORY="$(gh repo view --json nameWithOwner -q .nameWithOwner 2>/dev/null || true)"
fi

if [[ -z "$REPOSITORY" ]]; then
  REPOSITORY="$(extract_repo_from_remote)"
fi

if [[ -z "$REPOSITORY" ]]; then
  echo "Repository could not be resolved. Pass owner/repo as first argument." >&2
  exit 1
fi

if ! gh api "repos/${REPOSITORY}/branches/${BRANCH}" >/dev/null 2>&1; then
  echo "Branch '${BRANCH}' not found in repository '${REPOSITORY}'." >&2
  exit 1
fi

payload=$(cat <<'JSON'
{
  "required_status_checks": {
    "strict": true,
    "contexts": [
      "Backend Build and Test / build-test",
      "Docker Image Build / docker-build"
    ]
  },
  "enforce_admins": true,
  "required_pull_request_reviews": {
    "dismiss_stale_reviews": true,
    "require_code_owner_reviews": false,
    "required_approving_review_count": 1
  },
  "restrictions": null,
  "required_linear_history": true,
  "allow_force_pushes": false,
  "allow_deletions": false,
  "block_creations": false,
  "required_conversation_resolution": true,
  "lock_branch": false,
  "allow_fork_syncing": true
}
JSON
)

gh api \
  --method PUT \
  -H "Accept: application/vnd.github+json" \
  "repos/${REPOSITORY}/branches/${BRANCH}/protection" \
  --input - <<<"$payload"

echo "Branch protection applied for ${REPOSITORY}:${BRANCH}."
