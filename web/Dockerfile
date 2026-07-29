FROM node:22-alpine AS build
WORKDIR /src

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:1.29-alpine

# LOCAL_RESOLVERS każe entrypointowi przepisać nameservery z resolv.conf do
# NGINX_LOCAL_RESOLVERS - potrzebne, bo proxy_pass po zmiennej wymaga dyrektywy
# resolver, a jej adres różni się między Dockerem a siecią prywatną Railwaya.
#
# Filtr zawęża envsubst do zmiennych, które faktycznie podstawiamy. Bez niego
# podstawianie ruszyłoby każdą zmienną w kontenerze, a szablon jest pełen
# zmiennych nginxa ($host, $request_uri) o tej samej składni.
ENV GITTEZ_PORT=80 \
    GITTEZ_API_UPSTREAM=http://api:8080 \
    NGINX_ENTRYPOINT_LOCAL_RESOLVERS=1 \
    NGINX_ENVSUBST_FILTER='^(GITTEZ_|NGINX_LOCAL_RESOLVERS)'

COPY templates/ /etc/nginx/templates/
COPY --from=build /src/dist /usr/share/nginx/html
EXPOSE 80
