FROM node:22-alpine AS build
WORKDIR /app

COPY frontend/package*.json ./
RUN npm ci

COPY frontend/ ./
RUN npm run build

FROM nginx:1.27-alpine AS runtime
COPY deploy/nginx/default.conf /etc/nginx/conf.d/default.conf
COPY deploy/nginx/40-generate-runtime-config.sh /docker-entrypoint.d/40-generate-runtime-config.sh
RUN sed -i 's/\r$//' /docker-entrypoint.d/40-generate-runtime-config.sh \
    && chmod +x /docker-entrypoint.d/40-generate-runtime-config.sh

COPY --from=build /app/dist/granbrain-frontend /usr/share/nginx/html

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -qO- http://127.0.0.1:8080/healthz >/dev/null || exit 1
