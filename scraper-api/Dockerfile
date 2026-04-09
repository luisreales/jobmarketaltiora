FROM mcr.microsoft.com/playwright:v1.52.0-noble

WORKDIR /app

RUN apt-get update \
	&& apt-get install -y --no-install-recommends xvfb \
	&& rm -rf /var/lib/apt/lists/*

COPY package.json ./
RUN npm install --omit=dev

COPY server.js ./

ENV NODE_ENV=production
ENV PORT=3000

EXPOSE 3000
CMD ["node", "server.js"]
