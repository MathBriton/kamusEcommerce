# Build context: raiz do repositório (docker build -f apps/admin/Dockerfile .),
# porque o app importa os tokens compartilhados de packages/tokens.
FROM node:26-alpine AS deps
WORKDIR /repo/apps/admin
COPY apps/admin/package.json apps/admin/package-lock.json ./
RUN npm ci

FROM node:26-alpine AS build
WORKDIR /repo/apps/admin
ENV NEXT_TELEMETRY_DISABLED=1
COPY packages/ /repo/packages/
COPY --from=deps /repo/apps/admin/node_modules ./node_modules
COPY apps/admin/ ./
RUN npm run build

FROM node:26-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production NEXT_TELEMETRY_DISABLED=1 PORT=3001 HOSTNAME=0.0.0.0
# Com a raiz do monorepo como tracing root, o standalone espelha a estrutura apps/admin.
COPY --from=build --chown=node:node /repo/apps/admin/.next/standalone ./
COPY --from=build --chown=node:node /repo/apps/admin/.next/static ./apps/admin/.next/static
COPY --from=build --chown=node:node /repo/apps/admin/public ./apps/admin/public
USER node
EXPOSE 3001
CMD ["node", "apps/admin/server.js"]
