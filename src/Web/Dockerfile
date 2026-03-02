FROM nginx:alpine

# Copiar todos os arquivos da pasta Web para o Nginx
COPY src/Web/*.html /usr/share/nginx/html/

# Garantir que o dashboard.html seja o index.html principal
RUN cp /usr/share/nginx/html/dashboard.html /usr/share/nginx/html/index.html

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
