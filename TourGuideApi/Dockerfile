# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["TourGuideApi/TourGuideApi.csproj", "TourGuideApi/"]
RUN dotnet restore "TourGuideApi/TourGuideApi.csproj"

COPY . .
WORKDIR /src/TourGuideApi
RUN dotnet publish "TourGuideApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV PORT=8080

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "TourGuideApi.dll"]
