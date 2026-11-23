# 🚀 stage 1: build + publish (restore เฉพาะโปรเจกต์ API — ไม่รวม tests)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ./
COPY src/WebTestPdpa.Api/WebTestPdpa.Api.csproj src/WebTestPdpa.Api/
RUN dotnet restore src/WebTestPdpa.Api/WebTestPdpa.Api.csproj
COPY src/ src/
RUN dotnet publish src/WebTestPdpa.Api/WebTestPdpa.Api.csproj -c Release -o /app --no-restore

# 🚀 stage 2: runtime (non-root user `app` ที่มากับ image)
FROM mcr.microsoft.com/dotnet/aspnet:10.0
ENV ASPNETCORE_HTTP_PORTS=8080
# 🚀 R8: ฟอนต์ไทย Garuda (TLWG จาก Ubuntu main) + fontconfig สำหรับ QuestPDF render รายงาน PDF
RUN apt-get update \
    && apt-get install -y --no-install-recommends fonts-tlwg-garuda-ttf libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app ./
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "WebTestPdpa.Api.dll"]
