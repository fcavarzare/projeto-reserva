# Imagem ultra-leve do Nginx
FROM nginx:alpine

# Copiar o arquivo HTML para a pasta padrão do Nginx
# Vamos renomear para index.html para o Nginx reconhecê-lo como página inicial
COPY src/Web/dashboard.html /usr/share/nginx/html/index.html

# Expor a porta 80
EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
