FROM node:26.5.0-alpine AS deps
WORKDIR /app

RUN apk add --no-cache libc6-compat \
  && corepack enable \
  && corepack prepare pnpm@9.15.9 --activate

COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile

FROM node:26.5.0-alpine AS builder
WORKDIR /app

RUN apk add --no-cache libc6-compat \
  && corepack enable \
  && corepack prepare pnpm@9.15.9 --activate

COPY --from=deps /app/node_modules ./node_modules
COPY . .

ENV NEXT_TELEMETRY_DISABLED=1
ARG NEXT_PUBLIC_API_URL
ARG NEXT_PUBLIC_API_BASE_URL
ARG NEXT_PUBLIC_APP_URL
ARG NEXT_PUBLIC_ADMIN_API_URL
ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL
ENV NEXT_PUBLIC_API_BASE_URL=$NEXT_PUBLIC_API_BASE_URL
ENV NEXT_PUBLIC_APP_URL=$NEXT_PUBLIC_APP_URL
ENV NEXT_PUBLIC_ADMIN_API_URL=$NEXT_PUBLIC_ADMIN_API_URL

RUN corepack pnpm build

FROM node:26.5.0-alpine AS runner
WORKDIR /app

RUN apk add --no-cache libc6-compat \
  && corepack enable \
  && corepack prepare pnpm@9.15.9 --activate \
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
