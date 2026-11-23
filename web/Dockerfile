# 🚀 stage 1: build Angular (ต้องมี package-lock.json สำหรับ npm ci)
FROM node:24-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npm run build

# 🚀 stage 2: nginx เสิร์ฟไฟล์ static + proxy /api -> api
FROM nginx:alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/web/browser /usr/share/nginx/html
EXPOSE 80
