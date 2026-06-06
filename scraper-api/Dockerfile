FROM node:20-slim

WORKDIR /app

# Install Chromium (available for both amd64 and arm64) + dependencies
RUN apt-get update \
    && apt-get install -y --no-install-recommends chromium xvfb fonts-liberation \
    libatk-bridge2.0-0 libatk1.0-0 libcups2 libdbus-1-3 libgdk-pixbuf2.0-0 \
    libgtk-3-0 libnspr4 libnss3 libxcomposite1 libxdamage1 libxrandr2 \
    libxss1 libxtst6 xdg-utils ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Tell chrome-launcher where to find the browser
ENV CHROME_PATH=/usr/bin/chromium

COPY package.json ./
RUN npm install --omit=dev

COPY server.js ./

ENV NODE_ENV=production
ENV PORT=3000

EXPOSE 3000
CMD ["node", "server.js"]
