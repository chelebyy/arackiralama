FROM node:20-alpine AS deps
WORKDIR /app

RUN apk add --no-cache libc6-compat \
  && corepack enable \
  && corepack prepare pnpm@latest --activate

COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile

FROM node:20-alpine AS builder
WORKDIR /app

RUN apk add --no-cache libc6-compat \
  && corepack enable \
  && corepack prepare pnpm@latest --activate

COPY --from=deps /app/node_modules ./node_modules
COPY . .

ENV NEXT_TELEMETRY_DISABLED=1

RUN corepack pnpm build

FROM node:20-alpine AS runner
WORKDIR /app

RUN apk add --no-cache libc6-compat \
  && corepack enable \
  && corepack prepare pnpm@latest --activate \
  && addgroup -S nextjs -g 10001 \
  && adduser -S nextjs -u 10001 -G nextjs

ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
ENV HOSTNAME=0.0.0.0
ENV PORT=3000

COPY --from=builder --chown=nextjs:nextjs /app/public ./public
COPY --from=builder --chown=nextjs:nextjs /app/.next/standalone ./
COPY --from=builder --chown=nextjs:nextjs /app/.next/static ./.next/static

USER nextjs

EXPOSE 3000

CMD ["node", "server.js"]
