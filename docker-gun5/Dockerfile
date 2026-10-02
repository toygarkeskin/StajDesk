FROM nginx:latest

WORKDIR /usr/share/nginx/html

COPY index.html .

RUN echo "Web sayfasi Docker image'ina ekleniyor"

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]