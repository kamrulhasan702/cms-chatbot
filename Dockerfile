FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/ManualChatbot/ManualChatbot.csproj src/ManualChatbot/
RUN dotnet restore src/ManualChatbot/ManualChatbot.csproj
COPY src/ManualChatbot/ src/ManualChatbot/
RUN dotnet publish src/ManualChatbot/ManualChatbot.csproj -c Release -o /output --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /output .
RUN mkdir -p /app/App_Data && chown app:app /app/App_Data
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "ManualChatbot.dll"]
