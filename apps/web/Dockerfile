# Build context: raiz do repositório (docker build -f apps/web/Dockerfile .),
# porque o app importa os tokens compartilhados de packages/tokens.
FROM node:26-alpine AS deps
WORKDIR /repo/apps/web
COPY apps/web/package.json apps/web/package-lock.json ./
RUN npm ci

FROM node:26-alpine AS build
WORKDIR /repo/apps/web
ENV NEXT_TELEMETRY_DISABLED=1
COPY packages/ /repo/packages/
COPY --from=deps /repo/apps/web/node_modules ./node_modules
COPY apps/web/ ./
RUN npm run build

FROM node:26-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production NEXT_TELEMETRY_DISABLED=1 PORT=3000 HOSTNAME=0.0.0.0
# Com a raiz do monorepo como tracing root, o standalone espelha a estrutura apps/web.
COPY --from=build --chown=node:node /repo/apps/web/.next/standalone ./
COPY --from=build --chown=node:node /repo/apps/web/.next/static ./apps/web/.next/static
COPY --from=build --chown=node:node /repo/apps/web/public ./apps/web/public
USER node
EXPOSE 3000
CMD ["node", "apps/web/server.js"]
