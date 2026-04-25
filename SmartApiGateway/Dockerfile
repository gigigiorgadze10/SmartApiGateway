# 1. დეველოპმენტის ეტაპი (SDK-ის გადმოწერა და პროექტის და-build-ება)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# ვაკოპირებთ csproj ფაილს და ვიწერთ ბიბლიოთეკებს
COPY ["SmartApiGateway/SmartApiGateway.csproj", "SmartApiGateway/"]
RUN dotnet restore "SmartApiGateway/SmartApiGateway.csproj"

# ვაკოპირებთ სრულ კოდს და ვაკეთებთ ფაბლიშს
COPY . .
WORKDIR "/src/SmartApiGateway"
RUN dotnet publish "SmartApiGateway.csproj" -c Release -o /app/publish

# 2. გაშვების ეტაპი (მხოლოდ Runtime, რომ სერვერი მსუბუქი იყოს)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80

# ვაკოპირებთ და-build-ებულ ფაილებს წინა ეტაპიდან
COPY --from=build /app/publish .

# ვეუბნებით სერვერს რომელ პორტზე მოუსმინოს
ENV ASPNETCORE_URLS=http://+:80

# ვრთავთ პროექტს
ENTRYPOINT ["dotnet", "SmartApiGateway.dll"]