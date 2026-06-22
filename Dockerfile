# Tek parametreli, çok aşamalı Dockerfile. SERVICE_ROLE build arg'ı hangi projenin
# publish edileceğini, aynı isimli runtime env'i ise hangi dll'in çalışacağını seçer.
# Böylece api ve web servisleri TEK Dockerfile + TEK repo'dan ayrışır.

# ---- build (SDK 10) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG SERVICE_ROLE=api
WORKDIR /src
COPY . .
RUN if [ "$SERVICE_ROLE" = "web" ]; then \
      dotnet publish src/ZamanTakasi.Web/ZamanTakasi.Web.csproj -c Release -o /app; \
    else \
      dotnet publish src/ZamanTakasi.Api/ZamanTakasi.Api.csproj -c Release -o /app; \
    fi

# ---- runtime (ASP.NET 10 — ICU/globalization dahil) ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
# SERVICE_ROLE runtime env'i (Railway servis değişkeni) doğru dll'i seçer.
ENTRYPOINT ["sh", "-c", "if [ \"$SERVICE_ROLE\" = \"web\" ]; then exec dotnet ZamanTakasi.Web.dll; else exec dotnet ZamanTakasi.Api.dll; fi"]
