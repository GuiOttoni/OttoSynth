# OttoSynth docs — Docusaurus 3.5 site
# Two-stage: build static site, serve via nginx

# ─── Stage 1: Build ──────────────────────────────────────
FROM node:20-alpine AS builder

WORKDIR /app

# Install dependencies (cached if package.json doesn't change)
COPY package.json ./
RUN npm install --no-audit --no-fund

# Copy source files
COPY docusaurus.config.js sidebars.js ./
COPY src ./src
COPY static ./static
COPY docs ./docs

# Build static site to /app/build
RUN npm run build

# ─── Stage 2: Serve via nginx ────────────────────────────
FROM nginx:alpine AS prod

COPY --from=builder /app/build /usr/share/nginx/html

# Simple SPA-friendly config
RUN printf 'server {\n  listen 3000;\n  server_name _;\n  root /usr/share/nginx/html;\n  index index.html;\n  location / {\n    try_files $uri $uri/ /index.html;\n  }\n}\n' > /etc/nginx/conf.d/default.conf

EXPOSE 3000
CMD ["nginx", "-g", "daemon off;"]

# ─── Stage 3 (dev): hot-reload via npm start ─────────────
FROM node:20-alpine AS dev

WORKDIR /app

COPY package.json ./
RUN npm install --no-audit --no-fund

COPY . .

EXPOSE 3000
CMD ["npm", "start"]
