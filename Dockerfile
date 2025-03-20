FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /TalkingBot

COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -o dist

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /TalkingBot
COPY --from=build-env /TalkingBot/dist/TalkingBotRedux .

# Config mounted at /TalkingBot/Config.json
ENTRYPOINT [ "./TalkingBotRedux", "-C", "Config/Config.json" ]
