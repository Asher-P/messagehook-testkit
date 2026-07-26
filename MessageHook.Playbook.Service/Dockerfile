# Build context is the repo root: docker build -f MessageHook.Playbook.Service/Dockerfile .
# One container: builds the React UI, publishes the ASP.NET service, serves both.

# 1) Build the React UI -> static files
FROM node:20-alpine AS ui
WORKDIR /ui
COPY messagehook-ui/package.json ./
RUN npm install
COPY messagehook-ui/ ./
RUN npm run build

# 2) Publish the ASP.NET service
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY MessageHook.Core/ MessageHook.Core/
COPY MessageHook.Orchestration/ MessageHook.Orchestration/
COPY MessageHook.Kafka/ MessageHook.Kafka/
COPY MessageHook.Playbook/ MessageHook.Playbook/
COPY MessageHook.Playbook.Service/ MessageHook.Playbook.Service/
COPY icon.png ./
RUN dotnet publish MessageHook.Playbook.Service/MessageHook.Playbook.Service.csproj -c Release -o /app/publish
# Drop in the freshly built UI as the served static content
COPY --from=ui /ui/dist /app/publish/wwwroot

# 3) Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080
ENV DataDir=/data
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "MessageHook.Playbook.Service.dll"]
