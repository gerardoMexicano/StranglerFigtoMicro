FROM nginx:alpine

# Copiamos nuestra configuración personalizada
COPY nginx.conf /etc/nginx/nginx.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
